﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using ChatCompletion;
using Diagnostics;
using Extensions.Logging;
using Extensions.Logging.Abstractions;
using FunctionCalling.Extensions;
using SemanticKernel.AI.ChatCompletion;
using SemanticKernel.AI.TextCompletion;
using Text;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly


public abstract class ClientBase
{
    private const int MaxResultsPerPrompt = 128;


    // Prevent external inheritors
    protected private ClientBase(ILoggerFactory? loggerFactory = null) => Logger = loggerFactory is not null ? loggerFactory.CreateLogger(GetType().Name) : NullLogger.Instance;

    /// <summary>
    /// Model Id or Deployment Name
    /// </summary>
    protected private string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI / Azure OpenAI Client
    /// </summary>
    protected private abstract OpenAIClient Client { get; }

    protected private List<FunctionDefinition> GlobalFunctions { get; } = new();

    protected private Dictionary<string, Func<string, CancellationToken, Task<string>>> Calls { get; set; } = new();

    /// <summary>
    /// Logger instance
    /// </summary>
    protected private ILogger Logger { get; set; }

    /// <summary>
    /// Instance of <see cref="Meter"/> for metrics.
    /// </summary>
    private static Meter s_meter = new(typeof(ClientBase).Assembly.GetName().Name);

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of prompt tokens used.
    /// </summary>
    private static Counter<int> s_promptTokensCounter =
        s_meter.CreateCounter<int>(
            "SK.Connectors.OpenAI.PromptTokens",
            description: "Number of prompt tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of completion tokens used.
    /// </summary>
    private static Counter<int> s_completionTokensCounter =
        s_meter.CreateCounter<int>(
            "SK.Connectors.OpenAI.CompletionTokens",
            description: "Number of completion tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the total number of tokens used.
    /// </summary>
    private static Counter<int> s_totalTokensCounter =
        s_meter.CreateCounter<int>(
            "SK.Connectors.OpenAI.TotalTokens",
            description: "Total number of tokens used");


    /// <summary>
    /// Creates completions for the prompt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Completions generated by the remote model</returns>
    protected private async Task<IReadOnlyList<ITextResult>> InternalGetTextResultsAsync(
        string text,
        CompleteRequestSettings requestSettings,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(requestSettings);

        ValidateMaxTokens(requestSettings.MaxTokens);
        var options = CreateCompletionsOptions(text, requestSettings);

        Response<Completions>? response = await RunRequestAsync<Response<Completions>?>(
            () => Client.GetCompletionsAsync(ModelId, options, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new SKException("Text completions null response");
        }

        var responseData = response.Value;

        if (responseData.Choices.Count == 0)
        {
            throw new SKException("Text completions not found");
        }

        CaptureUsageDetails(responseData.Usage);

        return responseData.Choices.Select(choice => new TextResult(responseData, choice)).ToList();
    }


    /// <summary>
    /// Creates completions streams for the prompt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Stream the completions generated by the remote model</returns>
    protected private async IAsyncEnumerable<TextStreamingResult> InternalGetTextStreamingResultsAsync(
        string text,
        CompleteRequestSettings requestSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(requestSettings);

        ValidateMaxTokens(requestSettings.MaxTokens);
        var options = CreateCompletionsOptions(text, requestSettings);

        Response<StreamingCompletions>? response = await RunRequestAsync<Response<StreamingCompletions>>(
            () => Client.GetCompletionsStreamingAsync(ModelId, options, cancellationToken)).ConfigureAwait(false);

        using var streamingChatCompletions = response.Value;

        await foreach (var choice in streamingChatCompletions.GetChoicesStreaming(cancellationToken))
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
    protected private async Task<IList<ReadOnlyMemory<float>>> InternalGetEmbeddingsAsync(
        IList<string> data,
        CancellationToken cancellationToken = default)
    {
        List<ReadOnlyMemory<float>> result = new List<ReadOnlyMemory<float>>(data.Count);

        foreach (var text in data)
        {
            var options = new EmbeddingsOptions(text);

            Response<Embeddings>? response = await RunRequestAsync<Response<Embeddings>?>(
                () => Client.GetEmbeddingsAsync(ModelId, options, cancellationToken)).ConfigureAwait(false);

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
    /// <param name="chatSettings">AI request settings</param>
    /// <param name="functionCall"> Function To Call</param>
    /// <param name="functionCalls">Available Functions</param>
    /// <param name="cancellationToken">Async cancellation token</param>
    /// <returns>Generated chat message in string format</returns>
    protected private async Task<IReadOnlyList<IChatResult>> InternalGetChatResultsAsync(
        ChatHistory chat,
        ChatRequestSettings? chatSettings,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chat);
        chatSettings ??= new ChatRequestSettings();

        ValidateMaxTokens(chatSettings.MaxTokens);
        var chatOptions = CreateChatCompletionsOptions(chatSettings, chat);

        Response<ChatCompletions>? response = await RunRequestAsync<Response<ChatCompletions>?>(
            () => Client.GetChatCompletionsAsync(ModelId, chatOptions, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new SKException("Chat completions null response");
        }

        var responseData = response.Value;

        if (responseData.Choices.Count == 0)
        {
            throw new SKException("Chat completions not found");
        }

        Console.WriteLine($"Total Tokens: {responseData.Usage.TotalTokens} Prompt Tokens: {responseData.Usage.PromptTokens} Completion Tokens: {responseData.Usage.CompletionTokens}");
        CaptureUsageDetails(responseData.Usage);

        return responseData.Choices.Select(chatChoice => new ChatResult(responseData, chatChoice)).ToList();
    }


    protected private async Task<IReadOnlyList<IChatResult>> InternalGetChatResultsWithFunctionCallsAsync(
        ChatHistory chat,
        ChatRequestSettings? chatSettings,
        FunctionDefinition? functionCall = null,
        FunctionDefinition[]? functionCalls = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chat);
        chatSettings ??= new ChatRequestSettings();

        ValidateMaxTokens(chatSettings.MaxTokens);
        var chatOptions = CreateChatCompletionsOptionsWithFunctionCalls(chatSettings, chat, functionCall, functionCalls);

        List<string> functionNames = chatOptions.Functions.Select(f => f.Name).ToList();
        Response<ChatCompletions>? response = await RunRequestAsync<Response<ChatCompletions>?>(
            () => Client.GetChatCompletionsAsync(ModelId, chatOptions, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new SKException("Chat completions null response");
        }

        var responseData = response.Value;

        if (responseData.Choices.Count == 0)
        {
            throw new SKException("Chat completions not found");
        }

        Console.WriteLine($"Total Tokens: {responseData.Usage.TotalTokens} Prompt Tokens: {responseData.Usage.PromptTokens} Completion Tokens: {responseData.Usage.CompletionTokens}");
        CaptureUsageDetails(responseData.Usage);

        if (!responseData.IsFunctionCallResponse(functionNames))
        {
            return responseData.Choices.Select(chatChoice => new ChatResult(responseData, chatChoice)).ToList();
        }

        return responseData.Choices.Select(choice => choice.IsFunctionCall(functionNames)
                ? new ChatResult(responseData, choice, true)
                : new ChatResult(responseData, choice)).Cast<IChatResult>()
            .ToList();
    }


    /// <summary>
    /// Generate a new chat message stream
    /// </summary>
    /// <param name="chat">Chat history</param>
    /// <param name="requestSettings">AI request settings</param>
    /// <param name="cancellationToken">Async cancellation token</param>
    /// <returns>Streaming of generated chat message in string format</returns>
    protected private async IAsyncEnumerable<IChatStreamingResult> InternalGetChatStreamingResultsAsync(
        IEnumerable<ChatMessageBase> chat,
        ChatRequestSettings? requestSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chat);
        requestSettings ??= new ChatRequestSettings();

        ValidateMaxTokens(requestSettings.MaxTokens);

        var options = CreateChatCompletionsOptions(requestSettings, chat);

        Response<StreamingChatCompletions>? response = await RunRequestAsync<Response<StreamingChatCompletions>>(
            () => Client.GetChatCompletionsStreamingAsync(ModelId, options, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new SKException("Chat completions null response");
        }

        using var streamingChatCompletions = response.Value;

        await foreach (var choice in streamingChatCompletions.GetChoicesStreaming(cancellationToken).ConfigureAwait(false))
        {
            yield return new ChatStreamingResult(response.Value, choice);
        }
    }


    /// <summary>
    /// Create a new empty chat instance
    /// </summary>
    /// <param name="instructions">Optional chat instructions for the AI service</param>
    /// <returns>Chat object</returns>
    protected private static OpenAIChatHistory InternalCreateNewChat(string? instructions = null) => new(instructions);


    protected private async Task<IReadOnlyList<ITextResult>> InternalGetChatResultsAsTextAsync(
        string text,
        CompleteRequestSettings? textSettings,
        CancellationToken cancellationToken = default)
    {
        textSettings ??= new CompleteRequestSettings();
        ChatHistory chat = PrepareChatHistory(text, textSettings, out var chatSettings);

        return (await InternalGetChatResultsAsync(chat, chatSettings, cancellationToken).ConfigureAwait(false))
            .OfType<ITextResult>()
            .ToList();
    }


    protected private async IAsyncEnumerable<ITextStreamingResult> InternalGetChatStreamingResultsAsTextAsync(
        string text,
        CompleteRequestSettings? textSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatHistory chat = PrepareChatHistory(text, textSettings, out var chatSettings);

        await foreach (var chatCompletionStreamingResult in InternalGetChatStreamingResultsAsync(chat, chatSettings, cancellationToken))
        {
            yield return (ITextStreamingResult)chatCompletionStreamingResult;
        }
    }


    private static OpenAIChatHistory PrepareChatHistory(string text, CompleteRequestSettings? requestSettings, out ChatRequestSettings settings)
    {
        requestSettings ??= new CompleteRequestSettings();
        var chat = InternalCreateNewChat(requestSettings.ChatSystemPrompt);
        chat.AddUserMessage(text);
        settings = new ChatRequestSettings
        {
            MaxTokens = requestSettings.MaxTokens,
            Temperature = requestSettings.Temperature,
            TopP = requestSettings.TopP,
            PresencePenalty = requestSettings.PresencePenalty,
            FrequencyPenalty = requestSettings.FrequencyPenalty,
            StopSequences = requestSettings.StopSequences
        };
        return chat;
    }


    private static CompletionsOptions CreateCompletionsOptions(string text, CompleteRequestSettings requestSettings)
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
            User = null
        };

        foreach (KeyValuePair<int, int> keyValue in requestSettings.TokenSelectionBiases)
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


    private static ChatCompletionsOptions CreateChatCompletionsOptions(ChatRequestSettings requestSettings, IEnumerable<ChatMessageBase> chatHistory)
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


    private static ChatCompletionsOptions CreateChatCompletionsOptionsWithFunctionCalls(ChatRequestSettings requestSettings, IEnumerable<ChatMessageBase> chatHistory, FunctionDefinition? functionCall = null, IEnumerable<FunctionDefinition>? functions = null)
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

        // signaling that the user wants the model to choose the function
        if (functionCall == null)
        {
            options.FunctionCall = FunctionDefinition.Auto;
        }

        // signaling that this is the list of functions the user wants the model to choose from
        if (functions != null)
        {
            options.Functions = new List<FunctionDefinition>(functions);
        }

        foreach (KeyValuePair<int, int> keyValue in requestSettings.TokenSelectionBiases)
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
            validRole != ChatRole.Assistant &&
            validRole != ChatRole.Function)
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
        Logger.LogInformation(
            "Prompt tokens: {PromptTokens}. Completion tokens: {CompletionTokens}. Total tokens: {TotalTokens}.",
            usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);

        s_promptTokensCounter.Add(usage.PromptTokens);
        s_completionTokensCounter.Add(usage.CompletionTokens);
        s_totalTokensCounter.Add(usage.TotalTokens);
    }
}
