# Semantic Search Demo

A sample app showing how to use [Smart Components](https://github.com/dotnet-smartcomponents/smartcomponents), [pgvector](https://github.com/pgvector/pgvector)
and [OpenAI](https://github.com/openai/openai-dotnet) to generate vector embeddings and implement semantic search in .NET.

Vector embeddings can be generated locally by using [Local Embeddings](https://github.com/dotnet-smartcomponents/smartcomponents/blob/main/docs/local-embeddings.md) or by using OpenAI API.
The generated embeddings are stored in a PostgreSQL database and queried with Entity Framework Core and [pgvector-dotnet](https://github.com/pgvector/pgvector-dotnet)

## Useful resources

 - [The Illustrated Word2vec](https://jalammar.github.io/illustrated-word2vec/)
 - [Vector Embeddings Explained](https://weaviate.io/blog/vector-embeddings-explained)
 - [Distance Metrics in Vector Search](https://weaviate.io/blog/distance-metrics-in-vector-search)
 - [Why is Vector Search so fast?](https://weaviate.io/blog/why-is-vector-search-so-fast)
 - [Vectors are the new JSON in PostgreSQL](https://jkatz05.com/post/postgres/vectors-json-postgresql/)
 - [Vectors are the new JSON (PGConf.EU 2023 Recording)](https://www.youtube.com/watch?v=D_1zunKblAU)
 - [Postgres is all you need, even for vectors](https://anyblockers.com/posts/postgres-is-all-you-need-even-for-vectors)
 - [Vector Indexes in Postgres using pgvector: IVFFlat vs HNSW](https://tembo.io/blog/vector-indexes-in-pgvector)
 - [Understanding vector search and HNSW index with pgvector](https://neon.tech/blog/understanding-vector-search-and-hnsw-index-with-pgvector)
 - [Using Vector Databases for Multimodal Embeddings and Search - Zain Hasan - NDC London 2024](https://www.youtube.com/watch?v=2O81YU_VHDc)
