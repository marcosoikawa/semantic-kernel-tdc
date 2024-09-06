﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.TextGeneration;
using OllamaSharp.Models;
using Xunit;

namespace SemanticKernel.Connectors.Ollama.UnitTests;

public sealed class OllamaTextGenerationTests : IDisposable
{
    private readonly HttpMessageHandlerStub _messageHandlerStub;
    private readonly HttpClient _httpClient;

    public OllamaTextGenerationTests()
    {
        this._messageHandlerStub = new HttpMessageHandlerStub();
        this._messageHandlerStub.ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StreamContent(File.OpenRead("TestData/text_generation_test_response_stream.txt"))
        };
        this._httpClient = new HttpClient(this._messageHandlerStub, false) { BaseAddress = new Uri("http://localhost:11434") };
    }

    [Fact]
    public async Task ShouldSendPromptToServiceAsync()
    {
        //Arrange
        var expectedModel = "phi3";
        var sut = new OllamaTextGenerationService(
            expectedModel,
            httpClient: this._httpClient);

        //Act
        await sut.GetTextContentsAsync("fake-text");

        //Assert
        var requestPayload = JsonSerializer.Deserialize<GenerateRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(requestPayload);
        Assert.Equal("fake-text", requestPayload.Prompt);
    }

    [Fact]
    public async Task ShouldHandleServiceResponseAsync()
    {
        //Arrange
        var sut = new OllamaTextGenerationService(
            "fake-model",
            httpClient: this._httpClient);

        //Act
        var contents = await sut.GetTextContentsAsync("fake-test");

        //Assert
        Assert.NotNull(contents);

        var content = contents.SingleOrDefault();
        Assert.NotNull(content);
        Assert.Equal("This is test completion response", content.Text);
    }

    [Fact]
    public async Task GetTextContentsShouldHaveModelIdDefinedAsync()
    {
        //Arrange
        var expectedModel = "phi3";
        var sut = new OllamaTextGenerationService(
            expectedModel,
            httpClient: this._httpClient);

        // Act
        var textContent = await sut.GetTextContentAsync("Any prompt");

        // Assert
        Assert.NotNull(textContent.ModelId);
        Assert.Equal(expectedModel, textContent.ModelId);
    }

    [Fact]
    public async Task GetStreamingTextContentsShouldHaveModelIdDefinedAsync()
    {
        //Arrange
        var expectedModel = "phi3";
        var sut = new OllamaTextGenerationService(
            expectedModel,
            httpClient: this._httpClient);

        // Act
        StreamingTextContent? lastTextContent = null;
        await foreach (var textContent in sut.GetStreamingTextContentsAsync("Any prompt"))
        {
            lastTextContent = textContent;
        }

        // Assert
        Assert.NotNull(lastTextContent!.ModelId);
        Assert.Equal(expectedModel, lastTextContent.ModelId);
    }

    /// <summary>
    /// Disposes resources used by this class.
    /// </summary>
    public void Dispose()
    {
        this._messageHandlerStub.Dispose();

        this._httpClient.Dispose();
    }
}
