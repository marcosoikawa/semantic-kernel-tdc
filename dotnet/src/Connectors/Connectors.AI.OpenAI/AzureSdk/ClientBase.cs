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
using Microsoft.SemanticKernel.Prompt;
using Microsoft.SemanticKernel.Text;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

/// <summary>
/// Base class for AI clients that provides common functionality for interacting with OpenAI services.
/// </summary>
public abstract class ClientBase
{
    private const int MaxResultsPerPrompt = 128;
    private const string NameProperty = "Name";
    private const string ArgumentsProperty = "Arguments";

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
    private static readonly Meter s_meter = new(typeof(ClientBase).Assembly.GetName().Name);

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of prompt tokens used.
    /// </summary>
    private static readonly Counter<int> s_promptTokensCounter =
        s_meter.CreateCounter<int>(
            name: "SK.Connectors.OpenAI.PromptTokens",
            description: "Number of prompt tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of completion tokens used.
    /// </summary>
    private static readonly Counter<int> s_completionTokensCounter =
        s_meter.CreateCounter<int>(
            name: "SK.Connectors.OpenAI.CompletionTokens",
            description: "Number of completion tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the total number of tokens used.
    /// </summary>
    private static readonly Counter<int> s_totalTokensCounter =
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
        var options = this.CreateCompletionsOptions(text, textRequestSettings);

        Response<Completions>? response = await RunRequestAsync<Response<Completions>?>(
            () => this.Client.GetCompletionsAsync(options, cancellationToken)).ConfigureAwait(false);

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

        var options = this.CreateCompletionsOptions(text, textRequestSettings);

        StreamingResponse<Completions>? response = await RunRequestAsync<StreamingResponse<Completions>>(
            () => this.Client.GetCompletionsStreamingAsync(options, cancellationToken)).ConfigureAwait(false);

        var cachedChoices = new Dictionary<int, List<Choice>>();
        var results = new List<TextStreamingResult>();

        await foreach (Completions completions in response)
        {
            foreach (var choice in completions.Choices)
            {
                if (!cachedChoices.ContainsKey(choice.Index))
                {
                    cachedChoices.Add(choice.Index, new());

                    var result = new TextStreamingResult(completions, cachedChoices[choice.Index]);
                    results.Add(result);
                    yield return result;
                }
                cachedChoices[choice.Index].Add(choice);
            }
        }
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

        var options = this.CreateChatCompletionsOptions(chatRequestSettings, chat);

        StreamingResponse<StreamingChatCompletionsUpdate>? response = await RunRequestAsync<StreamingResponse<StreamingChatCompletionsUpdate>>(
            () => this.Client.GetChatCompletionsStreamingAsync(options, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new SKException("Chat completions null response");
        }

        var cachedChoices = new Dictionary<int, List<StreamingChatCompletionsUpdate>>();
        var results = new List<ChatStreamingResult>();
        await foreach (StreamingChatCompletionsUpdate update in response)
        {
            // Stores the streaming updates by index in 
            if (!cachedChoices.ContainsKey(update.ChoiceIndex ?? 0))
            {
                cachedChoices.Add(update.ChoiceIndex ?? 0, new());

                var result = new ChatStreamingResult(cachedChoices[update.ChoiceIndex ?? 0]);
                results.Add(result);
                yield return result;
            }
            cachedChoices[update.ChoiceIndex ?? 0].Add(update);
        }

        foreach (var result in results)
        {
            result.EndOfStream();
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
            var options = new EmbeddingsOptions(this.ModelId, new[] { text });

            Response<Embeddings>? response = await RunRequestAsync<Response<Embeddings>?>(
                () => this.Client.GetEmbeddingsAsync(options, cancellationToken)).ConfigureAwait(false);

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

        var chatOptions = this.CreateChatCompletionsOptions(chatRequestSettings, chat);

        Response<ChatCompletions>? response = await RunRequestAsync<Response<ChatCompletions>?>(
            () => this.Client.GetChatCompletionsAsync(chatOptions, cancellationToken)).ConfigureAwait(false);

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
    /// Create a new empty chat instance
    /// </summary>
    /// <param name="instructions">Optional chat instructions for the AI service</param>
    /// <returns>Chat object</returns>
    private protected static OpenAIChatHistory InternalCreateNewChat(string? instructions = null)
    {
        return new OpenAIChatHistory(instructions);
    }

    /// <summary>
    /// Create a new chat instance based on chat history.
    /// </summary>
    /// <param name="chatHistory">Instance of <see cref="ChatHistory"/>.</param>
    /// <returns>Chat object</returns>
    private protected static OpenAIChatHistory InternalCreateNewChat(ChatHistory chatHistory)
    {
        return new OpenAIChatHistory(chatHistory);
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
        await foreach (var chatCompletionStreamingResult in chatCompletionStreamingResults)
        {
            yield return (ITextStreamingResult)chatCompletionStreamingResult;
        }
    }

    private static OpenAIChatHistory PrepareChatHistory(string text, AIRequestSettings? requestSettings, out OpenAIRequestSettings settings)
    {
        settings = OpenAIRequestSettings.FromRequestSettings(requestSettings);

        if (XmlPromptParser.TryParse(text, out var nodes) && ChatPromptParser.TryParse(nodes, out var chatHistory))
        {
            return InternalCreateNewChat(chatHistory);
        }

        var chat = InternalCreateNewChat(settings.ChatSystemPrompt);
        chat.AddUserMessage(text);
        return chat;
    }

    private CompletionsOptions CreateCompletionsOptions(string text, OpenAIRequestSettings requestSettings)
    {
        if (requestSettings.ResultsPerPrompt is < 1 or > MaxResultsPerPrompt)
        {
            throw new ArgumentOutOfRangeException($"{nameof(requestSettings)}.{nameof(requestSettings.ResultsPerPrompt)}", requestSettings.ResultsPerPrompt, $"The value must be in range between 1 and {MaxResultsPerPrompt}, inclusive.");
        }

        var options = new CompletionsOptions
        {
            DeploymentName = this.ModelId,
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

    private ChatCompletionsOptions CreateChatCompletionsOptions(OpenAIRequestSettings requestSettings, IEnumerable<ChatMessageBase> chatHistory)
    {
        if (requestSettings.ResultsPerPrompt is < 1 or > MaxResultsPerPrompt)
        {
            throw new ArgumentOutOfRangeException($"{nameof(requestSettings)}.{nameof(requestSettings.ResultsPerPrompt)}", requestSettings.ResultsPerPrompt, $"The value must be in range between 1 and {MaxResultsPerPrompt}, inclusive.");
        }

        var options = new ChatCompletionsOptions
        {
            DeploymentName = this.ModelId,
            MaxTokens = requestSettings.MaxTokens,
            Temperature = (float?)requestSettings.Temperature,
            NucleusSamplingFactor = (float?)requestSettings.TopP,
            FrequencyPenalty = (float?)requestSettings.FrequencyPenalty,
            PresencePenalty = (float?)requestSettings.PresencePenalty,
            ChoiceCount = requestSettings.ResultsPerPrompt,
        };

        if (requestSettings.Functions is not null)
        {
            if (requestSettings.FunctionCall == OpenAIRequestSettings.FunctionCallAuto)
            {
                options.FunctionCall = FunctionDefinition.Auto;
                options.Functions = requestSettings.Functions.Select(f => f.ToFunctionDefinition()).ToList();
            }
            else if (requestSettings.FunctionCall != OpenAIRequestSettings.FunctionCallNone
                    && !requestSettings.FunctionCall.IsNullOrEmpty())
            {
                var filteredFunctions = requestSettings.Functions
                    .Where(f => f.FullyQualifiedName.Equals(requestSettings.FunctionCall, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                OpenAIFunction? function = filteredFunctions.FirstOrDefault();
                if (function is not null)
                {
                    options.FunctionCall = function.ToFunctionDefinition();
                    options.Functions = filteredFunctions.Select(f => f.ToFunctionDefinition()).ToList();
                }
            }
        }

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
            var azureMessage = new ChatMessage(new ChatRole(message.Role.Label), message.Content);

            if (message.AdditionalProperties?.TryGetValue(NameProperty, out string? name) is true)
            {
                azureMessage.Name = name;

                if (message.AdditionalProperties?.TryGetValue(ArgumentsProperty, out string? arguments) is true)
                {
                    azureMessage.FunctionCall = new FunctionCall(name, arguments);
                }
            }

            options.Messages.Add(azureMessage);
        }

        return options;
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
