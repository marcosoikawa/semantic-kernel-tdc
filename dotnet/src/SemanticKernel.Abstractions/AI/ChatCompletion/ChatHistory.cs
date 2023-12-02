﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

#pragma warning disable CA1710

namespace Microsoft.SemanticKernel.AI.ChatCompletion;

/// <summary>
/// Chat message history representation
/// </summary>
public class ChatHistory : List<ChatMessage>
{
    /// <summary>
    /// Creates a new instance of the <see cref="ChatHistory"/> class
    /// </summary>
    public ChatHistory()
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="ChatHistory"/> class with a system message
    /// </summary>
    /// <param name="systemMessage"></param>
    public ChatHistory(string systemMessage)
    {
        this.AddSystemMessage(systemMessage);
    }

    /// <summary>
    /// Add a message to the chat history
    /// </summary>
    /// <param name="authorRole">Role of the message author</param>
    /// <param name="content">Message content</param>
    /// <param name="additionalProperties">Dictionary for any additional message properties</param>
    public void AddMessage(AuthorRole authorRole, string content, IDictionary<string, string>? additionalProperties = null)
    {
        this.Add(new ChatMessage(authorRole, content, additionalProperties));
    }

    /// <summary>
    /// Add a message to the chat history
    /// </summary>
    /// <param name="chatContent">Chat content</param>
    /// <param name="additionalProperties">Additional properties</param>
    public void AddMessage(ChatContent chatContent, IDictionary<string, string>? additionalProperties = null)
    {
        this.Add(new ChatMessage(chatContent.Role, chatContent.Content, additionalProperties));
    }

    /// <summary>
    /// Insert a message into the chat history
    /// </summary>
    /// <param name="index">Index of the message to insert</param>
    /// <param name="authorRole">Role of the message author</param>
    /// <param name="content">Message content</param>
    /// <param name="additionalProperties">Dictionary for any additional message properties</param>
    public void InsertMessage(int index, AuthorRole authorRole, string content, IDictionary<string, string>? additionalProperties = null)
    {
        this.Insert(index, new ChatMessage(authorRole, content, additionalProperties));
    }

    /// <summary>
    /// Add a user message to the chat history
    /// </summary>
    /// <param name="content">Message content</param>
    public void AddUserMessage(string content)
    {
        this.AddMessage(AuthorRole.User, content);
    }

    /// <summary>
    /// Add an assistant message to the chat history
    /// </summary>
    /// <param name="content">Message content</param>
    public void AddAssistantMessage(string content)
    {
        this.AddMessage(AuthorRole.Assistant, content);
    }

    /// <summary>
    /// Add a system message to the chat history
    /// </summary>
    /// <param name="content">Message content</param>
    public void AddSystemMessage(string content)
    {
        this.AddMessage(AuthorRole.System, content);
    }
}
