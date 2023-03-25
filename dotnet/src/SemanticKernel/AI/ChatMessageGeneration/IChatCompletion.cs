﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.AI.ChatMessageGeneration;

public interface IChatCompletion
{
    /// <summary>
    /// Generate a new chat message
    /// </summary>
    /// <param name="chat">Chat history</param>
    /// <param name="requestSettings">AI request settings</param>
    /// <param name="cancellationToken">Async cancellation token</param>
    /// <returns></returns>
    public Task<string> GenerateMessageAsync(
        ChatHistory chat,
        ChatRequestSettings requestSettings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new empty chat instance
    /// </summary>
    /// <param name="instructions">Optional chat instructions for the backend</param>
    /// <returns>Chat object</returns>
    public ChatHistory CreateNewChat(string instructions = "");
}
