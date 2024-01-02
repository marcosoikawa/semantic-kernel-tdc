﻿#region HEADER

// Copyright (c) Microsoft. All rights reserved.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Gemini;
using Microsoft.SemanticKernel.Connectors.Gemini.Core;
using SemanticKernel.UnitTests;
using Xunit;

namespace SemanticKernel.Connectors.Gemini.UnitTests.Core;

public sealed class GeminiClientChatGenerationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpMessageHandlerStub _messageHandlerStub;
    private const string ChatTestDataFilePath = "./TestData/chat_one_response.json";

    public GeminiClientChatGenerationTests()
    {
        this._messageHandlerStub = new HttpMessageHandlerStub();
        this._messageHandlerStub.ResponseToReturn.Content = new StringContent(
            File.ReadAllText(ChatTestDataFilePath));

        this._httpClient = new HttpClient(this._messageHandlerStub, false);
    }

    [Fact]
    public async Task ShouldContainRolesInRequestAsync()
    {
        // Arrange
        this._messageHandlerStub.ResponseToReturn.Content = new StringContent(
            await File.ReadAllTextAsync(ChatTestDataFilePath));
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var chatHistory = CreateChatHistory();

        // Act
        await client.GenerateChatMessageAsync(chatHistory);

        // Assert
        GeminiRequest? request = JsonSerializer.Deserialize<GeminiRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(request);
        Assert.Collection(request.Contents,
            item => Assert.Equal(chatHistory[0].Role, item.Role),
            item => Assert.Equal(chatHistory[1].Role, item.Role),
            item => Assert.Equal(chatHistory[2].Role, item.Role));
    }

    [Fact]
    public async Task ShouldReturnValidChatResponseAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var chatHistory = CreateChatHistory();

        // Act
        var response = await client.GenerateChatMessageAsync(chatHistory);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("I'm fine, thanks. How are you?", response[0].Content);
        Assert.Equal(AuthorRole.Assistant, response[0].Role);
    }

    [Fact]
    public async Task ShouldReturnValidGeminiMetadataAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var chatHistory = CreateChatHistory();

        // Act
        var chatMessageContents = await client.GenerateChatMessageAsync(chatHistory);

        // Assert
        GeminiResponse testDataResponse = JsonSerializer.Deserialize<GeminiResponse>(
            await File.ReadAllTextAsync(ChatTestDataFilePath))!;
        var textContent = chatMessageContents.SingleOrDefault();
        Assert.NotNull(textContent);
        var metadata = textContent.Metadata as GeminiMetadata;
        Assert.NotNull(metadata);
        Assert.Equal(testDataResponse.PromptFeedback!.BlockReason, metadata.PromptFeedbackBlockReason);
        Assert.Equal(testDataResponse.Candidates[0].FinishReason, metadata.FinishReason);
        Assert.Equal(testDataResponse.Candidates[0].Index, metadata.Index);
        Assert.True(metadata.ResponseSafetyRatings!.Count
                    == testDataResponse.Candidates[0].SafetyRatings.Count);
        Assert.True(metadata.PromptFeedbackSafetyRatings!.Count
                    == testDataResponse.PromptFeedback.SafetyRatings.Count);
        Assert.Equal(testDataResponse.UsageMetadata!.PromptTokenCount, metadata.PromptTokenCount);
        Assert.Equal(testDataResponse.Candidates[0].TokenCount, metadata.CurrentCandidateTokenCount);
        Assert.Equal(testDataResponse.UsageMetadata.CandidatesTokenCount, metadata.CandidatesTokenCount);
        Assert.Equal(testDataResponse.UsageMetadata.TotalTokenCount, metadata.TotalTokenCount);
    }

    [Fact]
    public async Task ShouldReturnValidDictionaryMetadataAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var chatHistory = CreateChatHistory();

        // Act
        var chatMessageContents = await client.GenerateChatMessageAsync(chatHistory);

        // Assert
        GeminiResponse testDataResponse = JsonSerializer.Deserialize<GeminiResponse>(
            await File.ReadAllTextAsync(ChatTestDataFilePath))!;
        var textContent = chatMessageContents.SingleOrDefault();
        Assert.NotNull(textContent);
        var metadata = textContent.Metadata;
        Assert.NotNull(metadata);
        Assert.Equal(testDataResponse.PromptFeedback!.BlockReason, metadata[nameof(GeminiMetadata.PromptFeedbackBlockReason)]);
        Assert.Equal(testDataResponse.Candidates[0].FinishReason, metadata[nameof(GeminiMetadata.FinishReason)]);
        Assert.Equal(testDataResponse.Candidates[0].Index, metadata[nameof(GeminiMetadata.Index)]);
        Assert.True(((IList<GeminiMetadataSafetyRating>)metadata[nameof(GeminiMetadata.ResponseSafetyRatings)]!).Count
                    == testDataResponse.Candidates[0].SafetyRatings.Count);
        Assert.True(((IList<GeminiMetadataSafetyRating>)metadata[nameof(GeminiMetadata.PromptFeedbackSafetyRatings)]!).Count
                    == testDataResponse.PromptFeedback.SafetyRatings.Count);
        Assert.Equal(testDataResponse.UsageMetadata!.PromptTokenCount, metadata[nameof(GeminiMetadata.PromptTokenCount)]);
        Assert.Equal(testDataResponse.Candidates[0].TokenCount, metadata[nameof(GeminiMetadata.CurrentCandidateTokenCount)]);
        Assert.Equal(testDataResponse.UsageMetadata.CandidatesTokenCount, metadata[nameof(GeminiMetadata.CandidatesTokenCount)]);
        Assert.Equal(testDataResponse.UsageMetadata.TotalTokenCount, metadata[nameof(GeminiMetadata.TotalTokenCount)]);
    }

    [Fact]
    public async Task ShouldReturnResponseWithModelIdAsync()
    {
        // Arrange
        string modelId = "fake-model";
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: modelId);
        var chatHistory = CreateChatHistory();

        // Act
        var chatMessageContents = await client.GenerateChatMessageAsync(chatHistory);

        // Assert
        var chatMessageContent = chatMessageContents.SingleOrDefault();
        Assert.NotNull(chatMessageContent);
        Assert.Equal(modelId, chatMessageContent.ModelId);
    }

    [Fact]
    public async Task ShouldReturnResponseWithValidInnerContentAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var chatHistory = CreateChatHistory();

        // Act
        var chatMessageContents = await client.GenerateChatMessageAsync(chatHistory);

        // Assert
        string testDataResponseJson = JsonSerializer.Serialize(JsonSerializer.Deserialize<GeminiResponse>(
            await File.ReadAllTextAsync(ChatTestDataFilePath))!.Candidates[0]);
        var textContent = chatMessageContents.SingleOrDefault();
        Assert.NotNull(textContent);
        Assert.Equal(testDataResponseJson, JsonSerializer.Serialize(textContent.InnerContent));
    }

    [Fact]
    public async Task ShouldUsePromptExecutionSettingsAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var chatHistory = CreateChatHistory();
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 102,
            Temperature = 0.45,
            TopP = 0.6
        };

        // Act
        await client.GenerateChatMessageAsync(chatHistory, executionSettings);

        // Assert
        var geminiRequest = JsonSerializer.Deserialize<GeminiRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(geminiRequest);
        Assert.Equal(executionSettings.MaxTokens, geminiRequest.Configuration!.MaxOutputTokens);
        Assert.Equal(executionSettings.Temperature, geminiRequest.Configuration!.Temperature);
        Assert.Equal(executionSettings.TopP, geminiRequest.Configuration!.TopP);
    }

    [Fact]
    public async Task ShouldThrowNotSupportedIfChatHistoryContainSystemMessageAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var chatHistory = new ChatHistory("System message");

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(
            () => client.GenerateChatMessageAsync(chatHistory));
    }

    [Fact]
    public async Task ShouldThrowNotSupportedIfChatHistoryHaveIncorrectOrderAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello");
        chatHistory.AddAssistantMessage("Hi");
        chatHistory.AddAssistantMessage("Hi me again");
        chatHistory.AddUserMessage("How are you?");

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(
            () => client.GenerateChatMessageAsync(chatHistory));
    }

    [Fact]
    public async Task ShouldThrowNotSupportedIfChatHistoryNotEndWithUserMessageAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello");
        chatHistory.AddAssistantMessage("Hi");

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(
            () => client.GenerateChatMessageAsync(chatHistory));
    }

    [Fact]
    public async Task ShouldThrowArgumentExceptionIfChatHistoryIsEmptyAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var chatHistory = new ChatHistory();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GenerateChatMessageAsync(chatHistory));
    }

    private static ChatHistory CreateChatHistory()
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello");
        chatHistory.AddAssistantMessage("Hi");
        chatHistory.AddUserMessage("How are you?");
        return chatHistory;
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
        this._messageHandlerStub.Dispose();
    }
}
