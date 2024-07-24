﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Diagnostics;
using OpenAI.Chat;
using OpenAIChatCompletion = OpenAI.Chat.ChatCompletion;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

namespace Microsoft.SemanticKernel.Connectors.AzureOpenAI;

/// <summary>
/// Base class for AI clients that provides common functionality for interacting with Azure OpenAI services.
/// </summary>
internal partial class AzureClientCore
{
    private const string ContentFilterResultForPromptKey = "ContentFilterResultForPrompt";
    private const string ContentFilterResultForResponseKey = "ContentFilterResultForResponse";

    /// <inheritdoc/>
    protected override OpenAIPromptExecutionSettings GetSpecializedExecutionSettings(PromptExecutionSettings? executionSettings)
    {
        return AzureOpenAIPromptExecutionSettings.FromExecutionSettings(executionSettings);
    }

    /// <inheritdoc/>
    protected override Dictionary<string, object?> GetChatCompletionMetadata(OpenAIChatCompletion completions)
    {
#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return new Dictionary<string, object?>
        {
            { nameof(completions.Id), completions.Id },
            { nameof(completions.CreatedAt), completions.CreatedAt },
            { ContentFilterResultForPromptKey, completions.GetContentFilterResultForPrompt() },
            { nameof(completions.SystemFingerprint), completions.SystemFingerprint },
            { nameof(completions.Usage), completions.Usage },
            { ContentFilterResultForResponseKey, completions.GetContentFilterResultForResponse() },

            // Serialization of this struct behaves as an empty object {}, need to cast to string to avoid it.
            { nameof(completions.FinishReason), completions.FinishReason.ToString() },
            { nameof(completions.ContentTokenLogProbabilities), completions.ContentTokenLogProbabilities },
        };
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    /// <inheritdoc/>
    protected override Activity? StartCompletionActivity(ChatHistory chatHistory, PromptExecutionSettings settings)
        => ModelDiagnostics.StartCompletionActivity(this.Endpoint, this.DeploymentName, ModelProvider, chatHistory, settings);

    /// <inheritdoc/>
    protected override ChatCompletionOptions CreateChatCompletionOptions(
        OpenAIPromptExecutionSettings executionSettings,
        ChatHistory chatHistory,
        ToolCallingConfig toolCallingConfig,
        Kernel? kernel)
    {
        if (executionSettings is not AzureOpenAIPromptExecutionSettings azureSettings)
        {
            return base.CreateChatCompletionOptions(executionSettings, chatHistory, toolCallingConfig, kernel);
        }

        var options = new ChatCompletionOptions
        {
            MaxTokens = executionSettings.MaxTokens,
            Temperature = (float?)executionSettings.Temperature,
            TopP = (float?)executionSettings.TopP,
            FrequencyPenalty = (float?)executionSettings.FrequencyPenalty,
            PresencePenalty = (float?)executionSettings.PresencePenalty,
            Seed = executionSettings.Seed,
            User = executionSettings.User,
            TopLogProbabilityCount = executionSettings.TopLogprobs,
            IncludeLogProbabilities = executionSettings.Logprobs,
            ResponseFormat = GetResponseFormat(azureSettings) ?? ChatResponseFormat.Text,
            ToolChoice = toolCallingConfig.Choice
        };

        if (azureSettings.AzureChatDataSource is not null)
        {
#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            options.AddDataSource(azureSettings.AzureChatDataSource);
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        }

        if (toolCallingConfig.Tools is { Count: > 0 } tools)
        {
            options.Tools.AddRange(tools);
        }

        if (executionSettings.TokenSelectionBiases is not null)
        {
            foreach (var keyValue in executionSettings.TokenSelectionBiases)
            {
                options.LogitBiases.Add(keyValue.Key, keyValue.Value);
            }
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
}
