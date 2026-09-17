-- Measures Recall@10 for slide 28 (run in fabcon-demo AFTER measure-1 finishes).
-- 100 random rows; each row's own embedding is the query; exact top-10 vs
-- forced-ANN top-10 overlap. Divergent queries (if any) are listed - demo gold.
SET NOCOUNT ON;
DROP TABLE IF EXISTS #queries;
SELECT TOP (100) Id, Embedding INTO #queries FROM dbo.NewsItems ORDER BY NEWID();

DROP TABLE IF EXISTS #overlap;
CREATE TABLE #overlap (qid INT, matches INT);

DECLARE @qid INT, @qv VECTOR(1536);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Id, Embedding FROM #queries;
OPEN c;
FETCH NEXT FROM c INTO @qid, @qv;
WHILE @@FETCH_STATUS = 0
BEGIN
    WITH exact AS (
        SELECT TOP (10) Id
        FROM dbo.NewsItems
        ORDER BY VECTOR_DISTANCE('cosine', Embedding, @qv)
    ),
    ann AS (
        SELECT TOP (10) WITH APPROXIMATE t.Id
        FROM VECTOR_SEARCH(TABLE = dbo.NewsItems AS t, COLUMN = Embedding, SIMILAR_TO = @qv, METRIC = 'cosine') AS s WITH (FORCE_ANN_ONLY)
        ORDER BY s.distance
    )
    INSERT INTO #overlap
    SELECT @qid, (SELECT COUNT(*) FROM exact e JOIN ann a ON e.Id = a.Id);
    FETCH NEXT FROM c INTO @qid, @qv;
END
CLOSE c; DEALLOCATE c;

SELECT AVG(matches * 10.0)          AS recall_at_10_pct,      -- slide 28 Recall@10 cell
       MIN(matches)                 AS worst_query_matches,
       SUM(IIF(matches < 10, 1, 0)) AS queries_below_100pct,
       COUNT(*)                     AS queries_tested
FROM #overlap;

-- any divergent queries: a live specimen of 'approximate' being approximate
SELECT o.qid, n.Headline, o.matches
FROM #overlap o JOIN dbo.NewsItems n ON n.Id = o.qid
WHERE o.matches < 10
ORDER BY o.matches;
