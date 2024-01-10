﻿#region HEADER

// Copyright (c) Microsoft. All rights reserved.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Gemini.Core;
using Microsoft.SemanticKernel.Connectors.Gemini.Core.GoogleAI;
using SemanticKernel.UnitTests;
using Xunit;

namespace SemanticKernel.Connectors.Gemini.UnitTests.Core;

public sealed class GeminiClientEmbeddingsGenerationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpMessageHandlerStub _messageHandlerStub;
    private const string TestDataFilePath = "./TestData/embeddings_response.json";

    public GeminiClientEmbeddingsGenerationTests()
    {
        this._messageHandlerStub = new HttpMessageHandlerStub();
        this._messageHandlerStub.ResponseToReturn.Content = new StringContent(
            File.ReadAllText(TestDataFilePath));

        this._httpClient = new HttpClient(this._messageHandlerStub, false);
    }

    [Fact]
    public async Task ShouldSendModelIdInEachEmbeddingRequestAsync()
    {
        // Arrange
        var geminiConfiguration = new GeminiConfiguration("fake-api-key") { EmbeddingModelId = "fake-model" };
        GeminiClient client = this.CreateGeminiClient(geminiConfiguration);
        var dataToEmbed = new List<string>()
        {
            "Write a story about a magic backpack.",
            "Print color of backpack."
        };

        // Act
        await client.GenerateEmbeddingsAsync(dataToEmbed);

        // Assert
        var request = JsonSerializer.Deserialize<GeminiEmbeddingRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(request);
        Assert.Collection(request.Requests,
            item => Assert.Contains(geminiConfiguration.EmbeddingModelId, item.Model, StringComparison.Ordinal),
            item => Assert.Contains(geminiConfiguration.EmbeddingModelId, item.Model, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldReturnValidEmbeddingsResponseAsync()
    {
        // Arrange
        var geminiConfiguration = new GeminiConfiguration("fake-api-key") { EmbeddingModelId = "fake-model" };
        GeminiClient client = this.CreateGeminiClient(geminiConfiguration);
        var dataToEmbed = new List<string>()
        {
            "Write a story about a magic backpack.",
            "Print color of backpack."
        };

        // Act
        var embeddings = await client.GenerateEmbeddingsAsync(dataToEmbed);

        // Assert
        GeminiEmbeddingResponse testDataResponse = JsonSerializer.Deserialize<GeminiEmbeddingResponse>(
            await File.ReadAllTextAsync(TestDataFilePath))!;
        Assert.NotNull(embeddings);
        Assert.Collection(embeddings,
            values => Assert.Equal(testDataResponse.Embeddings[0].Values, values),
            values => Assert.Equal(testDataResponse.Embeddings[1].Values, values));
    }

    private GeminiClient CreateGeminiClient(GeminiConfiguration geminiConfiguration)
    {
        var client = new GeminiClient(
            httpClient: this._httpClient,
            configuration: geminiConfiguration,
            httpRequestFactory: new GoogleAIGeminiHttpRequestFactory(),
            endpointProvider: new GoogleAIGeminiEndpointProvider(geminiConfiguration.ApiKey));
        return client;
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
        this._messageHandlerStub.Dispose();
    }
}
