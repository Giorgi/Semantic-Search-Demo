-- Re-measures the slide 28 logical-reads cells on the CURRENT index build.
-- Run in fabcon-demo. Run the whole file TWICE and read the SECOND run's
-- Messages tab (first run warms the cache). Query vector = a stored embedding,
-- so no API calls - pure page reads.
--
-- How to total (Messages tab):
--   ANN   = sum of logical reads over NewsItems + vector_index_quantization_*
--           + vector_index_Graph_Edge_* + vector_index_watermark_* + Worktables
--           (STATISTICS IO counts row-overflow under 'lob logical reads')
--   Exact = NewsItems logical reads + lob logical reads
SET STATISTICS IO ON;

DECLARE @qv VECTOR(1536) = (SELECT Embedding FROM dbo.NewsItems WHERE Id = 7);

-- ANN through the index
SELECT TOP (10) WITH APPROXIMATE t.Id, t.Headline, s.distance
FROM VECTOR_SEARCH(TABLE = dbo.NewsItems AS t, COLUMN = Embedding, SIMILAR_TO = @qv, METRIC = 'cosine') AS s
ORDER BY s.distance;

-- exact scan
SELECT TOP (10) n.Id, n.Headline,
    VECTOR_DISTANCE('cosine', n.Embedding, @qv) AS distance
FROM dbo.NewsItems AS n
ORDER BY distance;

SET STATISTICS IO OFF;
