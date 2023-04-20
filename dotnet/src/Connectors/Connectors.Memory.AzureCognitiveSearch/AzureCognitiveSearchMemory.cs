﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;

public class AzureCognitiveSearchMemory : ISemanticTextMemory
{
    private readonly SearchIndexClient _adminClient;

    // private readonly SearchClient _searchClient;
    private readonly ConcurrentDictionary<string, SearchClient> _clientsByIndex = new();

    /// <summary>
    /// Create a new instance of semantic memory using Azure Cognitive Search.
    /// </summary>
    /// <param name="endpoint">Azure Cognitive Search URI, e.g. "https://contoso.search.windows.net"</param>
    /// <param name="apiKey">API Key</param>
    public AzureCognitiveSearchMemory(string endpoint, string apiKey)
    {
        AzureKeyCredential credentials = new(apiKey);
        this._adminClient = new SearchIndexClient(new Uri(endpoint), credentials);
    }

    /// <summary>
    /// Create a new instance of semantic memory using Azure Cognitive Search.
    /// </summary>
    /// <param name="endpoint">Azure Cognitive Search URI, e.g. "https://contoso.search.windows.net"</param>
    /// <param name="credentials">Azure service</param>
    public AzureCognitiveSearchMemory(string endpoint, TokenCredential credentials)
    {
        this._adminClient = new SearchIndexClient(new Uri(endpoint), credentials);
    }

    /// <inheritdoc />
    public Task<string> SaveInformationAsync(
        string collection,
        string text,
        string id,
        string? description = null,
        string? additionalMetadata = null,
        CancellationToken cancel = default)
    {
        collection = NormalizeIndexName(collection);

        AzureCognitiveSearchRecord record = new()
        {
            Id = EncodeId(id),
            Text = text,
            Description = description,
            AdditionalMetadata = additionalMetadata,
            IsReference = false,
        };

        return this.UpsertRecordAsync(collection, record, cancel);
    }

    /// <inheritdoc />
    public Task<string> SaveReferenceAsync(
        string collection,
        string text,
        string externalId,
        string externalSourceName,
        string? description = null,
        string? additionalMetadata = null,
        CancellationToken cancel = default)
    {
        collection = NormalizeIndexName(collection);

        AzureCognitiveSearchRecord record = new()
        {
            Id = EncodeId(externalId),
            Text = text,
            Description = description,
            AdditionalMetadata = additionalMetadata,
            ExternalSourceName = externalSourceName,
            IsReference = true,
        };

        return this.UpsertRecordAsync(collection, record, cancel);
    }

