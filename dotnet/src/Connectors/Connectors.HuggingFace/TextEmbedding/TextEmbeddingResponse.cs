﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.Text;

namespace Microsoft.SemanticKernel.Connectors.HuggingFace;

/// <summary>
/// Represents the response from the Hugging Face text embedding API.
/// </summary>
public sealed class TextEmbeddingResponse2
{
    /// <summary>
    /// Represents the embedding vector for a given text.
    /// </summary>
    public sealed class EmbeddingVector
    {
        /// <summary>
        /// The embedding vector as a ReadOnlyMemory of float values.
        /// </summary>
        [JsonPropertyName("embedding")]
        [JsonConverter(typeof(ReadOnlyMemoryConverter))]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }

    /// <summary>
    /// List of embeddings.
    /// </summary>
    [JsonPropertyName("data")]
    public IList<EmbeddingVector>? Embeddings { get; set; }
}

/// <summary>
/// Represents the response from the Hugging Face text embedding API.
/// </summary>
public sealed class TextEmbeddingResponse : List<List<List<ReadOnlyMemory<float>>>>
{
}
