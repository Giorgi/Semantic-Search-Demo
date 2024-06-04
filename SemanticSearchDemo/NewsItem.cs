using SmartComponents.LocalEmbeddings;

namespace SemanticSearchDemo;

public class NewsItem
{
    public required string Link { get; set; }
    public required string Headline { get; set; }
    public required string Category { get; set; }
    public required string ShortDescription { get; set; }
    public required string Authors { get; set; }
    public DateTime Date { get; set; }

    public EmbeddingF32 Embedding { get; set; }
}