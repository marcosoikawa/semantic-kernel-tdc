﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Services;
using Moq;
using OpenAI;
using SemanticKernel.Connectors.OpenAI.UnitTests;
using Xunit;

namespace SemanticKernel.Connectors.UnitTests.OpenAI.TextToImage;

/// <summary>
/// Unit tests for <see cref="OpenAITextToImageService"/> class.
/// </summary>
public sealed class OpenAITextToImageServiceTests : IDisposable
{
    private readonly HttpMessageHandlerStub _messageHandlerStub;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;

    public OpenAITextToImageServiceTests()
    {
        this._messageHandlerStub = new()
        {
            ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(File.ReadAllText("./TestData/text-to-image-response.txt"))
            }
        };
        this._httpClient = new HttpClient(this._messageHandlerStub, false);
        this._mockLoggerFactory = new Mock<ILoggerFactory>();
    }

    [Fact]
    public void ConstructorWorksCorrectly()
    {
        // Arrange & Act
        var sut = new OpenAITextToImageService("model", "api-key", "organization");

        // Assert
        Assert.NotNull(sut);
        Assert.Equal("organization", sut.Attributes[ClientCore.OrganizationKey]);
        Assert.Equal("model", sut.Attributes[AIServiceExtensions.ModelIdKey]);
    }

    [Fact]
    public void OpenAIClientConstructorWorksCorrectly()
    {
        // Arrange
        var sut = new OpenAITextToImageService("model", new OpenAIClient("apikey"));

        // Assert
        Assert.NotNull(sut);
        Assert.Equal("model", sut.Attributes[AIServiceExtensions.ModelIdKey]);
    }

    [Theory]
    [InlineData(256, 256, "dall-e-2")]
    [InlineData(512, 512, "dall-e-2")]
    [InlineData(1024, 1024, "dall-e-2")]
    [InlineData(1024, 1024, "dall-e-3")]
    [InlineData(1024, 1792, "dall-e-3")]
    [InlineData(1792, 1024, "dall-e-3")]
    public async Task GenerateImageWorksCorrectlyAsync(int width, int height, string modelId)
    {
        // Arrange
        var sut = new OpenAITextToImageService(modelId, "api-key", httpClient: this._httpClient);
        Assert.Equal(modelId, sut.Attributes["ModelId"]);

        // Act 
        var result = await sut.GenerateImageAsync("description", width, height);

        // Assert
        Assert.Equal("https://image-url/", result);
    }

    [Theory]
    [InlineData(123, 456, "dall-e-2")]
    [InlineData(256, 512, "dall-e-2")]
    [InlineData(256, 256, "dall-e-3")]
    [InlineData(512, 512, "dall-e-3")]
    [InlineData(1024, 1792, "dall-e-2")]
    [InlineData(1792, 1024, "dall-e-2")]
    public async Task GenerateImageThrowsWhenSizeIsNotSupportedAsync(int width, int height, string modelId)
    {
        // Arrange
        var sut = new OpenAITextToImageService(modelId, "apiKey");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.GenerateImageAsync("description", width, height));
    }

    [Theory]
    [InlineData(123, 456)]
    [InlineData(256, 512)]
    [InlineData(6546, 545)]
    [InlineData(16, 32)]
    public async Task GenerateImageAllowCustomSizeWhenNonDefaultEndpointIsUsedAsync(int width, int height)
    {
        // Arrange
        var sut = new OpenAITextToImageService("model", endpoint: new Uri("http://localhost"), httpClient: this._httpClient);
        Assert.Equal("model", sut.Attributes[AIServiceExtensions.ModelIdKey]);

        // Act 
        var result = await sut.GenerateImageAsync("description", width, height);

        // Assert
        Assert.Equal("https://image-url/", result);
    }

    [Fact]
    public async Task GenerateImageDoesLogActionAsync()
    {
        // Assert
        var modelId = "dall-e-2";
        var logger = new Mock<ILogger<OpenAITextToImageService>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        this._mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

        // Arrange
        var sut = new OpenAITextToImageService(modelId, "apiKey", httpClient: this._httpClient, loggerFactory: this._mockLoggerFactory.Object);

        // Act
        await sut.GenerateImageAsync("description", 256, 256);

        // Assert
        logger.VerifyLog(LogLevel.Information, $"Action: {nameof(OpenAITextToImageService.GenerateImageAsync)}. OpenAI Model ID: {modelId}.", Times.Once());
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
        this._messageHandlerStub.Dispose();
    }
}
