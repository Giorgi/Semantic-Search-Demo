﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenAI.Embeddings;
using Pgvector;
using Pgvector.EntityFrameworkCore;
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
            await MigrateDatabase();

            using var embedder = new LocalEmbedder();
            EmbeddingClient openAIClient = new(model: "text-embedding-3-small", config["OpenAI:ApiKey"]);

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

                var query = embedder.Embed(prompt);

                var (stopwatch, results) = SearchInMemory(query, newsItems);
                RenderResults(stopwatch, results);

                (stopwatch, results) = await SearchInPostgres(new Vector(query.Values));
                RenderResults(stopwatch, results);

                var searchQuery = await openAIClient.GenerateEmbeddingAsync(prompt);
                (stopwatch, results) = await SearchInPostgresOpenAI(new Vector(searchQuery.Value.Vector));

                RenderResults(stopwatch, results);
            } while (true);
        }

        private static async Task MigrateDatabase()
        {
            await using var context = new PgVectorPostgresNewsContext(config);
            if ((await context.Database.GetPendingMigrationsAsync()).Any())
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                await context.Database.MigrateAsync();
                stopwatch.Stop();

                AnsiConsole.MarkupLine($"[Green]Migrated in {stopwatch.Elapsed}[/]");
            }
        }


        private static async Task<(Stopwatch stopwatch, SimilarityScore<NewsItem>[] results)> SearchInPostgres(Vector query)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var context = new PgVectorPostgresNewsContext(config);

            var queryable = context.NewsItems
                                   .OrderBy(item => item.EmbeddingVector!.CosineDistance(query))
                                   .Take(10)
                                   .Select(item => new { item, distance = item.EmbeddingVector!.CosineDistance(query) });
            var matches = await queryable.ToListAsync();

            stopwatch.Stop();

            var results = matches.Select(arg => new SimilarityScore<NewsItem>(1 - (float)arg.distance, arg.item)).ToArray();

            return (stopwatch, results);
        }

        private static async Task<(Stopwatch stopwatch, SimilarityScore<NewsItem>[] results)> SearchInPostgresOpenAI(Vector query)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var context = new OpenAiPostgresNewsContext(config);

            var queryable = context.NewsItems
                                   .OrderBy(item => item.EmbeddingVector!.CosineDistance(query))
                                   .Take(10)
                                   .Select(item => new { item, distance = item.EmbeddingVector!.CosineDistance(query) });
            var matches = await queryable.ToListAsync();

            stopwatch.Stop();

            var results = matches.Select(arg => new SimilarityScore<NewsItem>(1 - (float)arg.distance, arg.item)).ToArray();

            return (stopwatch, results);
        }

        private static (Stopwatch stopwatch, SimilarityScore<NewsItem>[] results) SearchInMemory(EmbeddingF32 query, List<NewsItem> newsItems)
        {
            var stopwatch = Stopwatch.StartNew();

            var results = LocalEmbedder.FindClosestWithScore(query, newsItems.Select(item => (item, item.Embedding)), 10);

            stopwatch.Stop();

            return (stopwatch, results);
        }


        private static void RenderResults(Stopwatch stopwatch, SimilarityScore<NewsItem>[] results)
        {
            var table = new Table();

            table.AddColumn("Score").AddColumn("Result", column => column.Footer($"[Green]Search time: {stopwatch.ElapsedMilliseconds} Milliseconds[/]"));

            foreach (var newsItem in results)
            {
                table.AddRow(newsItem.Similarity.ToString(), newsItem.Item.Headline);
            }

            AnsiConsole.Write(table);
        }


        private static async Task<List<NewsItem>> GetNewsItems()
        {
            await using var context = new SqlServerNewsContext(config);
            await context.Database.EnsureCreatedAsync();
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

            await SaveToDatabase(newsItems);

            AnsiConsole.MarkupLineInterpolated($"[Green]Indexed {newsItems.Count} items in {stopwatch.Elapsed}, enter search text {Environment.NewLine}[/]");

            return newsItems;
        }

        private static async Task ImportEmbeddingsWithOpenAI(EmbeddingClient client)
        {
            var lines = File.ReadLines(@"News.json");

            var chunks = lines
                .Select(line => JsonSerializer.Deserialize<NewsItem>(line, JsonOptions))
                .Where(item => Categories.Contains(item.Category) && !string.IsNullOrEmpty(item.Headline))
                .Chunk(100);

            var newsItems = chunks
                .Select(items =>
                {
                    var embeddings = client.GenerateEmbeddings(items.Select(i => i.Headline));

                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i].EmbeddingVector = new Vector(embeddings.Value[i].Vector);
                    }

                    return items;
                }).SelectMany(items => items).ToList();

            await SaveToDatabase(newsItems);
        }

        private static async Task SaveToDatabase(List<NewsItem> newsItems)
        {
            await using var sqlServerContext = new SqlServerNewsContext(config);
            sqlServerContext.NewsItems.AddRange(newsItems);
            await sqlServerContext.SaveChangesAsync();

            await using var postgresContext = new PgVectorPostgresNewsContext(config);
            postgresContext.NewsItems.AddRange(newsItems);
            await postgresContext.SaveChangesAsync();
        }
    }
}
