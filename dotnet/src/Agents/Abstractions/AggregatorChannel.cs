﻿// Copyright (c) Microsoft. All rights reserved.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Agents;

/// <summary>
/// Adapt channel contract to underlying <see cref="AgentChat"/>.
/// </summary>
internal sealed class AggregatorChannel(AgentChat chat) : AgentChannel<AggregatorAgent>
{
    private readonly AgentChat _chat = chat;

    /// <inheritdoc/>
    protected internal override IAsyncEnumerable<ChatMessageContent> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        return this._chat.GetChatMessagesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected internal override async IAsyncEnumerable<(bool IsVisible, ChatMessageContent Message)> InvokeAsync(AggregatorAgent agent, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatMessageContent? lastMessage = null;

        await foreach (ChatMessageContent message in this._chat.InvokeAsync(cancellationToken).ConfigureAwait(false))
        {
            // For AggregatorMode.Flat, the entire aggregated chat is merged into the owning chat.
            if (agent.Mode == AggregatorMode.Flat)
            {
                yield return (IsVisible: true, message);
            }

            lastMessage = message;
        }

        // For AggregatorMode.Nested, only the final message is merged into the owning chat.
        // The entire history is always preserved within nested chat, however.
        if (agent.Mode == AggregatorMode.Nested && lastMessage is not null)
        {
            ChatMessageContent message =
                new(lastMessage.Role, lastMessage.Items, lastMessage.ModelId, lastMessage.InnerContent, lastMessage.Encoding, lastMessage.Metadata)
                {
                    AuthorName = agent.Name
                };

            yield return (IsVisible: true, message);
        }
    }

    /// <inheritdoc/>
    protected internal override Task ReceiveAsync(IEnumerable<ChatMessageContent> history, CancellationToken cancellationToken = default)
    {
        // Always receive the initial history from the owning chat.
        this._chat.AddChatMessages([.. history]);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected internal override Task ResetAsync(CancellationToken cancellationToken = default) =>
        this._chat.ResetAsync(cancellationToken);
}
