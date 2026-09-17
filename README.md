# Semantic Search Demo

> [!NOTE]
> For pgvector demo, check out the [pgvector branch](https://github.com/Giorgi/Semantic-Search-Demo/tree/pgvector).
> 
> For the pure T-SQL SQL Server demo (embeddings, DiskANN vector index, and reranking all inside the database - as presented at FabCon Europe 2026), check out the [sql-server branch](https://github.com/Giorgi/Semantic-Search-Demo/tree/sql-server).

A sample app showing how to use [Ollama](https://ollama.com/), [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
and [OpenAI](https://github.com/openai/openai-dotnet) to generate vector embeddings and implement semantic search in .NET.

Vector embeddings can be generated locally using [OllamaSharp](https://github.com/awaescher/OllamaSharp) or via the OpenAI API.
The generated embeddings are stored in a SQL Server database and queried with Entity Framework Core and [EFCore.SqlServer.VectorSearch](https://github.com/efcore/EFCore.SqlServer.VectorSearch/)

## Useful resources

General:
 - [The Illustrated Word2vec](https://jalammar.github.io/illustrated-word2vec/)
 - [Vector Embeddings Explained](https://weaviate.io/blog/vector-embeddings-explained)
 - [Distance Metrics in Vector Search](https://weaviate.io/blog/distance-metrics-in-vector-search)
 - [Why is Vector Search so fast?](https://weaviate.io/blog/why-is-vector-search-so-fast)
 - [Using Vector Databases for Multimodal Embeddings and Search - Zain Hasan - NDC London 2024](https://www.youtube.com/watch?v=2O81YU_VHDc)

PostgreSQL specific: 
 - [Vectors are the new JSON in PostgreSQL](https://jkatz05.com/post/postgres/vectors-json-postgresql/)
 - [Vectors are the new JSON (PGConf.EU 2023 Recording)](https://www.youtube.com/watch?v=D_1zunKblAU)
 - [Postgres is all you need, even for vectors](https://anyblockers.com/posts/postgres-is-all-you-need-even-for-vectors)
 - [Vector Indexes in Postgres using pgvector: IVFFlat vs HNSW](https://tembo.io/blog/vector-indexes-in-pgvector)
 - [Understanding vector search and HNSW index with pgvector](https://neon.tech/blog/understanding-vector-search-and-hnsw-index-with-pgvector)

SQL Server specific:
 - [Overview of vector search and vector indexes in the SQL Database Engine](https://learn.microsoft.com/en-us/sql/relational-databases/vectors/vectors-sql-server?view=sql-server-ver17) 
 - [Announcing Public Preview of DiskANN in SQL Server 2025](https://techcommunity.microsoft.com/blog/sqlserver/announcing-public-preview-of-diskann-in-sql-server-2025/4414683)
 - [Efficiently and Elegantly Modeling Embeddings in Azure SQL and SQL Server](https://devblogs.microsoft.com/azure-sql/efficiently-and-elegantly-modeling-embeddings-in-azure-sql-and-sql-server/)
