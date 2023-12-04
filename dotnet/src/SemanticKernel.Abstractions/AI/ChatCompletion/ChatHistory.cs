﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

#pragma warning disable CA1033 // Interface methods should be callable by child types
#pragma warning disable CA1710 // Identifiers should have correct suffix

namespace Microsoft.SemanticKernel.AI.ChatCompletion;

/// <summary>
/// Provides a history of chat messages from a chat conversation.
/// </summary>
public class ChatHistory : IList<ChatContent>, IReadOnlyList<ChatContent>
{
    /// <summary>The messages.</summary>
    private readonly List<ChatContent> _messages;

    /// <summary>Initializes an empty history.</summary>
    /// <summary>
    /// Creates a new instance of the <see cref="ChatHistory"/> class
    /// </summary>
    public ChatHistory()
    {
        this._messages = new();
    }

    /// <summary>
    /// Creates a new instance of the <see cref="ChatHistory"/> class with a system message
    /// </summary>
    /// <param name="systemMessage"></param>
    public ChatHistory(string systemMessage)
    {
        Verify.NotNullOrWhiteSpace(systemMessage);

        this._messages = new();
        this.AddSystemMessage(systemMessage);
    }

    /// <summary>Initializes the history will all of the specified messages.</summary>
    /// <param name="messages">The messages to copy into the history.</param>
    /// <exception cref="ArgumentNullException"><paramref name="messages"/> is null.</exception>
    public ChatHistory(IEnumerable<ChatContent> messages)
    {
        Verify.NotNull(messages);
        this._messages = new(messages);
    }

    /// <summary>Gets the number of messages in the history.</summary>
    public int Count => this._messages.Count;

    /// <summary>
    /// Add a message to the chat history
    /// </summary>
    /// <param name="chatContent">Chat content</param>
    public void AddMessage(ChatContent chatContent)
    {
        this.Add(chatContent);
    }

    /// <summary>
    /// <param name="authorRole">Role of the message author</param>
    /// <param name="content">Message content</param>
    /// <param name="encoding"></param>
    /// <param name="metadata">Dictionary for any additional metadata</param>
    /// </summary>
    public void AddMessage(AuthorRole authorRole, string content, Encoding? encoding = null, IDictionary<string, object?>? metadata = null) =>
        this.Add(new ChatContent(authorRole, content, null, encoding, metadata));

    /// <summary>
    /// Add a user message to the chat history
    /// </summary>
    /// <param name="content">Message content</param>
    public void AddUserMessage(string content) =>
        this.AddMessage(AuthorRole.User, content);

    /// <summary>
    /// Add an assistant message to the chat history
    /// </summary>
    /// <param name="content">Message content</param>
    public void AddAssistantMessage(string content) =>
        this.AddMessage(AuthorRole.Assistant, content);

    /// <summary>
    /// Add a system message to the chat history
    /// </summary>
    /// <param name="content">Message content</param>
    public void AddSystemMessage(string content) =>
        this.AddMessage(AuthorRole.System, content);

    /// <summary>Adds a message to the history.</summary>
    /// <param name="item">The message to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void Add(ChatContent item)
    {
        Verify.NotNull(item);
        this._messages.Add(item);
    }

    /// <summary>Adds the messages to the history.</summary>
    /// <param name="items">The collection whose messages should be added to the history.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    public void AddRange(IEnumerable<ChatContent> items)
    {
        Verify.NotNull(items);
        this._messages.AddRange(items);
    }

    /// <summary>Inserts a message into the history at the specified index.</summary>
    /// <param name="index">The index at which the item should be inserted.</param>
    /// <param name="item">The message to insert.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void Insert(int index, ChatContent item)
    {
        Verify.NotNull(item);
        this._messages.Insert(index, item);
    }

    /// <summary>
    /// Copies all of the messages in the history to an array, starting at the specified destination array index.
    /// </summary>
    /// <param name="array">The destination array into which the messages should be copied.</param>
    /// <param name="arrayIndex">The zero-based index into <paramref name="array"/> at which copying should begin.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is null.</exception>
    /// <exception cref="ArgumentException">The number of messages in the history is greater than the available space from <paramref name="arrayIndex"/> to the end of <paramref name="array"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception>
    public void CopyTo(ChatContent[] array, int arrayIndex) => this._messages.CopyTo(array, arrayIndex);

    /// <summary>Removes all messages from the history.</summary>
    public void Clear() => this._messages.Clear();

    /// <summary>Gets or sets the message at the specified index in the history.</summary>
    /// <param name="index">The index of the message to get or set.</param>
    /// <returns>The message at the specified index.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The <paramref name="index"/> was not valid for this history.</exception>
    public ChatContent this[int index]
    {
        get => this._messages[index];
        set
        {
            Verify.NotNull(value);
            this._messages[index] = value;
        }
    }

    /// <summary>Determines whether a message is in the history.</summary>
    /// <param name="item">The message to locate.</param>
    /// <returns>true if the message is found in the history; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public bool Contains(ChatContent item)
    {
        Verify.NotNull(item);
        return this._messages.Contains(item);
    }

    /// <summary>Searches for the specified message and returns the index of the first occurrence.</summary>
    /// <param name="item">The message to locate.</param>
    /// <returns>The index of the first found occurrence of the specified message; -1 if the message could not be found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public int IndexOf(ChatContent item)
    {
        Verify.NotNull(item);
        return this._messages.IndexOf(item);
    }

    /// <summary>Removes the message at the specified index from the history.</summary>
    /// <param name="index">The index of the message to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">The <paramref name="index"/> was not valid for this history.</exception>
    public void RemoveAt(int index) => this._messages.RemoveAt(index);

    /// <summary>Removes the first occurrence of the specified message from the history.</summary>
    /// <param name="item">The message to remove from the history.</param>
    /// <returns>true if the item was successfully removed; false if it wasn't located in the history.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public bool Remove(ChatContent item)
    {
        Verify.NotNull(item);
        return this._messages.Remove(item);
    }

    /// <inheritdoc/>
    bool ICollection<ChatContent>.IsReadOnly => false;

    /// <inheritdoc/>
    IEnumerator<ChatContent> IEnumerable<ChatContent>.GetEnumerator() => this._messages.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this._messages.GetEnumerator();
}
