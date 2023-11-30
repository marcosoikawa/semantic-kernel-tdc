﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.Prompt;

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
    private protected string DeploymentOrModelName { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI / Azure OpenAI Client
    /// </summary>
    private protected abstract OpenAIClient Client { get; }

    /// <summary>
    /// Logger instance
    /// </summary>
    private protected ILogger Logger { get; set; }

    /// <summary>
    /// Storage for AI service attributes.
    /// </summary>
    private protected Dictionary<string, string> InternalAttributes = new();

    /// <summary>
    /// Instance of <see cref="Meter"/> for metrics.
    /// </summary>
    private static readonly Meter s_meter = new("Microsoft.SemanticKernel.Connectors.AI.OpenAI");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of prompt tokens used.
    /// </summary>
    private static readonly Counter<int> s_promptTokensCounter =
        s_meter.CreateCounter<int>(
            name: "sk.connectors.openai.tokens.prompt",
            unit: "{token}",
            description: "Number of prompt tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of completion tokens used.
    /// </summary>
    private static readonly Counter<int> s_completionTokensCounter =
        s_meter.CreateCounter<int>(
            name: "sk.connectors.openai.tokens.completion",
            unit: "{token}",
            description: "Number of completion tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the total number of tokens used.
    /// </summary>
    private static readonly Counter<int> s_totalTokensCounter =
        s_meter.CreateCounter<int>(
            name: "sk.connectors.openai.tokens.total",
            unit: "{token}",
            description: "Number of tokens used");

    /// <summary>
    /// Creates completions for the prompt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Completions generated by the remote model</returns>
    private protected async Task<IReadOnlyList<ITextResult>> InternalGetTextResultsAsync(
        string text,
        PromptExecutionSettings? executionSettings,
        CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings textRequestSettings = OpenAIPromptExecutionSettings.FromRequestSettings(executionSettings, OpenAIPromptExecutionSettings.DefaultTextMaxTokens);

        ValidateMaxTokens(textRequestSettings.MaxTokens);
        var options = CreateCompletionsOptions(text, textRequestSettings, this.DeploymentOrModelName);

        Response<Completions>? response = await RunRequestAsync<Response<Completions>?>(
            () => this.Client.GetCompletionsAsync(options, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new KernelException("Text completions null response");
        }

        var responseData = response.Value;

        if (responseData.Choices.Count == 0)
        {
            throw new KernelException("Text completions not found");
        }

        this.CaptureUsageDetails(responseData.Usage);

        return responseData.Choices.Select(choice => new TextResult(responseData, choice)).ToList();
    }

    private protected async IAsyncEnumerable<T> InternalGetTextStreamingUpdatesAsync<T>(
        string prompt,
        PromptExecutionSettings? executionSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings textRequestSettings = OpenAIPromptExecutionSettings.FromRequestSettings(executionSettings, OpenAIPromptExecutionSettings.DefaultTextMaxTokens);

        ValidateMaxTokens(textRequestSettings.MaxTokens);

        var options = CreateCompletionsOptions(prompt, textRequestSettings, this.DeploymentOrModelName);

        StreamingResponse<Completions>? response = await RunRequestAsync<StreamingResponse<Completions>>(
            () => this.Client.GetCompletionsStreamingAsync(options, cancellationToken)).ConfigureAwait(false);

        int choiceIndex = 0;
        Dictionary<string, object>? responseMetadata = null;
        await foreach (Completions completions in response)
        {
            responseMetadata ??= GetResponseMetadata(completions);

            foreach (Choice choice in completions.Choices)
            {
                // If the provided T is a string, return the completion as is
                if (typeof(T) == typeof(string))
                {
                    yield return (T)(object)choice.Text;
                    continue;
                }

                // If the provided T is an specialized class of StreamingContent interface
                if (typeof(T) == typeof(StreamingTextContent) ||
                    typeof(T) == typeof(StreamingContent))
                {
                    yield return (T)(object)new StreamingTextContent(choice.Text, choice.Index, choice, responseMetadata);
                    continue;
                }

                throw new NotSupportedException($"Type {typeof(T)} is not supported");
            }
            choiceIndex++;
        }
    }

    private static Dictionary<string, object> GetResponseMetadata(Completions completions)
    {
        return new Dictionary<string, object>()
        {
            { $"{nameof(Completions)}.{nameof(completions.Id)}", completions.Id },
            { $"{nameof(Completions)}.{nameof(completions.Created)}", completions.Created },
            { $"{nameof(Completions)}.{nameof(completions.PromptFilterResults)}", completions.PromptFilterResults },
        };
    }

    private static Dictionary<string, object> GetResponseMetadata(StreamingChatCompletionsUpdate completions)
    {
        return new Dictionary<string, object>()
        {
            { $"{nameof(StreamingChatCompletionsUpdate)}.{nameof(completions.Id)}", completions.Id },
            { $"{nameof(StreamingChatCompletionsUpdate)}.{nameof(completions.Created)}", completions.Created },
        };
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
            var options = new EmbeddingsOptions(this.DeploymentOrModelName, new[] { text });

            Response<Embeddings>? response = await RunRequestAsync<Response<Embeddings>?>(
                () => this.Client.GetEmbeddingsAsync(options, cancellationToken)).ConfigureAwait(false);

            if (response is null)
            {
                throw new KernelException("Text embedding null response");
            }

            if (response.Value.Data.Count == 0)
            {
                throw new KernelException("Text embedding not found");
            }

            result.Add(response.Value.Data[0].Embedding.ToArray());
        }

        return result;
    }

    /// <summary>
    /// Generate a new chat message
    /// </summary>
    /// <param name="chat">Chat history</param>
    /// <param name="executionSettings">AI request settings</param>
    /// <param name="cancellationToken">Async cancellation token</param>
    /// <returns>Generated chat message in string format</returns>
    private protected async Task<IReadOnlyList<IChatResult>> InternalGetChatResultsAsync(
        ChatHistory chat,
        PromptExecutionSettings? executionSettings,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chat);

        OpenAIPromptExecutionSettings chatRequestSettings = OpenAIPromptExecutionSettings.FromRequestSettings(executionSettings);

        ValidateMaxTokens(chatRequestSettings.MaxTokens);

        var chatOptions = CreateChatCompletionsOptions(chatRequestSettings, chat, this.DeploymentOrModelName);

        Response<ChatCompletions>? response = await RunRequestAsync<Response<ChatCompletions>?>(
            () => this.Client.GetChatCompletionsAsync(chatOptions, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new KernelException("Chat completions null response");
        }

        var responseData = response.Value;

        if (responseData.Choices.Count == 0)
        {
            throw new KernelException("Chat completions not found");
        }

        this.CaptureUsageDetails(responseData.Usage);

        return responseData.Choices.Select(chatChoice => new ChatResult(responseData, chatChoice)).ToList();
    }

    private protected async IAsyncEnumerable<T> InternalGetChatStreamingUpdatesAsync<T>(
        IEnumerable<SemanticKernel.AI.ChatCompletion.ChatMessage> chat,
        PromptExecutionSettings? executionSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chat);

        OpenAIPromptExecutionSettings chatRequestSettings = OpenAIPromptExecutionSettings.FromRequestSettings(executionSettings);

        ValidateMaxTokens(chatRequestSettings.MaxTokens);

        var options = CreateChatCompletionsOptions(chatRequestSettings, chat, this.DeploymentOrModelName);

        var response = await RunRequestAsync<StreamingResponse<StreamingChatCompletionsUpdate>>(
           () => this.Client.GetChatCompletionsStreamingAsync(options, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new KernelException("Chat completions null response");
        }

        Dictionary<string, object>? responseMetadata = null;
        await foreach (StreamingChatCompletionsUpdate update in response)
        {
            responseMetadata ??= GetResponseMetadata(update);

            if (typeof(T) == typeof(string))
            {
                yield return (T)(object)update.ContentUpdate;
                continue;
            }

            // If the provided T is an specialized class of StreamingResultChunk interface
            if (typeof(T) == typeof(StreamingChatContent) ||
                typeof(T) == typeof(StreamingContent))
            {
                yield return (T)(object)new StreamingChatContent(update, update.ChoiceIndex ?? 0, responseMetadata);
                continue;
            }

            throw new NotSupportedException($"Type {typeof(T)} is not supported");
        }
    }

    private protected async Task<IReadOnlyList<ITextResult>> InternalGetChatResultsAsTextAsync(
        string text,
        PromptExecutionSettings? executionSettings,
        CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings chatSettings = OpenAIPromptExecutionSettings.FromRequestSettings(executionSettings);

        ChatHistory chat = this.InternalCreateNewChat(text, chatSettings);
        return (await this.InternalGetChatResultsAsync(chat, chatSettings, cancellationToken).ConfigureAwait(false))
            .OfType<ITextResult>()
            .ToList();
    }

    private protected void AddAttribute(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            this.InternalAttributes.Add(key, value!);
        }
    }

    /// <summary>Gets options to use for an OpenAIClient</summary>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>An instance of <see cref="OpenAIClientOptions"/>.</returns>
    internal static OpenAIClientOptions GetOpenAIClientOptions(HttpClient? httpClient)
    {
        OpenAIClientOptions options = new()
        {
            Diagnostics = { ApplicationId = HttpHeaderValues.UserAgent }
        };

        if (httpClient is not null)
        {
            options.Transport = new HttpClientTransport(httpClient);
            options.RetryPolicy = new RetryPolicy(maxRetries: 0); // Disable Azure SDK retry policy if and only if a custom HttpClient is provided.
        }

        return options;
    }

    /// <summary>
    /// Create a new empty chat instance
    /// </summary>
    /// <param name="text">Optional chat instructions for the AI service</param>
    /// <param name="executionSettings">Execution settings</param>
    /// <returns>Chat object</returns>
    private protected OpenAIChatHistory InternalCreateNewChat(string? text = null, OpenAIPromptExecutionSettings? executionSettings = null)
    {
        // If text is not provided, create an empty chat with the system prompt if provided
        if (string.IsNullOrWhiteSpace(text))
        {
            return new OpenAIChatHistory(executionSettings?.ChatSystemPrompt);
        }

        // Try to parse the text as a chat history
        if (XmlPromptParser.TryParse(text!, out var nodes) && ChatPromptParser.TryParse(nodes, out var chatHistory))
        {
            return new OpenAIChatHistory(chatHistory);
        }

        // If settings is not provided, create a new chat with the text as the system prompt
        var chat = new OpenAIChatHistory(executionSettings?.ChatSystemPrompt ?? text);
        if (executionSettings is not null)
        {
            // If settings is provided, add the prompt as the user message
            chat.AddUserMessage(text!);
        }

        return chat;
    }

    private static CompletionsOptions CreateCompletionsOptions(string text, OpenAIPromptExecutionSettings executionSettings, string deploymentOrModelName)
    {
        if (executionSettings.ResultsPerPrompt is < 1 or > MaxResultsPerPrompt)
        {
            throw new ArgumentOutOfRangeException($"{nameof(executionSettings)}.{nameof(executionSettings.ResultsPerPrompt)}", executionSettings.ResultsPerPrompt, $"The value must be in range between 1 and {MaxResultsPerPrompt}, inclusive.");
        }

        var options = new CompletionsOptions
        {
            Prompts = { text.Replace("\r\n", "\n") }, // normalize line endings
            MaxTokens = executionSettings.MaxTokens,
            Temperature = (float?)executionSettings.Temperature,
            NucleusSamplingFactor = (float?)executionSettings.TopP,
            FrequencyPenalty = (float?)executionSettings.FrequencyPenalty,
            PresencePenalty = (float?)executionSettings.PresencePenalty,
            Echo = false,
            ChoicesPerPrompt = executionSettings.ResultsPerPrompt,
            GenerationSampleCount = executionSettings.ResultsPerPrompt,
            LogProbabilityCount = null,
            User = null,
            DeploymentName = deploymentOrModelName
        };

        foreach (var keyValue in executionSettings.TokenSelectionBiases)
        {
            options.TokenSelectionBiases.Add(keyValue.Key, keyValue.Value);
        }

        if (executionSettings.StopSequences is { Count: > 0 })
        {
            foreach (var s in executionSettings.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }

        return options;
    }

    private static ChatCompletionsOptions CreateChatCompletionsOptions(OpenAIPromptExecutionSettings executionSettings, IEnumerable<SemanticKernel.AI.ChatCompletion.ChatMessage> chatHistory, string deploymentOrModelName)
    {
        if (executionSettings.ResultsPerPrompt is < 1 or > MaxResultsPerPrompt)
        {
            throw new ArgumentOutOfRangeException($"{nameof(executionSettings)}.{nameof(executionSettings.ResultsPerPrompt)}", executionSettings.ResultsPerPrompt, $"The value must be in range between 1 and {MaxResultsPerPrompt}, inclusive.");
        }

        var options = new ChatCompletionsOptions
        {
            MaxTokens = executionSettings.MaxTokens,
            Temperature = (float?)executionSettings.Temperature,
            NucleusSamplingFactor = (float?)executionSettings.TopP,
            FrequencyPenalty = (float?)executionSettings.FrequencyPenalty,
            PresencePenalty = (float?)executionSettings.PresencePenalty,
            ChoiceCount = executionSettings.ResultsPerPrompt,
            DeploymentName = deploymentOrModelName,
        };

        if (executionSettings.Functions is not null)
        {
            if (executionSettings.FunctionCall == OpenAIPromptExecutionSettings.FunctionCallAuto)
            {
                options.FunctionCall = FunctionDefinition.Auto;
                options.Functions = executionSettings.Functions.Select(f => f.ToFunctionDefinition()).ToList();
            }
            else if (executionSettings.FunctionCall != OpenAIPromptExecutionSettings.FunctionCallNone
                    && !string.IsNullOrEmpty(executionSettings.FunctionCall))
            {
                var filteredFunctions = executionSettings.Functions
                    .Where(f => f.FullyQualifiedName.Equals(executionSettings.FunctionCall, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                OpenAIFunction? function = filteredFunctions.FirstOrDefault();
                if (function is not null)
                {
                    options.FunctionCall = function.ToFunctionDefinition();
                    options.Functions = filteredFunctions.Select(f => f.ToFunctionDefinition()).ToList();
                }
            }
        }

        foreach (var keyValue in executionSettings.TokenSelectionBiases)
        {
            options.TokenSelectionBiases.Add(keyValue.Key, keyValue.Value);
        }

        if (executionSettings.StopSequences is { Count: > 0 })
        {
            foreach (var s in executionSettings.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }

        foreach (var message in chatHistory)
        {
            var azureMessage = new Azure.AI.OpenAI.ChatMessage(new ChatRole(message.Role.Label), message.Content);

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
            throw new KernelException($"MaxTokens {maxTokens} is not valid, the value must be greater than zero");
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
