﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Skills.Memory.Qdrant.Http.ApiSchema;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes: Used for Json Deserialization
internal class GetVectorsResponse : QdrantResponse
{
    internal class Record
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("payload")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Payload { get; set; }

        [JsonPropertyName("vector")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IEnumerable<float>? Vector { get; set; }

        [JsonConstructor]
        public Record(string id, Dictionary<string, object>? payload, IEnumerable<float>? vector)
        {
            this.Id = id;
            this.Payload = payload;
            this.Vector = vector;
        }
    }

    /// <summary>
    /// Array of vectors and their associated metadata
    /// </summary>
    [JsonPropertyName("result")]
    public IEnumerable<Record> Result { get; set; } = new List<Record>();
}
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
