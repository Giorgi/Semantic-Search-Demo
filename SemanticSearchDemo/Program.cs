using System.Diagnostics;
using SmartComponents.LocalEmbeddings;
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

        static void Main(string[] args)
        {
            using var embedder = new LocalEmbedder();
            var lines = File.ReadLines(@"News.json");

            var stopwatch = Stopwatch.StartNew();

            int count = 0;

            var newsItems = lines
                .Select(line => JsonSerializer.Deserialize<NewsItem>(line, JsonOptions))
                .Where(item => Categories.Contains(item.Category))
                .Select(item =>
                {
                    item.Embedding = embedder.Embed(item.Headline);

                    count++;
                    if (count % 10000 == 0)
                    {
                        Console.WriteLine($"Indexed {count} items");
                    }

                    return item;
                }).ToList();

            stopwatch.Stop();

            Console.WriteLine($"Indexed {newsItems.Count} items in {stopwatch.Elapsed}, enter search text {Environment.NewLine}");

            do
            {
                var query = embedder.Embed(Console.ReadLine());
                Console.WriteLine();

                var results = LocalEmbedder.FindClosestWithScore(query, newsItems.Select(item => (item, item.Embedding)), 10);

                foreach (var newsItem in results)
                {
                    Console.WriteLine($"{newsItem.Similarity}  {newsItem.Item.Headline}");
                }
                Console.WriteLine();

            } while (true);
        }
    }
}
