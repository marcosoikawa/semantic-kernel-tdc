﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.AI.TextCompletion;

/// <summary>
/// Interface for text completion services
/// </summary>
public interface ITextCompletion : IAIService
{
    /// <summary>
    /// Get completion results for the prompt and settings.
    /// </summary>
    /// <param name="prompt">The raw prompt input.</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different completions results generated by the remote model</returns>
    Task<IReadOnlyList<TextContent>> GetTextContentsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get streaming results for the prompt using the specified request settings.
    /// Each modality may support for different types of streaming contents.
    /// </summary>
    /// <remarks>
    /// Usage of this method with value types may be more efficient if the connector supports it.
    /// </remarks>
    /// <exception cref="NotSupportedException">Throws if the specified type is not the same or fail to cast</exception>
    /// <param name="prompt">The prompt to complete.</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming string updates generated by the remote model</returns>
    IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);

    #region Obsolete
    /// <summary>
    /// Get completion results for the prompt and settings.
    /// </summary>
    /// <param name="prompt">The prompt to complete.</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different completions results generated by the remote model</returns>
    [Obsolete("IChatCompletionV2")]
    Task<IReadOnlyList<ITextResult>> GetCompletionsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get streaming results for the prompt using the specified request settings.
    /// Each modality may support for different types of streaming contents.
    /// </summary>
    /// <remarks>
    /// Usage of this method with value types may be more efficient if the connector supports it.
    /// </remarks>
    /// <exception cref="NotSupportedException">Throws if the specified type is not the same or fail to cast</exception>
    /// <param name="prompt">The prompt to complete.</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming string updates generated by the remote model</returns>
    [Obsolete("IChatCompletionV2")]
    IAsyncEnumerable<T> GetStreamingContentAsync<T>(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);
    #endregion
}
