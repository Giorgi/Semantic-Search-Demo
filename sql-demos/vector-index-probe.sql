-- ============================================================================
-- VECTOR INDEX PROBE v3 - on-prem: any scratch db; Azure: run INSIDE the demo db
-- (never CREATE DATABASE on Azure - default SLO is provisioned = billed 24/7).
-- v2: GO-separated batches + dynamic SQL so preview-gated grammar compiles at
-- execution time (parse errors are catchable) and PREVIEW_FEATURES takes effect
-- before any preview syntax is compiled.
-- v3 (2026-09-16): CREATE VECTOR INDEX and other parse-safe statements are now
-- DIRECT statements - on Azure, creates issued via sp_executesql failed 5/5 with
-- Msg 42234 'internal error 200' (v3 async builder x nested batch interaction,
-- suspected), while the identical direct statement builds fine. Dynamic SQL is
-- kept ONLY where a parse error is the thing being tested (WITH APPROXIMATE and
-- ALLOW_STALE_VECTOR_INDEX are parse errors on-prem CU8; TRY/CATCH can't catch
-- parse errors without the dynamic-SQL indirection).
-- ============================================================================

SET NOCOUNT ON;
PRINT '=== ENGINE ===';
PRINT CONCAT('Version: ', @@VERSION);
PRINT CONCAT('Edition: ', CAST(SERVERPROPERTY('Edition') AS NVARCHAR(128)),
             ' | ProductVersion: ', CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(128)),
             ' | UpdateLevel: ', ISNULL(CAST(SERVERPROPERTY('ProductUpdateLevel') AS NVARCHAR(128)), 'n/a'));
GO

PRINT '';
PRINT '=== STEP 0: PREVIEW_FEATURES (required on-prem; may fail/be unneeded on Azure) ===';
BEGIN TRY
    EXEC sp_executesql N'ALTER DATABASE SCOPED CONFIGURATION SET PREVIEW_FEATURES = ON;';
    PRINT 'PREVIEW_FEATURES = ON succeeded';
