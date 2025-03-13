using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SemanticSearchDemo;

class SqlServerNewsContext(IConfiguration config) : NewsItemsBaseContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(config.GetConnectionString("SqlServer"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewsItem>().ToTable("NewsItems");
        modelBuilder.Entity<NewsItem>().Ignore(item => item.Embedding);
        modelBuilder.Entity<NewsItem>().Ignore(item => item.EmbeddingData);
        modelBuilder.Entity<NewsItem>().Ignore(item => item.EmbeddingVector);

        modelBuilder.Entity<NewsItem>().Property(item => item.Link).HasMaxLength(400);
        modelBuilder.Entity<NewsItem>().Property(item => item.Headline).HasMaxLength(400);
        modelBuilder.Entity<NewsItem>().Property(item => item.Category).HasMaxLength(30);
        modelBuilder.Entity<NewsItem>().Property(item => item.ShortDescription).HasMaxLength(4000);
        modelBuilder.Entity<NewsItem>().Property(item => item.Authors).HasMaxLength(400);
    }
}