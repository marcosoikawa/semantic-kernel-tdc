﻿// Copyright (c) Microsoft. All rights reserved.

using Amazon.BedrockRuntime;
using Connectors.Amazon.Extensions;
using Connectors.Amazon.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Amazon.Services;
using Microsoft.SemanticKernel.TextGeneration;
using Moq;
using Xunit;

namespace Connectors.Amazon.UnitTests;

/// <summary>
/// Kernel Builder Extension Tests for Bedrock.
/// </summary>
public class BedrockKernelBuilderExtensionTests
{
    /// <summary>
    /// Checks that AddBedrockTextGenerationService builds a proper kernel with a null bedrockRuntime.
    /// </summary>
    [Fact]
    public void AddBedrockTextGenerationCreatesServiceWithNullBedrockRuntime()
    {
        // Arrange
        var builder = Kernel.CreateBuilder();
        builder.AddBedrockTextGenerationService("amazon.titan-text-premier-v1:0");

        // Act
        var kernel = builder.Build();
        var service = kernel.GetRequiredService<ITextGenerationService>();

        // Assert
        Assert.NotNull(kernel);
        Assert.NotNull(service);
        Assert.IsType<BedrockTextGenerationService>(service);
    }

    /// <summary>
    /// Checks that AddBedrockTextGenerationService builds a proper kernel with a non-null bedrockRuntime.
    /// InlineData expects constant values or expressions that can be evaluated at compile-time so cannot pass new Mock IAmazonBedrockRuntime.Object at runtime.
    /// </summary>
    [Fact]
    public void AddBedrockTextGenerationCreatesServiceWithNonNullBedrockRuntime()
    {
        // Arrange
        var bedrockRuntime = new Mock<IAmazonBedrockRuntime>().Object;
        var builder = Kernel.CreateBuilder();
        builder.AddBedrockTextGenerationService("amazon.titan-text-premier-v1:0", bedrockRuntime);

        // Act
        var kernel = builder.Build();
        var service = kernel.GetRequiredService<ITextGenerationService>();

        // Assert
        Assert.NotNull(kernel);
        Assert.NotNull(service);
        Assert.IsType<BedrockTextGenerationService>(service);
    }

    /// <summary>
    /// Checks that AddBedrockChatCompletionService builds a proper kernel with a null bedrockRuntime.
    /// </summary>
    [Fact]
    public void AddBedrockChatCompletionCreatesServiceWithNullBedrockRuntime()
    {
        // Arrange
        var builder = Kernel.CreateBuilder();
        builder.AddBedrockChatCompletionService("amazon.titan-text-premier-v1:0", null);

        // Act
        var kernel = builder.Build();
        var service = kernel.GetRequiredService<IChatCompletionService>();

        // Assert
        Assert.NotNull(kernel);
        Assert.NotNull(service);
        Assert.IsType<BedrockChatCompletionService>(service);
    }

    /// <summary>
    /// Checks that AddBedrockChatCompletionService builds a proper kernel with a non-null bedrockRuntime.
    /// </summary>
    [Fact]
    public void AddBedrockChatCompletionCreatesServiceWithNonNullBedrockRuntime()
    {
        // Arrange
        var bedrockRuntime = new Mock<IAmazonBedrockRuntime>().Object;
        var builder = Kernel.CreateBuilder();
        builder.AddBedrockChatCompletionService("amazon.titan-text-premier-v1:0", bedrockRuntime);

        // Act
        var kernel = builder.Build();
        var service = kernel.GetRequiredService<IChatCompletionService>();

        // Assert
        Assert.NotNull(kernel);
        Assert.NotNull(service);
        Assert.IsType<BedrockChatCompletionService>(service);
    }
}
