# Semantic Search Demo — SQL Server, pure T-SQL

This branch demonstrates end-to-end semantic search **inside SQL Server** — no client-side AI code. The database generates the embeddings (`AI_GENERATE_EMBEDDINGS` + `CREATE EXTERNAL MODEL`), searches them (exact `VECTOR_DISTANCE` and approximate `VECTOR_SEARCH` over a DiskANN index), and even calls a reranker (`sp_invoke_external_rest_endpoint` → Cohere Rerank).

> Other flavors: [`main`](https://github.com/Giorgi/Semantic-Search-Demo/tree/main) — .NET client-side embeddings with EF Core · [`pgvector`](https://github.com/Giorgi/Semantic-Search-Demo/tree/pgvector) — PostgreSQL.

## Presented at

- **FabCon Europe 2026** (Barcelona) — *Supercharged Search with Semantic Search and Vector Embeddings*

## Prerequisites

- SQL Server 2025 (`PREVIEW_FEATURES = ON` for vector indexes) **or** Azure SQL Database — note: `VECTOR_SEARCH`/DiskANN preview is region-limited (check [feature availability by region](https://learn.microsoft.com/azure/azure-sql/database/region-availability#vector-search))
- An embedding endpoint: Azure OpenAI deployment of `text-embedding-3-small` (on Azure SQL, endpoints must be on the outbound allowlist; on-prem SQL Server can call any endpoint, including OpenAI directly or a local Ollama)
- Dataset: [News Category Dataset](https://www.kaggle.com/datasets/rmisra/news-category-dataset) (`News.json`, ~210K HuffPost headlines; the demos use 5 categories ≈ 53K rows)
- Optional, for the rerank demo: Cohere Rerank deployed in Microsoft Foundry (pay-per-call)

## Run order (`sql-demos/`)

| # | Script | What it does |
|---|--------|--------------|
| 1 | `demo1-setup.sql` (sections 0–5) | master key → credential → external model → smoke test → `NewsItems` table |
| 2 | the console app → *Load data without embeddings* | bulk-loads the headlines (resumable) |
| 3 | `demo1-setup.sql` (section 6) | bulk-embeds in-database, batched (per-row REST calls — expect hours for 53K in one loop; run two loops over disjoint Id ranges to halve it) |
| 4 | `demo1-setup.sql` (section 7) | `CREATE VECTOR INDEX` — **direct statement only**; via dynamic SQL it fails with Msg 42234 |
| 5 | `demo1-search-queries.sql` | semantic search, keyword contrast, cross-lingual queries |
| 6 | `demo2-contrast.sql` | exact vs ANN plans, `STATISTICS IO` index anatomy, the two-dialects error, live DML on the index |
| 7 | `demo3-rerank.sql` | two-stage retrieval: vector top-10 → Cohere Rerank → reorder, all from T-SQL |

Measurement harness (the numbers behind the talk): `measure-1-index-build-time.sql`, `measure-2-recall-at-10.sql`, `measure-3-logical-reads.sql`, `measure-4-query-latency.sql`. Feature probe for any environment: `vector-index-probe.sql`.

## Useful resources

SQL Server / Azure SQL:
 - [Vector search and vector indexes in the SQL Database Engine](https://learn.microsoft.com/en-us/sql/sql-server/ai/vectors?view=sql-server-ver17)
 - [AI_GENERATE_EMBEDDINGS and CREATE EXTERNAL MODEL are GA in Azure SQL](https://devblogs.microsoft.com/azure-sql/generate-embeddings-function-and-external-model-object-support-are-now-generally-available-in-azure-sql/)
 - [Announcing Public Preview of DiskANN in SQL Server 2025](https://techcommunity.microsoft.com/blog/sqlserver/announcing-public-preview-of-diskann-in-sql-server-2025/4414683)
 - [Efficiently and Elegantly Modeling Embeddings in Azure SQL and SQL Server](https://devblogs.microsoft.com/azure-sql/efficiently-and-elegantly-modeling-embeddings-in-azure-sql-and-sql-server/)
 - [sp_invoke_external_rest_endpoint](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-invoke-external-rest-endpoint-transact-sql)
 - [The Two Hybrid Searches in Microsoft SQL](https://devblogs.microsoft.com/azure-sql/two-hybrid-search/)

General:
 - [The Illustrated Word2vec](https://jalammar.github.io/illustrated-word2vec/)
 - [Vector Embeddings Explained](https://weaviate.io/blog/vector-embeddings-explained)
 - [Distance Metrics in Vector Search](https://weaviate.io/blog/distance-metrics-in-vector-search)
