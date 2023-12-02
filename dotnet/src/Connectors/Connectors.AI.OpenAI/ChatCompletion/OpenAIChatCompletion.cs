﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;

/// <summary>
/// OpenAI chat completion client.
/// TODO: forward ETW logging to ILogger, see https://learn.microsoft.com/en-us/dotnet/azure/sdk/logging
/// </summary>
public sealed class OpenAIChatCompletion : OpenAIClientBase, IChatCompletion, ITextCompletion
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Attributes => this.InternalAttributes;

    /// <summary>
    /// Create an instance of the OpenAI chat completion connector
    /// </summary>
    /// <param name="modelId">Model name</param>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="organization">OpenAI Organization Id (usually optional)</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public OpenAIChatCompletion(
        string modelId,
        string apiKey,
        string? organization = null,
        HttpClient? httpClient = null,
        ILoggerFactory? loggerFactory = null) : base(modelId, apiKey, organization, httpClient, loggerFactory)
    {
        this.AddAttribute(AIServiceExtensions.ModelIdKey, modelId);
        this.AddAttribute(OrganizationKey, organization);
    }

    /// <summary>
    /// Create an instance of the OpenAI chat completion connector
    /// </summary>
    /// <param name="modelId">Model name</param>
    /// <param name="openAIClient">Custom <see cref="OpenAIClient"/> for HTTP requests.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public OpenAIChatCompletion(
        string modelId,
        OpenAIClient openAIClient,
        ILoggerFactory? loggerFactory = null) : base(modelId, openAIClient, loggerFactory)
    {
        this.AddAttribute(AIServiceExtensions.ModelIdKey, modelId);
    }

    /// <inheritdoc/>
    public ChatHistory CreateNewChat(string? instructions = null)
        => InternalCreateNewChat(instructions);

    /// <inheritdoc/>
    public Task<IReadOnlyList<IChatResult>> GetChatCompletionsAsync(
        ChatHistory chat,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        this.LogActionDetails();

        return this.InternalGetChatResultsAsync(chat, executionSettings, kernel, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<IChatResult>> GetChatCompletionsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(prompt);

        this.LogActionDetails();

        var openAIExecutionSettings = OpenAIPromptExecutionSettings.FromExecutionSettings(executionSettings);
        var chatHistory = InternalCreateNewChat(prompt, openAIExecutionSettings);

        return this.InternalGetChatResultsAsync(chatHistory, executionSettings, kernel, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ITextResult>> GetCompletionsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(prompt);

        this.LogActionDetails();

        return this.InternalGetChatResultsAsTextAsync(prompt, executionSettings, kernel, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<T> GetStreamingContentAsync<T>(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(prompt);

        this.LogActionDetails();

        var openAIExecutionSettings = OpenAIPromptExecutionSettings.FromExecutionSettings(executionSettings);
        var chatHistory = InternalCreateNewChat(prompt, openAIExecutionSettings);

        return this.InternalGetChatStreamingUpdatesAsync<T>(chatHistory, openAIExecutionSettings, kernel, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<T> GetStreamingContentAsync<T>(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        this.LogActionDetails();

        return this.InternalGetChatStreamingUpdatesAsync<T>(chatHistory, executionSettings, kernel, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ChatContent>> GetChatContentsAsync(ChatHistory chat, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<StreamingChatContent> GetStreamingChatContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }
}
