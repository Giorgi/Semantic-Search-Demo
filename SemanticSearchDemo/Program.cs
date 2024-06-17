using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartComponents.LocalEmbeddings;
using Spectre.Console;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace SemanticSearchDemo
{
    internal class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        private static readonly HashSet<string> Categories = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
        {
            "POLITICS", "SCIENCE", "TECH", "WORLD NEWS", "TRAVEL"
        };

        private static IConfiguration config = new ConfigurationBuilder().AddUserSecrets(Assembly.GetExecutingAssembly()).Build();

        private static async Task Main(string[] args)
        {
            using var embedder = new LocalEmbedder();

            var newsItems = await GetNewsItems();

            if (newsItems.Count == 0)
            {
                AnsiConsole.MarkupLine("[Green]Indexing data ...[/]");
                newsItems = await ImportAndIndex(embedder);
            }
            else
            {
                AnsiConsole.MarkupLine($"[Green]Loaded {newsItems.Count} items from database[/]");
            }

            do
            {
                var prompt = AnsiConsole.Ask<string>("Enter search text:");
                Console.WriteLine();

                var stopwatch = Stopwatch.StartNew();

                var query = embedder.Embed(prompt);

                var results = LocalEmbedder.FindClosestWithScore(query, newsItems.Select(item => (item, item.Embedding)), 10);

                stopwatch.Stop();

                var table = new Table();

                table.AddColumn("Score").AddColumn("Result", column => column.Footer($"[Green]Search time: {stopwatch.ElapsedMilliseconds} Milliseconds[/]"));

                foreach (var newsItem in results)
                {
                    table.AddRow(newsItem.Similarity.ToString(), newsItem.Item.Headline);
                }

                AnsiConsole.Write(table);

            } while (true);
        }

        private static async Task<List<NewsItem>> GetNewsItems()
        {
            await using var context = new SqlServerNewsContext(config);
            var items = await context.NewsItems.ToListAsync();

            foreach (var item in items)
            {
                item.Embedding = new EmbeddingF32(item.EmbeddingBuffer);
            }

            return items;
        }

        private static async Task<List<NewsItem>> ImportAndIndex(LocalEmbedder embedder)
        {
            var lines = File.ReadLines(@"News.json");

            int count = 0;
            var stopwatch = Stopwatch.StartNew();

            var newsItems = lines
                .Select(line => JsonSerializer.Deserialize<NewsItem>(line, JsonOptions))
                //.Take(1000)
                .Where(item => Categories.Contains(item.Category))
                .Select(item =>
                {
                    var embedding = embedder.Embed(item.Headline);

                    item.Embedding = embedding;
                    item.EmbeddingBuffer = embedding.Buffer.ToArray();

                    count++;
                    if (count % 10000 == 0)
                    {
                        AnsiConsole.MarkupLineInterpolated($"[Green]Indexed {count} items[/]");
                    }

                    return item;
                }).ToList();

            stopwatch.Stop();

            await SaveToDatabase(newsItems);

            AnsiConsole.MarkupLineInterpolated($"[Green]Indexed {newsItems.Count} items in {stopwatch.Elapsed}, enter search text {Environment.NewLine}[/]");

            return newsItems;
        }

        private static async Task SaveToDatabase(List<NewsItem> newsItems)
        {
            await using var context = new SqlServerNewsContext(config);
            context.NewsItems.AddRange(newsItems);
            await context.SaveChangesAsync();
        }
    }
}
