using Microsoft.Extensions.Configuration;
using OpenAI.Embeddings;
using Pgvector;
using SmartComponents.LocalEmbeddings;
using Spectre.Console;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace SemanticSearchDemo
{
    internal class Program
    {
        private const string Search = "Search";
        private const string IndexOpenAI = "Index data with OpenAI";
        private const string IndexLocalModel = "Index data with a local model";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        private static readonly HashSet<string> Categories = new(StringComparer.InvariantCultureIgnoreCase)
        {
            "POLITICS", "SCIENCE", "TECH", "WORLD NEWS", "TRAVEL"
        };

        private static readonly IConfiguration Config = new ConfigurationBuilder().AddUserSecrets(Assembly.GetExecutingAssembly()).Build();

        private static async Task Main(string[] args)
        {
            var semanticSearch = new SemanticSearch(Config);

            while (true)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("What would you like to do?")
                        .PageSize(10)
                        .AddChoices(IndexLocalModel, IndexOpenAI, Search, "Quit"));

                switch (choice)
                {
                    case IndexLocalModel:
                        await HandleLocalEmbedderImport();
                        break;
                    case IndexOpenAI:
                        await HandleOpenAIImport();
                        break;
                    case Search:
                        await semanticSearch.HandleSearch();
                        break;
                    default:
                        return;
                }
            }
        }

        private static async Task HandleLocalEmbedderImport()
        {
            await using var context = new SqlServerNewsContext(Config);

            if (context.NewsItems.Any())
            {
                AnsiConsole.MarkupLine("[Green]Data already indexed[/]");
                return;
            }

            using var embedder = new LocalEmbedder(); AnsiConsole.MarkupLine("[Green]Indexing data ...[/]");
            await ImportEmbeddingsWithLocalModel(embedder);
        }

        private static async Task HandleOpenAIImport()
        {
            await using var context = new OpenAiPostgresNewsContext(Config);

            if (context.NewsItems.Any())
            {
                AnsiConsole.MarkupLine("[Green]Data already indexed[/]");
                return;
            }

            EmbeddingClient openAIClient = new(model: "text-embedding-3-small", Config["OpenAI:ApiKey"]);
            await ImportEmbeddingsWithOpenAI(openAIClient);
        }


        private static async Task ImportEmbeddingsWithLocalModel(LocalEmbedder embedder)
        {
            var lines = File.ReadLines("News.json");

            int count = 0;
            var stopwatch = Stopwatch.StartNew();

            var newsItems = lines
                .Select(line => JsonSerializer.Deserialize<NewsItem>(line, JsonOptions))
                //.Take(1000)
                .Where(item => Categories.Contains(item!.Category))
                .Select(item =>
                {
                    var embedding = embedder.Embed(item!.Headline);

                    item.Embedding = embedding;
                    item.EmbeddingBuffer = embedding.Buffer.ToArray();
                    item.EmbeddingVector = new Vector(embedding.Values);

                    count++;
                    if (count % 10000 == 0)
                    {
                        AnsiConsole.MarkupLineInterpolated($"[Green]Indexed {count} items[/]");
                    }

                    return item;
                }).ToList();

            stopwatch.Stop();

            await using var sqlServerNewsContext = new SqlServerNewsContext(Config);
            await using var postgresNewsContext = new PgVectorPostgresNewsContext(Config);

            await SaveToDatabase(newsItems, sqlServerNewsContext, postgresNewsContext);

            AnsiConsole.MarkupLineInterpolated($"[Green]Indexed {newsItems.Count} items in {stopwatch.Elapsed}[/]");
        }

        private static async Task ImportEmbeddingsWithOpenAI(EmbeddingClient client)
        {
            var lines = File.ReadLines("News.json");
            var stopwatch = Stopwatch.StartNew();

            var chunks = lines
                .Select(line => JsonSerializer.Deserialize<NewsItem>(line, JsonOptions)!)
                .Where(item => Categories.Contains(item.Category) && !string.IsNullOrEmpty(item.Headline))
                .Chunk(100);

            var newsItems = chunks
                .Select(items =>
                {
                    var embeddings = client.GenerateEmbeddings(items.Select(i => i.Headline));

                    for (int index = 0; index < items.Length; index++)
                    {
                        items[index].EmbeddingData = embeddings.Value[index].ToFloats().ToArray();
                        items[index].EmbeddingVector = new Vector(embeddings.Value[index].ToFloats());
                    }

                    return items;
                }).SelectMany(items => items).ToList();

            stopwatch.Stop();

            await using var postgresNewsContext = new OpenAiPostgresNewsContext(Config);
            await using var azureSqlServerNewsContext = new AzureSqlServerNewsContext(Config);

            await SaveToDatabase(newsItems, azureSqlServerNewsContext, postgresNewsContext);

            AnsiConsole.MarkupLineInterpolated($"[Green]Indexed {newsItems.Count} items in {stopwatch.Elapsed}[/]");
        }

        private static async Task SaveToDatabase(List<NewsItem> newsItems, params NewsItemsBaseContext[] databases)
        {
            foreach (var database in databases)
            {
                database.NewsItems.AddRange(newsItems);
                await database.SaveChangesAsync();
            }
        }
    }
}
