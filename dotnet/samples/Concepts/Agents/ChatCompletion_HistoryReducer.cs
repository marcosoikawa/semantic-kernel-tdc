﻿// Copyright (c) Microsoft. All rights reserved.
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.History;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Agents;

/// <summary>
/// Demonstrate creation of <see cref="ChatCompletionAgent"/> and
/// eliciting its response to three explicit user messages.
/// </summary>
public class ChatCompletion_HistoryReducer(ITestOutputHelper output) : BaseTest(output)
{
    private const string TranslatorName = "NumeroTranslator";
    private const string TranslatorInstructions = "Add one to latest user number and spell it in spanish without explanation.";

    /// <summary>
    /// Demonstrate the use of <see cref="ChatHistoryTruncationReducer"/> when directly
    /// invoking a <see cref="ChatCompletionAgent"/>.
    /// </summary>
    [Fact]
    public async Task TruncatedAgentReductionAsync()
    {
        // Define the agent
        ChatCompletionAgent agent = CreateTruncatingAgent(10, 5);

        await InvokeAgentAsync(agent, 50);
    }

    /// <summary>
    /// Demonstrate the use of <see cref="ChatHistorySummarizationReducer"/> when directly
    /// invoking a <see cref="ChatCompletionAgent"/>.
    /// </summary>
    [Fact]
    public async Task SummarizedAgentReductionAsync()
    {
        // Define the agent
        ChatCompletionAgent agent = CreateSummarizingAgent(10, 5);

        await InvokeAgentAsync(agent, 50);
    }

    /// <summary>
    /// Demonstrate the use of <see cref="ChatHistoryTruncationReducer"/> when using
    /// <see cref="AgentGroupChat"/> to invoke a <see cref="ChatCompletionAgent"/>.
    /// </summary>
    [Fact]
    public async Task TruncatedChatReductionAsync()
    {
        // Define the agent
        ChatCompletionAgent agent = CreateTruncatingAgent(10, 5);

        await InvokeChatAsync(agent, 50);
    }

    /// <summary>
    /// Demonstrate the use of <see cref="ChatHistorySummarizationReducer"/> when using
    /// <see cref="AgentGroupChat"/> to invoke a <see cref="ChatCompletionAgent"/>.
    /// </summary>
    [Fact]
    public async Task SummarizedChatReductionAsync()
    {
        // Define the agent
        ChatCompletionAgent agent = CreateSummarizingAgent(10, 5);

        await InvokeChatAsync(agent, 50);
    }

    // Proceed with dialog by directly invoking the agent and explicitly managing the history.
    private async Task InvokeAgentAsync(ChatCompletionAgent agent, int messageCount)
    {
        ChatHistory chat = [];

        int index = 1;
        while (index <= messageCount)
        {
            // Display the message count of the chat-history for visibility into reduction
            (bool isReduced, chat) = await agent.ReduceAsync(chat);
            Console.WriteLine($"\n@ Message Count: {chat.Count}");

            // Display summary messages (if present) when reduction has occurred
            if (isReduced)
            {
                int summaryIndex = 0;
                while (chat[summaryIndex].Metadata?.ContainsKey(ChatHistorySummarizationReducer.SummaryMetadataKey) ?? false)
                {
                    Console.WriteLine($"\tSummary: {chat[summaryIndex].Content}");
                    ++summaryIndex;
                }
            }

            // Display user input
            chat.Add(new ChatMessageContent(AuthorRole.User, $"{index}"));
            Console.WriteLine($"# {AuthorRole.User}: '{index}'");

            // Invoke and display assistant response
            await foreach (ChatMessageContent message in agent.InvokeAsync(chat))
            {
                chat.Add(message);
                Console.WriteLine($"# {message.Role} - {message.AuthorName ?? "*"}: '{message.Content}'");
            }

            index += 2;
        }
    }

    // Proceed with dialog with AgentGroupChat.
    private async Task InvokeChatAsync(ChatCompletionAgent agent, int messageCount)
    {
        AgentGroupChat chat = new();

        int lastHistoryCount = 0;

        int index = 1;
        while (index <= messageCount)
        {
            // Display the message count of the chat-history for visibility into reduction
            ChatMessageContent[] history = await chat.GetChatMessagesAsync(agent).ToArrayAsync();
            Console.WriteLine($"\n@ Message Count: {history.Length}");

            // Display summary messages (if present) when reduction has occurred
            if (lastHistoryCount < history.Length)
            {
                int summaryIndex = 0;
                while (history[summaryIndex].Metadata?.ContainsKey(ChatHistorySummarizationReducer.SummaryMetadataKey) ?? false)
                {
                    Console.WriteLine($"\tSummary: {history[summaryIndex].Content}");
                    ++summaryIndex;
                }
            }

            lastHistoryCount = history.Length;

            // Display user input
            chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, $"{index}"));
            Console.WriteLine($"# {AuthorRole.User}: '{index}'");

            // Invoke and display assistant response
            await foreach (ChatMessageContent message in chat.InvokeAsync(agent))
            {
                Console.WriteLine($"# {message.Role} - {message.AuthorName ?? "*"}: '{message.Content}'");
            }

            index += 2;
        }
    }

    private ChatCompletionAgent CreateSummarizingAgent(int reducerMessageCount, int reducerThresholdCount)
    {
        Kernel kernel = this.CreateKernelWithChatCompletion();
        return
            new()
            {
                Name = TranslatorName,
                Instructions = TranslatorInstructions,
                Kernel = kernel,
                HistoryReducer = new ChatHistorySummarizationReducer(kernel.GetRequiredService<IChatCompletionService>(), reducerMessageCount, reducerThresholdCount),
            };
    }

    private ChatCompletionAgent CreateTruncatingAgent(int reducerMessageCount, int reducerThresholdCount) =>
        new()
        {
            Name = TranslatorName,
            Instructions = TranslatorInstructions,
            Kernel = this.CreateKernelWithChatCompletion(),
            HistoryReducer = new ChatHistoryTruncationReducer(reducerMessageCount, reducerThresholdCount),
        };
}
