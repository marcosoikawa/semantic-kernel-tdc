﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Experimental.Assistants;
using Plugins;
using Resources;

// ReSharper disable once InconsistentNaming
/// <summary>
/// Showcase complex Open AI Assistant interactions using semantic kernel.
/// </summary>
public static class Example71_AssistantDelegation
{
    /// <summary>
    /// Specific model is required that supports assistants and function calling.
    /// Currently this is limited to Open AI hosted services.
    /// </summary>
    private const string OpenAIFunctionEnabledModel = "gpt-3.5-turbo-1106";

    /// <summary>
    /// Show how to combine coordinate multiple assistants.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("======== Example71_AssistantDelegation ========");

        if (TestConfiguration.OpenAI.ApiKey == null)
        {
            Console.WriteLine("OpenAI apiKey not found. Skipping example.");
            return;
        }

        var plugin = KernelPluginFactory.CreateFromObject<MenuPlugin>();

        var menuAssistant =
            await AssistantBuilder.FromDefinitionAsync(
                TestConfiguration.OpenAI.ApiKey,
                model: OpenAIFunctionEnabledModel,
                template: EmbeddedResource.Read("Assistants.ToolAssistant.yaml"),
                new[] { plugin });

        var parrotAssistant =
            await AssistantBuilder.FromDefinitionAsync(
                TestConfiguration.OpenAI.ApiKey,
                model: OpenAIFunctionEnabledModel,
                template: EmbeddedResource.Read("Assistants.ParrotAssistant.yaml"));

        var helperAssistantPlugins = Import(menuAssistant, parrotAssistant);

        var toolAssistant =
            await AssistantBuilder.FromDefinitionAsync(
                TestConfiguration.OpenAI.ApiKey,
                model: OpenAIFunctionEnabledModel,
                template: EmbeddedResource.Read("Assistants.ToolAssistant.yaml"),
                helperAssistantPlugins);

        var messages = new string[]
        {
            "What's on the menu?",
            "Can you talk like pirate?",
            "Thank you",
        };

        var thread = await toolAssistant.NewThreadAsync();
        foreach (var message in messages)
        {
            var messageUser = await thread.AddUserMessageAsync(message).ConfigureAwait(true);
            DisplayMessage(messageUser);

            var assistantMessages = await thread.InvokeAsync(toolAssistant).ConfigureAwait(true);
            DisplayMessages(assistantMessages);
        }


        IEnumerable<IKernelPlugin> Import(params IAssistant[] assistants)
        {
            var plugins = new KernelPluginCollection();

            foreach (var assistant in assistants)
            {
                plugins.Add(KernelPluginFactory.CreateFromObject(assistant, assistant.Id));
            }

            return plugins;
        }
    }

    private static void DisplayMessages(IEnumerable<IChatMessage> messages)
    {
        foreach (var message in messages)
        {
            DisplayMessage(message);
        }
    }

    private static void DisplayMessage(IChatMessage message)
    {
        Console.WriteLine($"[{message.Id}]");
        Console.WriteLine($"# {message.Role}: {message.Content}");
    }
}
