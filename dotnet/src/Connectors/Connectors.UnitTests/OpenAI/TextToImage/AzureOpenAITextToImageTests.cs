﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Services;
using Xunit;

namespace SemanticKernel.Connectors.UnitTests.OpenAI.TextToImage;

/// <summary>
/// Unit tests for <see cref="AzureOpenAITextToImageServiceTests"/> class.
/// </summary>
public sealed class AzureOpenAITextToImageServiceTests
{
    [Theory]
    [InlineData(1024, 1024, null)]
    [InlineData(1792, 1024, null)]
    [InlineData(1024, 1792, null)]
    [InlineData(512, 512, typeof(NotSupportedException))]
    [InlineData(256, 256, typeof(NotSupportedException))]
    [InlineData(123, 456, typeof(NotSupportedException))]
    public async Task ItValidatesTheModelIdAsync(int width, int height, Type? expectedExceptionType)
    {
        // Arrange
        using var messageHandlerStub = new HttpMessageHandlerStub();
        using var httpClient = new HttpClient(messageHandlerStub, false);
        messageHandlerStub.ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(@"{
                                            ""created"": 1702575371,
                                            ""data"": [
                                                {
                                                    ""revised_prompt"": ""A photo capturing the diversity of the Earth's landscapes."",
                                                    ""url"": ""https://dalleprodsec.blob.core.windows.net/private/images/0f20c621-7eb0-449d-87fd-8dd2a3a15fbe/generated_00.png?se=2023-12-15T17%3A36%3A25Z&sig=jd2%2Fa8jOM9NmclrUbOLdRgAxcFDFPezOpG%2BSF82d7zM%3D&ske=2023-12-20T10%3A10%3A28Z&skoid=e52d5ed7-0657-4f62-bc12-7e5dbb260a96&sks=b&skt=2023-12-13T10%3A10%3A28Z&sktid=33e01921-4d64-4f8c-a055-5bdaffd5e33d&skv=2020-10-02&sp=r&spr=https&sr=b&sv=2020-10-02""
                                                }
                                            ]
                                        }", Encoding.UTF8, "application/json")
        };

        var textToImageCompletion = new AzureOpenAITextToImageService(deploymentName: "gpt-35-turbo", modelId: "gpt-3.5-turbo", endpoint: "https://az.com", apiKey: "NOKEY", httpClient: httpClient);

        if (expectedExceptionType is not null)
        {
            await Assert.ThrowsAsync(expectedExceptionType, () => textToImageCompletion.GenerateImageAsync("anything", width, height));
        }
        else
        {
            // Act
            var result = await textToImageCompletion.GenerateImageAsync("anything", width, height);

            // Assert
            Assert.NotNull(result);
        }
    }

    [Theory]
    [InlineData("gpt-35-turbo", "gpt-3.5-turbo")]
    [InlineData("gpt-35-turbo", null)]
    [InlineData("gpt-4-turbo", "gpt-4")]
    public void ItHasPropertiesAsDefined(string deploymentName, string? modelId)
    {
        var service = new AzureOpenAITextToImageService(deploymentName, "https://az.com", "NOKEY", modelId);
        Assert.Contains(AzureOpenAITextToImageService.DeploymentNameKey, service.Attributes);
        Assert.Equal(deploymentName, service.Attributes[AzureOpenAITextToImageService.DeploymentNameKey]);

        if (modelId is null)
        {
            return;
        }

        Assert.Contains(AIServiceExtensions.ModelIdKey, service.Attributes);
        Assert.Equal(modelId, service.Attributes[AIServiceExtensions.ModelIdKey]);
    }
}
