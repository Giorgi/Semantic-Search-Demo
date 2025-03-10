using Microsoft.EntityFrameworkCore;

namespace SemanticSearchDemo;

internal class NewsItemsBaseContext : DbContext
{
    public DbSet<NewsItem> NewsItems { get; set; }
}