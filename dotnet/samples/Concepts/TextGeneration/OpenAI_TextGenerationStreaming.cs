﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace TextGeneration;

/**
 * The following example shows how to use Semantic Kernel with streaming text generation.
 *
 * This example will NOT work with regular chat completion models. It will only work with
 * text completion models.
 *
 * Note that all text generation models are deprecated by OpenAI and will be removed in a future release.
 *
 * Refer to example 33 for streaming chat completion.
 */
public class OpenAI_TextGenerationStreaming(ITestOutputHelper output) : BaseTest(output)
{
    [Fact]
    public async Task AzureOpenAITextGenerationStreamAsync()
    {
        Console.WriteLine("======== Azure OpenAI - Text Generation - Raw Streaming ========");

        var textGeneration = new AzureOpenAITextGenerationService(
            deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
            endpoint: TestConfiguration.AzureOpenAI.Endpoint,
            apiKey: TestConfiguration.AzureOpenAI.ApiKey,
            modelId: TestConfiguration.AzureOpenAI.ModelId);

        var executionSettings = new AzureOpenAIPromptExecutionSettings()
        {
            MaxTokens = 100,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            Temperature = 1,
            TopP = 0.5
        };

        var prompt = "Write one paragraph why AI is awesome";

        Console.WriteLine("Prompt: " + prompt);
        await foreach (var content in textGeneration.GetStreamingTextContentsAsync(prompt, executionSettings))
        {
            Console.Write(content);
        }

        Console.WriteLine();
    }
}
