﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Experimental.Agents;

/// <summary>
/// Represent an agent that is built around the SK ChatCompletion API and leverages the API's capabilities.
/// </summary>
public sealed class ChatCompletionAgent
{
    private readonly Kernel _kernel;
    private readonly string _instructions;
    private readonly PromptExecutionSettings? _promptExecutionSettings;
    private readonly IChatCompletionService _chatCompletionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatCompletionAgent"/> class.
    /// </summary>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use by the agent.</param>
    /// <param name="instructions">The instructions for the agent.</param>
    /// <param name="executionSettings">The optional execution settings for the agent. If not provided, default settings will be used.</param>
    /// <param name="chatCompletionService">The optional chat completion service. If not provided, it'll be resolved from the services registered on kernel.</param>
    public ChatCompletionAgent(Kernel kernel, string instructions, PromptExecutionSettings? executionSettings = null, IChatCompletionService? chatCompletionService = null)
    {
        Verify.NotNull(kernel, nameof(kernel));
        this._kernel = kernel;

        Verify.NotNullOrWhiteSpace(instructions, nameof(instructions));
        this._instructions = instructions;

        this._promptExecutionSettings = executionSettings;

        this._chatCompletionService = chatCompletionService ?? kernel.GetRequiredService<IChatCompletionService>();
    }

    /// <summary>
    /// Invokes the agent to process the given messages and generate a response.
    /// </summary>
    /// <param name="messages">A list of the messages for the agent to process.</param>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>List of messages representing the agent's response.</returns>
    public async Task<IReadOnlyList<ChatMessageContent>> InvokeAsync(IReadOnlyList<ChatMessageContent> messages, CancellationToken cancellationToken = default)
    {
        var chat = new ChatHistory(this._instructions);
        chat.AddRange(messages);

        var chatMessageContent = await this._chatCompletionService.GetChatMessageContentsAsync(
            chat,
            this._promptExecutionSettings,
            this._kernel,
            cancellationToken).ConfigureAwait(false);

        return chatMessageContent;
    }
}