    /// <inheritdoc />
    public async Task<MemoryQueryResult?> GetAsync(
        string collection,
        string key,
        bool withEmbedding = false,
        CancellationToken cancel = default)
    {
        collection = NormalizeIndexName(collection);

        var client = await this.GetSearchClientAsync(collection, cancel).ConfigureAwait(false);

        Response<AzureCognitiveSearchRecord>? result = await client.GetDocumentAsync<AzureCognitiveSearchRecord>(
            EncodeId(key), cancellationToken: cancel).ConfigureAwait(false);

        if (result == null || result.Value == null)
        {
            throw new AzureCognitiveSearchMemoryException("Memory read returned null");
        }

        return new MemoryQueryResult(ToMemoryRecordMetadata(result.Value), 1, null);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MemoryQueryResult> SearchAsync(
        string collection,
        string query,
        int limit = 1,
        double minRelevanceScore = 0.7,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancel = default)
    {
        collection = NormalizeIndexName(collection);

        var client = await this.GetSearchClientAsync(collection, cancel).ConfigureAwait(false);

        var options = new SearchOptions
        {
            QueryType = SearchQueryType.Semantic,
            SemanticConfigurationName = "default",
            QueryLanguage = "en-us", // TODO: this shouldn't be required
            Size = limit,
        };

        Response<SearchResults<AzureCognitiveSearchRecord>>? searchResult = await client
            .SearchAsync<AzureCognitiveSearchRecord>(query, options, cancellationToken: cancel)
            .ConfigureAwait(false);

        await foreach (SearchResult<AzureCognitiveSearchRecord>? doc in searchResult.Value.GetResultsAsync())
        {
            if (doc.RerankerScore < minRelevanceScore) { break; }

            yield return new MemoryQueryResult(ToMemoryRecordMetadata(doc.Document), doc.RerankerScore ?? 1, null);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string collection, string key, CancellationToken cancel = default)
    {
        collection = NormalizeIndexName(collection);

        var records = new List<AzureCognitiveSearchRecord> { new() { Id = EncodeId(key) } };

        var client = await this.GetSearchClientAsync(collection, cancel).ConfigureAwait(false);

        await client.DeleteDocumentsAsync(records, cancellationToken: cancel).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IList<string>> GetCollectionsAsync(CancellationToken cancel = default)
    {
        ConfiguredCancelableAsyncEnumerable<SearchIndex> indexes = this._adminClient.GetIndexesAsync(cancel).ConfigureAwait(false);

        var result = new List<string>();
        await foreach (var index in indexes)
        {
            result.Add(index.Name);
        }

        return result;
    }

    /// <summary>
    /// Get a search client for the index specified.
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    /// <returns>Search client ready to read/write</returns>
    private async Task<SearchClient> GetSearchClientAsync(
        string indexName,
        CancellationToken cancellationToken = default)
    {
        Response<SearchIndex>? existingIndex = null;
        try
        {
            // Search the index
            existingIndex = await this._adminClient.GetIndexAsync(indexName, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
        }

        // Create the index if it doesn't exist
        if (existingIndex == null || existingIndex.Value == null)
        {
            await this.CreateIndexAsync(indexName, cancellationToken).ConfigureAwait(false);
        }

        // Search an available client from the local cache
        if (!this._clientsByIndex.TryGetValue(indexName, out SearchClient? client) || client == null)
        {
            client = this._adminClient.GetSearchClient(indexName);
            this._clientsByIndex[indexName] = client;
        }

        return client;
    }

    /// <summary>
    /// Create a new search index.
    /// </summary>
    /// <param name="indexName">Index name</param>
    /// <param name="cancellationToken">Task cancellation token</param>
    private Task CreateIndexAsync(
        string indexName,
        CancellationToken cancellationToken = default)
    {
        var fieldBuilder = new FieldBuilder();
        var fields = fieldBuilder.Build(typeof(AzureCognitiveSearchRecord));
        var newIndex = new SearchIndex(indexName, fields)
        {
            SemanticSettings = new SemanticSettings
            {
                Configurations =
                {
                    new SemanticConfiguration("default", new PrioritizedFields
                    {
                        TitleField = new SemanticField { FieldName = "Description" },
                        ContentFields =
                        {
                            new SemanticField { FieldName = "Text" },
                            new SemanticField { FieldName = "AdditionalMetadata" },
                        }
                    })
                }
            }
        };

        return this._adminClient.CreateIndexAsync(newIndex, cancellationToken);
    }

    private async Task<string> UpsertRecordAsync(
        string indexName,
        AzureCognitiveSearchRecord record,
        CancellationToken cancellationToken = default)
    {
        var client = await this.GetSearchClientAsync(indexName, cancellationToken).ConfigureAwait(false);

        Response<IndexDocumentsResult>? result = await client.MergeOrUploadDocumentsAsync(
            new List<AzureCognitiveSearchRecord> { record },
            new IndexDocumentsOptions { ThrowOnAnyError = true },
            cancellationToken).ConfigureAwait(false);

        if (result == null)
        {
            throw new AzureCognitiveSearchMemoryException("Memory write returned null");
        }

        return result.Value.Results[0].Key;
    }

    private static MemoryRecordMetadata ToMemoryRecordMetadata(AzureCognitiveSearchRecord data)
    {
        return new MemoryRecordMetadata(
            isReference: data.IsReference,
            id: DecodeId(data.Id),
            text: data.Text ?? string.Empty,
            description: data.Description ?? string.Empty,
            externalSourceName: data.ExternalSourceName,
            additionalMetadata: data.AdditionalMetadata ?? string.Empty);
    }

    /// <summary>
    /// Normalize index name to match ACS rules.
    /// The method doesn't handle all the error scenarios, leaving it to the service
    /// to throw an error for edge cases not handled locally.
    /// </summary>
    /// <param name="indexName">Value to normalize</param>
    /// <returns>Normalized name</returns>
    private static string NormalizeIndexName(string indexName)
    {
        if (indexName.Length > 128)
        {
            throw new AzureCognitiveSearchMemoryException("The collection name is too long, it cannot exceed 128 chars");
        }

#pragma warning disable CA1308 // The service expects a lowercase string
        indexName = indexName.ToLowerInvariant();
#pragma warning restore CA1308

        return Regex.Replace(indexName.Trim(), "[\\s|\\\\|/|.|_|:]", "-");
    }

    /// <summary>
    /// ACS keys can contain only letters, digits, underscore, dash, equal sign, recommending
    /// to encode values with a URL-safe algorithm.
    /// </summary>
    /// <param name="realId">Original Id</param>
    /// <returns>Encoded id</returns>
    private static string EncodeId(string realId)
    {
        var bytes = Encoding.UTF8.GetBytes(realId);
        return Convert.ToBase64String(bytes);
    }

    private static string DecodeId(string encodedId)
    {
        var bytes = Convert.FromBase64String(encodedId);
        return Encoding.UTF8.GetString(bytes);
    }
}
