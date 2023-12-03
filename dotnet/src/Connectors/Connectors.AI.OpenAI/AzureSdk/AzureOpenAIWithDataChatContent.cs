﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletionWithData;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI;

/// <summary>
/// OpenAI specialized with data chat content
/// </summary>
[Experimental("SKEXP0010")]
public sealed class AzureOpenAIWithDataChatContent : ChatContent
{
    /// <summary>
    /// Content from data source, including citations.
    /// For more information see <see href="https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/use-your-data#conversation-history-for-better-results"/>.
    /// </summary>
    public string? ToolContent { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIChatContent"/> class.
    /// </summary>
    /// <param name="chatChoice">Azure Chat With Data Choice</param>
    /// <param name="metadata">Additional metadata</param>
    internal AzureOpenAIWithDataChatContent(ChatWithDataChoice chatChoice, IDictionary<string, object?>? metadata = null)
        : base(default, string.Empty, chatChoice, System.Text.Encoding.UTF8, metadata ?? new Dictionary<string, object?>())
    {
        // An assistant message content must be present, otherwise the chat is not valid.
        var chatMessage = chatChoice.Messages.First(m => string.Equals(m.Role, AuthorRole.Assistant.Label, System.StringComparison.OrdinalIgnoreCase));

        this.Content = chatMessage.Content;
        this.Role = new AuthorRole(chatMessage.Role);

        this.ToolContent = chatChoice.Messages.FirstOrDefault(message => message.Role.Equals(AuthorRole.Tool.Label, StringComparison.OrdinalIgnoreCase))?.Content;
        this.Metadata!.Add(nameof(this.ToolContent), this.ToolContent);
    }
}
