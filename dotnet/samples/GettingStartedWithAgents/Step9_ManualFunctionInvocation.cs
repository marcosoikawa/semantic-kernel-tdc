// Copyright (c) Microsoft. All rights reserved.
using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents.Filters;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GettingStarted;

/// <summary>
/// Demonstrate creation of <see cref="AgentChat"/> with <see cref="AgentGroupChatSettings"/>
/// that inform how chat proceeds with regards to: Agent selection, chat continuation, and maximum
/// number of agent interactions.
/// </summary>
public class Step9_ManualFunctionInvocation(ITestOutputHelper output) : BaseTest(output)
{
    private const string HostName = "Host";
    private const string HostInstructions = "Answer questions about the menu.";

    [Fact]
    public async Task UseChatCompletionWithPluginAgentAsync()
    {
        // Define the agent
        ChatCompletionAgent agent =
            new()
            {
                Instructions = HostInstructions,
                Name = HostName,
                Kernel = this.CreateKernelWithChatCompletion(),
                ExecutionSettings = new OpenAIPromptExecutionSettings() { ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions },
            };

        // Initialize plugin and add to the agent's Kernel (same as direct Kernel usage).
        KernelPlugin plugin = KernelPluginFactory.CreateFromType<MenuPlugin>();
        agent.Kernel.Plugins.Add(plugin);

        // Create a chat for agent interaction.
        AgentGroupChat chat =
            new()
            {
                ManualFunctionCallProcessor = new ManualFunctionProcessor(), // %%%
            };

        // Respond to user input, invoking functions where appropriate.
        await InvokeAgentAsync("Hello");
        await InvokeAgentAsync("What is the special soup?");
        await InvokeAgentAsync("What is the special drink?");
        await InvokeAgentAsync("Thank you");

        // Local function to invoke agent and display the conversation messages.
        async Task InvokeAgentAsync(string input)
        {
            chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));
            Console.WriteLine($"# {AuthorRole.User}: '{input}'");

            await foreach (ChatMessageContent content in chat.InvokeAsync(agent))
            {
                Console.WriteLine($"# {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");
            }
        }
    }

    public sealed class MenuPlugin
    {
        [KernelFunction, Description("Provides a list of specials from the menu.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Too smart")]
        public string GetSpecials()
        {
            return @"
Special Soup: Clam Chowder
Special Salad: Cobb Salad
Special Drink: Chai Tea
";
        }

        [KernelFunction, Description("Provides the price of the requested menu item.")]
        public string GetItemPrice(
            [Description("The name of the menu item.")]
        string menuItem)
        {
            return "$9.99";
        }
    }

    private class ManualFunctionProcessor : IManualFunctionCallProcessor // %%%
    {
        public async Task OnProcessFunctionCallAsync(ManualFunctionCallContext context)
        {
            context.Result = await context.Function.InvokeAsync(context.Kernel, context.Arguments).ConfigureAwait(false);
        }
    }
}
