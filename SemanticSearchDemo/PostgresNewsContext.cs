﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SemanticSearchDemo;

class PostgresNewsContext(IConfiguration config) : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(config.GetConnectionString("Postgres"), options =>options.UseVector());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<NewsItem>().ToTable("NewsItems");
        modelBuilder.Entity<NewsItem>().Ignore(item => item.Embedding);
        modelBuilder.Entity<NewsItem>().Ignore(item => item.EmbeddingBuffer);

        modelBuilder.Entity<NewsItem>().Property(item => item.EmbeddingVector).HasColumnType("vector(384)");

        modelBuilder.Entity<NewsItem>().Property(item => item.Link).HasMaxLength(400);
        modelBuilder.Entity<NewsItem>().Property(item => item.Headline).HasMaxLength(400);
        modelBuilder.Entity<NewsItem>().Property(item => item.Category).HasMaxLength(30);
        modelBuilder.Entity<NewsItem>().Property(item => item.ShortDescription).HasMaxLength(4000);
        modelBuilder.Entity<NewsItem>().Property(item => item.Authors).HasMaxLength(400);
    }

    public DbSet<NewsItem> NewsItems { get; set; }
}