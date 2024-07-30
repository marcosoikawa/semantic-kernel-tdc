﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.SemanticKernel.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Microsoft.SemanticKernel.Connectors.AzureCosmosDBMongoDB;

internal sealed class AzureCosmosDBMongoDBVectorStoreRecordMapper<TRecord> : IVectorStoreRecordMapper<TRecord, BsonDocument>
    where TRecord : class
{
    /// <summary>A set of types that a key on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedKeyTypes =
    [
        typeof(string)
    ];

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

    /// <summary>A set of types that vectors on the provided model may have.</summary>
    private static readonly HashSet<Type> s_supportedVectorTypes =
    [
        typeof(ReadOnlyMemory<float>),
        typeof(ReadOnlyMemory<float>?),
        typeof(ReadOnlyMemory<double>),
        typeof(ReadOnlyMemory<double>?)
    ];

    /// <summary>A dictionary that maps from a property name to the storage name.</summary>
    private readonly Dictionary<string, string> _storagePropertyNames = [];

    /// <summary>A dictionary that maps from a storage property name to the data model property name.</summary>
    private readonly Dictionary<string, string> _reversedStoragePropertyNames = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureCosmosDBMongoDBVectorStoreRecordMapper{TRecord}"/> class.
    /// </summary>
    /// <param name="keyProperty">A property info object that points at the key property for the current model, allowing easy reading and writing of this property.</param>
    /// <param name="dataProperties">A list of property info objects that point at the data properties in the current model, and allows easy reading and writing of these properties.</param>
    /// <param name="vectorProperties">A list of property info objects that point at the vector properties in the current model, and allows easy reading and writing of these properties.</param>
    /// <param name="storagePropertyNames">A dictionary that maps from a property name to the configured name that should be used when storing it.</param>
    public AzureCosmosDBMongoDBVectorStoreRecordMapper(PropertyInfo keyProperty, List<PropertyInfo> dataProperties, List<PropertyInfo> vectorProperties, Dictionary<string, string> storagePropertyNames)
    {
        VectorStoreRecordPropertyReader.VerifyPropertyTypes([keyProperty], s_supportedKeyTypes, "Key");
        VectorStoreRecordPropertyReader.VerifyPropertyTypes(dataProperties, s_supportedDataTypes, "Data", supportEnumerable: true);
        VectorStoreRecordPropertyReader.VerifyPropertyTypes(vectorProperties, s_supportedVectorTypes, "Vector");

        this._storagePropertyNames = storagePropertyNames;

        // Use Mongo reserved key property name as storage key property name
        this._storagePropertyNames[keyProperty.Name] = AzureCosmosDBMongoDBConstants.MongoReservedKeyPropertyName;

        this._reversedStoragePropertyNames = ReverseDictionary(storagePropertyNames);
    }

    public BsonDocument MapFromDataToStorageModel(TRecord dataModel)
        => MapDocument(dataModel.ToBsonDocument(), this._storagePropertyNames);

    public TRecord MapFromStorageToDataModel(BsonDocument storageModel, StorageToDataModelMapperOptions options)
        => BsonSerializer.Deserialize<TRecord>(MapDocument(storageModel, this._reversedStoragePropertyNames));

    #region private

    private static BsonDocument MapDocument(BsonDocument document, Dictionary<string, string> propertyMappings)
    {
        var newDocument = new BsonDocument();

        foreach (var element in document)
        {
            BsonValue newValue;
            if (element.Value.IsBsonDocument)
            {
                newValue = MapDocument(element.Value.AsBsonDocument, propertyMappings);
            }
            else if (element.Value.IsBsonArray)
            {
                newValue = MapDocumentInArray(element.Value.AsBsonArray, propertyMappings);
            }
            else
            {
                newValue = element.Value;
            }

            if (propertyMappings.TryGetValue(element.Name, out var newName))
            {
                newDocument[newName] = newValue;
            }
            else
            {
                newDocument[element.Name] = newValue;
            }
        }

        return newDocument;
    }

    private static BsonArray MapDocumentInArray(BsonArray array, Dictionary<string, string> propertyMappings)
    {
        var newArray = new BsonArray();

        foreach (var item in array)
        {
            if (item.IsBsonDocument)
            {
                newArray.Add(MapDocument(item.AsBsonDocument, propertyMappings));
            }
            else if (item.IsBsonArray)
            {
                newArray.Add(MapDocumentInArray(item.AsBsonArray, propertyMappings));
            }
            else
            {
                newArray.Add(item);
            }
        }

        return newArray;
    }

    private static Dictionary<string, string> ReverseDictionary(Dictionary<string, string> original)
    {
        var reversed = new Dictionary<string, string>();

        foreach (var kvp in original)
        {
            reversed[kvp.Value] = kvp.Key;
        }

        return reversed;
    }

    #endregion
}
