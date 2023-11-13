﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Experimental.Assistants.Models;

/// <summary>
/// Wrapper for parameter map.
/// </summary>
internal class OpenAIParameters
{
    /// <summary>
    /// Empty parameter set.
    /// </summary>
    public static readonly OpenAIParameters Empty = new();

    /// <summary>
    /// Always "object"
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    /// <summary>
    /// Set of parameters.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, OpenAIParameter> Properties { get; set; } = new Dictionary<string, OpenAIParameter>();

    /// <summary>
    /// Set of parameters.
    /// </summary>
    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new List<string>();
}

/// <summary>
/// Wrapper for parameter definition.
/// </summary>
internal class OpenAIParameter
{
    /// <summary>
    /// The parameter type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    /// <summary>
    /// The parameter description.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}
