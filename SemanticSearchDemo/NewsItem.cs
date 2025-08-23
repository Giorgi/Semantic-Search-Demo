namespace SemanticSearchDemo;

public class NewsItem
{
    public int Id { get; set; }
    public required string Link { get; set; }
    public required string Headline { get; set; }
    public required string Category { get; set; }
    public required string ShortDescription { get; set; }
    public required string Authors { get; set; }
    public DateOnly Date { get; set; }

    //Used by Azure SQL Data vector data type
    public float[] EmbeddingData { get; set; }
}