﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletionWithData;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

/// <summary>
/// Streaming chat result update.
/// </summary>
public sealed class OpenAIStreamingChatWithDataContent : StreamingChatContent
{
    /// <inheritdoc/>
    public string? FunctionName { get; set; }

    /// <inheritdoc/>
    public string? FunctionArgument { get; set; }

    /// <summary>
    /// Create a new instance of the <see cref="OpenAIStreamingChatContent"/> class.
    /// </summary>
    /// <param name="choice">Azure message update representation from WithData apis</param>
    /// <param name="choiceIndex">Index of the choice</param>
    /// <param name="metadata">Additional metadata</param>
    internal OpenAIStreamingChatWithDataContent(ChatWithDataStreamingChoice choice, int choiceIndex, Dictionary<string, object> metadata) : base(AuthorRole.Assistant, null, choice, choiceIndex, metadata)
    {
        var message = choice.Messages.FirstOrDefault(this.IsValidMessage);
        var messageContent = message?.Delta?.Content;

        this.Content = messageContent;
    }

    /// <inheritdoc/>
    public override byte[] ToByteArray() => Encoding.UTF8.GetBytes(this.ToString());

    /// <inheritdoc/>
    public override string ToString() => this.Content ?? string.Empty;

    private bool IsValidMessage(ChatWithDataStreamingMessage message)
    {
        return !message.EndTurn &&
            (message.Delta.Role is null || !message.Delta.Role.Equals(AuthorRole.Tool.Label, StringComparison.Ordinal));
    }
}
