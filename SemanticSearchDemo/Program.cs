using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;
using OpenAI.Embeddings;
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
        private const string IndexLocalModel = "Index data with a local model (Ollama)";
        internal const string Model = "all-minilm";

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
                        await HandleOllamaImport();
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

        private static async Task HandleOllamaImport()
        {
            await using var context = new SqlServerNewsContext(Config);

            await context.Database.EnsureCreatedAsync();

            if (context.NewsItems.Any())
            {
                AnsiConsole.MarkupLine("[Green]Data already indexed[/]");
                return;
            }

            AnsiConsole.MarkupLine("[Green]Indexing data ...[/]");

            using IEmbeddingGenerator<string, Embedding<float>> generator = new OllamaApiClient(new Uri("http://localhost:11434/"), Model);

            await ImportEmbeddings(generator);
        }

        private static async Task HandleOpenAIImport()
        {
            using IEmbeddingGenerator<string, Embedding<float>> generator = new EmbeddingClient("text-embedding-3-small", Config["OpenAI:ApiKey"])
                                                                                .AsIEmbeddingGenerator();

            await ImportEmbeddings(generator);
        }


        private static async Task ImportEmbeddings(IEmbeddingGenerator<string, Embedding<float>> embedder)
        {
            var lines = File.ReadLines("News.json");

            int count = 0;
            var stopwatch = Stopwatch.StartNew();

            var chunks = lines
                .Select(line => JsonSerializer.Deserialize<NewsItem>(line, JsonOptions)!)
                //.Take(1000)
                .Where(item => Categories.Contains(item.Category) && !string.IsNullOrEmpty(item.Headline))
                .Chunk(1000).ToList();

            foreach (var chunk in chunks)
            {
                count++;
                Console.WriteLine("Processing chunk {0}", count);

                var embeddings = await embedder.GenerateAsync(chunk.Select(item => item.Headline));

                foreach (var (item, embedding) in chunk.Zip(embeddings))
                {
                    item.EmbeddingData = embedding.Vector.ToArray();
                }
            }

            var newsItems = chunks.SelectMany(items => items).ToList();

            stopwatch.Stop();

            await using var sqlServerNewsContext = new SqlServerNewsContext(Config);

            sqlServerNewsContext.NewsItems.AddRange(newsItems);
            await sqlServerNewsContext.SaveChangesAsync();

            AnsiConsole.MarkupLineInterpolated($"[Green]Indexed {newsItems.Count} items in {stopwatch.Elapsed}[/]");
        }
    }
}
