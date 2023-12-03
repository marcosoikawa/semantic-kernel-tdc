﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using RepoUtils;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

/**
 * The following example shows how to plug into SK a custom text completion model.
 *
 * This might be useful in a few scenarios, for example:
 * - You are not using OpenAI or Azure OpenAI models
 * - You are using OpenAI/Azure OpenAI models but the models are behind a web service with a different API schema
 * - You want to use a local model
 *
 * Note that all text completion models are deprecated by OpenAI and will be removed in a future release.
 *
 * Refer to example 33 for streaming chat completion.
 */
// ReSharper disable StringLiteralTypo
// ReSharper disable once InconsistentNaming
public static class Example16_CustomLLM
{
    private const string LLMResultText = @" ..output from your custom model... Example:
    AI is awesome because it can help us solve complex problems, enhance our creativity,
    and improve our lives in many ways. AI can perform tasks that are too difficult,
    tedious, or dangerous for humans, such as diagnosing diseases, detecting fraud, or
    exploring space. AI can also augment our abilities and inspire us to create new forms
    of art, music, or literature. AI can also improve our well-being and happiness by
    providing personalized recommendations, entertainment, and assistance. AI is awesome";

    public static async Task RunAsync()
    {
        await CustomTextCompletionWithSKFunctionAsync();

        await CustomTextCompletionAsync();
        await CustomTextCompletionStreamAsync();
    }

    private static async Task CustomTextCompletionWithSKFunctionAsync()
    {
        Console.WriteLine("======== Custom LLM - Text Completion - SKFunction ========");

        Kernel kernel = new KernelBuilder().WithServices(c =>
        {
            c.AddSingleton(ConsoleLogger.LoggerFactory)
            // Add your text completion service as a singleton instance
            .AddKeyedSingleton<ITextCompletion>("myService1", new MyTextCompletionService())
            // Add your text completion service as a factory method
            .AddKeyedSingleton<ITextCompletion>("myService2", (_, _) => new MyTextCompletionService());
        }).Build();

        const string FunctionDefinition = "Does the text contain grammar errors (Y/N)? Text: {{$input}}";

        var textValidationFunction = kernel.CreateFunctionFromPrompt(FunctionDefinition);

        var result = await textValidationFunction.InvokeAsync(kernel, "I mised the training session this morning");
        Console.WriteLine(result.GetValue<string>());

        // Details of the my custom model response
        Console.WriteLine(JsonSerializer.Serialize(
            result.GetModelResults(),
            new JsonSerializerOptions() { WriteIndented = true }
        ));
    }

    private static async Task CustomTextCompletionAsync()
    {
        Console.WriteLine("======== Custom LLM  - Text Completion - Raw ========");
        var completionService = new MyTextCompletionService();

        var result = await completionService.GetTextContentAsync("I missed the training session this morning");

        Console.WriteLine(result);
    }

    private static async Task CustomTextCompletionStreamAsync()
    {
        Console.WriteLine("======== Custom LLM  - Text Completion - Raw Streaming ========");

        Kernel kernel = new KernelBuilder().WithLoggerFactory(ConsoleLogger.LoggerFactory).Build();
        ITextCompletion textCompletion = new MyTextCompletionService();

        var prompt = "Write one paragraph why AI is awesome";
        await TextCompletionStreamAsync(prompt, textCompletion);
    }

    private static async Task TextCompletionStreamAsync(string prompt, ITextCompletion textCompletion)
    {
        var executionSettings = new OpenAIPromptExecutionSettings()
        {
            MaxTokens = 100,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            Temperature = 1,
            TopP = 0.5
        };

        Console.WriteLine("Prompt: " + prompt);
        await foreach (var message in textCompletion.GetStreamingTextContentsAsync(prompt, executionSettings))
        {
            Console.Write(message);
        }

        Console.WriteLine();
    }

    private sealed class MyTextCompletionService : ITextCompletion
    {
        public string? ModelId { get; private set; }

        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

        public Task<IReadOnlyList<ITextResult>> GetCompletionsAsync(string prompt, PromptExecutionSettings? executionSettings, Kernel? kernel, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<T> GetStreamingContentAsync<T>(string prompt, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(string prompt, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (string word in LLMResultText.Split(' '))
            {
                await Task.Delay(50, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                yield return new MyStreamingContent(word);
            }
        }

        public Task<IReadOnlyList<TextContent>> GetTextContentsAsync(string prompt, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TextContent>>(new List<TextContent>
            {
                new(LLMResultText)
            });
        }
    }

    private sealed class MyStreamingContent : StreamingTextContent
    {
        public MyStreamingContent(string content) : base(content)
        {
        }

        public override byte[] ToByteArray()
        {
            return Encoding.UTF8.GetBytes(this.Text ?? string.Empty);
        }

        public override string ToString()
        {
            return this.Text ?? string.Empty;
        }
    }
}
