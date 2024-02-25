﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.GoogleVertexAI;
using Moq;
using Xunit;

namespace SemanticKernel.Connectors.GoogleVertexAI.UnitTests.Core.Gemini.Common;

[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
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
        var client = this.CreateChatCompletionClient();
        var chatHistory = CreateSampleChatHistory();

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
        var client = this.CreateChatCompletionClient();
        var chatHistory = CreateSampleChatHistory();

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
        var client = this.CreateChatCompletionClient();
        var chatHistory = CreateSampleChatHistory();

        // Act
        var chatMessageContents = await client.GenerateChatMessageAsync(chatHistory);

        // Assert
        GeminiResponse testDataResponse = JsonSerializer.Deserialize<GeminiResponse>(
            await File.ReadAllTextAsync(ChatTestDataFilePath))!;
        var testDataCandidate = testDataResponse.Candidates![0];
        var textContent = chatMessageContents.SingleOrDefault();
        Assert.NotNull(textContent);
        var metadata = textContent.Metadata as GeminiMetadata;
        Assert.NotNull(metadata);
        Assert.Equal(testDataResponse.PromptFeedback!.BlockReason, metadata.PromptFeedbackBlockReason);
        Assert.Equal(testDataCandidate.FinishReason, metadata.FinishReason);
        Assert.Equal(testDataCandidate.Index, metadata.Index);
        Assert.True(metadata.ResponseSafetyRatings!.Count
                    == testDataCandidate.SafetyRatings!.Count);
        Assert.True(metadata.PromptFeedbackSafetyRatings!.Count
                    == testDataResponse.PromptFeedback.SafetyRatings.Count);
        for (var i = 0; i < metadata.ResponseSafetyRatings.Count; i++)
        {
            Assert.Equal(testDataCandidate.SafetyRatings[i].Block, metadata.ResponseSafetyRatings[i].Block);
            Assert.Equal(testDataCandidate.SafetyRatings[i].Category, metadata.ResponseSafetyRatings[i].Category);
            Assert.Equal(testDataCandidate.SafetyRatings[i].Probability, metadata.ResponseSafetyRatings[i].Probability);
        }

        for (var i = 0; i < metadata.PromptFeedbackSafetyRatings.Count; i++)
        {
            Assert.Equal(testDataResponse.PromptFeedback.SafetyRatings[i].Block, metadata.PromptFeedbackSafetyRatings[i].Block);
            Assert.Equal(testDataResponse.PromptFeedback.SafetyRatings[i].Category, metadata.PromptFeedbackSafetyRatings[i].Category);
            Assert.Equal(testDataResponse.PromptFeedback.SafetyRatings[i].Probability, metadata.PromptFeedbackSafetyRatings[i].Probability);
        }

        Assert.Equal(testDataResponse.UsageMetadata!.PromptTokenCount, metadata.PromptTokenCount);
        Assert.Equal(testDataCandidate.TokenCount, metadata.CurrentCandidateTokenCount);
        Assert.Equal(testDataResponse.UsageMetadata.CandidatesTokenCount, metadata.CandidatesTokenCount);
        Assert.Equal(testDataResponse.UsageMetadata.TotalTokenCount, metadata.TotalTokenCount);
    }

    [Fact]
    public async Task ShouldReturnValidDictionaryMetadataAsync()
    {
        // Arrange
        var client = this.CreateChatCompletionClient();
        var chatHistory = CreateSampleChatHistory();

        // Act
        var chatMessageContents = await client.GenerateChatMessageAsync(chatHistory);

        // Assert
        GeminiResponse testDataResponse = JsonSerializer.Deserialize<GeminiResponse>(
            await File.ReadAllTextAsync(ChatTestDataFilePath))!;
        var testDataCandidate = testDataResponse.Candidates![0];
        var textContent = chatMessageContents.SingleOrDefault();
        Assert.NotNull(textContent);
        var metadata = textContent.Metadata;
        Assert.NotNull(metadata);
        Assert.Equal(testDataResponse.PromptFeedback!.BlockReason, metadata[nameof(GeminiMetadata.PromptFeedbackBlockReason)]);
        Assert.Equal(testDataCandidate.FinishReason, metadata[nameof(GeminiMetadata.FinishReason)]);
        Assert.Equal(testDataCandidate.Index, metadata[nameof(GeminiMetadata.Index)]);
        var responseSafetyRatings = (IList<GeminiSafetyRating>)metadata[nameof(GeminiMetadata.ResponseSafetyRatings)]!;
        for (var i = 0; i < responseSafetyRatings.Count; i++)
        {
            Assert.Equal(testDataCandidate.SafetyRatings![i].Block, responseSafetyRatings[i].Block);
            Assert.Equal(testDataCandidate.SafetyRatings[i].Category, responseSafetyRatings[i].Category);
            Assert.Equal(testDataCandidate.SafetyRatings[i].Probability, responseSafetyRatings[i].Probability);
        }

        var promptSafetyRatings = (IList<GeminiSafetyRating>)metadata[nameof(GeminiMetadata.PromptFeedbackSafetyRatings)]!;
        for (var i = 0; i < promptSafetyRatings.Count; i++)
        {
            Assert.Equal(testDataResponse.PromptFeedback.SafetyRatings[i].Block, promptSafetyRatings[i].Block);
            Assert.Equal(testDataResponse.PromptFeedback.SafetyRatings[i].Category, promptSafetyRatings[i].Category);
            Assert.Equal(testDataResponse.PromptFeedback.SafetyRatings[i].Probability, promptSafetyRatings[i].Probability);
        }

        Assert.Equal(testDataResponse.UsageMetadata!.PromptTokenCount, metadata[nameof(GeminiMetadata.PromptTokenCount)]);
        Assert.Equal(testDataCandidate.TokenCount, metadata[nameof(GeminiMetadata.CurrentCandidateTokenCount)]);
        Assert.Equal(testDataResponse.UsageMetadata.CandidatesTokenCount, metadata[nameof(GeminiMetadata.CandidatesTokenCount)]);
        Assert.Equal(testDataResponse.UsageMetadata.TotalTokenCount, metadata[nameof(GeminiMetadata.TotalTokenCount)]);
    }

    [Fact]
    public async Task ShouldReturnResponseWithModelIdAsync()
    {
        // Arrange
        string modelId = "fake-model";
        var client = this.CreateChatCompletionClient(modelId: modelId);
        var chatHistory = CreateSampleChatHistory();

        // Act
        var chatMessageContents = await client.GenerateChatMessageAsync(chatHistory);

        // Assert
        var chatMessageContent = chatMessageContents.SingleOrDefault();
        Assert.NotNull(chatMessageContent);
        Assert.Equal(modelId, chatMessageContent.ModelId);
    }

    [Fact]
    public async Task ShouldUsePromptExecutionSettingsAsync()
    {
        // Arrange
        var client = this.CreateChatCompletionClient();
        var chatHistory = CreateSampleChatHistory();
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 102,
            Temperature = 0.45,
            TopP = 0.6
        };

        // Act
        await client.GenerateChatMessageAsync(chatHistory, executionSettings: executionSettings);

        // Assert
        var geminiRequest = JsonSerializer.Deserialize<GeminiRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(geminiRequest);
        Assert.Equal(executionSettings.MaxTokens, geminiRequest.Configuration!.MaxOutputTokens);
        Assert.Equal(executionSettings.Temperature, geminiRequest.Configuration!.Temperature);
        Assert.Equal(executionSettings.TopP, geminiRequest.Configuration!.TopP);
    }

    [Fact]
    public async Task ShouldThrowInvalidOperationExceptionIfChatHistoryContainsOnlySystemMessageAsync()
    {
        // Arrange
        var client = this.CreateChatCompletionClient();
        var chatHistory = new ChatHistory("System message");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateChatMessageAsync(chatHistory));
    }

    [Fact]
    public async Task ShouldThrowInvalidOperationExceptionIfChatHistoryContainsOnlyManySystemMessagesAsync()
    {
        // Arrange
        var client = this.CreateChatCompletionClient();
        var chatHistory = new ChatHistory("System message");
        chatHistory.AddSystemMessage("System message 2");
        chatHistory.AddSystemMessage("System message 3");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateChatMessageAsync(chatHistory));
    }

    [Fact]
    public async Task ShouldThrowInvalidOperationExceptionIfChatHistoryContainsMoreThanOneSystemMessageAsync()
    {
        var client = this.CreateChatCompletionClient();
        var chatHistory = new ChatHistory("System message");
        chatHistory.AddSystemMessage("System message 2");
        chatHistory.AddSystemMessage("System message 3");
        chatHistory.AddUserMessage("hello");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateChatMessageAsync(chatHistory));
    }

    [Fact]
    public async Task ShouldPassConvertedSystemMessageToUserMessageToRequestAsync()
    {
        // Arrange
        var client = this.CreateChatCompletionClient();
        string message = "System message";
        var chatHistory = new ChatHistory(message);
        chatHistory.AddUserMessage("Hello");

        // Act
        await client.GenerateChatMessageAsync(chatHistory);

        // Assert
        GeminiRequest? request = JsonSerializer.Deserialize<GeminiRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(request);
        var systemMessage = request.Contents[0].Parts[0].Text;
        var messageRole = request.Contents[0].Role;
        Assert.Equal(AuthorRole.User, messageRole);
        Assert.Equal(message, systemMessage);
    }

    [Fact]
    public async Task ShouldThrowNotSupportedIfChatHistoryHaveIncorrectOrderAsync()
    {
        // Arrange
        var client = this.CreateChatCompletionClient();
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
        var client = this.CreateChatCompletionClient();
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
        var client = this.CreateChatCompletionClient();
        var chatHistory = new ChatHistory();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GenerateChatMessageAsync(chatHistory));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public async Task ShouldThrowArgumentExceptionIfExecutionSettingMaxTokensIsLessThanOneAsync(int? maxTokens)
    {
        // Arrange
        var client = this.CreateChatCompletionClient();
        GeminiPromptExecutionSettings executionSettings = new()
        {
            MaxTokens = maxTokens
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GenerateChatMessageAsync(CreateSampleChatHistory(), executionSettings: executionSettings));
    }

    [Fact]
    public async Task ShouldCallGetChatEndpointAsync()
    {
        // Arrange
        var endpointProviderMock = new Mock<IEndpointProvider>();
        endpointProviderMock.Setup(x => x.GetGeminiChatCompletionEndpoint(It.IsAny<string>()))
            .Returns(new Uri("https://fake-endpoint.com/"));
        var sut = this.CreateChatCompletionClient(endpointProvider: endpointProviderMock.Object);

        // Act
        await sut.GenerateChatMessageAsync(CreateSampleChatHistory());

        // Assert
        endpointProviderMock.VerifyAll();
    }

    [Fact]
    public async Task ShouldCallCreatePostRequestAsync()
    {
        // Arrange
        var requestFactoryMock = new Mock<IHttpRequestFactory>();
        requestFactoryMock.Setup(x => x.CreatePost(It.IsAny<object>(), It.IsAny<Uri>()))
            .Returns(new HttpRequestMessage(HttpMethod.Post, new Uri("https://fake-endpoint.com/")));
        var sut = this.CreateChatCompletionClient(httpRequestFactory: requestFactoryMock.Object);

        // Act
        await sut.GenerateChatMessageAsync(CreateSampleChatHistory());

        // Assert
        requestFactoryMock.VerifyAll();
    }

    private static ChatHistory CreateSampleChatHistory()
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello");
        chatHistory.AddAssistantMessage("Hi");
        chatHistory.AddUserMessage("How are you?");
        return chatHistory;
    }

    private GeminiChatCompletionClient CreateChatCompletionClient(
        string modelId = "fake-model",
        string apiKey = "fake-api-key",
        IEndpointProvider? endpointProvider = null,
        IHttpRequestFactory? httpRequestFactory = null)
    {
        return new GeminiChatCompletionClient(
            httpClient: this._httpClient,
            modelId: modelId,
            httpRequestFactory: httpRequestFactory ?? new FakeHttpRequestFactory(),
            endpointProvider: endpointProvider ?? new FakeEndpointProvider());
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
        this._messageHandlerStub.Dispose();
    }
}