END TRY
BEGIN CATCH
    PRINT CONCAT('PREVIEW_FEATURES failed: Msg ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
END CATCH;
GO

PRINT '';
PRINT '=== STEP 1: table + 120 mock vectors (min 100 required for index) ===';
IF OBJECT_ID('dbo.VectorIndexProbe','U') IS NOT NULL DROP TABLE dbo.VectorIndexProbe;
GO
CREATE TABLE dbo.VectorIndexProbe
(
    id INT PRIMARY KEY CLUSTERED,   -- clustered PK is a vector-index requirement
    title NVARCHAR(100),
    v VECTOR(5)
);
GO
INSERT INTO dbo.VectorIndexProbe (id, title, v)
SELECT value,
       CONCAT('Row ', value),
       CAST(JSON_ARRAY(value*0.01, value*0.02, value*0.03, value*0.04, value*0.05) AS VECTOR(5))
FROM GENERATE_SERIES(1, 120);
PRINT CONCAT('Rows inserted: ', @@ROWCOUNT);
GO

PRINT '';
PRINT '=== STEP 2: CREATE VECTOR INDEX (DiskANN) ===';
BEGIN TRY
    -- DIRECT statement, deliberately NOT sp_executesql (Azure v3 builder fails via dynamic SQL - Msg 42234)
    CREATE VECTOR INDEX vip_idx ON dbo.VectorIndexProbe(v) WITH (METRIC = 'cosine', TYPE = 'diskann');
    PRINT 'Vector index created';
    PRINT 'Waiting 15s for async build (Azure builds DiskANN indexes in the background - Msg 42256 if queried too soon)...';
    WAITFOR DELAY '00:00:15';
END TRY
BEGIN CATCH
    PRINT CONCAT('CREATE VECTOR INDEX failed: Msg ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
END CATCH;
GO

PRINT '';
PRINT '=== STEP 3: index version (v3/latest = DML + iterative filtering) ===';
BEGIN TRY
    EXEC sp_executesql N'
        SELECT i.name AS index_name,
               JSON_VALUE(vi.build_parameters, ''$.Version'') AS index_version,
               vi.build_parameters
        FROM sys.vector_indexes vi
        JOIN sys.indexes i ON vi.object_id = i.object_id AND vi.index_id = i.index_id
        WHERE vi.object_id = OBJECT_ID(''dbo.VectorIndexProbe'');';
END TRY
BEGIN CATCH
    PRINT CONCAT('sys.vector_indexes query failed: Msg ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
END CATCH;
GO

PRINT '';
PRINT '=== TEST A: DML after index - INSERT ===';
BEGIN TRY
    INSERT INTO dbo.VectorIndexProbe (id, title, v) VALUES (997, N'post-index insert', CAST('[0.1,0.2,0.3,0.4,0.5]' AS VECTOR(5)));
    PRINT 'INSERT SUCCEEDED -> table is NOT read-only';
END TRY
BEGIN CATCH
    PRINT CONCAT('INSERT FAILED -> read-only behavior: Msg ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
END CATCH;
GO

PRINT '';
PRINT '=== TEST A2: DML after index - UPDATE ===';
BEGIN TRY
    UPDATE dbo.VectorIndexProbe SET title = N'updated' WHERE id = 1;
    PRINT 'UPDATE SUCCEEDED';
END TRY
BEGIN CATCH
    PRINT CONCAT('UPDATE FAILED: Msg ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
END CATCH;
GO

PRINT '';
PRINT '=== TEST B: ALLOW_STALE_VECTOR_INDEX (docs: not available on SQL Server 2025) ===';
BEGIN TRY
    EXEC sp_executesql N'ALTER DATABASE SCOPED CONFIGURATION SET ALLOW_STALE_VECTOR_INDEX = ON;';
    PRINT 'ALLOW_STALE_VECTOR_INDEX = ON succeeded (available on this engine)';
    BEGIN TRY
        EXEC sp_executesql N'INSERT INTO dbo.VectorIndexProbe (id, title, v) VALUES (998, N''stale-mode insert'', CAST(''[0.5,0.4,0.3,0.2,0.1]'' AS VECTOR(5)));';
        PRINT 'INSERT under ALLOW_STALE succeeded (index stale until rebuilt)';
    END TRY
    BEGIN CATCH
        PRINT CONCAT('INSERT under ALLOW_STALE failed: Msg ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
    END CATCH;
END TRY
BEGIN CATCH
    PRINT CONCAT('ALLOW_STALE_VECTOR_INDEX FAILED (expected on-prem): Msg ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
END CATCH;
GO

PRINT '';
PRINT '=== TEST C1: NEW syntax - SELECT TOP(N) WITH APPROXIMATE ===';
BEGIN TRY
    EXEC sp_executesql N'
        DECLARE @qv VECTOR(5) = ''[0.3,0.3,0.3,0.3,0.3]'';
        SELECT TOP(3) WITH APPROXIMATE t.id, t.title, s.distance
        FROM VECTOR_SEARCH(TABLE = dbo.VectorIndexProbe AS t, COLUMN = v, SIMILAR_TO = @qv, METRIC = ''cosine'') AS s
        ORDER BY s.distance;';
    PRINT 'NEW syntax (TOP WITH APPROXIMATE) SUCCEEDED';
END TRY
BEGIN CATCH
    PRINT CONCAT('NEW syntax FAILED: Msg ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
END CATCH;
GO

PRINT '';
PRINT '=== TEST C2: OLD syntax - TOP_N parameter (deprecated; Msg 42274 expected on v3) ===';
BEGIN TRY
    DECLARE @qv2 VECTOR(5) = '[0.3,0.3,0.3,0.3,0.3]';
    SELECT TOP(3) t.id, t.title, s.distance
    FROM VECTOR_SEARCH(TABLE = dbo.VectorIndexProbe AS t, COLUMN = v, SIMILAR_TO = @qv2, METRIC = 'cosine', TOP_N = 3) AS s
    ORDER BY s.distance;
    PRINT 'OLD syntax (TOP_N) SUCCEEDED';
END TRY
BEGIN CATCH
    PRINT CONCAT('OLD syntax FAILED: Msg ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
END CATCH;
GO

PRINT '';
PRINT '=== DONE. Manual cleanup when finished inspecting: ===';
PRINT '-- DROP INDEX vip_idx ON dbo.VectorIndexProbe; DROP TABLE dbo.VectorIndexProbe;';
