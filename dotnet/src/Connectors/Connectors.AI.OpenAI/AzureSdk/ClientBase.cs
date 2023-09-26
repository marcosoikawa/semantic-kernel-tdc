﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Text;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

/// <summary>
/// Base class for AI clients that provides common functionality for interacting with OpenAI services.
/// </summary>
public abstract class ClientBase
{
    private const int MaxResultsPerPrompt = 128;

    // Prevent external inheritors
    private protected ClientBase(ILoggerFactory? loggerFactory = null)
    {
        this.Logger = loggerFactory is not null ? loggerFactory.CreateLogger(this.GetType()) : NullLogger.Instance;
    }

    /// <summary>
    /// Model Id or Deployment Name
    /// </summary>
    private protected string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI / Azure OpenAI Client
    /// </summary>
    private protected abstract OpenAIClient Client { get; }

    /// <summary>
    /// Logger instance
    /// </summary>
    private protected ILogger Logger { get; set; }

    /// <summary>
    /// Instance of <see cref="Meter"/> for metrics.
    /// </summary>
    private static Meter s_meter = new(typeof(ClientBase).Assembly.GetName().Name);

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of prompt tokens used.
    /// </summary>
    private static Counter<int> s_promptTokensCounter =
        s_meter.CreateCounter<int>(
            name: "SK.Connectors.OpenAI.PromptTokens",
            description: "Number of prompt tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of completion tokens used.
    /// </summary>
    private static Counter<int> s_completionTokensCounter =
        s_meter.CreateCounter<int>(
            name: "SK.Connectors.OpenAI.CompletionTokens",
            description: "Number of completion tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the total number of tokens used.
    /// </summary>
    private static Counter<int> s_totalTokensCounter =
        s_meter.CreateCounter<int>(
            name: "SK.Connectors.OpenAI.TotalTokens",
            description: "Total number of tokens used");

    /// <summary>
    /// Creates completions for the prompt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Completions generated by the remote model</returns>
    private protected async Task<IReadOnlyList<ITextResult>> InternalGetTextResultsAsync(
        string text,
        AIRequestSettings? requestSettings,
        CancellationToken cancellationToken = default)
    {
        OpenAIRequestSettings textRequestSettings = OpenAIRequestSettings.FromRequestSettings(requestSettings, OpenAIRequestSettings.DefaultTextMaxTokens);

        ValidateMaxTokens(textRequestSettings.MaxTokens);
        var options = CreateCompletionsOptions(text, textRequestSettings);

        Response<Completions>? response = await RunRequestAsync<Response<Completions>?>(
            () => this.Client.GetCompletionsAsync(this.ModelId, options, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new SKException("Text completions null response");
        }

        var responseData = response.Value;

        if (responseData.Choices.Count == 0)
        {
            throw new SKException("Text completions not found");
        }

        this.CaptureUsageDetails(responseData.Usage);

        return responseData.Choices.Select(choice => new TextResult(responseData, choice)).ToList();
    }

    /// <summary>
    /// Creates completions streams for the prompt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Stream the completions generated by the remote model</returns>
    private protected async IAsyncEnumerable<TextStreamingResult> InternalGetTextStreamingResultsAsync(
        string text,
        AIRequestSettings? requestSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        OpenAIRequestSettings textRequestSettings = OpenAIRequestSettings.FromRequestSettings(requestSettings, OpenAIRequestSettings.DefaultTextMaxTokens);

        ValidateMaxTokens(textRequestSettings.MaxTokens);

        var options = CreateCompletionsOptions(text, textRequestSettings);

        Response<StreamingCompletions>? response = await RunRequestAsync<Response<StreamingCompletions>>(
            () => this.Client.GetCompletionsStreamingAsync(this.ModelId, options, cancellationToken)).ConfigureAwait(false);

        using StreamingCompletions streamingChatCompletions = response.Value;
        await foreach (StreamingChoice choice in streamingChatCompletions.GetChoicesStreaming(cancellationToken))
        {
            yield return new TextStreamingResult(streamingChatCompletions, choice);
        }
    }

    /// <summary>
    /// Generates an embedding from the given <paramref name="data"/>.
    /// </summary>
    /// <param name="data">List of strings to generate embeddings for</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of embeddings</returns>
    private protected async Task<IList<ReadOnlyMemory<float>>> InternalGetEmbeddingsAsync(
        IList<string> data,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ReadOnlyMemory<float>>(data.Count);
        foreach (string text in data)
        {
            var options = new EmbeddingsOptions(text);

            Response<Embeddings>? response = await RunRequestAsync<Response<Embeddings>?>(
                () => this.Client.GetEmbeddingsAsync(this.ModelId, options, cancellationToken)).ConfigureAwait(false);

            if (response is null)
            {
                throw new SKException("Text embedding null response");
            }

            if (response.Value.Data.Count == 0)
            {
                throw new SKException("Text embedding not found");
            }

            result.Add(response.Value.Data[0].Embedding.ToArray());
        }

        return result;
    }

    /// <summary>
    /// Generate a new chat message
    /// </summary>
    /// <param name="chat">Chat history</param>
    /// <param name="requestSettings">AI request settings</param>
    /// <param name="cancellationToken">Async cancellation token</param>
    /// <returns>Generated chat message in string format</returns>
    private protected async Task<IReadOnlyList<IChatResult>> InternalGetChatResultsAsync(
        ChatHistory chat,
        AIRequestSettings? requestSettings,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chat);

        OpenAIRequestSettings chatRequestSettings = OpenAIRequestSettings.FromRequestSettings(requestSettings);

        ValidateMaxTokens(chatRequestSettings.MaxTokens);

        var chatOptions = CreateChatCompletionsOptions(chatRequestSettings, chat);

        Response<ChatCompletions>? response = await RunRequestAsync<Response<ChatCompletions>?>(
            () => this.Client.GetChatCompletionsAsync(this.ModelId, chatOptions, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new SKException("Chat completions null response");
        }

        var responseData = response.Value;

        if (responseData.Choices.Count == 0)
        {
            throw new SKException("Chat completions not found");
        }

        this.CaptureUsageDetails(responseData.Usage);

        return responseData.Choices.Select(chatChoice => new ChatResult(responseData, chatChoice)).ToList();
    }

    /// <summary>
    /// Generate a new chat message stream
    /// </summary>
    /// <param name="chat">Chat history</param>
    /// <param name="requestSettings">AI request settings</param>
    /// <param name="cancellationToken">Async cancellation token</param>
    /// <returns>Streaming of generated chat message in string format</returns>
    private protected async IAsyncEnumerable<IChatStreamingResult> InternalGetChatStreamingResultsAsync(
        IEnumerable<ChatMessageBase> chat,
        AIRequestSettings? requestSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chat);

        OpenAIRequestSettings chatRequestSettings = OpenAIRequestSettings.FromRequestSettings(requestSettings);

        ValidateMaxTokens(chatRequestSettings.MaxTokens);

        var options = CreateChatCompletionsOptions(chatRequestSettings, chat);

        Response<StreamingChatCompletions>? response = await RunRequestAsync<Response<StreamingChatCompletions>>(
            () => this.Client.GetChatCompletionsStreamingAsync(this.ModelId, options, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new SKException("Chat completions null response");
        }

        using StreamingChatCompletions streamingChatCompletions = response.Value;
        await foreach (StreamingChatChoice choice in streamingChatCompletions.GetChoicesStreaming(cancellationToken).ConfigureAwait(false))
        {
            yield return new ChatStreamingResult(response.Value, choice);
        }
    }

    /// <summary>
    /// Create a new empty chat instance
    /// </summary>
    /// <param name="instructions">Optional chat instructions for the AI service</param>
    /// <returns>Chat object</returns>
    private protected static OpenAIChatHistory InternalCreateNewChat(string? instructions = null)
    {
        return new OpenAIChatHistory(instructions);
    }

    private protected async Task<IReadOnlyList<ITextResult>> InternalGetChatResultsAsTextAsync(
        string text,
        AIRequestSettings? requestSettings,
        CancellationToken cancellationToken = default)
    {
        ChatHistory chat = PrepareChatHistory(text, requestSettings, out OpenAIRequestSettings chatSettings);

        return (await this.InternalGetChatResultsAsync(chat, chatSettings, cancellationToken).ConfigureAwait(false))
            .OfType<ITextResult>()
            .ToList();
    }

    private protected async IAsyncEnumerable<ITextStreamingResult> InternalGetChatStreamingResultsAsTextAsync(
        string text,
        AIRequestSettings? requestSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatHistory chat = PrepareChatHistory(text, requestSettings, out OpenAIRequestSettings chatSettings);

        IAsyncEnumerable<IChatStreamingResult> chatCompletionStreamingResults = this.InternalGetChatStreamingResultsAsync(chat, chatSettings, cancellationToken);
        var resultCount = 0;
        await foreach (var chatCompletionStreamingResult in chatCompletionStreamingResults)
        {
            resultCount++;
            yield return (ITextStreamingResult)chatCompletionStreamingResult;

            if (resultCount >= chatSettings.ResultsPerPrompt)
            {
                break;
            }
        }
    }

    private static OpenAIChatHistory PrepareChatHistory(string text, AIRequestSettings? requestSettings, out OpenAIRequestSettings settings)
    {
        settings = OpenAIRequestSettings.FromRequestSettings(requestSettings);
        var chat = InternalCreateNewChat(settings.ChatSystemPrompt);
        chat.AddUserMessage(text);
        return chat;
    }

    private static CompletionsOptions CreateCompletionsOptions(string text, OpenAIRequestSettings requestSettings)
    {
        if (requestSettings.ResultsPerPrompt is < 1 or > MaxResultsPerPrompt)
        {
            throw new ArgumentOutOfRangeException($"{nameof(requestSettings)}.{nameof(requestSettings.ResultsPerPrompt)}", requestSettings.ResultsPerPrompt, $"The value must be in range between 1 and {MaxResultsPerPrompt}, inclusive.");
        }

        var options = new CompletionsOptions
        {
            Prompts = { text.NormalizeLineEndings() },
            MaxTokens = requestSettings.MaxTokens,
            Temperature = (float?)requestSettings.Temperature,
            NucleusSamplingFactor = (float?)requestSettings.TopP,
            FrequencyPenalty = (float?)requestSettings.FrequencyPenalty,
            PresencePenalty = (float?)requestSettings.PresencePenalty,
            Echo = false,
            ChoicesPerPrompt = requestSettings.ResultsPerPrompt,
            GenerationSampleCount = requestSettings.ResultsPerPrompt,
            LogProbabilityCount = null,
            User = null,
        };

        foreach (var keyValue in requestSettings.TokenSelectionBiases)
        {
            options.TokenSelectionBiases.Add(keyValue.Key, keyValue.Value);
        }

        if (requestSettings.StopSequences is { Count: > 0 })
        {
            foreach (var s in requestSettings.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }

        return options;
    }

    private static ChatCompletionsOptions CreateChatCompletionsOptions(OpenAIRequestSettings requestSettings, IEnumerable<ChatMessageBase> chatHistory)
    {
        if (requestSettings.ResultsPerPrompt is < 1 or > MaxResultsPerPrompt)
        {
            throw new ArgumentOutOfRangeException($"{nameof(requestSettings)}.{nameof(requestSettings.ResultsPerPrompt)}", requestSettings.ResultsPerPrompt, $"The value must be in range between 1 and {MaxResultsPerPrompt}, inclusive.");
        }

        var options = new ChatCompletionsOptions
        {
            MaxTokens = requestSettings.MaxTokens,
            Temperature = (float?)requestSettings.Temperature,
            NucleusSamplingFactor = (float?)requestSettings.TopP,
            FrequencyPenalty = (float?)requestSettings.FrequencyPenalty,
            PresencePenalty = (float?)requestSettings.PresencePenalty,
            ChoiceCount = requestSettings.ResultsPerPrompt
        };

        foreach (var keyValue in requestSettings.TokenSelectionBiases)
        {
            options.TokenSelectionBiases.Add(keyValue.Key, keyValue.Value);
        }

        if (requestSettings.StopSequences is { Count: > 0 })
        {
            foreach (var s in requestSettings.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }

        foreach (var message in chatHistory)
        {
            var validRole = GetValidChatRole(message.Role);
            options.Messages.Add(new ChatMessage(validRole, message.Content));
        }

        return options;
    }

    private static ChatRole GetValidChatRole(AuthorRole role)
    {
        var validRole = new ChatRole(role.Label);

        if (validRole != ChatRole.User &&
            validRole != ChatRole.System &&
            validRole != ChatRole.Assistant)
        {
            throw new ArgumentException($"Invalid chat message author role: {role}");
        }

        return validRole;
    }

    private static void ValidateMaxTokens(int? maxTokens)
    {
        if (maxTokens.HasValue && maxTokens < 1)
        {
            throw new SKException($"MaxTokens {maxTokens} is not valid, the value must be greater than zero");
        }
    }

    private static async Task<T> RunRequestAsync<T>(Func<Task<T>> request)
    {
        try
        {
            return await request.Invoke().ConfigureAwait(false);
        }
        catch (RequestFailedException e)
        {
            throw e.ToHttpOperationException();
        }
    }

    /// <summary>
    /// Captures usage details, including token information.
    /// </summary>
    /// <param name="usage">Instance of <see cref="CompletionsUsage"/> with usage details.</param>
    private void CaptureUsageDetails(CompletionsUsage usage)
    {
        this.Logger.LogInformation(
            "Prompt tokens: {PromptTokens}. Completion tokens: {CompletionTokens}. Total tokens: {TotalTokens}.",
            usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);

        s_promptTokensCounter.Add(usage.PromptTokens);
        s_completionTokensCounter.Add(usage.CompletionTokens);
        s_totalTokensCounter.Add(usage.TotalTokens);
    }
}
