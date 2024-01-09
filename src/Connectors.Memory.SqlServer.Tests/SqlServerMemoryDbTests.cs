// Copyright (c) Kevin BEAUGRAND. All rights reserved.

using KernelMemory.MemoryStorage.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.MemoryStorage;
using Moq;
using SemanticKernel.IntegrationTests.Connectors.Memory.SqlServer;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Connectors.Memory.SqlServer.Tests;

/// <summary>
/// Represents a SQL Server memory store test class.
/// </summary>
public class SqlServerMemoryDbTests : IAsyncLifetime
{
    /// <summary>
    /// The SQL Server configuration.
    /// </summary>
    private SqlServerConfig _config;

    /// <summary>
    /// The text embedding generator mock.
    /// </summary>
    private Mock<ITextEmbeddingGenerator> _textEmbeddingGeneratorMock = new Mock<ITextEmbeddingGenerator>();

    /// <summary>
    /// Creates a new instance of the <see cref="SqlServerMemory"/> class.
    /// </summary>
    /// <returns></returns>
    private SqlServerMemory CreateMemoryDb()
    {
        return new SqlServerMemory(_config, _textEmbeddingGeneratorMock.Object);
    }

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        // Load configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<SqlServerMemoryStoreTests>()
            .Build();

        var connectionString = configuration["SqlServer:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException("SqlServer memory connection string is not configured");
        }

        this._config = new SqlServerConfig
        {
            ConnectionString = connectionString,
        };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        var memoryDb = this.CreateMemoryDb();

