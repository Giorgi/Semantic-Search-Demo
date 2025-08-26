using Microsoft.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;
using Spectre.Console;
using System.Data;
using System.Diagnostics;
using System.Numerics.Tensors;

namespace SemanticSearchDemo;

class SemanticSearch(IConfiguration config)
{
    public async Task HandleSearch()
    {
        using IEmbeddingGenerator<string, Embedding<float>> embedder = new OllamaApiClient(new Uri("http://localhost:11434/"), Program.Model);

        AnsiConsole.MarkupLine("Loading data in memory");

        var newsItems = GetNewsItems();

        do
        {
            var prompt = AnsiConsole.Prompt(new TextPrompt<string>("Enter search text:").AllowEmpty());

            Console.WriteLine();

            if (string.IsNullOrEmpty(prompt))
            {
                return;
            }

            var query = await embedder.GenerateVectorAsync(prompt);

            var (stopwatch, results) = SearchInMemory(query, newsItems);
            RenderResults(stopwatch, results, "Searching in-memory");

            (stopwatch, results) = await SearchInDatabase(query);
            RenderResults(stopwatch, results, "Searching in SQL Server 2025");

            (stopwatch, results) = await SearchInDatabaseWithIndex(query);
            RenderResults(stopwatch, results, "Searching in SQL Server 2025 with DiskAnn index");
        } while (true);
    }

    private async Task<(Stopwatch stopwatch, List<SimilarityScore<NewsItem>> results)> SearchInDatabase(ReadOnlyMemory<float> query)
    {
        var stopwatch = Stopwatch.StartNew();

        await using var context = new SqlServerNewsContext(config);

        var queryable = context.NewsItems
            .OrderBy(item => EF.Functions.VectorDistance("cosine", item.Embedding, query.ToArray()))
            .Take(10)
            .Select(item => new { item, distance = EF.Functions.VectorDistance("cosine", item.Embedding, query.ToArray()) });

        var matches = await queryable.ToListAsync();

        stopwatch.Stop();

        var results = matches.Select(arg => new SimilarityScore<NewsItem>(arg.item, 1 - arg.distance)).ToList();

        return (stopwatch, results);
    }

    private async Task<(Stopwatch stopwatch, List<SimilarityScore<NewsItem>> results)> SearchInDatabaseWithIndex(ReadOnlyMemory<float> query)
    {
        var topN = new SqlParameter("@topN", SqlDbType.Int) { Value = 10 };
        var search = new SqlParameter("@query", SqlDbTypeExtensions.Vector)
        {
            Value = new SqlVector<float>(query)
        };

        FormattableString sql = $"""
                                 SELECT *
                                 FROM VECTOR_SEARCH(
                                     table = NewsItems AS t,
                                     column = Embedding,
                                     similar_to = {search},
                                     metric = 'cosine',
                                     top_n = {topN}
                                 ) AS s
                                 """;
        var stopwatch = Stopwatch.StartNew();

        await using var context = new SqlServerNewsContext(config);

        var queryable = context.Database.SqlQuery<NewsItemDistance>(sql).OrderBy(d => d.Distance)
            .Select(d => new SimilarityScore<NewsItem>(new NewsItem
            {
                Headline = d.Headline,
                Authors = d.Authors,
                Category = d.Category,
                Link = d.Link,
                ShortDescription = d.ShortDescription,
            }, 1 - d.Distance));

        var results = await queryable.ToListAsync();

        stopwatch.Stop();
        return (stopwatch, results);
    }

    private static (Stopwatch stopwatch, List<SimilarityScore<NewsItem>> results) SearchInMemory(ReadOnlyMemory<float> query, List<NewsItem> newsItems)
    {
        var stopwatch = Stopwatch.StartNew();

        var results = newsItems.Select(item => new SimilarityScore<NewsItem>(item, TensorPrimitives.CosineSimilarity(item.Embedding, query.ToArray())))
                               .OrderByDescending(match => match.Similarity)
                               .Take(10)
                               .ToList();

        stopwatch.Stop();

        return (stopwatch, results);
    }

    private static void RenderResults(Stopwatch stopwatch, List<SimilarityScore<NewsItem>> results, string title = "")
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

    private List<NewsItem> GetNewsItems()
    {
        using var context = new SqlServerNewsContext(config);

        return context.NewsItems.ToList();
    }
}

public record SimilarityScore<T>(T Item, double Similarity);