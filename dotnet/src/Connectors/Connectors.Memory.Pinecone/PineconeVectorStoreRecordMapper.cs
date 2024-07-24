﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel.Data;
using Pinecone;

namespace Microsoft.SemanticKernel.Connectors.Pinecone;

/// <summary>
/// Mapper between a Pinecone record and the consumer data model that uses json as an intermediary to allow supporting a wide range of models.
/// </summary>
/// <typeparam name="TRecord">The consumer data model to map to or from.</typeparam>
internal sealed class PineconeVectorStoreRecordMapper<TRecord> : IVectorStoreRecordMapper<TRecord, Vector>
    where TRecord : class
{
    /// <summary>A set of types that a key on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedKeyTypes = [typeof(string)];

    /// <summary>A set of types that data properties on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedDataTypes =
    [
        typeof(bool),
        typeof(bool?),
        typeof(string),
        typeof(int),
        typeof(int?),
        typeof(long),
        typeof(long?),
        typeof(float),
        typeof(float?),
        typeof(double),
        typeof(double?),
        typeof(decimal),
        typeof(decimal?),
    ];

    /// <summary>A set of types that enumerable data properties on the provided model may use as their element types.</summary>
    private static readonly HashSet<Type> s_supportedEnumerableDataElementTypes =
    [
        typeof(string)
    ];

    /// <summary>A set of types that vectors on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedVectorTypes =
    [
        typeof(ReadOnlyMemory<float>),
        typeof(ReadOnlyMemory<float>?),
    ];

    private readonly PropertyInfo _keyPropertyInfo;

    private readonly List<PropertyInfo> _dataPropertiesInfo;

    private readonly PropertyInfo _vectorPropertyInfo;

    private readonly Dictionary<string, string> _storagePropertyNames = [];

    private readonly Dictionary<string, string> _jsonPropertyNames = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="PineconeVectorStoreRecordMapper{TDataModel}"/> class.
    /// </summary>
    /// <param name="keyProperty">A property info object that points at the key property for the current model, allowing easy reading and writing of this property.</param>
    /// <param name="dataProperties">A list of property info objects that point at the data properties in the current model, and allows easy reading and writing of these properties.</param>
    /// <param name="vectorProperties">A list of property info objects that point at the vector properties in the current model, and allows easy reading and writing of these properties.</param>
    /// <param name="storagePropertyNames">A dictionary that maps from a property name to the configured name that should be used when storing it.</param>
    public PineconeVectorStoreRecordMapper(PropertyInfo keyProperty, List<PropertyInfo> dataProperties, List<PropertyInfo> vectorProperties, Dictionary<string, string> storagePropertyNames)
    {
        Verify.True(vectorProperties.Count == 1, "There should be exactly one vector property in the data model.");

        VectorStoreRecordPropertyReader.VerifyPropertyTypes([keyProperty], s_supportedKeyTypes, "Key");
        VectorStoreRecordPropertyReader.VerifyPropertyTypes(dataProperties, s_supportedDataTypes, "Data", s_supportedEnumerableDataElementTypes);
        VectorStoreRecordPropertyReader.VerifyPropertyTypes(vectorProperties, s_supportedVectorTypes, "Vector");

        this._keyPropertyInfo = keyProperty;
        this._dataPropertiesInfo = dataProperties;
        this._vectorPropertyInfo = vectorProperties[0];
        this._storagePropertyNames = storagePropertyNames;

        foreach (var property in dataProperties.Concat(vectorProperties).Concat([keyProperty]))
        {
            this._jsonPropertyNames[property.Name] = VectorStoreRecordPropertyReader.GetJsonPropertyName(JsonSerializerOptions.Default, property);
        }
    }

    /// <inheritdoc />
    public Vector MapFromDataToStorageModel(TRecord dataModel)
    {
        var keyObject = this._keyPropertyInfo.GetValue(dataModel);
        if (keyObject is null)
        {
            throw new VectorStoreRecordMappingException($"Key property {this._keyPropertyInfo.Name} on provided record of type {typeof(TRecord).FullName} may not be null.");
        }

        var metadata = new MetadataMap();
        foreach (var dataPropertyInfo in this._dataPropertiesInfo)
        {
            var propertyName = this._storagePropertyNames[dataPropertyInfo.Name];
            var propertyValue = dataPropertyInfo.GetValue(dataModel);
            if (propertyValue != null)
            {
                metadata[propertyName] = ConvertToMetadataValue(propertyValue);
            }
        }

        var valuesObject = this._vectorPropertyInfo.GetValue(dataModel);
        if (valuesObject is not ReadOnlyMemory<float> values)
        {
            throw new VectorStoreRecordMappingException($"Vector property {this._vectorPropertyInfo.Name} on provided record of type {typeof(TRecord).FullName} may not be null.");
        }

        // TODO: what about sparse values?
        var result = new Vector
        {
            Id = (string)keyObject,
            Values = values.ToArray(),
            Metadata = metadata,
            SparseValues = null
        };

        return result;
    }

    /// <inheritdoc />
    public TRecord MapFromStorageToDataModel(Vector storageModel, StorageToDataModelMapperOptions options)
    {
        var keyJsonName = this._jsonPropertyNames[this._keyPropertyInfo.Name];
        var outputJsonObject = new JsonObject
        {
            { keyJsonName, JsonValue.Create(storageModel.Id) },
        };

        if (options?.IncludeVectors is true)
        {
            var propertyName = this._storagePropertyNames[this._vectorPropertyInfo.Name];
            var jsonName = this._jsonPropertyNames[this._vectorPropertyInfo.Name];
            outputJsonObject.Add(jsonName, new JsonArray(storageModel.Values.Select(x => JsonValue.Create(x)).ToArray()));
        }

        if (storageModel.Metadata != null)
        {
            foreach (var dataProperty in this._dataPropertiesInfo)
            {
                var propertyName = this._storagePropertyNames[dataProperty.Name];
                var jsonName = this._jsonPropertyNames[dataProperty.Name];

                if (storageModel.Metadata.TryGetValue(propertyName, out var value))
                {
                    outputJsonObject.Add(jsonName, ConvertFromMetadataValueToJsonNode(value));
                }
            }
        }

        return outputJsonObject.Deserialize<TRecord>()!;
    }

    private static JsonNode? ConvertFromMetadataValueToJsonNode(MetadataValue metadataValue)
        => metadataValue.Inner switch
        {
            null => null,
            bool boolValue => JsonValue.Create(boolValue),
            string stringValue => JsonValue.Create(stringValue),
            int intValue => JsonValue.Create(intValue),
            long longValue => JsonValue.Create(longValue),
            float floatValue => JsonValue.Create(floatValue),
            double doubleValue => JsonValue.Create(doubleValue),
            decimal decimalValue => JsonValue.Create(decimalValue),
            MetadataValue[] array => new JsonArray(array.Select(ConvertFromMetadataValueToJsonNode).ToArray()),
            List<MetadataValue> list => new JsonArray(list.Select(ConvertFromMetadataValueToJsonNode).ToArray()),
            _ => throw new VectorStoreRecordMappingException($"Unsupported metadata type: '{metadataValue.Inner?.GetType().FullName}'."),
        };

    // TODO: take advantage of MetadataValue.TryCreate once we upgrade the version of Pinecone.NET
    private static MetadataValue ConvertToMetadataValue(object? sourceValue)
        => sourceValue switch
        {
            bool boolValue => boolValue,
            string stringValue => stringValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            decimal decimalValue => decimalValue,
            string[] stringArray => stringArray,
            List<string> stringList => stringList,
            IEnumerable<string> stringEnumerable => stringEnumerable.ToArray(),
            _ => throw new VectorStoreRecordMappingException($"Unsupported source value type '{sourceValue?.GetType().FullName}'.")
        };
}