        foreach (var item in await memoryDb.GetIndexesAsync(CancellationToken.None).ConfigureAwait(false))
        {
            await memoryDb.DeleteIndexAsync(item, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Verify that get indexes should return newly created index.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetIndexesShouldReturnNewlyCreatedIndexTestAsync()
    {
        var memoryDb = this.CreateMemoryDb();

        await memoryDb.CreateIndexAsync("test", 1536, CancellationToken.None).ConfigureAwait(true);

        var collections = await memoryDb.GetIndexesAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.NotNull(collections);
        Assert.Single(collections);
        Assert.Equal("test", collections.First());
    }


    /// <summary>
    /// Verify that get indexes should not return newly deleted index.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetIndexesShouldNotReturnNewlyDeletedIndexTestAsync()
    {
        var memoryDb = this.CreateMemoryDb();

        await memoryDb.CreateIndexAsync("test", 1536, CancellationToken.None).ConfigureAwait(true);

        var collections = await memoryDb.GetIndexesAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.NotNull(collections);
        Assert.Single(collections);
        Assert.Equal("test", collections.First());

        await memoryDb.DeleteIndexAsync("test", CancellationToken.None).ConfigureAwait(true);

        collections = await memoryDb.GetIndexesAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.NotNull(collections);
        Assert.Empty(collections);
    }

    /// <summary>
    /// Verify that create index should not throw exception if index already exists.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task CreateIndexShouldNotThrowExceptionIfIndexAlreadyExistsTestAsync()
    {
        var memoryDb = this.CreateMemoryDb();

        await memoryDb.CreateIndexAsync("test", 1536, CancellationToken.None).ConfigureAwait(true);

        var collections = await memoryDb.GetIndexesAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.NotNull(collections);
        Assert.Single(collections);
        Assert.Equal("test", collections.First());

        await memoryDb.CreateIndexAsync("test", 1536, CancellationToken.None).ConfigureAwait(true);

        collections = await memoryDb.GetIndexesAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.NotNull(collections);
        Assert.Single(collections);
        Assert.Equal("test", collections.First());
    }

    /// <summary>
    /// Test that upsert record should pass.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task UpsertRecordShouldPassAsync()
    {
        var memoryDb = this.CreateMemoryDb();

        await memoryDb.CreateIndexAsync("test", 1536, CancellationToken.None).ConfigureAwait(true);

        var record = new MemoryRecord()
        {
            Id = "test",
            Vector = new float[] { 1, 2, 3 }
        };

        await memoryDb.UpsertAsync("test", record, CancellationToken.None).ConfigureAwait(true);
    }

    /// <summary>
    /// Test that upsert record should pass.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetListAsyncShouldReturnTheStoredRecordsAsync()
    {
        var memoryDb = this.CreateMemoryDb();

        await memoryDb.CreateIndexAsync("test", 1536, CancellationToken.None).ConfigureAwait(true);

        var record = new MemoryRecord()
        {
            Id = "test",
            Vector = new float[] { 1, 2, 3 }
        };

        await memoryDb.UpsertAsync("test", record, CancellationToken.None).ConfigureAwait(true);

        var results = await memoryDb.GetListAsync("test", null, limit: 10, withEmbeddings: true, CancellationToken.None).ToListAsync().ConfigureAwait(true);

        Assert.NotNull(results);
        Assert.Single(results);
        Assert.Equal("test", results.First().Id);
    }

    /// <summary>
    /// Test that existing record can be removed.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ExistingRecordCanBeRemovedAsync()
    {
        var memoryDb = this.CreateMemoryDb();

        await memoryDb.CreateIndexAsync("test", 1536, CancellationToken.None).ConfigureAwait(true);

        var record = new MemoryRecord()
        {
            Id = "test",
            Vector = new float[] { 1, 2, 3 }
        };

        await memoryDb.UpsertAsync("test", record, CancellationToken.None).ConfigureAwait(true);

        var results = await memoryDb.GetListAsync("test", null, limit: 10, withEmbeddings: true, CancellationToken.None).ToListAsync().ConfigureAwait(true);

        Assert.NotNull(results);
        Assert.Single(results);
        Assert.Equal("test", results.First().Id);

        await memoryDb.DeleteAsync("test", record, CancellationToken.None).ConfigureAwait(true);

        results = await memoryDb.GetListAsync("test", null, limit: 10, withEmbeddings: true, CancellationToken.None).ToListAsync().ConfigureAwait(true);

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    /// <summary>
    /// Test that existing record can be updated
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task UpsertShouldUpdateRecordTestAsync()
    {
        var memoryDb = this.CreateMemoryDb();

        await memoryDb.CreateIndexAsync("test", 1536, CancellationToken.None).ConfigureAwait(true);

        var record = new MemoryRecord()
        {
            Id = "test",
            Vector = new float[] { 1, 2, 3 }
        };

        await memoryDb.UpsertAsync("test", record, CancellationToken.None).ConfigureAwait(true);

        var results = await memoryDb.GetListAsync("test", null, limit: 10, withEmbeddings: true, CancellationToken.None).ToListAsync().ConfigureAwait(true);

        Assert.NotNull(results);
        Assert.Single(results);
        Assert.Equal("test", results.First().Id);

        record.Vector = new float[] { 4, 5, 6 };

        await memoryDb.UpsertAsync("test", record, CancellationToken.None).ConfigureAwait(true);

        results = await memoryDb.GetListAsync("test", null, limit: 10, withEmbeddings: true, CancellationToken.None).ToListAsync().ConfigureAwait(true);

        Assert.NotNull(results);
        Assert.Single(results);
        Assert.Equal("test", results.First().Id);
        Assert.Equal(4, results.First().Vector.Data.ToArray()[0]);
        Assert.Equal(5, results.First().Vector.Data.ToArray()[1]);
        Assert.Equal(6, results.First().Vector.Data.ToArray()[2]);
    }

    [Fact]
    public async Task GetListShouldReturnFilteredRecordsTestAsync()
    {
        var memoryDb = this.CreateMemoryDb();

        await memoryDb.CreateIndexAsync("test", 1536, CancellationToken.None).ConfigureAwait(true);

        var record1 = new MemoryRecord()
        {
            Id = "record1",
            Vector = new float[] { 1, 2, 3 },
            Tags = new TagCollection()
            {
                { "test", "value1" }
            }
        };

        await memoryDb.UpsertAsync("test", record1, CancellationToken.None).ConfigureAwait(true);

        var record2 = new MemoryRecord()
        {
            Id = "record2",
            Vector = new float[] { 1, 2, 3 },
            Tags = new TagCollection()
            {
                { "test", "value2" }
            }
        };

        await memoryDb.UpsertAsync("test", record2, CancellationToken.None).ConfigureAwait(true);

        var results = await memoryDb.GetListAsync("test", new[] { new MemoryFilter().ByTag("test", "value1") }, limit: 10, withEmbeddings: true, CancellationToken.None).ToListAsync().ConfigureAwait(true);

        Assert.NotNull(results);
        Assert.Single(results);
        Assert.Equal("record1", results.First().Id);
    }

    /// <summary>
    /// Test that get similar list should return expected results.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetSimilarListShouldReturnExpectedAsync()
    {
        // Arrange
        var memoryDb = this.CreateMemoryDb();

        var compareEmbedding = new ReadOnlyMemory<float>(new float[] { 1, 1, 1 });
        string collection = "test_collection";
        await memoryDb.CreateIndexAsync(collection, 1536);
        int i = 0;

        MemoryRecord testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { 1, 1, 1 }
        };

        _ = await memoryDb.UpsertAsync(collection, testRecord);

        i++;
        testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { -1, -1, -1 }
        };
        _ = await memoryDb.UpsertAsync(collection, testRecord);

        i++;
        testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { 1, 2, 3 }
        };
        _ = await memoryDb.UpsertAsync(collection, testRecord);

        i++;
        testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { -1, -2, -3 }
        };
        _ = await memoryDb.UpsertAsync(collection, testRecord);

