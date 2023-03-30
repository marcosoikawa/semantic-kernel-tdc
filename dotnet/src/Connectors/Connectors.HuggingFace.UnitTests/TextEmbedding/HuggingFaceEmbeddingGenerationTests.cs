﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.HuggingFace.TextEmbedding;
using Xunit;

namespace SemanticKernel.Connectors.HuggingFace.UnitTests.TextEmbedding;

/// <summary>
/// Unit tests for <see cref="HuggingFaceEmbeddingGeneration"/> class.
/// </summary>
public class HuggingFaceEmbeddingGenerationTests : IDisposable
{
    private const string BaseUri = "http://localhost:5000";
    private const string Model = "gpt2";

    private readonly HttpResponseMessage _response = new()
    {
        StatusCode = HttpStatusCode.OK,
    };

    /// <summary>
    /// Verifies that <see cref="HuggingFaceEmbeddingGeneration.GenerateEmbeddingsAsync(IList{string})"/>
    /// returns expected list of generated embeddings without errors.
    /// </summary>
    [Fact]
    public async Task ItReturnsEmbeddingsCorrectlyAsync()
    {
        // Arrange
        const int expectedEmbeddingCount = 1;
        const int expectedVectorCount = 8;
        List<string> data = new() { "test_string_1", "test_string_2", "test_string_3" };

        using var service = this.CreateService(HuggingFaceTestHelper.GetTestResponse("embeddings_test_response.json"));

        // Act
        var embeddings = await service.GenerateEmbeddingsAsync(data);

        // Assert
        Assert.NotNull(embeddings);
        Assert.Equal(expectedEmbeddingCount, embeddings.Count);
        Assert.Equal(expectedVectorCount, embeddings.First().Count);
    }

    /// <summary>
    /// Initializes <see cref="HuggingFaceEmbeddingGeneration"/> with mocked <see cref="HttpClientHandler"/>.
    /// </summary>
    /// <param name="testResponse">Test response for <see cref="HttpClientHandler"/> to return.</param>
    private HuggingFaceEmbeddingGeneration CreateService(string testResponse)
    {
        this._response.Content = new StringContent(testResponse);

        var httpClientHandler = HuggingFaceTestHelper.GetHttpClientHandlerMock(this._response);

        return new HuggingFaceEmbeddingGeneration(new Uri(BaseUri), Model, httpClientHandler);
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._response.Dispose();
        }
    }
}
