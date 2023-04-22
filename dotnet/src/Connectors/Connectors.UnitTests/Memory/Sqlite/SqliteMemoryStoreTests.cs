﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.Connectors.Memory.Sqlite;
using Microsoft.SemanticKernel.Memory;
using Xunit;

namespace SemanticKernel.Connectors.UnitTests.Memory.Sqlite;

/// <summary>
/// Unit tests of <see cref="SqliteMemoryStore"/>.
/// </summary>
[Collection("Sequential")]
public class SqliteMemoryStoreTests : IDisposable
{
    private const string DatabaseFile = "SqliteMemoryStoreTests.db";
    private bool _disposedValue = false;

    public SqliteMemoryStoreTests()
    {
        if (File.Exists(DatabaseFile))
        {
            File.Delete(DatabaseFile);
        }

        using (var stream = File.Create(DatabaseFile)) { }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!this._disposedValue)
        {
            if (disposing)
            {
                File.Delete(DatabaseFile);
            }

            this._disposedValue = true;
        }
    }

    private int _collectionNum = 0;

    private IEnumerable<MemoryRecord> CreateBatchRecords(int numRecords)
    {
        Assert.True(numRecords % 2 == 0, "Number of records must be even");
        Assert.True(numRecords > 0, "Number of records must be greater than 0");

        IEnumerable<MemoryRecord> records = new List<MemoryRecord>(numRecords);
        for (int i = 0; i < numRecords / 2; i++)
        {
            var testRecord = MemoryRecord.LocalRecord(
                id: "test" + i,
                text: "text" + i,
                description: "description" + i,
                embedding: new Embedding<float>(new float[] { 1, 1, 1 }));
            records = records.Append(testRecord);
        }

        for (int i = numRecords / 2; i < numRecords; i++)
        {
            var testRecord = MemoryRecord.ReferenceRecord(
                externalId: "test" + i,
                sourceName: "sourceName" + i,
                description: "description" + i,
                embedding: new Embedding<float>(new float[] { 1, 2, 3 }));
            records = records.Append(testRecord);
        }

        return records;
    }

    [Fact]
    public async Task InitializeDbConnectionSucceedsAsync()
    {
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        // Assert
        Assert.NotNull(db);
    }

    [Fact]
    public async Task ItCanCreateAndGetCollectionAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        var collections = db.GetCollectionsAsync();

        // Assert
        Assert.NotEmpty(collections.ToEnumerable());
        Assert.True(await collections.ContainsAsync(collection).ConfigureAwait(false));
    }

    [Fact]
    public async Task ItCanCheckIfCollectionExistsAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        string collection = "my_collection";
        this._collectionNum++;

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);

        // Assert
        Assert.True(await db.DoesCollectionExistAsync("my_collection").ConfigureAwait(false));
        Assert.False(await db.DoesCollectionExistAsync("my_collection2").ConfigureAwait(false));
    }

    [Fact]
    public async Task CreatingDuplicateCollectionDoesNothingAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        var collections = db.GetCollectionsAsync();
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);

        // Assert
        var collections2 = db.GetCollectionsAsync();
        Assert.Equal(await collections.CountAsync().ConfigureAwait(false), await collections.CountAsync().ConfigureAwait(false));
    }

    [Fact]
    public async Task CollectionsCanBeDeletedAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        var collections = await db.GetCollectionsAsync().ToListAsync().ConfigureAwait(false);
        Assert.True(collections.Count > 0);

        // Act
        foreach (var c in collections)
        {
            await db.DeleteCollectionAsync(c).ConfigureAwait(false);
        }

        // Assert
        var collections2 = db.GetCollectionsAsync();
        Assert.True(await collections2.CountAsync().ConfigureAwait(false) == 0);
    }

    [Fact]
    public async Task ItCanInsertIntoNonExistentCollectionAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        MemoryRecord testRecord = MemoryRecord.LocalRecord(
            id: "test",
            text: "text",
            description: "description",
            embedding: new Embedding<float>(new float[] { 1, 2, 3 }),
            key: null,
            timestamp: null);

        // Arrange
        var key = await db.UpsertAsync("random collection", testRecord).ConfigureAwait(false);
        var actual = await db.GetAsync("random collection", key, true).ConfigureAwait(false);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(testRecord.Metadata.Id, key);
        Assert.Equal(testRecord.Metadata.Id, actual.Key);
        Assert.Equal(testRecord.Embedding.Vector, actual.Embedding.Vector);
        Assert.Equal(testRecord.Metadata.Text, actual.Metadata.Text);
        Assert.Equal(testRecord.Metadata.Description, actual.Metadata.Description);
        Assert.Equal(testRecord.Metadata.ExternalSourceName, actual.Metadata.ExternalSourceName);
        Assert.Equal(testRecord.Metadata.Id, actual.Metadata.Id);
    }

    [Fact]
    public async Task GetAsyncReturnsEmptyEmbeddingUnlessSpecifiedAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        MemoryRecord testRecord = MemoryRecord.LocalRecord(
            id: "test",
            text: "text",
            description: "description",
            embedding: new Embedding<float>(new float[] { 1, 2, 3 }),
            key: null,
            timestamp: null);
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        var key = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);
        var actualDefault = await db.GetAsync(collection, key).ConfigureAwait(false);
        var actualWithEmbedding = await db.GetAsync(collection, key, true).ConfigureAwait(false);

        // Assert
        Assert.NotNull(actualDefault);
        Assert.NotNull(actualWithEmbedding);
        Assert.Empty(actualDefault.Embedding.Vector);
        Assert.NotEmpty(actualWithEmbedding.Embedding.Vector);
    }

    [Fact]
    public async Task ItCanUpsertAndRetrieveARecordWithNoTimestampAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        MemoryRecord testRecord = MemoryRecord.LocalRecord(
            id: "test",
            text: "text",
            description: "description",
            embedding: new Embedding<float>(new float[] { 1, 2, 3 }),
            key: null,
            timestamp: null);
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        var key = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);
        var actual = await db.GetAsync(collection, key, true).ConfigureAwait(false);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(testRecord.Metadata.Id, key);
        Assert.Equal(testRecord.Metadata.Id, actual.Key);
        Assert.Equal(testRecord.Embedding.Vector, actual.Embedding.Vector);
        Assert.Equal(testRecord.Metadata.Text, actual.Metadata.Text);
        Assert.Equal(testRecord.Metadata.Description, actual.Metadata.Description);
        Assert.Equal(testRecord.Metadata.ExternalSourceName, actual.Metadata.ExternalSourceName);
        Assert.Equal(testRecord.Metadata.Id, actual.Metadata.Id);
    }

    [Fact]
    public async Task ItCanUpsertAndRetrieveARecordWithTimestampAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        MemoryRecord testRecord = MemoryRecord.LocalRecord(
            id: "test",
            text: "text",
            description: "description",
            embedding: new Embedding<float>(new float[] { 1, 2, 3 }),
            key: null,
            timestamp: DateTimeOffset.UtcNow);
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        var key = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);
        var actual = await db.GetAsync(collection, key, true).ConfigureAwait(false);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(testRecord.Metadata.Id, key);
        Assert.Equal(testRecord.Metadata.Id, actual.Key);
        Assert.Equal(testRecord.Embedding.Vector, actual.Embedding.Vector);
        Assert.Equal(testRecord.Metadata.Text, actual.Metadata.Text);
        Assert.Equal(testRecord.Metadata.Description, actual.Metadata.Description);
        Assert.Equal(testRecord.Metadata.ExternalSourceName, actual.Metadata.ExternalSourceName);
        Assert.Equal(testRecord.Metadata.Id, actual.Metadata.Id);
    }

    [Fact]
    public async Task UpsertReplacesExistingRecordWithSameIdAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        string commonId = "test";
        MemoryRecord testRecord = MemoryRecord.LocalRecord(
            id: commonId,
            text: "text",
            description: "description",
            embedding: new Embedding<float>(new float[] { 1, 2, 3 }));
        MemoryRecord testRecord2 = MemoryRecord.LocalRecord(
            id: commonId,
            text: "text2",
            description: "description2",
            embedding: new Embedding<float>(new float[] { 1, 2, 4 }));
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        var key = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);
        var key2 = await db.UpsertAsync(collection, testRecord2).ConfigureAwait(false);
        var actual = await db.GetAsync(collection, key, true).ConfigureAwait(false);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(testRecord.Metadata.Id, key);
        Assert.Equal(testRecord2.Metadata.Id, actual.Key);
        Assert.NotEqual(testRecord.Embedding.Vector, actual.Embedding.Vector);
        Assert.Equal(testRecord2.Embedding.Vector, actual.Embedding.Vector);
        Assert.NotEqual(testRecord.Metadata.Text, actual.Metadata.Text);
        Assert.Equal(testRecord2.Metadata.Description, actual.Metadata.Description);
    }

    [Fact]
    public async Task ExistingRecordCanBeRemovedAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        MemoryRecord testRecord = MemoryRecord.LocalRecord(
            id: "test",
            text: "text",
            description: "description",
            embedding: new Embedding<float>(new float[] { 1, 2, 3 }));
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        var key = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);
        await db.RemoveAsync(collection, key).ConfigureAwait(false);
        var actual = await db.GetAsync(collection, key).ConfigureAwait(false);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public async Task RemovingNonExistingRecordDoesNothingAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        await db.RemoveAsync(collection, "key").ConfigureAwait(false);
        var actual = await db.GetAsync(collection, "key").ConfigureAwait(false);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public async Task ItCanListAllDatabaseCollectionsAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        string[] testCollections = { "random_collection1", "random_collection2", "random_collection3" };
        this._collectionNum += 3;
        await db.CreateCollectionAsync(testCollections[0]).ConfigureAwait(false);
        await db.CreateCollectionAsync(testCollections[1]).ConfigureAwait(false);
        await db.CreateCollectionAsync(testCollections[2]).ConfigureAwait(false);

        // Act
        var collections = await db.GetCollectionsAsync().ToListAsync().ConfigureAwait(false);

