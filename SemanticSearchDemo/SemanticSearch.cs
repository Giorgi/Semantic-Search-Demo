using Microsoft.EntityFrameworkCore;
using OpenAI.Embeddings;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using SmartComponents.LocalEmbeddings;
using Spectre.Console;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace SemanticSearchDemo;

class SemanticSearch(IConfiguration config)
{
    public async Task HandleSearch()
    {
        using var embedder = new LocalEmbedder();

        EmbeddingClient openAIClient = new(model: "text-embedding-3-small", config["OpenAI:ApiKey"]);

        AnsiConsole.MarkupLine("Loading data in memory");

        var newsItems = await GetNewsItems();

        do
        {
            var prompt = AnsiConsole.Prompt(new TextPrompt<string>("Enter search text:").AllowEmpty());

            Console.WriteLine();

            if (string.IsNullOrEmpty(prompt))
            {
                return;
            }

            var query = embedder.Embed(prompt);

            var (stopwatch, results) = SearchInMemory(query, newsItems);
            RenderResults(stopwatch, results, "Searching in-memory");

            #region PostgreSQL

            (stopwatch, results) = await SearchInPostgres(new Vector(query.Values));
            RenderResults(stopwatch, results, "Searching in database - local embeddings ");

            var searchQuery = await openAIClient.GenerateEmbeddingAsync(prompt);
            (stopwatch, results) = await SearchInPostgresOpenAI(new Vector(searchQuery.Value.ToFloats()));
            RenderResults(stopwatch, results, "Searching in database - OpenAI embeddings"); 
            
            #endregion
        } while (true);
    }

    private async Task<(Stopwatch stopwatch, SimilarityScore<NewsItem>[] results)> SearchInPostgres(Vector query)
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

    private async Task<(Stopwatch stopwatch, SimilarityScore<NewsItem>[] results)> SearchInPostgresOpenAI(Vector query)
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

    private static void RenderResults(Stopwatch stopwatch, SimilarityScore<NewsItem>[] results, string title = "")
    {
        var table = new Table();

        table.Title($"{title} [Green]Search time: {stopwatch.ElapsedMilliseconds} Milliseconds[/]");

        table.AddColumn("Score").AddColumn("Result");

        foreach (var newsItem in results)
        {
            table.AddRow(newsItem.Similarity.ToString(), newsItem.Item.Headline);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private async Task<List<NewsItem>> GetNewsItems()
    {
        await using var context = new SqlServerNewsContext(config);
        var items = await context.NewsItems.ToListAsync();

        foreach (var item in items)
        {
            item.Embedding = new EmbeddingF32(item.EmbeddingBuffer);
        }

        return items;
    }
}