﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Xunit;

namespace SemanticKernel.UnitTests.AI.ChatCompletion;

/// <summary>
/// Unit tests of <see cref="ChatHistory"/>.
/// </summary>
public class ChatHistoryTests
{
    [Fact]
    public void ItCanBeSerialised()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, "Hello");
        chatHistory.AddMessage(AuthorRole.Assistant, "Hi");

        // Act
        var chatHistoryJson = JsonSerializer.Serialize(chatHistory);

        // Assert
        Assert.NotNull(chatHistoryJson);
        Assert.Equal("[{\"Role\":{\"Label\":\"user\"},\"Content\":\"Hello\",\"AdditionalProperties\":null},{\"Role\":{\"Label\":\"assistant\"},\"Content\":\"Hi\",\"AdditionalProperties\":null}]", chatHistoryJson);
    }

    [Fact]
    public void ItCanBeDeserialised()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.User, "Hello");
        chatHistory.AddMessage(AuthorRole.Assistant, "Hi");
        var chatHistoryJson = JsonSerializer.Serialize(chatHistory, options);

        // Act
        var chatHistoryDeserialised = JsonSerializer.Deserialize<ChatHistory>(chatHistoryJson, options);

        // Assert
        Assert.NotNull(chatHistoryDeserialised);
        Assert.Equal(chatHistory.Count, chatHistoryDeserialised.Count);
        for (var i = 0; i < chatHistory.Count; i++)
        {
            Assert.Equal(chatHistory[i].Role.Label, chatHistoryDeserialised[i].Role.Label);
            Assert.Equal(chatHistory[i].Content, chatHistoryDeserialised[i].Content);
        }
    }

    //[Fact]
    //public void ItAddMessageWithNullContent()
    //{
    //    // Arrange
    //    var chatHistory = new ChatHistory();

    //    // Act
    //    chatHistory.AddMessage(AuthorRole.User, null);

    //    // Assert
    //}

    //[Fact]
    //public void ItCreateMessageWithNullContent()
    //{
    //    // Arrange
    //    // Act
    //    var chatMessage = new ChatMessage(AuthorRole.User, null);

    //    // Assert
    //}
}
