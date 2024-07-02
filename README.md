# Semantic Search Demo

A sample app showing how to use [Smart Components](https://github.com/dotnet-smartcomponents/smartcomponents), [pgvector](https://github.com/pgvector/pgvector)
and [OpenAI](https://github.com/openai/openai-dotnet) to generate vector embeddings and implement semantic search in .NET.

Vector embeddings can be generated locally by using [Local Embeddings](https://github.com/dotnet-smartcomponents/smartcomponents/blob/main/docs/local-embeddings.md) or by using OpenAI API.
The generated embeddings are stored in a PostgreSQL database and queried with Entity Framework Core and [pgvector-dotnet](https://github.com/pgvector/pgvector-dotnet)
