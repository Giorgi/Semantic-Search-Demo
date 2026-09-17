-- DEMO 2: exact vs ANN, the index anatomy, the dialect wall, live DML
-- Before running: enable Include Actual Execution Plan (Ctrl+M)

-- Exact kNN: scans all 53,110 rows, computes every distance
DECLARE @query NVARCHAR(400) = N'Volcano disrupting flights';
DECLARE @query_vector VECTOR(1536) = AI_GENERATE_EMBEDDINGS(@query USE MODEL TextEmbeddingModel);

SELECT TOP (10) n.Id, n.Headline,
    VECTOR_DISTANCE('cosine', n.Embedding, @query_vector) AS distance
FROM dbo.NewsItems AS n
ORDER BY distance;

-- Same question through the DiskANN index: watch for the 'Vector Index Seek' operator
SELECT TOP (10) WITH APPROXIMATE t.Id, t.Headline, s.distance
FROM VECTOR_SEARCH(TABLE = dbo.NewsItems AS t, COLUMN = Embedding, SIMILAR_TO = @query_vector, METRIC = 'cosine') AS s
ORDER BY s.distance;
GO


-- The anatomy: STATISTICS IO exposes the index internals - quantization codes,
-- graph edges, DML watermark (~5K reads) vs the full scan (~86K reads, 30K of them LOB)
SET STATISTICS IO ON;

DECLARE @query_vector VECTOR(1536) = AI_GENERATE_EMBEDDINGS(N'Volcano disrupting flights' USE MODEL TextEmbeddingModel);

SELECT TOP (10) WITH APPROXIMATE t.Id, t.Headline, s.distance
FROM VECTOR_SEARCH(TABLE = dbo.NewsItems AS t, COLUMN = Embedding, SIMILAR_TO = @query_vector, METRIC = 'cosine') AS s
ORDER BY s.distance;

SELECT TOP (10) n.Id, n.Headline,
    VECTOR_DISTANCE('cosine', n.Embedding, @query_vector) AS distance
FROM dbo.NewsItems AS n
ORDER BY distance;

SET STATISTICS IO OFF;
GO


-- The dialect wall: the legacy TOP_N syntax parses - and the v3 index refuses it (Msg 42274)
DECLARE @query_vector VECTOR(1536) = AI_GENERATE_EMBEDDINGS(N'Volcano disrupting flights' USE MODEL TextEmbeddingModel);

SELECT TOP (10) t.Id, t.Headline, s.distance
FROM VECTOR_SEARCH(TABLE = dbo.NewsItems AS t, COLUMN = Embedding, SIMILAR_TO = @query_vector, METRIC = 'cosine', TOP_N = 10) AS s
ORDER BY s.distance;
GO


-- The 'before': nothing in a 2012-2022 news corpus resembles a tech conference in Spain
DECLARE @query_vector VECTOR(1536) = AI_GENERATE_EMBEDDINGS(N'tech conference in Spain' USE MODEL TextEmbeddingModel);

SELECT TOP (5) WITH APPROXIMATE t.Id, t.Headline, s.distance
FROM VECTOR_SEARCH(TABLE = dbo.NewsItems AS t, COLUMN = Embedding, SIMILAR_TO = @query_vector, METRIC = 'cosine') AS s
ORDER BY s.distance;
GO


-- Live DML: insert a brand-new row - the v3 index absorbs it in real time (no rebuild, not read-only)
INSERT INTO dbo.NewsItems (Link, Headline, Category, ShortDescription, Authors, Date, Embedding)
VALUES ('https://fabconeurope.com', N'FabCon Europe lights up Barcelona as data professionals gather',
        'TECH', N'Thousands of data platform professionals meet at the CCIB for Microsoft Fabric sessions.',
        N'Giorgi Dalakishvili', '2026-09-30',
        AI_GENERATE_EMBEDDINGS(N'FabCon Europe lights up Barcelona as data professionals gather' USE MODEL TextEmbeddingModel));
GO

-- ...and the INDEX already finds it (WITH APPROXIMATE = served by DiskANN, seconds after the insert)
DECLARE @query_vector VECTOR(1536) = AI_GENERATE_EMBEDDINGS(N'tech conference in Spain' USE MODEL TextEmbeddingModel);

SELECT TOP (5) WITH APPROXIMATE t.Id, t.Headline, s.distance
FROM VECTOR_SEARCH(TABLE = dbo.NewsItems AS t, COLUMN = Embedding, SIMILAR_TO = @query_vector, METRIC = 'cosine') AS s
ORDER BY s.distance;
GO


-- Reset after rehearsals (keeps the demo re-runnable)
-- DELETE FROM dbo.NewsItems WHERE Link = 'https://fabconeurope.com';
