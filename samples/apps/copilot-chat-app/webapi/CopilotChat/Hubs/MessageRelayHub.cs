﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace SemanticKernel.Service.CopilotChat.Hubs;

/// <summary>
/// Represents a chat hub for real-time communication.
/// </summary>
public class MessageRelayHub : Hub
{
    private readonly string _receiveMessageClientCall = "ReceiveMessage";
    private readonly ILogger<MessageRelayHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRelayHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public MessageRelayHub(ILogger<MessageRelayHub> logger)
    {
        this._logger = logger;
    }

    /// <summary>
    /// Adds the user to the groups that they are a member of.
    /// Groups are identified by the chat ID.
    /// TODO: Retrieve the user ID from the claims and call this method
    /// from the OnConnectedAsync method instead of the frontend.
    /// </summary>
    /// <param name="chatId"></param>
    public async Task AddClientToGroupAsync(string chatId)
    {
        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, chatId);
    }

    /// <summary>
    /// Sends a message to all users except the sender.
    /// </summary>
    /// <param name="chatId">The ChatID used as group id for SignalR.</param>
    /// <param name="message">The message to send.</param>
    public async Task SendMessageAsync(string chatId, object message)
    {
        await Clients.OthersInGroup(chatId).SendAsync(_receiveMessageClientCall, message, chatId);
    }
}
