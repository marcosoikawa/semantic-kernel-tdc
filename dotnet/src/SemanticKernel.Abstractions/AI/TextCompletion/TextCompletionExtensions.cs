﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.AI.TextCompletion;

/// <summary>
/// Class sponsor that holds extension methods for ITextCompletion interface.
/// </summary>
public static class TextCompletionExtensions
{
    /// <summary>
    /// Creates a completion for the prompt and settings.
    /// </summary>
    /// <param name="textCompletion">Target interface to extend</param>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <remarks>This extension does not support multiple prompt results (Only the first will be returned)</remarks>
    /// <returns>Text generated by the remote model</returns>
    public static async Task<string> CompleteAsync(this ITextCompletion textCompletion,
        string text,
        PromptExecutionSettings? requestSettings = null,
        CancellationToken cancellationToken = default)
    {
        var completions = await textCompletion.GetCompletionsAsync(text, requestSettings, cancellationToken).ConfigureAwait(false);
        var firstResult = completions[0];

        return await firstResult.GetCompletionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get streaming completion results for the prompt and settings.
    /// </summary>
    /// <param name="textCompletion">Target text completion</param>
    /// <param name="input">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming result updates generated by the remote model</returns>
    public static IAsyncEnumerable<StreamingContent> GetStreamingContentAsync(
        this ITextCompletion textCompletion,
        string input,
        PromptExecutionSettings? requestSettings = null,
        CancellationToken cancellationToken = default)
        => textCompletion.GetStreamingContentAsync<StreamingContent>(input, requestSettings, cancellationToken);
}
