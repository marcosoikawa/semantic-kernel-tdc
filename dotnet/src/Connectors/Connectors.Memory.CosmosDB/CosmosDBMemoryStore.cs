﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.AI.Embeddings.VectorOperations;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Memory.Collections;

namespace Connectors.Memory.CosmosDB;

/// <summary>
/// An implementation of <see cref="IMemoryStore"/> for Azure Cosmos DB.
/// </summary>
/// <remarks>The Embedding data is saved to the Azure Cosmos DB database container specified in the constructor.
/// The embedding data persists between subsequent instances and has similarity search capability, handled by the client as Azure Cosmos DB is not a vector-native DB.
/// </remarks>
public class CosmosDBMemoryStore : IMemoryStore
{
    private Database _database;
    private string _databaseName;
    private ILogger _log;

    [SuppressMessage("Performance", "CS8618:Non-nullable field is uninitialized. Consider declaring as nullable.",
        Justification = "Class instance is created and populated via factor method.")]
    private CosmosDBMemoryStore()
    {
    }

    /// <summary>
    /// Factory method to initialize a new instance of the <see cref="CosmosDBMemoryStore"/> class.
    /// </summary>
    /// <param name="client">Client with endpoint and authentication to the Azure CosmosDB Account.</param>
    /// <param name="databaseName">The name of the database to back the memory store.</param>
    /// <param name="log">Optional logger.</param>
    /// <param name="cancel">Optional cancellation token.</param>
    /// <exception cref="CosmosException"></exception>
    public async Task<CosmosDBMemoryStore> CreateAsync(CosmosClient client, string databaseName, ILogger? log = null, CancellationToken cancel = default)
    {
        var newStore = new CosmosDBMemoryStore();

        newStore._databaseName = databaseName;
        this._log = log ?? NullLogger<CosmosDBMemoryStore>.Instance;
        var response = await client.CreateDatabaseIfNotExistsAsync(newStore._databaseName, cancellationToken: cancel);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            newStore._log.LogInformation("Created database {0}", newStore._databaseName);
        }
        else if (response.StatusCode == HttpStatusCode.OK)
        {
            newStore._log.LogInformation("Database {0}", newStore._databaseName);
        }
        else
        {
            throw new CosmosException("Database does not exist and was not created", response.StatusCode, 0, newStore._databaseName, 0);
        }

        newStore._database = response.Database;

        return newStore;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> GetCollectionsAsync(CancellationToken cancel = default)
    {
        // Azure Cosmos DB does not support listing all Containers, this does not break the interface but it is not ideal.
        this._log.LogWarning("Listing all containers is not supported by Azure Cosmos DB, returning empty list.");

        return Enumerable.Empty<string>().ToAsyncEnumerable();
    }

