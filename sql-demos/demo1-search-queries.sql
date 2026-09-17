-- The database turns text into a vector: one statement, no client code
SELECT AI_GENERATE_EMBEDDINGS(N'Volcano disrupting flights' USE MODEL TextEmbeddingModel)

-- Keyword search: both words must literally appear - and they almost never do
SELECT TOP (10) n.Id, n.Headline, n.Category, n.Date
FROM dbo.NewsItems AS n
WHERE n.Headline LIKE N'%volcano%' AND n.Headline LIKE N'%flight%'
ORDER BY n.Date DESC;


-- Semantic search: matches meaning, not words (ash, eruptions, airport shutdowns)
DECLARE @query NVARCHAR(400) = N'Volcano disrupting flights';
DECLARE @query_vector VECTOR(1536) = AI_GENERATE_EMBEDDINGS(@query USE MODEL TextEmbeddingModel);

SELECT TOP (10) n.Id, n.Headline, n.Category, n.Date,
    VECTOR_DISTANCE('cosine', n.Embedding, @query_vector) AS distance
FROM dbo.NewsItems AS n
ORDER BY distance;


-- Finds MH370 and Amelia Earhart from two words - note the impostor at #1
DECLARE @query NVARCHAR(400)  = N'flight that disappeared';
DECLARE @query_vector VECTOR(1536) = AI_GENERATE_EMBEDDINGS(@query USE MODEL TextEmbeddingModel);

SELECT TOP (10) n.Id, n.Headline, n.Category, n.Date,
    VECTOR_DISTANCE('cosine', n.Embedding, @query_vector) AS distance
FROM dbo.NewsItems AS n
ORDER BY distance;


-- Cross-lingual: Spanish query, English headlines, no translation layer anywhere
DECLARE @query_es NVARCHAR(400) = N'avión que desapareció';   -- 'plane that disappeared'
DECLARE @query_vector_es VECTOR(1536) = AI_GENERATE_EMBEDDINGS(@query_es USE MODEL TextEmbeddingModel);

SELECT TOP (10) n.Id, n.Headline, n.Category, n.Date,
    VECTOR_DISTANCE('cosine', n.Embedding, @query_vector_es) AS distance
FROM dbo.NewsItems AS n
ORDER BY distance;


