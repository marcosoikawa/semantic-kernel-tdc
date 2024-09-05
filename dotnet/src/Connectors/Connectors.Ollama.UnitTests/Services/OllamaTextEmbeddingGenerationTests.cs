﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp.Models;
using Xunit;

namespace SemanticKernel.Connectors.Ollama.UnitTests;

public sealed class OllamaTextEmbeddingGenerationTests : IDisposable
{
    private readonly HttpMessageHandlerStub _messageHandlerStub;
    private readonly HttpClient _httpClient;

    public OllamaTextEmbeddingGenerationTests()
    {
        this._messageHandlerStub = new HttpMessageHandlerStub();
        this._messageHandlerStub.ResponseToReturn.Content = new StringContent(File.ReadAllText("TestData/embeddings_test_response.json"));
        this._httpClient = new HttpClient(this._messageHandlerStub, false);
    }

    [Fact]
    public async Task UserAgentHeaderShouldBeUsedAsync()
    {
        //Arrange
        var sut = new OllamaTextEmbeddingGenerationService(
            "fake-model",
            new Uri("http://localhost:11434"),
            httpClient: this._httpClient);

        //Act
        await sut.GenerateEmbeddingsAsync(new List<string> { "fake-text" });

        //Assert
        Assert.True(this._messageHandlerStub.RequestHeaders?.Contains("User-Agent"));

        var values = this._messageHandlerStub.RequestHeaders!.GetValues("User-Agent");
        var value = values.SingleOrDefault();
        Assert.Equal("Semantic-Kernel", value);
    }

    [Fact]
    public async Task WhenHttpClientDoesNotHaveBaseAddressProvidedEndpointShouldBeUsedAsync()
    {
        //Arrange
        this._httpClient.BaseAddress = null;
        var sut = new OllamaTextEmbeddingGenerationService("fake-model", new Uri("https://fake-random-test-host/fake-path/"), httpClient: this._httpClient);

        //Act
        await sut.GenerateEmbeddingsAsync(new List<string> { "fake-text" });

        //Assert
        Assert.StartsWith("https://fake-random-test-host/fake-path", this._messageHandlerStub.RequestUri?.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldSendPromptToServiceAsync()
    {
        //Arrange
        var sut = new OllamaTextEmbeddingGenerationService(
            "fake-model",
            new Uri("http://localhost:11434"),
            httpClient: this._httpClient);

        //Act
        await sut.GenerateEmbeddingsAsync(["fake-text"]);

        //Assert
        var requestPayload = JsonSerializer.Deserialize<GenerateEmbeddingRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(requestPayload);
        Assert.Equal("fake-text", requestPayload.Input[0]);
    }

    [Fact]
    public async Task ShouldHandleServiceResponseAsync()
    {
        //Arrange
        var sut = new OllamaTextEmbeddingGenerationService(
            "fake-model",
            new Uri("http://localhost:11434"),
            httpClient: this._httpClient);

        //Act
        var contents = await sut.GenerateEmbeddingsAsync(new List<string> { "fake-text" });

        //Assert
        Assert.NotNull(contents);
        Assert.Equal(2, contents.Count);

        var content = contents[0];
        Assert.Equal(5, content.Length);
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
        this._messageHandlerStub.Dispose();
    }
}
