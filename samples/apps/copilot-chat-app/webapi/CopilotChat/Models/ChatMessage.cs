﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SemanticKernel.Service.CopilotChat.Storage;

namespace SemanticKernel.Service.CopilotChat.Models;

/// <summary>
/// Information about a single chat message.
/// </summary>
public class ChatMessage : IStorageEntity
{
    /// <summary>
    /// Role of the author of a chat message.
    /// </summary>
    public enum AuthorRoles
    {
        /// <summary>
        /// The current user of the chat.
        /// </summary>
        User = 0,

        /// <summary>
        /// The bot.
        /// </summary>
        Bot,

        /// <summary>
        /// The participant who is not the current user nor the bot of the chat.
        /// </summary>
        Participant
    }

    /// <summary>
    /// Type of the chat message.
    /// </summary>
    public enum ChatMessageType
    {
        /// <summary>
        /// A standard message
        /// </summary>
        Message,

        /// <summary>
        /// A message for a Plan
        /// </summary>
        Plan,

        /// <summary>
        /// An uploaded document notification
        /// </summary>
        Document,
    }

    /// <summary>
    /// Timestamp of the message.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Id of the user who sent this message.
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; }

    /// <summary>
    /// Id of the chat this message belongs to.
    /// </summary>
    [JsonPropertyName("chatId")]
    public string ChatId { get; set; }

    /// <summary>
    /// Content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; }

    /// <summary>
    /// Id of the message.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Role of the author of the message.
    /// </summary>
    [JsonPropertyName("authorRole")]
    public AuthorRoles AuthorRole { get; set; }

    /// <summary>
    /// Prompt used to generate the message.
    /// Will be empty if the message is not generated by a prompt.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Type of the message.
    /// </summary>
    [JsonPropertyName("type")]
    public ChatMessageType Type { get; set; }

    /// <summary>
    /// Create a new chat message. Timestamp is automatically generated.
    /// </summary>
    /// <param name="userId">Id of the user who sent this message</param>
    /// <param name="chatId">The chat ID that this message belongs to</param>
    /// <param name="content">The message</param>
    /// <param name="prompt">The prompt used to generate the message</param>
    /// <param name="authorRole">Role of the author</param>
    /// <param name="type">Type of the message</param>
    public ChatMessage(string userId, string chatId, string content, string prompt = "", AuthorRoles authorRole = AuthorRoles.User, ChatMessageType type = ChatMessageType.Message)
    {
        this.Timestamp = DateTimeOffset.Now;
        this.UserId = userId;
        this.ChatId = chatId;
        this.Content = content;
        this.Id = Guid.NewGuid().ToString();
        this.Prompt = prompt;
        this.AuthorRole = authorRole;
        this.Type = type;
    }

    /// <summary>
    /// Create a new chat message for the bot response.
    /// </summary>
    /// <param name="chatId">The chat ID that this message belongs to</param>
    /// <param name="content">The message</param>
    /// <param name="prompt">The prompt used to generate the message</param>
    public static ChatMessage CreateBotResponseMessage(string chatId, string content, string prompt)
    {
        return new ChatMessage("bot", chatId, content, prompt, AuthorRoles.Bot, IsPlan(content) ? ChatMessageType.Plan : ChatMessageType.Message);
    }

    /// <summary>
    /// Serialize the object to a formatted string.
    /// </summary>
    /// <returns>A formatted string</returns>
    public string ToFormattedString()
    {
        var content = this.Content;
        if (this.Type == ChatMessageType.Document)
        {
            var documentDetails = DocumentMessageContent.FromString(content);
            content = $"Sent a file named \"{documentDetails?.Name}\" with a size of {documentDetails?.Size}.";
        }

        return $"[{this.Timestamp.ToString("G", CultureInfo.CurrentCulture)}]: {content}";
    }

    /// <summary>
    /// Serialize the object to a JSON string.
    /// </summary>
    /// <returns>A serialized json string</returns>
    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }

    /// <summary>
    /// Deserialize a JSON string to a ChatMessage object.
    /// </summary>
    /// <param name="json">A json string</param>
    /// <returns>A ChatMessage object</returns>
    public static ChatMessage? FromString(string json)
    {
        return JsonSerializer.Deserialize<ChatMessage>(json);
    }

    /// <summary>
    /// Check if the response is a Plan.
    /// This is a copy of the `isPlan` function on the frontend.
    /// </summary>
    /// <param name="response">The response from the bot.</param>
    /// <returns>True if the response represents  Plan, false otherwise.</returns>
    private static bool IsPlan(string response)
    {
        var planPrefix = "proposedPlan\":";
        return response.IndexOf(planPrefix, StringComparison.Ordinal) != -1;
    }
}
