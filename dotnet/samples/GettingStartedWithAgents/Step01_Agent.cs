﻿// Copyright (c) Microsoft. All rights reserved.
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Resources;

namespace GettingStarted;

/// <summary>
/// Demonstrate creation of <see cref="ChatCompletionAgent"/> and
/// eliciting its response to three explicit user messages.
/// </summary>
public class Step01_Agent(ITestOutputHelper output) : BaseAgentsTest(output)
{
    private const string ParrotName = "Parrot";
    private const string ParrotInstructions = "Repeat the user message in the voice of a pirate and then end with a parrot sound.";

    [Fact]
    public async Task UseSingleChatCompletionAgentAsync()
    {
        Kernel kernel = this.CreateKernelWithChatCompletion();

        // Define the agent
        ChatCompletionAgent agent =
            new()
            {
                Name = ParrotName,
                Instructions = ParrotInstructions,
                Kernel = this.CreateKernelWithChatCompletion(),
            };

        /// Create the chat history to capture the agent interaction.
        ChatHistory chat = [];

        // Respond to user input
        await InvokeAgentAsync("Fortune favors the bold.");
        await InvokeAgentAsync("I came, I saw, I conquered.");
        await InvokeAgentAsync("Practice makes perfect.");

        // Local function to invoke agent and display the conversation messages.
        async Task InvokeAgentAsync(string input)
        {
            ChatMessageContent message = new(AuthorRole.User, input);
            chat.Add(message);
            this.WriteAgentChatMessage(message);

            await foreach (ChatMessageContent response in agent.InvokeAsync(chat))
            {
                chat.Add(response);

                this.WriteAgentChatMessage(response);
            }
        }
    }

    [Fact]
    public async Task UseTemplateForChatCompletionAgentAsync()
    {
        // Define the agent
        string generateStoryYaml = EmbeddedResource.Read("GenerateStory.yaml");
        PromptTemplateConfig templateConfig = KernelFunctionYaml.ToPromptTemplateConfig(generateStoryYaml);
        Kernel kernel = this.CreateKernelWithChatCompletion();
        ChatCompletionAgent agent = ChatCompletionAgent.FromTemplate(templateConfig, new KernelPromptTemplateFactory(), kernel);

        /// Create the chat history to capture the agent interaction.
        ChatHistory chat = [];

        // Respond to user input
        await InvokeAgentAsync(
            new()
            {
                { "topic", "Dog" },
                { "length", "3" },
            });

        await InvokeAgentAsync(
            new()
            {
                { "topic", "Cat" },
                { "length", "3" },
            });

        // Local function to invoke agent and display the conversation messages.
        async Task InvokeAgentAsync(KernelArguments arguments)
        {
            await foreach (ChatMessageContent content in agent.InvokeAsync(chat, arguments))
            {
                chat.Add(content);

                WriteAgentChatMessage(content);
            }
        }
    }
}
