// Copyright (c) Kevin BEAUGRAND. All rights reserved.

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.MemoryStorage;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.MemoryStorage.SqlServer;

/// <summary>
/// Represents a memory store implementation that uses a SQL Server database as its backing store.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
                                                   Justification = "We need to build the full table name using schema and collection, it does not support parameterized passing.")]
public class SqlServerMemory : IMemoryDb
{
    /// <summary>
    /// The SQL Server collections table name.
    /// </summary>
    internal const string MemoryCollectionTableName = "SKMemoryCollections";

    /// <summary>
    /// The SQL Server memories table name.
    /// </summary>
    internal const string MemoryTableName = "SKMemories";

    /// <summary>
    /// The SQL Server embeddings table name.
    /// </summary>
    internal const string EmbeddingsTableName = "SKEmbeddings";

    /// <summary>
    /// The SQL Server tags table name.
    /// </summary>
    internal const string TagsTableName = "SKMemoriesTags";

    /// <summary>
    /// The SQL Server configuration.
    /// </summary>
    private readonly SqlServerConfig _config;

    /// <summary>
    /// The text embedding generator.
    /// </summary>
    private readonly ITextEmbeddingGenerator _embeddingGenerator;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger<SqlServerMemory> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerMemory"/> class.
    /// </summary>
    /// <param name="config">The SQL server instance configuration.</param>
    /// <param name="embeddingGenerator">The text embedding generator.</param>
    /// <param name="log">The logger.</param>
    public SqlServerMemory(
        SqlServerConfig config,
        ITextEmbeddingGenerator embeddingGenerator,
        ILogger<SqlServerMemory>? log = null)
    {
        this._embeddingGenerator = embeddingGenerator;
        this._log = log ?? DefaultLogger<SqlServerMemory>.Instance;

        this._config = config;

        if (this._embeddingGenerator == null)
        {
            throw new SqlServerMemoryException("Embedding generator not configured");
        }

        this.CreateTablesIfNotExists();
    }

    /// <inheritdoc/>
    public async Task CreateIndexAsync(string index, int vectorSize, CancellationToken cancellationToken = default)
    {
        if (await this.DoesIndexExistsAsync(index, cancellationToken).ConfigureAwait(false))
        {
            // Index already exists
            return;
        }

        using var connection = new SqlConnection(this._config.ConnectionString);

        await connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);

        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = $@"
                    INSERT INTO {this.GetFullTableName(MemoryCollectionTableName)}([id])
                    VALUES (@index);
                    
                    IF OBJECT_ID(N'{this.GetFullTableName($"{EmbeddingsTableName}_{index}")}', N'U') IS NULL
                    CREATE TABLE {this.GetFullTableName($"{EmbeddingsTableName}_{index}")}
                    (   
                        [memory_id] UNIQUEIDENTIFIER NOT NULL,
                        [vector_value_id] [int] NOT NULL,
                        [vector_value] [float] NOT NULL
                        FOREIGN KEY ([memory_id]) REFERENCES {this.GetFullTableName(MemoryTableName)}([id]) ON DELETE CASCADE
                    );
                    
                    IF OBJECT_ID(N'{this._config.Schema}.IXC_{$"{EmbeddingsTableName}_{index}"}', N'U') IS NULL
                    CREATE CLUSTERED COLUMNSTORE INDEX IXC_{$"{EmbeddingsTableName}_{index}"}
                    ON {this.GetFullTableName($"{EmbeddingsTableName}_{index}")};
                    
                    IF OBJECT_ID(N'{this.GetFullTableName($"{TagsTableName}_{index}")}', N'U') IS NULL
                    CREATE TABLE {this.GetFullTableName($"{TagsTableName}_{index}")}
                    (
                        [memory_id] UNIQUEIDENTIFIER NOT NULL,
                        [name] NVARCHAR(256)  NOT NULL,
                        [value] NVARCHAR(256) NOT NULL,
                        FOREIGN KEY ([memory_id]) REFERENCES {this.GetFullTableName(MemoryTableName)}([id]) ON DELETE CASCADE
                    );
            ";

