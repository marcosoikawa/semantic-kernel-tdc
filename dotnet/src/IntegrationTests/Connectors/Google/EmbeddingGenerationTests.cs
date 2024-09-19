﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.SemanticKernel.Embeddings;
using xRetry;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.Google;

public sealed class EmbeddingGenerationTests(ITestOutputHelper output) : TestBase(output)
{
    [RetryTheory]
    [InlineData(ServiceType.GoogleAI, Skip = "This test is for manual verification.")]
    [InlineData(ServiceType.VertexAI, Skip = "This test is for manual verification.")]
    public async Task EmbeddingGenerationAsync(ServiceType serviceType)
    {
        // Arrange
        const string Input = "LLM is Large Language Model.";
        var sut = this.GetEmbeddingService(serviceType);

        // Act
        var response = await sut.GenerateEmbeddingAsync(Input);

        // Assert
        this.Output.WriteLine($"Count of returned embeddings: {response.Length}");
        Assert.Equal(768, response.Length);
    }
}
