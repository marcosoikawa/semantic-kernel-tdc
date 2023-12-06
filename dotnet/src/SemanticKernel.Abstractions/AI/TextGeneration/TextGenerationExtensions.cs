﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI.ChatCompletion;

namespace Microsoft.SemanticKernel.AI.TextGeneration;

/// <summary>
/// Class sponsor that holds extension methods for <see cref ="ITextGenerationService" /> interface.
/// </summary>
public static class TextGenerationExtensions
{
    /// <summary>
    /// Get a single text completion result for the prompt and settings.
    /// </summary>
    /// <param name="textGenerationService">Text generation service</param>
    /// <param name="prompt">The standardized prompt input.</param>
    /// <param name="executionSettings">The AI execution settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different text results generated by the remote model</returns>
    public static async Task<TextContent> GetTextContentAsync(
        this ITextGenerationService textGenerationService,
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
        => (await textGenerationService.GetTextContentsAsync(prompt, executionSettings, kernel, cancellationToken).ConfigureAwait(false))
            .Single();

    /// <summary>
    /// Get a single text completion result for the standardized prompt and settings.
    /// </summary>
    /// <param name="textGenerationService">Text generation service</param>
    /// <param name="prompt">The standardized prompt input.</param>
    /// <param name="executionSettings">The AI execution settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different text results generated by the remote model</returns>
    internal static async Task<TextContent> GetTextContentWithDefaultParserAsync(
        this ITextGenerationService textGenerationService,
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        if (textGenerationService is IChatCompletionService chatCompletion)
        {
            // Try to parse the text as a chat history
            if (XmlPromptParser.TryParse(prompt!, out var nodes) && ChatPromptParser.TryParse(nodes, out var chatHistory))
            {
                var chatMessage = await chatCompletion.GetChatMessageContentAsync(chatHistory, executionSettings, kernel, cancellationToken).ConfigureAwait(false);
                return new TextContent(chatMessage.Content, chatMessage.ModelId, chatMessage.InnerContent, chatMessage.Encoding, chatMessage.Metadata);
            }

            // No TextPromptParser found...
        }

        //Otherwise, fallback to use the prompt as the chat system message
        return await textGenerationService.GetTextContentAsync(prompt, executionSettings, kernel, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get streaming results for the prompt using the specified execution settings.
    /// Each modality may support for different types of streaming contents.
    /// </summary>
    /// <remarks>
    /// Usage of this method with value types may be more efficient if the connector supports it.
    /// </remarks>
    /// <exception cref="NotSupportedException">Throws if the specified type is not the same or fail to cast</exception>
    /// <param name="textGenerationService">Text generation service</param>
    /// <param name="prompt">The standardized prompt to complete.</param>
    /// <param name="executionSettings">The AI execution settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming string updates generated by the remote model</returns>
    internal static async IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsWithDefaultParserAsync(
        this ITextGenerationService textGenerationService,
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (textGenerationService is IChatCompletionService chatCompletion)
        {
            // Try to parse the text as a chat history
            if (XmlPromptParser.TryParse(prompt!, out var nodes) && ChatPromptParser.TryParse(nodes, out var chatHistory))
            {
                await foreach (var chatMessage in chatCompletion.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken))
                {
                    yield return new StreamingTextContent(chatMessage.Content, chatMessage.ChoiceIndex, chatMessage.ModelId, chatMessage, chatMessage.Encoding, chatMessage.Metadata);
                }

                yield break;
            }
        }

        // When using against text completions, the prompt will be used as is.
        await foreach (var textChunk in textGenerationService.GetStreamingTextContentsAsync(prompt, executionSettings, kernel, cancellationToken))
        {
            yield return textChunk;
        }
    }
}
