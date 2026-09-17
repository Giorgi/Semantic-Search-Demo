-- ============================================================================
-- DEMO 1 SETUP - database-side embeddings in fabcon-demo
-- Run section by section in SSMS connected to fabcon-demo (check the guard!).
-- TWO placeholders to fill with your own values:
--   <MASTER-KEY-PASSWORD>  and  <API-KEY>
-- Keep this file placeholder-only: it will end up in the public demo repo.
-- Note: first statement resumes the paused db (~30-60s) - that is expected.
-- ============================================================================

-- 0. context guard
SELECT DB_NAME() AS must_be_fabcon_demo;
GO

-- 1. master key (one-time; encrypts the credential secret at rest)
IF NOT EXISTS (SELECT 1 FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
    CREATE MASTER KEY ENCRYPTION BY PASSWORD = '<MASTER-KEY-PASSWORD>';
GO

-- 2. credential - the NAME must equal the endpoint base URL that LOCATION starts with
CREATE DATABASE SCOPED CREDENTIAL [https://fabcon-openai-gd26.openai.azure.com]
WITH IDENTITY = 'HTTPEndpointHeaders',
     SECRET = '{"api-key":"<API-KEY>"}';
GO

-- 3. the external model (slide 20 still says MyEmbeddingModel - deck update pending)
-- If this or the smoke test errors with an api-version complaint, try api-version=2024-02-01.
CREATE EXTERNAL MODEL TextEmbeddingModel
WITH (
    LOCATION   = 'https://fabcon-openai-gd26.openai.azure.com/openai/deployments/text-embedding-3-small/embeddings?api-version=2023-05-15',
    API_FORMAT = 'Azure OpenAI',
    MODEL_TYPE = EMBEDDINGS,
    MODEL      = 'text-embedding-3-small',
    CREDENTIAL = [https://fabcon-openai-gd26.openai.azure.com]
);
GO

-- 4. SMOKE TEST - the database's first embedding
SELECT AI_GENERATE_EMBEDDINGS(N'Supercharged search in Barcelona' USE MODEL TextEmbeddingModel) AS embedding;
GO

-- 5. the corpus table (loader inserts text; Embedding stays NULL until step 6)
CREATE TABLE dbo.NewsItems
(
    Id               INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,  -- clustered PK: vector-index requirement
    Link             NVARCHAR(400)  NOT NULL,
    Headline         NVARCHAR(400)  NOT NULL,
    Category         NVARCHAR(30)   NOT NULL,
    ShortDescription NVARCHAR(4000) NOT NULL,
    Authors          NVARCHAR(400)  NOT NULL,
    Date             DATE           NOT NULL,
    Embedding        VECTOR(1536)   NULL
);
GO

-- 6. AFTER the loader has inserted the 53K rows: bulk-embed IN THE DATABASE,
-- batched so each statement is one commit and rate limits stay comfortable.
-- (data prep, run once during prep - NOT a stage act; ~1-2 cents total)
/*
DECLARE @batch INT = 1;
WHILE @batch > 0
BEGIN
    UPDATE TOP (1000) dbo.NewsItems
    SET Embedding = AI_GENERATE_EMBEDDINGS(Headline USE MODEL TextEmbeddingModel)
    WHERE Embedding IS NULL;
    SET @batch = @@ROWCOUNT;
    RAISERROR('batch done: %d rows', 0, 1, @batch) WITH NOWAIT;
END
SELECT COUNT(*) AS total, COUNT(Embedding) AS embedded FROM dbo.NewsItems;
*/
GO

-- 7. THEN the vector index - DIRECT STATEMENT ONLY (sp_executesql-issued creates
-- fail on Azure v3: internal DBCC TRACEON permission error -> Msg 42234)
/*
CREATE VECTOR INDEX news_embedding_idx ON dbo.NewsItems(Embedding)
WITH (METRIC = 'cosine', TYPE = 'diskann');
-- verify: StartId must be a real value, NOT "0" (0 = failed-build corpse)
SELECT i.name, JSON_VALUE(vi.build_parameters,'$.Version') AS version, vi.build_parameters
FROM sys.vector_indexes vi JOIN sys.indexes i ON vi.object_id = i.object_id AND vi.index_id = i.index_id;
*/