            command.Parameters.AddWithValue("@index", index);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        };
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string index, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(this._config.ConnectionString);

        using SqlCommand cmd = connection.CreateCommand();

        await connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);

        cmd.CommandText = $@"
            DELETE [embeddings]
            FROM {this.GetFullTableName($"{EmbeddingsTableName}_{index}")} [embeddings]
            INNER JOIN {this.GetFullTableName(MemoryTableName)} ON [embeddings].[memory_id] = {this.GetFullTableName(MemoryTableName)}.[id]
            WHERE 
                {this.GetFullTableName(MemoryTableName)}.[collection] = @index
            AND {this.GetFullTableName(MemoryTableName)}.[key]=@key;
            
            DELETE [tags]
            FROM {this.GetFullTableName($"{TagsTableName}_{index}")} [tags]
            INNER JOIN {this.GetFullTableName(MemoryTableName)} ON [tags].[memory_id] = {this.GetFullTableName(MemoryTableName)}.[id]
            WHERE 
                {this.GetFullTableName(MemoryTableName)}.[collection] = @index
            AND {this.GetFullTableName(MemoryTableName)}.[key]=@key;    

            DELETE FROM {this.GetFullTableName(MemoryTableName)} WHERE [collection] = @index AND [key]=@key;
        ";

        cmd.Parameters.AddWithValue("@index", index);
        cmd.Parameters.AddWithValue("@key", record.Id);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        if (!(await this.DoesIndexExistsAsync(index, cancellationToken).ConfigureAwait(false)))
        {
            // Index does not exist
            return;
        }

        using var connection = new SqlConnection(this._config.ConnectionString);

        await connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);

        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = $@"DELETE FROM {this.GetFullTableName(MemoryCollectionTableName)}
                                     WHERE [id] = @index;

                                     DROP TABLE {this.GetFullTableName($"{EmbeddingsTableName}_{index}")};
                                     DROP TABLE {this.GetFullTableName($"{TagsTableName}_{index}")}
                                    ";

            command.Parameters.AddWithValue("@index", index);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetIndexesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(this._config.ConnectionString);

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        List<string> indexes = new();

        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT [id] FROM {this.GetFullTableName(MemoryCollectionTableName)}";

            using var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                indexes.Add(dataReader.GetString(dataReader.GetOrdinal("id")));
            }
        };

        return indexes;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<MemoryRecord> GetListAsync(string index, ICollection<MemoryFilter>? filters = null, int limit = 1, bool withEmbeddings = false, CancellationToken cancellationToken = default)
    {
        string queryColumns = "[key], [payload], [tags]";

        if (withEmbeddings)
        {
            queryColumns += ", [embedding]";
        }

        using var connection = new SqlConnection(this._config.ConnectionString);

        await connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);

        using SqlCommand cmd = connection.CreateCommand();

        var tagFilters = new TagCollection();

        cmd.CommandText = $@"
            WITH [filters] AS 
		    (
			    SELECT 
				    cast([filters].[key] AS NVARCHAR(256)) COLLATE SQL_Latin1_General_CP1_CI_AS AS [name],
				    cast([filters].[value] AS NVARCHAR(256)) COLLATE SQL_Latin1_General_CP1_CI_AS AS [value]
			    FROM openjson(@filters) [filters]
		    )
            SELECT TOP (@limit)
                {queryColumns}
            FROM 
                {this.GetFullTableName(MemoryTableName)}
		    WHERE 1=1
            AND {this.GetFullTableName(MemoryTableName)}.[collection] = @index
            {GenerateFilters(index, cmd.Parameters, filters)};";


        cmd.Parameters.AddWithValue("@index", index);
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@filters", JsonSerializer.Serialize(tagFilters));

        using var dataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return await this.ReadEntryAsync(dataReader, withEmbeddings, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<(MemoryRecord, double)> GetSimilarListAsync(string index, string text, ICollection<MemoryFilter>? filters = null, double minRelevance = 0, int limit = 1, bool withEmbeddings = false, CancellationToken cancellationToken = default)
    {
        Embedding embedding = await this._embeddingGenerator.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);

        string queryColumns = "[id], [payload], [tags]";

        if (withEmbeddings)
        {
            queryColumns += ", [embedding]";
        }

        using var connection = new SqlConnection(this._config.ConnectionString);

        await connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);

        using SqlCommand cmd = connection.CreateCommand();

        cmd.CommandText = $@"
        WITH 
        [embedding] as
        (
            SELECT 
                cast([key] AS INT) AS [vector_value_id],
                cast([value] AS FLOAT) AS [vector_value]
            FROM 
                openjson(@vector)
        ),
        [similarity] AS
        (
            SELECT TOP (@limit)
            {this.GetFullTableName($"{EmbeddingsTableName}_{index}")}.[memory_id], 
            SUM([embedding].[vector_value] * {this.GetFullTableName($"{EmbeddingsTableName}_{index}")}.[vector_value]) / 
            (
                SQRT(SUM([embedding].[vector_value] * [embedding].[vector_value])) 
                * 
                SQRT(SUM({this.GetFullTableName($"{EmbeddingsTableName}_{index}")}.[vector_value] * {this.GetFullTableName($"{EmbeddingsTableName}_{index}")}.[vector_value]))
            ) AS cosine_similarity
            -- sum([embedding].[vector_value] * {this.GetFullTableName($"{EmbeddingsTableName}_{index}")}.[vector_value]) as cosine_distance -- Optimized as per https://platform.openai.com/docs/guides/embeddings/which-distance-function-should-i-use
        FROM 
            [embedding]
        INNER JOIN 
            {this.GetFullTableName($"{EmbeddingsTableName}_{index}")} ON [embedding].vector_value_id = {this.GetFullTableName($"{EmbeddingsTableName}_{index}")}.vector_value_id
        GROUP BY
            {this.GetFullTableName($"{EmbeddingsTableName}_{index}")}.[memory_id]
        ORDER BY
            cosine_similarity DESC
        )
        SELECT DISTINCT
            {this.GetFullTableName(MemoryTableName)}.[id],
            {this.GetFullTableName(MemoryTableName)}.[key],    
            {this.GetFullTableName(MemoryTableName)}.[payload],
            {this.GetFullTableName(MemoryTableName)}.[tags],
            [similarity].[cosine_similarity]
        FROM 
            [similarity] 
        INNER JOIN 
            {this.GetFullTableName(MemoryTableName)} ON [similarity].[memory_id] = {this.GetFullTableName(MemoryTableName)}.[id]
        WHERE 1=1
        AND cosine_similarity >= @min_relevance_score
        {GenerateFilters(index, cmd.Parameters, filters)}";        

        cmd.Parameters.AddWithValue("@vector", JsonSerializer.Serialize(embedding.Data.ToArray()));
        cmd.Parameters.AddWithValue("@index", index);
        cmd.Parameters.AddWithValue("@min_relevance_score", minRelevance);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var dataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            double cosineSimilarity = dataReader.GetDouble(dataReader.GetOrdinal("cosine_similarity"));
            yield return (await this.ReadEntryAsync(dataReader, withEmbeddings, cancellationToken).ConfigureAwait(false), cosineSimilarity);
        }
    }

    /// <inheritdoc/>
    public async Task<string> UpsertAsync(string index, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(this._config.ConnectionString);

        await connection.OpenAsync(cancellationToken)
                .ConfigureAwait(false);

        using SqlCommand cmd = connection.CreateCommand();

        cmd.CommandText = $@"
                MERGE INTO {this.GetFullTableName(MemoryTableName)}
                USING (SELECT @key) as [src]([key])
                ON {this.GetFullTableName(MemoryTableName)}.[key] = [src].[key]
                WHEN MATCHED THEN
                    UPDATE SET payload=@payload, embedding=@embedding, tags=@tags
                WHEN NOT MATCHED THEN
                    INSERT ([id], [key], [collection], [payload], [tags], [embedding])
                    VALUES (NEWID(), @key, @index, @payload, @tags, @embedding);

                MERGE {this.GetFullTableName($"{EmbeddingsTableName}_{index}")} AS [tgt]  
                USING (
                    SELECT 
                        {this.GetFullTableName(MemoryTableName)}.[id],
                        cast([vector].[key] AS INT) AS [vector_value_id],
                        cast([vector].[value] AS FLOAT) AS [vector_value] 
                    FROM {this.GetFullTableName(MemoryTableName)}
                    CROSS APPLY
                        openjson(@embedding) [vector]
                    WHERE {this.GetFullTableName(MemoryTableName)}.[key] = @key
                        AND {this.GetFullTableName(MemoryTableName)}.[collection] = @index
                ) AS [src]
                ON [tgt].[memory_id] = [src].[id] AND [tgt].[vector_value_id] = [src].[vector_value_id]
                WHEN MATCHED THEN
                    UPDATE SET [tgt].[vector_value] = [src].[vector_value]
                WHEN NOT MATCHED THEN
                    INSERT ([memory_id], [vector_value_id], [vector_value])
                    VALUES ([src].[id], 
                            [src].[vector_value_id], 
                            [src].[vector_value] );

				DELETE FROM [tgt]
				FROM  {this.GetFullTableName($"{TagsTableName}_{index}")} AS [tgt]
				INNER JOIN {this.GetFullTableName(MemoryTableName)} ON [tgt].[memory_id] = {this.GetFullTableName(MemoryTableName)}.[id]
				WHERE {this.GetFullTableName(MemoryTableName)}.[key] = @key
                        AND {this.GetFullTableName(MemoryTableName)}.[collection] = @index;

                MERGE {this.GetFullTableName($"{TagsTableName}_{index}")} AS [tgt]  
                USING (
                    SELECT 
                        {this.GetFullTableName(MemoryTableName)}.[id],
                        cast([tags].[key] AS NVARCHAR(256)) COLLATE SQL_Latin1_General_CP1_CI_AS AS [tag_name],
                        [tag_value].[value] AS [value] 
                    FROM {this.GetFullTableName(MemoryTableName)}
                    CROSS APPLY openjson(@tags) [tags]                    
                    CROSS APPLY openjson(cast([tags].[value] AS NVARCHAR(256)) COLLATE SQL_Latin1_General_CP1_CI_AS) [tag_value]
                    WHERE {this.GetFullTableName(MemoryTableName)}.[key] = @key
                        AND {this.GetFullTableName(MemoryTableName)}.[collection] = @index
                ) AS [src]
                ON [tgt].[memory_id] = [src].[id] AND [tgt].[name] = [src].[tag_name]
                WHEN MATCHED THEN
                    UPDATE SET [tgt].[value] = [src].[value]
                WHEN NOT MATCHED THEN
                    INSERT ([memory_id], [name], [value])
                    VALUES ([src].[id], 
                            [src].[tag_name], 
                            [src].[value]);";

        cmd.Parameters.AddWithValue("@index", index);
        cmd.Parameters.AddWithValue("@key", record.Id);
        cmd.Parameters.AddWithValue("@payload", JsonSerializer.Serialize(record.Payload) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@tags", JsonSerializer.Serialize(record.Tags) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@embedding", JsonSerializer.Serialize(record.Vector.Data.ToArray()));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return record.Id;
    }

    /// <summary>
    /// Creates the SQL Server tables if they do not exist.
    /// </summary>
    /// <returns></returns>
    private void CreateTablesIfNotExists()
    {
        var sql = $@"IF OBJECT_ID(N'{this.GetFullTableName(MemoryCollectionTableName)}', N'U') IS NULL
                    CREATE TABLE {this.GetFullTableName(MemoryCollectionTableName)}
                    (   [id] NVARCHAR(256) NOT NULL,
                        PRIMARY KEY ([id])
                    );

                    IF OBJECT_ID(N'{this.GetFullTableName(MemoryTableName)}', N'U') IS NULL
                    CREATE TABLE {this.GetFullTableName(MemoryTableName)}
                    (   [id] UNIQUEIDENTIFIER NOT NULL,
                        [key] NVARCHAR(256)  NOT NULL,
                        [collection] NVARCHAR(256) NOT NULL,
                        [payload] NVARCHAR(MAX),
                        [tags] NVARCHAR(MAX),
                        [embedding] NVARCHAR(MAX),
                        PRIMARY KEY ([id]),
                        FOREIGN KEY ([collection]) REFERENCES {this.GetFullTableName(MemoryCollectionTableName)}([id]) ON DELETE CASCADE,
                        CONSTRAINT UK_{MemoryTableName} UNIQUE([collection], [key])
                    );
                    ";

        using var connection = new SqlConnection(this._config.ConnectionString);

        connection.Open();

        using (SqlCommand command = connection.CreateCommand())
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Checks if the index exists.
    /// </summary>
    /// <param name="indexName">The index name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    private async Task<bool> DoesIndexExistsAsync(string indexName,
        CancellationToken cancellationToken = default)
    {
        var collections = await this.GetIndexesAsync(cancellationToken)
                                    .ConfigureAwait(false);

        foreach (var item in collections)
        {
            if (item.Equals(indexName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the full table name with schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <returns></returns>
    private string GetFullTableName(string tableName)
    {
        return $"[{this._config.Schema}].[{tableName}]";
    }

    /// <summary>
    /// Generates the filters as SQL commands and sets the SQL parameters
    /// </summary>
    /// <param name="index">The index name.</param>
    /// <param name="parameters">The SQL parameters to populate.</param>
    /// <param name="filters">The filters to apply</param>
    /// <returns></returns>
    private string GenerateFilters(string index, SqlParameterCollection parameters, ICollection<MemoryFilter> ? filters = null)
    {
        var filterBuilder = new StringBuilder();

        if (filters is not null)
        {
            filterBuilder.Append($@"AND (
                ");

            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters.ElementAt(i);

                if (i > 0)
                {
                    filterBuilder.Append(" OR ");
                }

                filterBuilder.Append($@"EXISTS (
                        SELECT
	                        1 
                        FROM (
	                        SELECT 
	                          cast([filters].[key] AS NVARCHAR(256)) COLLATE SQL_Latin1_General_CP1_CI_AS AS [name],
	                          [tag_value].[value] AS[value]
	                          FROM openjson(@filter_{i}) [filters]
	                          CROSS APPLY openjson(cast([filters].[value] AS NVARCHAR(256)) COLLATE SQL_Latin1_General_CP1_CI_AS)[tag_value]
                          ) AS [filter]
                          INNER JOIN {this.GetFullTableName($"{TagsTableName}_{index}")} AS [tags] ON [filter].[name] = [tags].[name] AND [filter].[value] = [tags].[value]
                        WHERE 
	                        [tags].[memory_id] = {this.GetFullTableName(MemoryTableName)}.[id]
                    )
                    ");

                parameters.AddWithValue($"@filter_{i}", JsonSerializer.Serialize(filter));
            }

            filterBuilder.Append(@"
            )");
        }

        return filterBuilder.ToString();
    }

    private async Task<MemoryRecord> ReadEntryAsync(SqlDataReader dataReader, bool withEmbedding, CancellationToken cancellationToken = default)
    {
        var entry = new MemoryRecord();

        entry.Id = dataReader.GetString(dataReader.GetOrdinal("key"));

        if (!(await dataReader.IsDBNullAsync(dataReader.GetOrdinal("payload"), cancellationToken).ConfigureAwait(false)))
        {
            entry.Payload = JsonSerializer.Deserialize<Dictionary<string, object>>(dataReader.GetString(dataReader.GetOrdinal("payload")))!;
        }

        if (!(await dataReader.IsDBNullAsync(dataReader.GetOrdinal("tags"), cancellationToken).ConfigureAwait(false)))
        {
            entry.Tags = JsonSerializer.Deserialize<TagCollection>(dataReader.GetString(dataReader.GetOrdinal("tags")))!;
        }

        if (withEmbedding)
        {
            entry.Vector = new ReadOnlyMemory<float>(JsonSerializer.Deserialize<IEnumerable<float>>(dataReader.GetString(dataReader.GetOrdinal("embedding")))!.ToArray());
        }

        return entry;
    }
}
