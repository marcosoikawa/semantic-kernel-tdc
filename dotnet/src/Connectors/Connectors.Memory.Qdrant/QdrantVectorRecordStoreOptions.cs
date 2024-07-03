﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Data;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Options when creating a <see cref="QdrantVectorRecordStore{TRecord}"/>.
/// </summary>
public sealed class QdrantVectorRecordStoreOptions<TRecord>
    where TRecord : class
{
    /// <summary>
    /// Gets or sets the default collection name to use.
    /// If not provided here, the collection name will need to be provided for each operation or the operation will throw.
    /// </summary>
    public string? DefaultCollectionName { get; init; } = null;

    /// <summary>
    /// Gets or sets a value indicating whether the vectors in the store are named and multiple vectors are supported, or whether there is just a single unnamed vector per qdrant point.
    /// Defaults to single vector per point.
    /// </summary>
    public bool HasNamedVectors { get; set; } = false;

    /// <summary>
    /// Gets or sets the choice of mapper to use when converting between the data model and the qdrant point.
    /// </summary>
    public QdrantRecordMapperType MapperType { get; init; } = QdrantRecordMapperType.Default;

    /// <summary>
    /// Gets or sets an optional custom mapper to use when converting between the data model and the qdrant point.
    /// </summary>
    /// <remarks>
    /// Set <see cref="MapperType"/> to <see cref="QdrantRecordMapperType.QdrantPointStructCustomMapper"/> to use this mapper."/>
    /// </remarks>
    public IVectorStoreRecordMapper<TRecord, PointStruct>? PointStructCustomMapper { get; init; } = null;

    /// <summary>
    /// Gets or sets an optional record definition that defines the schema of the record type.
    /// </summary>
    /// <remarks>
    /// If not provided, the schema will be inferred from the record model class using reflection.
    /// In this case, the record model properties must be annotated with the appropriate attributes to indicate their usage.
    /// See <see cref="VectorStoreRecordKeyAttribute"/>, <see cref="VectorStoreRecordDataAttribute"/> and <see cref="VectorStoreRecordVectorAttribute"/>.
    /// </remarks>
    public VectorStoreRecordDefinition? VectorStoreRecordDefinition { get; init; } = null;
}
