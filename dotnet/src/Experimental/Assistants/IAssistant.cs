﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Experimental.Assistants;

/// <summary>
/// $$$
/// </summary>
public interface IAssistant
{
    /// <summary>
    /// Identifier, which can be referenced in API endpoints
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Always "assistant"
    /// </summary>
#pragma warning disable CA1720 // Identifier contains type name - We don't control the schema
#pragma warning disable CA1716 // Identifiers should not match keywords
    string Object { get; }
#pragma warning restore CA1716 // Identifiers should not match keywords
#pragma warning restore CA1720 // Identifier contains type name

    /// <summary>
    /// Unix timestamp (in seconds) for when the assistant was created
    /// </summary>
    long CreatedAt { get; }

    /// <summary>
    /// Name of the assistant
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The description of the assistant
    /// </summary>
    string Description { get; }

    /// <summary>
    /// ID of the model to use
    /// </summary>
    string Model { get; }

    /// <summary>
    /// The system instructions that the assistant uses
    /// </summary>
    string Instructions { get; }
}
