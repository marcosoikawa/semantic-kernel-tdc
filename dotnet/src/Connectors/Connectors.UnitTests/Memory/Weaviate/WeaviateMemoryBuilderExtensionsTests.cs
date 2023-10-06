﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System;
using Xunit;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Connectors.Memory.Weaviate;
using Microsoft.SemanticKernel.AI.Embeddings;
using Moq;

namespace SemanticKernel.Connectors.UnitTests.Memory.Weaviate;

public sealed class WeaviateMemoryBuilderExtensionsTests : IDisposable
{
    private readonly HttpMessageHandlerStub _messageHandlerStub;
    private readonly HttpClient _httpClient;

    public WeaviateMemoryBuilderExtensionsTests()
    {
        this._messageHandlerStub = new HttpMessageHandlerStub();

        this._httpClient = new HttpClient(this._messageHandlerStub, false);
    }

    [Theory]
    [InlineData(null, "https://fake-random-test-weaviate-host/v1/objects/fake-key")]
    [InlineData("v2", "https://fake-random-test-weaviate-host/v2/objects/fake-key")]
    public async Task WeaviateMemoryStoreShouldBeProperlyInitializedAsync(string? apiVersion, string expectedAddress)
    {
        // Arrange
        var embeddingGenerationMock = Mock.Of<ITextEmbeddingGeneration>();

        var getResponse = new
        {
            Properties = new Dictionary<string, string> {
                { "sk_id", "fake_id" },
                { "sk_description", "fake_description" },
                { "sk_text", "fake_text" },
                { "sk_additional_metadata", "fake_additional_metadata" }
            }
        };

        this._messageHandlerStub.ResponseToReturn.Content = new StringContent(JsonSerializer.Serialize(getResponse, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), Encoding.UTF8, MediaTypeNames.Application.Json);

        var builder = new MemoryBuilder();
        builder.WithWeaviateMemoryStore(this._httpClient, "https://fake-random-test-weaviate-host", "fake-api-key", apiVersion);
        builder.WithTextEmbeddingGeneration(embeddingGenerationMock);

        var memory = builder.Build(); //This call triggers the internal factory registered by WithWeaviateMemoryStore method to create an instance of the WeaviateMemoryStore class.

        // Act
        await memory.GetAsync("fake-collection", "fake-key"); //This call triggers a subsequent call to Weaviate memory store.

        // Assert
        Assert.Equal(expectedAddress, this._messageHandlerStub?.RequestUri?.AbsoluteUri);

        var headerValues = Enumerable.Empty<string>();
        var headerExists = this._messageHandlerStub?.RequestHeaders?.TryGetValues("Authorization", out headerValues);
        Assert.True(headerExists);
        Assert.Contains(headerValues!, (value) => value == "fake-api-key");
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
        this._messageHandlerStub.Dispose();
    }
}