        i++;
        testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { 1, -1, -2 }
        };
        _ = await memoryDb.UpsertAsync(collection, testRecord);

        _ = this._textEmbeddingGeneratorMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compareEmbedding);

        // Act
        double threshold = .75;
        var topNResults = memoryDb.GetSimilarListAsync(collection, "Sample", limit: 1, minRelevance: threshold)
                                    .ToEnumerable()
                                    .ToArray();

        // Assert
        Assert.NotNull(topNResults);
        Assert.Single(topNResults);

        Assert.Equal("test0", topNResults[0].Item1.Id);
        Assert.True(topNResults[0].Item2 >= threshold);
    }

    /// <summary>
    /// Test that get similar list should return expected results.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetSimilarListShouldReturnExpectedWithFiltersAsync()
    {
        // Arrange
        var memoryDb = this.CreateMemoryDb();

        var compareEmbedding = new ReadOnlyMemory<float>(new float[] { 1, 1, 1 });
        string collection = "test_collection";
        await memoryDb.CreateIndexAsync(collection, 1536);
        int i = 0;

        MemoryRecord testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { 1, 1, 1 }
        };

        testRecord.Tags.Add("test", "record0");

        _ = await memoryDb.UpsertAsync(collection, testRecord);

        i++;
        testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { -1, -1, -1 }
        };
        testRecord.Tags.Add("test", "record1");
        _ = await memoryDb.UpsertAsync(collection, testRecord);

        i++;
        testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { 1, 2, 3 }
        };
        testRecord.Tags.Add("test", "record2");
        _ = await memoryDb.UpsertAsync(collection, testRecord);

        i++;
        testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { -1, -2, -3 }
        };
        testRecord.Tags.Add("test", "record3");
        _ = await memoryDb.UpsertAsync(collection, testRecord);

        i++;
        testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { 1, -1, -2 }
        };
        testRecord.Tags.Add("test", "record4");
        _ = await memoryDb.UpsertAsync(collection, testRecord);

        _ = this._textEmbeddingGeneratorMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compareEmbedding);

        var filter = new MemoryFilter().ByTag("test", "record0");

        // Act
        double threshold = .75;
        var topNResults = memoryDb.GetSimilarListAsync(
                index: collection,
                text: "Sample",
                limit: 4,
                minRelevance: threshold,
                filters: new[] { filter })
            .ToEnumerable()
            .ToArray();

        // Assert
        Assert.NotNull(topNResults);
        Assert.Single(topNResults);

        Assert.Equal("test0", topNResults[0].Item1.Id);
        Assert.True(topNResults[0].Item2 >= threshold);
    }

    /// <summary>
    /// Test that get similar list should return expected results.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetSimilarListShouldNotReturnExpectedWithFiltersAsync()
    {
        // Arrange
        var memoryDb = this.CreateMemoryDb();

        var compareEmbedding = new ReadOnlyMemory<float>(new float[] { 1, 1, 1 });
        string collection = "test_collection";
        await memoryDb.CreateIndexAsync(collection, 1536);
        int i = 0;

        MemoryRecord testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { 1, 1, 1 }
        };

        testRecord.Tags.Add("test", "record0");

        _ = await memoryDb.UpsertAsync(collection, testRecord);

        var filter = new MemoryFilter().ByTag("test", "record1");

        _ = this._textEmbeddingGeneratorMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(compareEmbedding);

        // Act
        double threshold = -1;
        var topNResults = memoryDb.GetSimilarListAsync(
                index: collection,
                text: "Sample",
                limit: 4,
                minRelevance: threshold,
                filters: new[] { filter })
            .ToEnumerable()
            .ToArray();

        // Assert
        Assert.NotNull(topNResults);
        Assert.Empty(topNResults);
    }

    /// <summary>
    /// Test that get similar list should return expected results.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetSimilarListShouldNotReturnExpectedWithFiltersWithANDClauseAsync()
    {
        // Arrange
        var memoryDb = this.CreateMemoryDb();

        var compareEmbedding = new ReadOnlyMemory<float>(new float[] { 1, 1, 1 });
        string collection = "test_collection";
        await memoryDb.CreateIndexAsync(collection, 1536);
        int i = 0;

        MemoryRecord testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { 1, 1, 1 }
        };

        testRecord.Tags.Add("test", "record0");
        testRecord.Tags.Add("test", "test");

        _ = await memoryDb.UpsertAsync(collection, testRecord);

        testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { 1, 1, 1 }
        };

        testRecord.Tags.Add("test", "record1");
        testRecord.Tags.Add("test", "test");

        _ = await memoryDb.UpsertAsync(collection, testRecord);

        _ = this._textEmbeddingGeneratorMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(compareEmbedding);

        // Act
        double threshold = -1;
        var topNResults = memoryDb.GetSimilarListAsync(
                index: collection,
                text: "Sample",
                limit: 4,
                minRelevance: threshold,
                filters: new[] {
                    new MemoryFilter()
                        .ByTag("test", "record0")
                        .ByTag("test", "test")
                })
            .ToEnumerable()
            .ToArray();

        // Assert
        Assert.NotNull(topNResults);
        Assert.Single(topNResults);
    }

    /// <summary>
    /// Test that get similar list should return expected results.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetSimilarListShouldNotReturnExpectedWithFiltersWithORClauseAsync()
    {
        // Arrange
        var memoryDb = this.CreateMemoryDb();
        var compareEmbedding = new ReadOnlyMemory<float>(new float[] { 1, 1, 1 });

        string collection = "test_collection";
        await memoryDb.CreateIndexAsync(collection, 1536);
        int i = 0;

        MemoryRecord testRecord = new MemoryRecord()
        {
            Id = "test" + i++,
            Vector = new float[] { 1, 1, 1 }
        };

        testRecord.Tags.Add("test", "record0");
        testRecord.Tags.Add("test", "test");

        _ = await memoryDb.UpsertAsync(collection, testRecord);

        testRecord = new MemoryRecord()
        {
            Id = "test" + i,
            Vector = new float[] { 1, 1, 1 }
        };

        testRecord.Tags.Add("test", "record1");
        testRecord.Tags.Add("test", "test");

        _ = await memoryDb.UpsertAsync(collection, testRecord);

        _ = this._textEmbeddingGeneratorMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(compareEmbedding);        
        
        // Act
        double threshold = -1;
        var topNResults = memoryDb.GetSimilarListAsync(
                index: collection,
                text: "Sample",
                limit: 4,
                minRelevance: threshold,
                filters: new[] {
                    new MemoryFilter()
                        .ByTag("test", "record0"),
                    new MemoryFilter()
                        .ByTag("test", "record1")
                })
            .ToEnumerable()
            .ToArray();

        // Assert
        Assert.NotNull(topNResults);
        Assert.Equal(2, topNResults.Length);
    }
}
