﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using Xunit;

namespace SemanticKernel.Connectors.MistralAI.UnitTests;

/// <summary>
/// Unit tests for <see cref="MistralAIPromptExecutionSettings"/>.
/// </summary>
public class MistralAIPromptExecutionSettingsTests
{
    [Fact]
    public void FromExecutionSettingsWhenAlreadyMistralShouldReturnSame()
    {
        // Arrange
        var executionSettings = new MistralAIPromptExecutionSettings();

        // Act
        var mistralExecutionSettings = MistralAIPromptExecutionSettings.FromExecutionSettings(executionSettings);

        // Assert
        Assert.Same(executionSettings, mistralExecutionSettings);
    }

    [Fact]
    public void FromExecutionSettingsWhenNullShouldReturnDefaultSettings()
    {
        // Arrange
        PromptExecutionSettings? executionSettings = null;

        // Act
        var MistralExecutionSettings = MistralAIPromptExecutionSettings.FromExecutionSettings(executionSettings);

        // Assert
        Assert.Equal(0.7, MistralExecutionSettings.Temperature);
        Assert.Equal(1, MistralExecutionSettings.TopP);
        Assert.Null(MistralExecutionSettings.MaxTokens);
        Assert.False(MistralExecutionSettings.SafePrompt);
        Assert.Null(MistralExecutionSettings.RandomSeed);
    }

    [Fact]
    public void FromExecutionSettingsWhenSerializedHasPropertiesShouldPopulateSpecialized()
    {
        // Arrange
        string jsonSettings = """
                                {
                                    "temperature": 0.5,
                                    "top_p": 0.9,
                                    "max_tokens": 100,
                                    "max_time": 10.0,
                                    "safe_prompt": true,
                                    "random_seed": 123
                                }
                                """;

        // Act
        var executionSettings = JsonSerializer.Deserialize<PromptExecutionSettings>(jsonSettings);
        var MistralExecutionSettings = MistralAIPromptExecutionSettings.FromExecutionSettings(executionSettings);

        // Assert
        Assert.Equal(0.5, MistralExecutionSettings.Temperature);
        Assert.Equal(0.9, MistralExecutionSettings.TopP);
        Assert.Equal(100, MistralExecutionSettings.MaxTokens);
        Assert.True(MistralExecutionSettings.SafePrompt);
        Assert.Equal(123, MistralExecutionSettings.RandomSeed);
    }
}
