﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.AI.HuggingFace.HttpSchema;

/// <summary>
/// HTTP Schema for embedding response.
/// </summary>
public sealed class EmbeddingResponse
{
    /// <summary>
    /// Model containing embedding.
    /// </summary>
    public sealed class EmbeddingVector
    {
        [JsonPropertyName("embedding")]
        public IList<float>? Embedding { get; set; }
    }

    /// <summary>
    /// List of embeddings.
    /// </summary>
    [JsonPropertyName("data")]
    public IList<EmbeddingVector>? Embeddings { get; set; }
}
