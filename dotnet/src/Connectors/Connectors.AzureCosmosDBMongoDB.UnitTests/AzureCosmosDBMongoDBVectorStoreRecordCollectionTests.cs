﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBMongoDB;
using Microsoft.SemanticKernel.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace SemanticKernel.Connectors.AzureCosmosDBMongoDB.UnitTests;

/// <summary>
/// Unit tests for <see cref="AzureCosmosDBMongoDBVectorStoreRecordCollection{TRecord}"/> class.
/// </summary>
public sealed class AzureCosmosDBMongoDBVectorStoreRecordCollectionTests
{
    private readonly Mock<IMongoDatabase> _mockMongoDatabase = new();
    private readonly Mock<IMongoCollection<BsonDocument>> _mockMongoCollection = new();

    public AzureCosmosDBMongoDBVectorStoreRecordCollectionTests()
    {
        this._mockMongoDatabase
            .Setup(l => l.GetCollection<BsonDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(this._mockMongoCollection.Object);
    }

    [Fact]
    public void ConstructorForModelWithoutKeyThrowsException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new AzureCosmosDBMongoDBVectorStoreRecordCollection<object>(this._mockMongoDatabase.Object, "collection"));
        Assert.Contains("No key property found", exception.Message);
    }

    [Fact]
    public void ConstructorWithDeclarativeModelInitializesCollection()
    {
        // Act & Assert
        var collection = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(
            this._mockMongoDatabase.Object,
            "collection");

        Assert.NotNull(collection);
    }

    [Fact]
    public void ConstructorWithImperativeModelInitializesCollection()
    {
        // Arrange
        var definition = new VectorStoreRecordDefinition
        {
            Properties = [new VectorStoreRecordKeyProperty("Id", typeof(string))]
        };

        // Act
        var collection = new AzureCosmosDBMongoDBVectorStoreRecordCollection<TestModel>(
            this._mockMongoDatabase.Object,
            "collection",
            new() { VectorStoreRecordDefinition = definition });

        // Assert
        Assert.NotNull(collection);
    }

    [Theory]
    [MemberData(nameof(CollectionExistsData))]
    public async Task CollectionExistsReturnsValidResultAsync(List<string> collections, string collectionName, bool expectedResult)
    {
        // Arrange
        var mockCursor = new Mock<IAsyncCursor<string>>();

        mockCursor
            .Setup(l => l.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mockCursor
            .Setup(l => l.Current)
            .Returns(collections);

        this._mockMongoDatabase
            .Setup(l => l.ListCollectionNamesAsync(It.IsAny<ListCollectionNamesOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(
            this._mockMongoDatabase.Object,
            collectionName);

        // Act
        var actualResult = await sut.CollectionExistsAsync();

        // Assert
        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task CreateCollectionInvokesValidMethodsAsync(bool indexExists, int actualIndexCreations)
    {
        // Arrange
        const string CollectionName = "collection";

        List<BsonDocument> indexes = indexExists ? [new BsonDocument { ["name"] = "DescriptionEmbedding_" }] : [];

        var mockIndexCursor = new Mock<IAsyncCursor<BsonDocument>>();
        mockIndexCursor
            .SetupSequence(l => l.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(true)
            .Returns(false);

        mockIndexCursor
            .Setup(l => l.Current)
            .Returns(indexes);

        var mockMongoIndexManager = new Mock<IMongoIndexManager<BsonDocument>>();

        mockMongoIndexManager
            .Setup(l => l.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockIndexCursor.Object);

        this._mockMongoCollection
            .Setup(l => l.Indexes)
            .Returns(mockMongoIndexManager.Object);

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(this._mockMongoDatabase.Object, CollectionName);

        // Act
        await sut.CreateCollectionAsync();

        // Assert
        this._mockMongoDatabase.Verify(l => l.CreateCollectionAsync(
            CollectionName,
            It.IsAny<CreateCollectionOptions>(),
            It.IsAny<CancellationToken>()), Times.Once());

        this._mockMongoDatabase.Verify(l => l.RunCommandAsync<BsonDocument>(
            It.Is<BsonDocumentCommand<BsonDocument>>(command =>
                command.Document["createIndexes"] == CollectionName &&
                command.Document["indexes"].GetType() == typeof(BsonArray) &&
                ((BsonArray)command.Document["indexes"]).Count == 1),
            It.IsAny<ReadPreference>(),
            It.IsAny<CancellationToken>()), Times.Exactly(actualIndexCreations));
    }

    [Theory]
    [MemberData(nameof(CreateCollectionIfNotExistsData))]
    public async Task CreateCollectionIfNotExistsInvokesValidMethodsAsync(List<string> collections, int actualCollectionCreations)
    {
        // Arrange
        const string CollectionName = "collection";

        var mockCursor = new Mock<IAsyncCursor<string>>();
        mockCursor
            .Setup(l => l.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mockCursor
            .Setup(l => l.Current)
            .Returns(collections);

        this._mockMongoDatabase
            .Setup(l => l.ListCollectionNamesAsync(It.IsAny<ListCollectionNamesOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        var mockIndexCursor = new Mock<IAsyncCursor<BsonDocument>>();
        mockIndexCursor
            .SetupSequence(l => l.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(true)
            .Returns(false);

        mockIndexCursor
            .Setup(l => l.Current)
            .Returns([]);

        var mockMongoIndexManager = new Mock<IMongoIndexManager<BsonDocument>>();

        mockMongoIndexManager
            .Setup(l => l.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockIndexCursor.Object);

        this._mockMongoCollection
            .Setup(l => l.Indexes)
            .Returns(mockMongoIndexManager.Object);

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(
            this._mockMongoDatabase.Object,
            CollectionName);

        // Act
        await sut.CreateCollectionIfNotExistsAsync();

        // Assert
        this._mockMongoDatabase.Verify(l => l.CreateCollectionAsync(
            CollectionName,
            It.IsAny<CreateCollectionOptions>(),
            It.IsAny<CancellationToken>()), Times.Exactly(actualCollectionCreations));
    }

    [Fact]
    public async Task DeleteInvokesValidMethodsAsync()
    {
        // Arrange
        const string RecordKey = "key";

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(
            this._mockMongoDatabase.Object,
            "collection");

        var serializerRegistry = BsonSerializer.SerializerRegistry;
        var documentSerializer = serializerRegistry.GetSerializer<BsonDocument>();
        var expectedDefinition = Builders<BsonDocument>.Filter.Eq(document => document["_id"], RecordKey);

        // Act
        await sut.DeleteAsync(RecordKey);

        // Assert
        this._mockMongoCollection.Verify(l => l.DeleteOneAsync(
            It.Is<FilterDefinition<BsonDocument>>(definition =>
                definition.Render(documentSerializer, serializerRegistry) ==
                expectedDefinition.Render(documentSerializer, serializerRegistry)),
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task DeleteBatchInvokesValidMethodsAsync()
    {
        // Arrange
        List<string> recordKeys = ["key1", "key2"];

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(
            this._mockMongoDatabase.Object,
            "collection");

        var serializerRegistry = BsonSerializer.SerializerRegistry;
        var documentSerializer = serializerRegistry.GetSerializer<BsonDocument>();
        var expectedDefinition = Builders<BsonDocument>.Filter.In(document => document["_id"].AsString, recordKeys);

        // Act
        await sut.DeleteBatchAsync(recordKeys);

        // Assert
        this._mockMongoCollection.Verify(l => l.DeleteManyAsync(
            It.Is<FilterDefinition<BsonDocument>>(definition =>
                definition.Render(documentSerializer, serializerRegistry) ==
                expectedDefinition.Render(documentSerializer, serializerRegistry)),
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task DeleteCollectionInvokesValidMethodsAsync()
    {
        // Arrange
        const string CollectionName = "collection";

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(
            this._mockMongoDatabase.Object,
            CollectionName);

        // Act
        await sut.DeleteCollectionAsync();

        // Assert
        this._mockMongoDatabase.Verify(l => l.DropCollectionAsync(
            It.Is<string>(name => name == CollectionName),
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task GetReturnsValidRecordAsync()
    {
        // Arrange
        const string RecordKey = "key";

        var document = new BsonDocument { ["_id"] = RecordKey, ["HotelName"] = "Test Name" };

        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        mockCursor
            .Setup(l => l.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mockCursor
            .Setup(l => l.Current)
            .Returns([document]);

        this._mockMongoCollection
            .Setup(l => l.FindAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<FindOptions<BsonDocument>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(
            this._mockMongoDatabase.Object,
            "collection");

        // Act
        var result = await sut.GetAsync(RecordKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RecordKey, result.HotelId);
        Assert.Equal("Test Name", result.HotelName);
    }

    [Fact]
    public async Task GetBatchReturnsValidRecordAsync()
    {
        // Arrange
        var document1 = new BsonDocument { ["_id"] = "key1", ["HotelName"] = "Test Name 1" };
        var document2 = new BsonDocument { ["_id"] = "key2", ["HotelName"] = "Test Name 2" };
        var document3 = new BsonDocument { ["_id"] = "key3", ["HotelName"] = "Test Name 3" };

        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>();
        mockCursor
            .SetupSequence(l => l.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        mockCursor
            .Setup(l => l.Current)
            .Returns([document1, document2, document3]);

        this._mockMongoCollection
            .Setup(l => l.FindAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<FindOptions<BsonDocument>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(
            this._mockMongoDatabase.Object,
            "collection");

        // Act
        var results = await sut.GetBatchAsync(["key1", "key2", "key3"]).ToListAsync();

        // Assert
        Assert.NotNull(results[0]);
        Assert.Equal("key1", results[0].HotelId);
        Assert.Equal("Test Name 1", results[0].HotelName);

        Assert.NotNull(results[1]);
        Assert.Equal("key2", results[1].HotelId);
        Assert.Equal("Test Name 2", results[1].HotelName);

        Assert.NotNull(results[2]);
        Assert.Equal("key3", results[2].HotelId);
        Assert.Equal("Test Name 3", results[2].HotelName);
    }

    [Fact]
    public async Task UpsertReturnsRecordKeyAsync()
    {
        // Arrange
        var hotel = new AzureCosmosDBMongoDBHotelModel("key") { HotelName = "Test Name" };

        var serializerRegistry = BsonSerializer.SerializerRegistry;
        var documentSerializer = serializerRegistry.GetSerializer<BsonDocument>();
        var expectedDefinition = Builders<BsonDocument>.Filter.Eq(document => document["_id"], "key");

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(
            this._mockMongoDatabase.Object,
            "collection");

        // Act
        var result = await sut.UpsertAsync(hotel);

        // Assert
        Assert.Equal("key", result);

        this._mockMongoCollection.Verify(l => l.ReplaceOneAsync(
            It.Is<FilterDefinition<BsonDocument>>(definition =>
                definition.Render(documentSerializer, serializerRegistry) ==
                expectedDefinition.Render(documentSerializer, serializerRegistry)),
            It.Is<BsonDocument>(document =>
                document["_id"] == "key" &&
                document["HotelName"] == "Test Name"),
            It.IsAny<ReplaceOptions>(),
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task UpsertBatchReturnsRecordKeysAsync()
    {
        // Arrange
        var hotel1 = new AzureCosmosDBMongoDBHotelModel("key1") { HotelName = "Test Name 1" };
        var hotel2 = new AzureCosmosDBMongoDBHotelModel("key2") { HotelName = "Test Name 2" };
        var hotel3 = new AzureCosmosDBMongoDBHotelModel("key3") { HotelName = "Test Name 3" };

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<AzureCosmosDBMongoDBHotelModel>(
            this._mockMongoDatabase.Object,
            "collection");

        // Act
        var results = await sut.UpsertBatchAsync([hotel1, hotel2, hotel3]).ToListAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);

        Assert.Equal("key1", results[0]);
        Assert.Equal("key2", results[1]);
        Assert.Equal("key3", results[2]);
    }

    [Fact]
    public async Task UpsertWithModelWorksCorrectlyAsync()
    {
        var definition = new VectorStoreRecordDefinition
        {
            Properties = new List<VectorStoreRecordProperty>
            {
                new VectorStoreRecordKeyProperty("Id", typeof(string)),
                new VectorStoreRecordDataProperty("HotelName", typeof(string))
            }
        };

        await this.TestUpsertWithModeAsync<TestModel>(
            dataModel: new TestModel { Id = "key", HotelName = "Test Name" },
            expectedPropertyName: "HotelName",
            definition: definition);
    }

    [Fact]
    public async Task UpsertWithVectorStoreModelWorksCorrectlyAsync()
    {
        await this.TestUpsertWithModeAsync<VectorStoreTestModel>(
            dataModel: new VectorStoreTestModel { Id = "key", HotelName = "Test Name" },
            expectedPropertyName: "hotel_name");
    }

    [Fact]
    public async Task UpsertWithBsonModelWorksCorrectlyAsync()
    {
        var definition = new VectorStoreRecordDefinition
        {
            Properties = new List<VectorStoreRecordProperty>
            {
                new VectorStoreRecordKeyProperty("Id", typeof(string)),
                new VectorStoreRecordDataProperty("HotelName", typeof(string))
            }
        };

        await this.TestUpsertWithModeAsync<BsonTestModel>(
            dataModel: new BsonTestModel { Id = "key", HotelName = "Test Name" },
            expectedPropertyName: "hotel_name",
            definition: definition);
    }

    [Fact]
    public async Task UpsertWithBsonVectorStoreModelWorksCorrectlyAsync()
    {
        await this.TestUpsertWithModeAsync<BsonVectorStoreTestModel>(
            dataModel: new BsonVectorStoreTestModel { Id = "key", HotelName = "Test Name" },
            expectedPropertyName: "hotel_name");
    }

    public static TheoryData<List<string>, string, bool> CollectionExistsData => new()
    {
        { ["collection-2"], "collection-2", true },
        { [], "non-existent-collection", false }
    };

    public static TheoryData<List<string>, int> CreateCollectionIfNotExistsData => new()
    {
        { ["collection"], 0 },
        { [], 1 }
    };

    #region private

    private async Task TestUpsertWithModeAsync<TDataModel>(
        TDataModel dataModel,
        string expectedPropertyName,
        VectorStoreRecordDefinition? definition = null)
        where TDataModel : class
    {
        // Arrange
        var serializerRegistry = BsonSerializer.SerializerRegistry;
        var documentSerializer = serializerRegistry.GetSerializer<BsonDocument>();
        var expectedDefinition = Builders<BsonDocument>.Filter.Eq(document => document["_id"], "key");

        AzureCosmosDBMongoDBVectorStoreRecordCollectionOptions<TDataModel>? options = definition != null ?
            new() { VectorStoreRecordDefinition = definition } :
            null;

        var sut = new AzureCosmosDBMongoDBVectorStoreRecordCollection<TDataModel>(
            this._mockMongoDatabase.Object,
            "collection",
            options);

        // Act
        var result = await sut.UpsertAsync(dataModel);

        // Assert
        Assert.Equal("key", result);

        this._mockMongoCollection.Verify(l => l.ReplaceOneAsync(
            It.Is<FilterDefinition<BsonDocument>>(definition =>
                definition.Render(documentSerializer, serializerRegistry) ==
                expectedDefinition.Render(documentSerializer, serializerRegistry)),
            It.Is<BsonDocument>(document =>
                document["_id"] == "key" &&
                document.Contains(expectedPropertyName) &&
                document[expectedPropertyName] == "Test Name"),
            It.IsAny<ReplaceOptions>(),
            It.IsAny<CancellationToken>()), Times.Once());
    }

#pragma warning disable CA1812
    private sealed class TestModel
    {
        public string? Id { get; set; }

        public string? HotelName { get; set; }
    }

    private sealed class VectorStoreTestModel
    {
        [VectorStoreRecordKey]
        public string? Id { get; set; }

        [VectorStoreRecordData(StoragePropertyName = "hotel_name")]
        public string? HotelName { get; set; }
    }

    private sealed class BsonTestModel
    {
        [BsonId]
        public string? Id { get; set; }

        [BsonElement("hotel_name")]
        public string? HotelName { get; set; }
    }

    private sealed class BsonVectorStoreTestModel
    {
        [BsonId]
        [VectorStoreRecordKey]
        public string? Id { get; set; }

        [BsonElement("hotel_name")]
        [VectorStoreRecordData]
        public string? HotelName { get; set; }
    }
#pragma warning restore CA1812

    #endregion
}
