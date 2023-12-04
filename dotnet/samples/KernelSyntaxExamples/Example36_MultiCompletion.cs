﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;

/**
 * The following example shows how to use Semantic Kernel with streaming Multiple Results Chat Completion.
 */
// ReSharper disable once InconsistentNaming
public static class Example36_MultiCompletion
{
    public static async Task RunAsync()
    {
        await AzureOpenAIMultiChatCompletionAsync();
        await OpenAIMultiChatCompletionAsync();
    }

    private static async Task AzureOpenAIMultiChatCompletionAsync()
    {
        Console.WriteLine("======== Azure OpenAI - Multiple Chat Completion ========");

        var chatCompletionService = new AzureOpenAIChatCompletionService(
            TestConfiguration.AzureOpenAI.ChatDeploymentName,
            TestConfiguration.AzureOpenAI.ChatModelId,
            TestConfiguration.AzureOpenAI.Endpoint,
            TestConfiguration.AzureOpenAI.ApiKey);

        await ChatCompletionAsync(chatCompletionService);
    }

    private static async Task OpenAIMultiChatCompletionAsync()
    {
        Console.WriteLine("======== Open AI - Multiple Chat Completion ========");

        var chatCompletionService = new OpenAIChatCompletionService(
            TestConfiguration.OpenAI.ChatModelId,
            TestConfiguration.OpenAI.ApiKey);

        await ChatCompletionAsync(chatCompletionService);
    }

    private static async Task ChatCompletionAsync(IChatCompletionService chatCompletionService)
    {
        var executionSettings = new OpenAIPromptExecutionSettings()
        {
            MaxTokens = 200,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            Temperature = 1,
            TopP = 0.5,
            ResultsPerPrompt = 2,
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Write one paragraph about why AI is awesome");

        foreach (var completions in await chatCompletionService.GetChatCompletionsAsync(chatHistory))
        {
            var result = await completions.GetChatMessageAsync();
            Console.Write(result.Content);
            Console.WriteLine("-------------");
        }

        Console.WriteLine();
    }
}
