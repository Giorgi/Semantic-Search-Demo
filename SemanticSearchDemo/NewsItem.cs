using SmartComponents.LocalEmbeddings;

namespace SemanticSearchDemo;

public class NewsItem
{
    public string Link { get; set; }
    public string Headline { get; set; }
    public string Category { get; set; }
    public string ShortDescription { get; set; }
    public string Authors { get; set; }
    public DateTime Date { get; set; }

    public EmbeddingF32 Embedding { get; set; }
}