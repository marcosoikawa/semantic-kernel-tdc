﻿// Copyright (c) Microsoft. All rights reserved.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.Agents;

/// <summary>
/// A <see cref="KernelAgent"/> specialization based on <see cref="IChatCompletionService"/>.
/// </summary>
/// <remarks>
/// NOTE: Enable OpenAIPromptExecutionSettings.ToolCallBehavior for agent plugins.
/// (<see cref="ChatHistoryKernelAgent.Arguments"/>)
/// </remarks>
public sealed class ChatCompletionAgent : ChatHistoryKernelAgent
{
    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatMessageContent> InvokeAsync(
        ChatHistory history,
        KernelArguments? arguments = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        kernel ??= this.Kernel;
        arguments ??= this.Arguments;

        (IChatCompletionService chatCompletionService, PromptExecutionSettings? executionSettings) = this.GetChatCompletionService(kernel, arguments);

        ChatHistory chat = await this.SetupAgentChatHistoryAsync(history, arguments, kernel, cancellationToken).ConfigureAwait(false);

        int messageCount = chat.Count;

        this.Logger.LogAgentChatServiceInvokingAgent(nameof(InvokeAsync), this.Id, chatCompletionService.GetType());

        IReadOnlyList<ChatMessageContent> messages =
            await chatCompletionService.GetChatMessageContentsAsync(
                chat,
                executionSettings,
                kernel,
                cancellationToken).ConfigureAwait(false);

        this.Logger.LogAgentChatServiceInvokedAgent(nameof(InvokeAsync), this.Id, chatCompletionService.GetType(), messages.Count);

        // Capture mutated messages related function calling / tools
        for (int messageIndex = messageCount; messageIndex < chat.Count; messageIndex++)
        {
            ChatMessageContent message = chat[messageIndex];

            message.AuthorName = this.Name;

            history.Add(message);
        }

        foreach (ChatMessageContent message in messages ?? [])
        {
            message.AuthorName = this.Name;

            yield return message;
        }
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<StreamingChatMessageContent> InvokeStreamingAsync(
        ChatHistory history,
        KernelArguments? arguments = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        kernel ??= this.Kernel;
        arguments ??= this.Arguments;

        (IChatCompletionService chatCompletionService, PromptExecutionSettings? executionSettings) = this.GetChatCompletionService(kernel, arguments);

        ChatHistory chat = await this.SetupAgentChatHistoryAsync(history, arguments, kernel, cancellationToken).ConfigureAwait(false);

        int messageCount = chat.Count;

        this.Logger.LogAgentChatServiceInvokingAgent(nameof(InvokeAsync), this.Id, chatCompletionService.GetType());

        IAsyncEnumerable<StreamingChatMessageContent> messages =
            chatCompletionService.GetStreamingChatMessageContentsAsync(
                chat,
                executionSettings,
                kernel,
                cancellationToken);

        this.Logger.LogAgentChatServiceInvokedStreamingAgent(nameof(InvokeAsync), this.Id, chatCompletionService.GetType());

        await foreach (StreamingChatMessageContent message in messages.ConfigureAwait(false))
        {
            message.AuthorName = this.Name;

            yield return message;
        }

        // Capture mutated messages related function calling / tools
        for (int messageIndex = messageCount; messageIndex < chat.Count; messageIndex++)
        {
            ChatMessageContent message = chat[messageIndex];

            message.AuthorName = this.Name;

            history.Add(message);
        }
    }

    private (IChatCompletionService service, PromptExecutionSettings? executionSettings) GetChatCompletionService(Kernel kernel, KernelArguments? arguments)
    {
        // Need to provide a KernelFunction to the service selector as a container for the execution-settings.
        KernelFunction nullPrompt = KernelFunctionFactory.CreateFromPrompt("placeholder", arguments?.ExecutionSettings?.Values);
        (IChatCompletionService chatCompletionService, PromptExecutionSettings? executionSettings) =
            kernel.ServiceSelector.SelectAIService<IChatCompletionService>(
                kernel,
                nullPrompt,
                arguments ?? []);

        return (chatCompletionService, executionSettings);
    }

    private async Task<ChatHistory> SetupAgentChatHistoryAsync(
        IReadOnlyList<ChatMessageContent> history,
        KernelArguments? arguments,
        Kernel kernel,
        CancellationToken cancellationToken)
    {
        ChatHistory chat = [];

        string? instructions =
            this.Template == null ?
                null : // %%% this.Instructions :
                await this.Template.RenderAsync(kernel, arguments, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(instructions))
        {
            chat.Add(new ChatMessageContent(AuthorRole.System, instructions) { AuthorName = this.Name });
        }

        chat.AddRange(history);

        return chat;
    }
}
