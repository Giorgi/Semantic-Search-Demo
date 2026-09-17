-- Measures warm query latency for slide 28 (run in fabcon-demo).
-- 11 iterations per variant; iteration 0 is the warm-up and is excluded.
-- Results go into temp tables so both variants pay identical materialization cost.
-- Quote: avg_ms (typical) and min_ms (best case) from the summary.
SET NOCOUNT ON;
DECLARE @qv VECTOR(1536) = (SELECT Embedding FROM dbo.NewsItems WHERE Id = 7);

DROP TABLE IF EXISTS #timings;
CREATE TABLE #timings (variant VARCHAR(10), iter INT, ms FLOAT);

DECLARE @i INT = 0, @t0 DATETIME2;
WHILE @i <= 10
BEGIN
    DROP TABLE IF EXISTS #r1;
    SET @t0 = SYSDATETIME();
    SELECT TOP (10) WITH APPROXIMATE t.Id, s.distance
    INTO #r1
    FROM VECTOR_SEARCH(TABLE = dbo.NewsItems AS t, COLUMN = Embedding, SIMILAR_TO = @qv, METRIC = 'cosine') AS s
    ORDER BY s.distance;
    INSERT INTO #timings VALUES ('ANN', @i, DATEDIFF(MICROSECOND, @t0, SYSDATETIME()) / 1000.0);

    DROP TABLE IF EXISTS #r2;
    SET @t0 = SYSDATETIME();
    SELECT TOP (10) n.Id, VECTOR_DISTANCE('cosine', n.Embedding, @qv) AS distance
    INTO #r2
    FROM dbo.NewsItems AS n
    ORDER BY distance;
    INSERT INTO #timings VALUES ('Exact', @i, DATEDIFF(MICROSECOND, @t0, SYSDATETIME()) / 1000.0);

    SET @i += 1;
END

SELECT variant,
       COUNT(*)                 AS runs,
       CAST(AVG(ms) AS DECIMAL(8,1)) AS avg_ms,
       CAST(MIN(ms) AS DECIMAL(8,1)) AS min_ms,
       CAST(MAX(ms) AS DECIMAL(8,1)) AS max_ms
FROM #timings
WHERE iter > 0            -- iteration 0 = warm-up, excluded
GROUP BY variant;

-- per-iteration detail if the spread looks odd
SELECT * FROM #timings ORDER BY variant, iter;
