﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletionWithData;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

/// <summary>
/// Streaming chat result update.
/// </summary>
public sealed class StreamingChatWithDataResultChunk : StreamingResultChunk
{
    /// <inheritdoc/>
    public override string Type => "openai_chat_message_update";

    /// <inheritdoc/>
    public override int ChoiceIndex { get; }

    /// <summary>
    /// Chat message abstraction
    /// </summary>
    public SemanticKernel.AI.ChatCompletion.ChatMessage ChatMessage { get; }

    /// <summary>
    /// Create a new instance of the <see cref="StreamingChatResultChunk"/> class.
    /// </summary>
    /// <param name="choice">Azure message update representation from WithData apis</param>
    /// <param name="resultIndex">Index of the choice</param>
    internal StreamingChatWithDataResultChunk(ChatWithDataStreamingChoice choice, int resultIndex) : base(choice)
    {
        this.ChoiceIndex = resultIndex;
        var message = choice.Messages.FirstOrDefault(this.IsValidMessage);

        this.ChatMessage = new AzureOpenAIChatMessage(AuthorRole.Assistant.Label, message?.Delta?.Content ?? string.Empty);
    }

    /// <inheritdoc/>
    public override byte[] ToByteArray()
    {
        return Encoding.UTF8.GetBytes(this.ToString());
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }

    private bool IsValidMessage(ChatWithDataStreamingMessage message)
    {
        return !message.EndTurn &&
            (message.Delta.Role is null || !message.Delta.Role.Equals(AuthorRole.Tool.Label, StringComparison.Ordinal));
    }
}
