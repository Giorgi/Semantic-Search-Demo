using System.Diagnostics;
using SmartComponents.LocalEmbeddings;
using System.Text.Json;
using Spectre.Console;

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

        static void Main(string[] args)
        {
            using var embedder = new LocalEmbedder();
            var lines = File.ReadLines(@"News.json");

            AnsiConsole.MarkupLine("[Green]Indexing data ...[/]");
            var stopwatch = Stopwatch.StartNew();

            int count = 0;

            var newsItems = lines
                .Select(line => JsonSerializer.Deserialize<NewsItem>(line, JsonOptions))
                //.Take(1000)
                .Where(item => Categories.Contains(item.Category))
                .Select(item =>
                {
                    item.Embedding = embedder.Embed(item.Headline);

                    count++;
                    if (count % 10000 == 0)
                    {
                        AnsiConsole.MarkupLineInterpolated($"[Green]Indexed {count} items[/]");
                    }

                    return item;
                }).ToList();

            stopwatch.Stop();

            AnsiConsole.MarkupLineInterpolated($"[Green]Indexed {newsItems.Count} items in {stopwatch.Elapsed}, enter search text {Environment.NewLine}[/]");

            do
            {
                var prompt = AnsiConsole.Ask<string>("Enter search text:");
                Console.WriteLine();

                stopwatch.Restart();

                var query = embedder.Embed(prompt);

                var results = LocalEmbedder.FindClosestWithScore(query, newsItems.Select(item => (item, item.Embedding)), 10);

                stopwatch.Stop();

                var table = new Table();

                table.AddColumn("Score").AddColumn("Result",column => column.Footer($"[Green]Search time: {stopwatch.ElapsedMilliseconds} Milliseconds[/]"));

                foreach (var newsItem in results)
                {
                    table.AddRow(newsItem.Similarity.ToString(), newsItem.Item.Headline);
                }

                AnsiConsole.Write(table);

            } while (true);
        }
    }
}