    /// <inheritdoc />
    public async Task CreateCollectionAsync(string collectionName, CancellationToken cancel = default)
    {
        var response = await this._database.CreateContainerIfNotExistsAsync(collectionName, "/" + collectionName, cancellationToken: cancel);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            this._log.LogInformation("Created collection {0}", collectionName);
        }
        else if (response.StatusCode == HttpStatusCode.OK)
        {
            this._log.LogInformation("Collection {0} already exists", collectionName);
        }
        else
        {
            throw new CosmosException("Collection does not exist and was not created", response.StatusCode, 0, collectionName, 0);
        }
    }

    /// <inheritdoc />
    public Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancel = default)
    {
        // Azure Cosmos DB does not support checking if container exists without attempting to create it. This does not break the interface but it is not ideal.
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancel = default)
    {
        var container = this._database.Client.GetContainer(this._databaseName, collectionName);
        _ = await container.DeleteContainerAsync(cancellationToken: cancel);
    }

    /// <inheritdoc />
    public async Task<MemoryRecord?> GetAsync(string collectionName, string key, CancellationToken cancel = default)
    {
        var id = this.ToCosmosFriendlyId(key);
        var partitionKey = PartitionKey.None;

        var container = this._database.Client.GetContainer(this._databaseName, collectionName);
        MemoryRecord? memoryRecord = null;

        var response = await container.ReadItemAsync<CosmosDBMemoryRecord>(id, partitionKey, cancellationToken: cancel);

        if (response == null)
        {
            this._log?.LogWarning("Received no get response collection {1}", collectionName);
        }
        else if (response.StatusCode != HttpStatusCode.OK)
        {
            this._log?.LogWarning("Failed to get record from collection {1} with status code {2}", collectionName, response.StatusCode);
        }
        else
        {
            var result = response.Resource;

            var vector = System.Text.Json.JsonSerializer.Deserialize<float[]>(result.EmbeddingString);

            if (vector != null)
            {
                memoryRecord = MemoryRecord.FromJson(
                    result.MetadataString,
                    new Embedding<float>(vector),
                    result.Id,
                    result.Timestamp);
            }
        }

        return memoryRecord;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(string collectionName, IEnumerable<string> keys,
        [EnumeratorCancellation] CancellationToken cancel = default)
    {
        foreach (var key in keys)
        {
            var record = await this.GetAsync(collectionName, key, cancel);

            if (record != null)
            {
                yield return record;
            }
        }
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancel = default)
    {
        record.Key = this.ToCosmosFriendlyId(record.Metadata.Id);

        var entity = new CosmosDBMemoryRecord
        {
            CollectionId = this.ToCosmosFriendlyId(collectionName),
            Id = record.Key,
            Timestamp = record.Timestamp,
            EmbeddingString = System.Text.Json.JsonSerializer.Serialize(record.Embedding.Vector),
            MetadataString = record.GetSerializedMetadata()
        };

        var container = this._database.Client.GetContainer(this._databaseName, collectionName);

        var response = await container.UpsertItemAsync(entity, cancellationToken: cancel);

        if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
        {
            this._log.LogInformation("Upserted item to collection {0}", collectionName);
        }
        else
        {
            throw new CosmosException("Unable to upsert item collection", response.StatusCode, 0, collectionName, 0);
        }

        return record.Key;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<MemoryRecord> records, [EnumeratorCancellation] CancellationToken cancel = default)
    {
        foreach (var r in records)
        {
            yield return await this.UpsertAsync(collectionName, r, cancel);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string collectionName, string key, CancellationToken cancel = default)
    {
        var container = this._database.Client.GetContainer(this._databaseName, collectionName);
        var response = await container.DeleteItemAsync<CosmosDBMemoryRecord>(
            key,
            PartitionKey.None,
            cancellationToken: cancel);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            this._log.LogInformation("Record deleted from {0}", collectionName);
        }
        else
        {
            throw new CosmosException("Unable to delete record", response.StatusCode, 0, collectionName, 0);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancel = default)
    {
        await Task.WhenAll(keys.Select(k => this.RemoveAsync(collectionName, k, cancel)));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(
        string collectionName,
        Embedding<float> embedding,
        int limit,
        double minRelevanceScore = 0,
        [EnumeratorCancellation] CancellationToken cancel = default)
    {
        {
            if (limit <= 0)
            {
                yield break;
            }

            var collectionMemories = new List<MemoryRecord>();
            TopNCollection<MemoryRecord> embeddings = new(limit);

            await foreach (var entry in this.GetAllAsync(collectionName, cancel))
            {
                if (entry != null)
                {
                    double similarity = embedding
                        .AsReadOnlySpan()
                        .CosineSimilarity(entry.Embedding.AsReadOnlySpan());
                    if (similarity >= minRelevanceScore)
                    {
                        embeddings.Add(new(entry, similarity));
                    }
                }
            }

            embeddings.SortByScore();

            foreach (var item in embeddings)
            {
                yield return (item.Value, item.Score.Value);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<(MemoryRecord, double)?> GetNearestMatchAsync(string collectionName, Embedding<float> embedding, double minRelevanceScore = 0,
        CancellationToken cancel = default)
    {
        return await this.GetNearestMatchesAsync(
            collectionName: collectionName,
            embedding: embedding,
            limit: 1,
            minRelevanceScore: minRelevanceScore,
            cancel: cancel).FirstOrDefaultAsync(cancellationToken: cancel);
    }

    private async IAsyncEnumerable<MemoryRecord> GetAllAsync(string collectionName, [EnumeratorCancellation] CancellationToken cancel = default)
    {
        var container = this._database.Client.GetContainer(this._databaseName, collectionName);
        var query = new QueryDefinition("SELECT * FROM c");

        var iterator = container.GetItemQueryIterator<CosmosDBMemoryRecord>(query);

        var items = await iterator.ReadNextAsync(cancel).ConfigureAwait(false);

        foreach (var item in items)
        {
            var vector = System.Text.Json.JsonSerializer.Deserialize<float[]>(item.EmbeddingString);

            if (vector != null)
            {
                yield return MemoryRecord.FromJson(
                    item.MetadataString,
                    new Embedding<float>(vector),
                    item.Id,
                    item.Timestamp);
            }
        }
    }

    private string ToCosmosFriendlyId(string id)
    {
        return $"{id.Trim().Replace(' ', '-').Replace('/', '_').Replace('\\', '_').Replace('?', '_').Replace('#', '_').ToUpperInvariant()}";
    }
}
