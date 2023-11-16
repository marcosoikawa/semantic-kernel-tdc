﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.AI.ChatCompletion;

/// <summary>
/// Chat message abstraction
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Role of the author of the message
    /// </summary>
    public AuthorRole Role { get; set; }

    /// <summary>
    /// Content of the message
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Dictionary for any additional message properties
    /// </summary>
    public IDictionary<string, string>? AdditionalProperties { get; set; }

    /// <summary>
    /// Creates a new instance of the <see cref="ChatMessage"/> class
    /// </summary>
    /// <param name="role">Role of the author of the message</param>
    /// <param name="content">Content of the message</param>
    /// <param name="additionalProperties">Dictionary for any additional message properties</param>
    [JsonConstructor]
    public ChatMessage(AuthorRole role, string content, IDictionary<string, string>? additionalProperties = null)
    {
        this.Role = role;
        this.Content = content;
        this.AdditionalProperties = additionalProperties;
    }
}
