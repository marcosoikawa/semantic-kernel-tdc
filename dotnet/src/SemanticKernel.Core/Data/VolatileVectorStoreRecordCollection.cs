﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Service for storing and retrieving vector records, that uses an in memory dictionary as the underlying storage.
/// </summary>
/// <typeparam name="TKey">The data type of the record key.</typeparam>
/// <typeparam name="TRecord">The data model to use for adding, updating and retrieving data from storage.</typeparam>
[Experimental("SKEXP0001")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class VolatileVectorStoreRecordCollection<TKey, TRecord> : IVectorStoreRecordCollection<TKey, TRecord>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    where TKey : notnull
    where TRecord : class
{
    /// <summary>A set of types that vectors on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedVectorTypes =
    [
        typeof(ReadOnlyMemory<float>),
        typeof(ReadOnlyMemory<float>?),
    ];

    /// <summary>Internal storage for the record collection.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<object, object>> _internalCollection;

    /// <summary>The data type of each collection, to enforce a single type per collection.</summary>
    private readonly ConcurrentDictionary<string, Type> _internalCollectionTypes;

    /// <summary>Optional configuration options for this class.</summary>
    private readonly VolatileVectorStoreRecordCollectionOptions _options;

    /// <summary>The name of the collection that this <see cref="VolatileVectorStoreRecordCollection{TKey,TRecord}"/> will access.</summary>
    private readonly string _collectionName;

    /// <summary>A dictionary of vector properties on the provided model, keyed by the property name.</summary>
    private readonly Dictionary<string, VectorStoreRecordVectorProperty> _vectorProperties;

    /// <summary>The name of the first vector field for the collections that this class is used with.</summary>
    private readonly string? _firstVectorPropertyName = null;

    /// <summary>An function to look up vectors from the records.</summary>
    private readonly VolatileVectorStoreVectorResolver _vectorResolver;

    /// <summary>An function to look up keys from the records.</summary>
    private readonly VolatileVectorStoreKeyResolver _keyResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatileVectorStoreRecordCollection{TKey,TRecord}"/> class.
    /// </summary>
    /// <param name="collectionName">The name of the collection that this <see cref="VolatileVectorStoreRecordCollection{TKey,TRecord}"/> will access.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public VolatileVectorStoreRecordCollection(string collectionName, VolatileVectorStoreRecordCollectionOptions? options = default)
    {
        // Verify.
        Verify.NotNullOrWhiteSpace(collectionName);
        Verify.True(
            !(typeof(TRecord).IsGenericType && typeof(TRecord).GetGenericTypeDefinition() == typeof(VectorStoreGenericDataModel<>) && options?.VectorStoreRecordDefinition is null),
            $"A {nameof(VectorStoreRecordDefinition)} must be provided when using {nameof(VectorStoreGenericDataModel<string>)}.",
            nameof(options));

        // Assign.
        this._collectionName = collectionName;
        this._internalCollection = new();
        this._internalCollectionTypes = new();
        this._options = options ?? new VolatileVectorStoreRecordCollectionOptions();
        var vectorStoreRecordDefinition = this._options.VectorStoreRecordDefinition ?? VectorStoreRecordPropertyReader.CreateVectorStoreRecordDefinitionFromType(typeof(TRecord), true);

        // Validate property types.
        var properties = VectorStoreRecordPropertyReader.SplitDefinitionAndVerify(typeof(TRecord).Name, vectorStoreRecordDefinition, supportsMultipleVectors: true, requiresAtLeastOneVector: false);
        VectorStoreRecordPropertyReader.VerifyPropertyTypes(properties.VectorProperties, s_supportedVectorTypes, "Vector");
        this._vectorProperties = properties.VectorProperties.ToDictionary(x => x.DataModelPropertyName);
        if (properties.VectorProperties.Count > 0)
        {
            this._firstVectorPropertyName = properties.VectorProperties.First().DataModelPropertyName;
        }

        // Assign resolvers.
        this._vectorResolver = CreateVectorResolver(this._options.VectorResolver, this._vectorProperties);
        this._keyResolver = CreateKeyResolver(this._options.KeyResolver, properties.KeyProperty);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatileVectorStoreRecordCollection{TKey,TRecord}"/> class.
    /// </summary>
    /// <param name="internalCollection">Internal storage for the record collection.</param>
    /// <param name="internalCollectionTypes">The data type of each collection, to enforce a single type per collection.</param>
    /// <param name="collectionName">The name of the collection that this <see cref="VolatileVectorStoreRecordCollection{TKey,TRecord}"/> will access.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    internal VolatileVectorStoreRecordCollection(
        ConcurrentDictionary<string, ConcurrentDictionary<object, object>> internalCollection,
        ConcurrentDictionary<string, Type> internalCollectionTypes,
        string collectionName,
        VolatileVectorStoreRecordCollectionOptions? options = default)
        : this(collectionName, options)
    {
        this._internalCollection = internalCollection;
        this._internalCollectionTypes = internalCollectionTypes;
    }

    /// <inheritdoc />
    public string CollectionName => this._collectionName;

    /// <inheritdoc />
    public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        return this._internalCollection.ContainsKey(this._collectionName) ? Task.FromResult(true) : Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task CreateCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (!this._internalCollection.ContainsKey(this._collectionName))
        {
            this._internalCollection.TryAdd(this._collectionName, new ConcurrentDictionary<object, object>());
            this._internalCollectionTypes.TryAdd(this._collectionName, typeof(TRecord));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task CreateCollectionIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        if (!await this.CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await this.CreateCollectionAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
    {
        this._internalCollection.TryRemove(this._collectionName, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TRecord?> GetAsync(TKey key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        if (collectionDictionary.TryGetValue(key, out var record))
        {
            return Task.FromResult<TRecord?>(record as TRecord);
        }

        return Task.FromResult<TRecord?>(null);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TRecord> GetBatchAsync(IEnumerable<TKey> keys, GetRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var record = await this.GetAsync(key, options, cancellationToken).ConfigureAwait(false);

            if (record is not null)
            {
                yield return record;
            }
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(TKey key, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        collectionDictionary.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteBatchAsync(IEnumerable<TKey> keys, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        foreach (var key in keys)
        {
            collectionDictionary.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TKey> UpsertAsync(TRecord record, UpsertRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = this.GetCollectionDictionary();

        var key = (TKey)this._keyResolver(record)!;
        collectionDictionary.AddOrUpdate(key!, record, (key, currentValue) => record);

        return Task.FromResult(key!);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TKey> UpsertBatchAsync(IEnumerable<TRecord> records, UpsertRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            yield return await this.UpsertAsync(record, options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously - Need to satisfy the interface which returns IAsyncEnumerable
    public async IAsyncEnumerable<VectorSearchResult<TRecord>> VectorizedSearchAsync<TVector>(TVector vector, VectorSearchOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore CS1998
    {
        Verify.NotNull(vector);

        if (this._firstVectorPropertyName is null)
        {
            throw new InvalidOperationException("The collection does not have any vector fields, so vector search is not possible.");
        }

        if (vector is not ReadOnlyMemory<float> floatVector)
        {
            throw new NotSupportedException($"The provided vector type {vector.GetType().Name} is not supported by the Qdrant connector.");
        }

        // Resolve options and get requested vector property or first as default.
        var internalOptions = options ?? Data.VectorSearchOptions.Default;

        var vectorPropertyName = string.IsNullOrWhiteSpace(internalOptions.VectorFieldName) ? this._firstVectorPropertyName : internalOptions.VectorFieldName;
        if (!this._vectorProperties.TryGetValue(vectorPropertyName!, out var vectorProperty))
        {
            throw new InvalidOperationException($"The collection does not have a vector field named '{internalOptions.VectorFieldName}', so vector search is not possible.");
        }

        // Filter records using the provided filter before doing the vector comparison.
        var filteredRecords = VolatileVectorStoreCollectionSearchMapping.FilterRecords(internalOptions.Filter, this.GetCollectionDictionary().Values);

        // Compare each vector in the filtered results with the provided vector.
        var results = filteredRecords.Select<object, (object record, float score)?>((record) =>
        {
            var vectorObject = this._vectorResolver(vectorPropertyName!, record);
            if (vectorObject is not ReadOnlyMemory<float> dbVector)
            {
                return null;
            }

            var score = VolatileVectorStoreCollectionSearchMapping.CompareVectors(floatVector.Span, dbVector.Span, vectorProperty.DistanceFunction);
            var convertedscore = VolatileVectorStoreCollectionSearchMapping.ConvertScore(score, vectorProperty.DistanceFunction);
            return (record, convertedscore);
        });

        // Get the non-null results, sort them appropriately for the selected distance function and return the requested page.
        var nonNullResults = results.Where(x => x.HasValue).Select(x => x!.Value);
        var sortedScoredResults = VolatileVectorStoreCollectionSearchMapping.ShouldSortDescending(vectorProperty.DistanceFunction) ?
            nonNullResults.OrderByDescending(x => x.score) :
            nonNullResults.OrderBy(x => x.score);

        foreach (var scoredResult in sortedScoredResults.Skip(internalOptions.Offset).Take(internalOptions.Limit))
        {
            yield return new VectorSearchResult<TRecord>((TRecord)scoredResult.record, scoredResult.score);
        }
    }

    /// <summary>
    /// Get the collection dictionary from the internal storage, throws if it does not exist.
    /// </summary>
    /// <returns>The retrieved collection dictionary.</returns>
    private ConcurrentDictionary<object, object> GetCollectionDictionary()
    {
        if (!this._internalCollection.TryGetValue(this._collectionName, out var collectionDictionary))
        {
            throw new VectorStoreOperationException($"Call to vector store failed. Collection '{this._collectionName}' does not exist.");
        }

        return collectionDictionary;
    }

    /// <summary>
    /// Pick / create a vector resolver that will read a vector from a record in the store based on the vector name.
    /// 1. If an override resolver is provided, use that.
    /// 2. If the record type is <see cref="VectorStoreGenericDataModel{TKey}"/> create a resolver that looks up the vector in its <see cref="VectorStoreGenericDataModel{TKey}.Vectors"/> dictionary.
    /// 3. Otherwise, create a resolver that assumes the vector is a property directly on the record and use the record definition to determine the name.
    /// </summary>
    /// <param name="overrideVectorResolver">The override vector resolver if one was provided.</param>
    /// <param name="vectorProperties">A dictionary of vector properties from the record definition.</param>
    /// <returns>The <see cref="VolatileVectorStoreVectorResolver"/>.</returns>
    private static VolatileVectorStoreVectorResolver CreateVectorResolver(VolatileVectorStoreVectorResolver? overrideVectorResolver, Dictionary<string, VectorStoreRecordVectorProperty> vectorProperties)
    {
        // Custom resolver.
        if (overrideVectorResolver is not null)
        {
            return overrideVectorResolver;
        }

        // Generic data model resolver.
        if (typeof(TRecord).IsGenericType && typeof(TRecord).GetGenericTypeDefinition() == typeof(VectorStoreGenericDataModel<>))
        {
            return (vectorName, record) =>
            {
                var genericDataModelRecord = (VectorStoreGenericDataModel<TKey>)record;
                var vectorsDictionary = genericDataModelRecord.Vectors;
                if (vectorsDictionary != null && vectorsDictionary.TryGetValue(vectorName, out var vector))
                {
                    return vector;
                }

                throw new InvalidOperationException($"The collection does not have a vector field named '{vectorName}', so vector search is not possible.");
            };
        }

        // Default resolver.
        var vectorPropertiesInfo = vectorProperties.Values
            .Select(x => x.DataModelPropertyName)
            .Select(x => typeof(TRecord).GetProperty(x) ?? throw new ArgumentException($"Vector property '{x}' was not found on {typeof(TRecord).Name}"))
            .ToDictionary(x => x.Name);

        return (vectorName, record) =>
        {
            if (vectorPropertiesInfo.TryGetValue(vectorName, out var vectorPropertyInfo))
            {
                return vectorPropertyInfo.GetValue(record);
            }

            throw new InvalidOperationException($"The collection does not have a vector field named '{vectorName}', so vector search is not possible.");
        };
    }

    /// <summary>
    /// Pick / create a key resolver that will read a key from a record in the store.
    /// 1. If an override resolver is provided, use that.
    /// 2. If the record type is <see cref="VectorStoreGenericDataModel{TKey}"/> create a resolver that reads the Key property from it.
    /// 3. Otherwise, create a resolver that assumes the key is a property directly on the record and use the record definition to determine the name.
    /// </summary>
    /// <param name="overrideKeyResolver">The override key resolver if one was provided.</param>
    /// <param name="keyProperty">They key property from the record definition.</param>
    /// <returns>The <see cref="VolatileVectorStoreKeyResolver"/>.</returns>
    private static VolatileVectorStoreKeyResolver CreateKeyResolver(VolatileVectorStoreKeyResolver? overrideKeyResolver, VectorStoreRecordKeyProperty keyProperty)
    {
        // Custom resolver.
        if (overrideKeyResolver is not null)
        {
            return overrideKeyResolver;
        }

        // Generic data model resolver.
        if (typeof(TRecord).IsGenericType && typeof(TRecord).GetGenericTypeDefinition() == typeof(VectorStoreGenericDataModel<>))
        {
            return (record) =>
            {
                var genericDataModelRecord = (VectorStoreGenericDataModel<TKey>)record;
                return genericDataModelRecord.Key;
            };
        }

        // Default resolver.
        var keyPropertyInfo = typeof(TRecord).GetProperty(keyProperty.DataModelPropertyName) ?? throw new ArgumentException($"Key property {keyProperty.DataModelPropertyName} not found on {typeof(TRecord).Name}");
        return keyPropertyInfo.GetValue;
    }
}