#pragma warning disable CA1851 // Possible multiple enumerations of 'IEnumerable' collection
        // Assert
        foreach (var collection in testCollections)
        {
            Assert.True(await db.DoesCollectionExistAsync(collection).ConfigureAwait(false));
        }

        Assert.NotNull(collections);
        Assert.NotEmpty(collections);
        Assert.Equal(testCollections.Length, collections.Count);
        Assert.True(collections.Contains(testCollections[0]),
            $"Collections does not contain the newly-created collection {testCollections[0]}");
        Assert.True(collections.Contains(testCollections[1]),
            $"Collections does not contain the newly-created collection {testCollections[1]}");
        Assert.True(collections.Contains(testCollections[2]),
            $"Collections does not contain the newly-created collection {testCollections[2]}");
    }
#pragma warning restore CA1851 // Possible multiple enumerations of 'IEnumerable' collection

    [Fact]
    public async Task GetNearestMatchesReturnsAllResultsWithNoMinScoreAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        var compareEmbedding = new Embedding<float>(new float[] { 1, 1, 1 });
        int topN = 4;
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        int i = 0;
        MemoryRecord testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { 1, 1, 1 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { -1, -1, -1 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { 1, 2, 3 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { -1, -2, -3 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { 1, -1, -2 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        // Act
        double threshold = -1;
        var topNResults = db.GetNearestMatchesAsync(collection, compareEmbedding, limit: topN, minRelevanceScore: threshold).ToEnumerable().ToArray();

        // Assert
        Assert.Equal(topN, topNResults.Length);
        for (int j = 0; j < topN - 1; j++)
        {
            int compare = topNResults[j].Item2.CompareTo(topNResults[j + 1].Item2);
            Assert.True(compare >= 0);
        }
    }

    [Fact]
    public async Task GetNearestMatchAsyncReturnsEmptyEmbeddingUnlessSpecifiedAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        var compareEmbedding = new Embedding<float>(new float[] { 1, 1, 1 });
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        int i = 0;
        MemoryRecord testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { 1, 1, 1 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { -1, -1, -1 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { 1, 2, 3 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { -1, -2, -3 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { 1, -1, -2 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        // Act
        double threshold = 0.75;
        var topNResultDefault = await db.GetNearestMatchAsync(collection, compareEmbedding, minRelevanceScore: threshold).ConfigureAwait(false);
        var topNResultWithEmbedding = await db.GetNearestMatchAsync(collection, compareEmbedding, minRelevanceScore: threshold, withEmbedding: true).ConfigureAwait(false);

        // Assert
        Assert.NotNull(topNResultDefault);
        Assert.NotNull(topNResultWithEmbedding);
        Assert.Empty(topNResultDefault.Value.Item1.Embedding.Vector);
        Assert.NotEmpty(topNResultWithEmbedding.Value.Item1.Embedding.Vector);
    }

    [Fact]
    public async Task GetNearestMatchAsyncReturnsExpectedAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        var compareEmbedding = new Embedding<float>(new float[] { 1, 1, 1 });
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        int i = 0;
        MemoryRecord testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { 1, 1, 1 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { -1, -1, -1 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { 1, 2, 3 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { -1, -2, -3 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        i++;
        testRecord = MemoryRecord.LocalRecord(
            id: "test" + i,
            text: "text" + i,
            description: "description" + i,
            embedding: new Embedding<float>(new float[] { 1, -1, -2 }));
        _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);

        // Act
        double threshold = 0.75;
        var topNResult = await db.GetNearestMatchAsync(collection, compareEmbedding, minRelevanceScore: threshold).ConfigureAwait(false);

        // Assert
        Assert.NotNull(topNResult);
        Assert.Equal("test0", topNResult.Value.Item1.Metadata.Id);
        Assert.True(topNResult.Value.Item2 >= threshold);
    }

    [Fact]
    public async Task GetNearestMatchesDifferentiatesIdenticalVectorsByKeyAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        var compareEmbedding = new Embedding<float>(new float[] { 1, 1, 1 });
        int topN = 4;
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);

        for (int i = 0; i < 10; i++)
        {
            MemoryRecord testRecord = MemoryRecord.LocalRecord(
                id: "test" + i,
                text: "text" + i,
                description: "description" + i,
                embedding: new Embedding<float>(new float[] { 1, 1, 1 }));
            _ = await db.UpsertAsync(collection, testRecord).ConfigureAwait(false);
        }

        // Act
        var topNResults = db.GetNearestMatchesAsync(collection, compareEmbedding, limit: topN, minRelevanceScore: 0.75).ToEnumerable().ToArray();
        IEnumerable<string> topNKeys = topNResults.Select(x => x.Item1.Key).ToImmutableSortedSet();

        // Assert
        Assert.Equal(topN, topNResults.Length);
        Assert.Equal(topN, topNKeys.Count());

        for (int i = 0; i < topNResults.Length; i++)
        {
            int compare = topNResults[i].Item2.CompareTo(0.75);
            Assert.True(compare >= 0);
        }
    }

    [Fact]
    public async Task ItCanBatchUpsertRecordsAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        int numRecords = 10;
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;
        IEnumerable<MemoryRecord> records = this.CreateBatchRecords(numRecords);

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        var keys = db.UpsertBatchAsync(collection, records);
        var resultRecords = db.GetBatchAsync(collection, keys.ToEnumerable());

        // Assert
        Assert.NotNull(keys);
        Assert.Equal(numRecords, keys.ToEnumerable().Count());
        Assert.Equal(numRecords, resultRecords.ToEnumerable().Count());
    }

    [Fact]
    public async Task ItCanBatchGetRecordsAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        int numRecords = 10;
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;
        IEnumerable<MemoryRecord> records = this.CreateBatchRecords(numRecords);
        var keys = db.UpsertBatchAsync(collection, records);

        // Act
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);
        var results = db.GetBatchAsync(collection, keys.ToEnumerable());

        // Assert
        Assert.NotNull(keys);
        Assert.NotNull(results);
        Assert.Equal(numRecords, results.ToEnumerable().Count());
    }

    [Fact]
    public async Task ItCanBatchRemoveRecordsAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        int numRecords = 10;
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;
        IEnumerable<MemoryRecord> records = this.CreateBatchRecords(numRecords);
        await db.CreateCollectionAsync(collection).ConfigureAwait(false);

        List<string> keys = new();

        // Act
        await foreach (var key in db.UpsertBatchAsync(collection, records))
        {
            keys.Add(key);
        }

        await db.RemoveBatchAsync(collection, keys).ConfigureAwait(false);

        // Assert
        await foreach (var result in db.GetBatchAsync(collection, keys))
        {
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task DeletingNonExistentCollectionDoesNothingAsync()
    {
        // Arrange
        using SqliteMemoryStore db = await SqliteMemoryStore.ConnectAsync(DatabaseFile).ConfigureAwait(false);
        string collection = "test_collection" + this._collectionNum;
        this._collectionNum++;

        // Act
        await db.DeleteCollectionAsync(collection).ConfigureAwait(false);
    }
}
