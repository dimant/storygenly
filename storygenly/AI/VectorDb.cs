using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace StoryGenly.AI
{
    public class VectorDb
    {
        private readonly string _connectionString;
        private readonly int _embeddingDimension;

        /// <summary>
        /// Initializes a new instance of the VectorDb class for storing and querying vector embeddings.
        /// Creates a SQLite database at the specified path and sets up the necessary schema for storing
        /// code chunks with their corresponding vector embeddings for semantic search capabilities.
        /// </summary>
        /// <param name="dbFilePath">The file path where the SQLite database will be created or accessed</param>
        /// <param name="embeddingDimension">The dimension of the vector embeddings (default: 768, common for many embedding models)</param>
        public VectorDb(string dbFilePath, int embeddingDimension = 768)
        {
            _connectionString = $"Data Source={dbFilePath};";
            _embeddingDimension = embeddingDimension;
            
            if (!File.Exists(dbFilePath))
            {
                // Microsoft.Data.Sqlite creates the file automatically on open, so just touch it
                using (File.Create(dbFilePath)) { }
            }
            EnsureSchema();
        }

        /// <summary>
        /// Creates the database schema if it doesn't exist. Sets up the code_chunks table with
        /// columns for metadata (id, file_path, chunk_index, chunk, hash) and individual columns
        /// for each embedding dimension (e0, e1, e2, ... e{n-1}).
        /// </summary>
        private void EnsureSchema()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            var embeddingCols = string.Join(", ", Enumerable.Range(0, _embeddingDimension).Select(i => $"e{i} REAL"));
            cmd.CommandText = $@"
                CREATE TABLE IF NOT EXISTS code_chunks (
                    id TEXT PRIMARY KEY,
                    file_path TEXT,
                    chunk_index INTEGER,
                    chunk TEXT,
                    hash TEXT,
                    {embeddingCols}
                );
            ";
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Inserts or replaces a code chunk with its vector embedding into the database.
        /// This method stores both the textual content and its corresponding vector representation
        /// for later semantic search and retrieval operations.
        /// </summary>
        /// <param name="id">Unique identifier for this code chunk</param>
        /// <param name="filePath">Path to the source file containing this chunk</param>
        /// <param name="chunkIndex">Index position of this chunk within the source file</param>
        /// <param name="chunk">The actual text content of the code chunk</param>
        /// <param name="hash">Hash value of the chunk content for change detection</param>
        /// <param name="embedding">Vector embedding representing the semantic content of the chunk</param>
        /// <exception cref="ArgumentException">Thrown when embedding dimension doesn't match the configured dimension</exception>
        public void InsertRow(string id, string filePath, int chunkIndex, string chunk, string hash, float[] embedding)
        {
            if (embedding.Length != _embeddingDimension)
                throw new ArgumentException($"Embedding must be of length {_embeddingDimension}.");

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            var embeddingCols = string.Join(", ", Enumerable.Range(0, _embeddingDimension).Select(i => $"e{i}"));
            var embeddingParams = string.Join(", ", Enumerable.Range(0, _embeddingDimension).Select(i => $"@e{i}"));

            cmd.CommandText = $@"
                INSERT OR REPLACE INTO code_chunks
                (id, file_path, chunk_index, chunk, hash, {embeddingCols})
                VALUES (@id, @file_path, @chunk_index, @chunk, @hash, {embeddingParams});
            ";

            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@file_path", filePath);
            cmd.Parameters.AddWithValue("@chunk_index", chunkIndex);
            cmd.Parameters.AddWithValue("@chunk", chunk);
            cmd.Parameters.AddWithValue("@hash", hash);
            for (int i = 0; i < _embeddingDimension; i++)
                cmd.Parameters.AddWithValue($"@e{i}", embedding[i]);

            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Deletes a code chunk and its associated embedding from the database.
        /// Use this method to remove outdated or no longer relevant code chunks.
        /// </summary>
        /// <param name="id">The unique identifier of the code chunk to delete</param>
        public void DeleteRow(string id)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM code_chunks WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Retrieves all code chunks that belong to a specific file.
        /// This method is useful for getting all chunks from a particular source file,
        /// including their embeddings and metadata.
        /// </summary>
        /// <param name="filePath">The path of the file to search for chunks</param>
        /// <returns>A list of tuples containing chunk information: id, filePath, chunkIndex, chunk text, hash, and embedding vector</returns>
        public List<(string id, string filePath, int chunkIndex, string chunk, string hash, float[] embedding)> SearchByFileName(string filePath)
        {
            var results = new List<(string, string, int, string, string, float[])>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM code_chunks WHERE file_path = @file_path;";
            cmd.Parameters.AddWithValue("@file_path", filePath);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var embedding = new float[_embeddingDimension];
                for (int i = 0; i < _embeddingDimension; i++)
                    embedding[i] = reader.GetFloat(reader.GetOrdinal($"e{i}"));
                results.Add((
                    reader.GetString(reader.GetOrdinal("id")),
                    reader.GetString(reader.GetOrdinal("file_path")),
                    reader.GetInt32(reader.GetOrdinal("chunk_index")),
                    reader.GetString(reader.GetOrdinal("chunk")),
                    reader.GetString(reader.GetOrdinal("hash")),
                    embedding
                ));
            }
            return results;
        }

        /// <summary>
        /// Performs semantic search by finding code chunks with embeddings most similar to the query embedding.
        /// Uses cosine similarity to measure the semantic similarity between the query and stored embeddings.
        /// This is the primary method for finding semantically relevant code chunks based on natural language queries.
        /// </summary>
        /// <param name="queryEmbedding">The vector embedding of the search query</param>
        /// <param name="topK">Maximum number of results to return, ordered by similarity score (default: 5)</param>
        /// <returns>A list of tuples containing chunk information and similarity scores, ordered by descending similarity</returns>
        /// <exception cref="ArgumentException">Thrown when query embedding dimension doesn't match the configured dimension</exception>
        public List<(string id, string filePath, int chunkIndex, string chunk, string hash, float[] embedding, float score)> SearchByCosineSimilarity(float[] queryEmbedding, int topK = 5)
        {
            if (queryEmbedding.Length != _embeddingDimension)
                throw new ArgumentException($"Embedding must be of length {_embeddingDimension}.");

            // Compute cosine similarity directly in SQL
            var results = new List<(string, string, int, string, string, float[], float)>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            // Build SQL query that computes dot product, normA (query norm), and normB (stored vector norm)
            var dotProductTerms = new List<string>();
            var normQueryTerms = new List<string>();
            var normStoredTerms = new List<string>();
            
            for (int i = 0; i < _embeddingDimension; i++)
            {
                dotProductTerms.Add($"(e{i} * @e{i})");
                normQueryTerms.Add($"(@e{i} * @e{i})");
                normStoredTerms.Add($"(e{i} * e{i})");
            }
            
            var dotProduct = string.Join(" + ", dotProductTerms);
            var normQuery = string.Join(" + ", normQueryTerms);
            var normStored = string.Join(" + ", normStoredTerms);
            
            cmd.CommandText = $@"
                SELECT 
                    id, file_path, chunk_index, chunk, hash,
                    ({dotProduct}) / (SQRT({normQuery}) * SQRT({normStored}) + 1.0e-8) AS similarity
                FROM code_chunks
                ORDER BY similarity DESC
                LIMIT @topK;
            ";
            
            // Add parameters for the query embedding
            for (int i = 0; i < _embeddingDimension; i++)
                cmd.Parameters.AddWithValue($"@e{i}", queryEmbedding[i]);
                
            cmd.Parameters.AddWithValue("@topK", topK);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(reader.GetOrdinal("id"));
                // Create empty array for embeddings - we don't load them for performance
                var embedding = new float[_embeddingDimension];
                
                results.Add((
                    id,
                    reader.GetString(reader.GetOrdinal("file_path")),
                    reader.GetInt32(reader.GetOrdinal("chunk_index")),
                    reader.GetString(reader.GetOrdinal("chunk")),
                    reader.GetString(reader.GetOrdinal("hash")),
                    embedding,
                    reader.GetFloat(reader.GetOrdinal("similarity"))
                ));
            }

            return results;
        }

        /// <summary>
        /// Retrieves the vector embedding for a specific code chunk by its ID.
        /// This method is useful when you need to load the full embedding vector for a chunk
        /// that was previously found through similarity search (which returns empty embeddings for performance).
        /// </summary>
        /// <param name="id">The unique identifier of the code chunk</param>
        /// <returns>The vector embedding for the specified chunk, or an empty array if the chunk is not found</returns>
        public float[] GetEmbeddingById(string id)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM code_chunks WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var embedding = new float[_embeddingDimension];
                for (int i = 0; i < _embeddingDimension; i++)
                    embedding[i] = reader.GetFloat(reader.GetOrdinal($"e{i}"));
                return embedding;
            }
            return new float[_embeddingDimension]; // Return empty embedding if not found
        }
    }
}