// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.Memory.Pinecone;

public static class PineconeDocumentExtensions
{
    public static PineconeDocument ToPineconeDocument(this MemoryRecord memoryRecord)
    {
        string key = !string.IsNullOrEmpty(memoryRecord.Key)
            ? memoryRecord.Key
            : memoryRecord.Metadata.Id;

        Dictionary<string, object> metadata = new()
        {
            ["document_Id"] = memoryRecord.Metadata.Id,
            ["text"] = memoryRecord.Metadata.Text,
            ["source_Id"] = memoryRecord.Metadata.ExternalSourceName,
            ["created_at"] = memoryRecord.HasTimestamp
                ? memoryRecord.Timestamp?.ToString("o") ?? DateTimeOffset.UtcNow.ToString("o")
                : DateTimeOffset.UtcNow.ToString("o")
        };

        if (!string.IsNullOrEmpty(memoryRecord.Metadata.AdditionalMetadata))
        {
            JsonSerializerOptions options = PineconeUtils.DefaultSerializerOptions;
            var additionalMetaData = JsonSerializer.Deserialize<Dictionary<string, object>>(memoryRecord.Metadata.AdditionalMetadata, options);
            if (additionalMetaData != null)
            {
                foreach (var item in additionalMetaData)
                {
                    metadata[item.Key] = item.Value;
                }
            }
        }

        return PineconeDocument.Create(key, memoryRecord.Embedding.Vector)
            .WithMetadata(metadata);
    }

    public static MemoryRecord ToMemoryRecord(this PineconeDocument pineconeDocument)
    {
        Embedding<float> embedding = new(pineconeDocument.Values);

        string additionalMetadataJson = pineconeDocument.GetSerializedMetadata();

        MemoryRecordMetadata memoryRecordMetadata = new(
            false,
            pineconeDocument.DocumentId ?? string.Empty,
            pineconeDocument.Text ?? string.Empty,
            string.Empty,
            pineconeDocument.SourceId ?? string.Empty,
            additionalMetadataJson
        );

        DateTimeOffset? timestamp = pineconeDocument.CreatedAt != null
            ? DateTimeOffset.Parse(pineconeDocument.CreatedAt, DateTimeFormatInfo.InvariantInfo)
            : null;

        return MemoryRecord.FromMetadata(memoryRecordMetadata, embedding, pineconeDocument.Id, timestamp);

    }
}
