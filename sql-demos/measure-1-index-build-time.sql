-- Measures the vector index build time for slide 28 (run in fabcon-demo).
-- Drops + recreates news_embedding_idx, then polls until the index actually
-- serves queries (build is async). ANN queries fail during the rebuild window.
-- Poll runs via sp_executesql: index-binding errors after a schema change are
-- statement-recompilation errors that direct TRY/CATCH cannot catch.
DECLARE @t0 DATETIME2 = SYSDATETIME();

DROP INDEX IF EXISTS news_embedding_idx ON dbo.NewsItems;

-- DIRECT statement only (never dynamic SQL - the Msg 42234 lesson)
CREATE VECTOR INDEX news_embedding_idx ON dbo.NewsItems(Embedding)
WITH (METRIC = 'cosine', TYPE = 'diskann');

PRINT CONCAT('create statement returned after: ', DATEDIFF(SECOND, @t0, SYSDATETIME()), ' s');

DECLARE @ready BIT = 0;
WHILE @ready = 0
BEGIN
    BEGIN TRY
        EXEC sp_executesql N'
            DECLARE @qv VECTOR(1536) = (SELECT TOP (1) Embedding FROM dbo.NewsItems ORDER BY Id);
            DECLARE @d INT;
            SELECT TOP (1) WITH APPROXIMATE @d = t.Id
            FROM VECTOR_SEARCH(TABLE = dbo.NewsItems AS t, COLUMN = Embedding, SIMILAR_TO = @qv, METRIC = ''cosine'') AS s WITH (FORCE_ANN_ONLY)
            ORDER BY s.distance;';
        SET @ready = 1;
    END TRY
    BEGIN CATCH
        WAITFOR DELAY '00:00:05';
    END CATCH
END
PRINT CONCAT('TIME TO QUERYABLE (slide 28 build-time cell): ', DATEDIFF(SECOND, @t0, SYSDATETIME()), ' s');

-- sanity: real StartId, not a corpse
SELECT i.name, JSON_VALUE(vi.build_parameters, '$.Version') AS version, vi.build_parameters
FROM sys.vector_indexes vi JOIN sys.indexes i ON vi.object_id = i.object_id AND vi.index_id = i.index_id;
