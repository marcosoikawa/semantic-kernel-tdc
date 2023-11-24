﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.AI.ChatCompletion;

/// <summary>
/// Interface for chat completion services
/// </summary>
public interface IChatCompletion : IAIService
{
    /// <summary>
    /// Create a new empty chat instance
    /// </summary>
    /// <param name="instructions">Optional chat instructions for the AI service</param>
    /// <returns>Chat object</returns>
    ChatHistory CreateNewChat(string? instructions = null);

    /// <summary>
    /// Get chat completion results for the prompt and settings.
    /// </summary>
    /// <param name="chat">The chat history context.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different chat results generated by the remote model</returns>
    Task<IReadOnlyList<IChatResult>> GetChatCompletionsAsync(
        ChatHistory chat,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get chat streaming completion results for the prompt and settings.
    /// </summary>
    /// <param name="chat">The chat history context.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>AsyncEnumerable list of different streaming chat results generated by the remote model</returns>
    IAsyncEnumerable<IChatStreamingResult> GetStreamingChatCompletionsAsync2(
        ChatHistory chat,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get streaming results for the prompt using the specified request settings.
    /// Each modality may support for different types of streaming result.
    /// </summary>
    /// <remarks>
    /// Usage of this method may be more efficient if the connector has a dedicated API to return this result without extra allocations for StreamingResultChunk abstraction.
    /// </remarks>
    /// <exception cref="NotSupportedException">Throws if the specified type is not the same or fail to cast</exception>
    /// <param name="prompt">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming string updates generated by the remote model</returns>
    IAsyncEnumerable<T> GetStreamingContentAsync<T>(
        string prompt,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);
}
