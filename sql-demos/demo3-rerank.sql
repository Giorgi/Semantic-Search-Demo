-- DEMO 3 (FINALE): reranking from T-SQL - Cohere Rerank v4.0-fast via sp_invoke_external_rest_endpoint
-- Fill <RERANK-KEY> with your Foundry deployment key (setup section only)

-- One-time setup: credential for the Foundry endpoint (name must match the URL base)
/*
CREATE DATABASE SCOPED CREDENTIAL [https://fabconfoundryh3740598439.services.ai.azure.com]
WITH IDENTITY = 'HTTPEndpointHeaders',
     SECRET = '{"api-key":"<RERANK-KEY>"}';
*/
-- If sp_invoke refuses the domain (outbound allowlist), use the same resource's other domain:
-- credential + URLs on https://fabconfoundryh3740598439.cognitiveservices.azure.com instead.
GO

-- Smoke test: 3 documents, the database calls the reranker
DECLARE @response NVARCHAR(MAX);
EXEC sp_invoke_external_rest_endpoint
    @url = 'https://fabconfoundryh3740598439.services.ai.azure.com/providers/cohere/v2/rerank',
    @method = 'POST',
    @credential = [https://fabconfoundryh3740598439.services.ai.azure.com],
    @payload = N'{"model":"Cohere-rerank-v4.0-fast","query":"flight that disappeared","documents":["What Is Up With These Disappearing Airfares?","Missing Malaysian Airlines Flight 370","How To Miss Your Flight"],"top_n":3}',
    @response = @response OUTPUT;
SELECT @response AS raw_response;
GO


-- ============ THE FINALE: before / after on the real corpus ============

-- Stage 1: vector search finds the top 10 - the impostor sits at #1
DECLARE @query NVARCHAR(400) = N'flight that disappeared';
DECLARE @query_vector VECTOR(1536) = AI_GENERATE_EMBEDDINGS(@query USE MODEL TextEmbeddingModel);

DROP TABLE IF EXISTS #candidates;
SELECT TOP (10)
    n.Id, n.Headline,
    VECTOR_DISTANCE('cosine', n.Embedding, @query_vector) AS distance,
    ROW_NUMBER() OVER (ORDER BY VECTOR_DISTANCE('cosine', n.Embedding, @query_vector)) - 1 AS doc_index
INTO #candidates
FROM dbo.NewsItems AS n
ORDER BY distance;

-- BEFORE: ranked by vector distance alone
SELECT doc_index + 1 AS rank, Headline, distance FROM #candidates ORDER BY doc_index;

-- Stage 2: the database sends the candidates to the reranker
DECLARE @documents NVARCHAR(MAX) =
    (SELECT STRING_AGG('"' + STRING_ESCAPE(Headline, 'json') + '"', ',') WITHIN GROUP (ORDER BY doc_index)
     FROM #candidates);
DECLARE @payload NVARCHAR(MAX) =
    N'{"model":"Cohere-rerank-v4.0-fast","query":"' + STRING_ESCAPE(@query, 'json')
    + N'","documents":[' + @documents + N'],"top_n":10}';

DECLARE @response NVARCHAR(MAX);
EXEC sp_invoke_external_rest_endpoint
    @url = 'https://fabconfoundryh3740598439.services.ai.azure.com/providers/cohere/v2/rerank',
    @method = 'POST',
    @credential = [https://fabconfoundryh3740598439.services.ai.azure.com],
    @payload = @payload,
    @response = @response OUTPUT;

-- AFTER: reordered by what the query actually MEANS
SELECT ROW_NUMBER() OVER (ORDER BY x.relevance_score DESC) AS new_rank,
       c.Headline,
       x.relevance_score,
       c.doc_index + 1 AS vector_rank
FROM OPENJSON(@response, '$.result.results')
     WITH (doc_index INT '$.index', relevance_score FLOAT '$.relevance_score') AS x
JOIN #candidates AS c ON c.doc_index = x.doc_index
ORDER BY new_rank;
