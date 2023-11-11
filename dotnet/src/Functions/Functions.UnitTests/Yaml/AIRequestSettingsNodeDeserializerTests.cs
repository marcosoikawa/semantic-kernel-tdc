﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Functions.Yaml;
using Microsoft.SemanticKernel.Models;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemanticKernel.Functions.UnitTests.Yaml;

/// <summary>
/// Tests for <see cref="AIRequestSettingsNodeDeserializer"/>.
/// </summary>
public sealed class AIRequestSettingsNodeDeserializerTests
{
    [Fact]
    public void ItShouldCreateSemanticFunctionFromYamlWithCustomModelSettings()
    {
        // Arrange
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithNodeDeserializer(new AIRequestSettingsNodeDeserializer())
            .Build();

        // Act
        var semanticFunctionConfig = deserializer.Deserialize<SemanticFunctionConfig>(this._yaml);

        // Assert
        Assert.NotNull(semanticFunctionConfig);
        Assert.Equal("SayHello", semanticFunctionConfig.Name);
        Assert.Equal("Say hello to the specified person using the specified language", semanticFunctionConfig.Description);
        Assert.Equal(2, semanticFunctionConfig.InputParameters.Count);
        Assert.Equal("language", semanticFunctionConfig.InputParameters[1].Name);
        Assert.Equal(2, semanticFunctionConfig.ModelSettings.Count);
        Assert.Equal("gpt-3.5", semanticFunctionConfig.ModelSettings[1].ModelId);
    }

    private readonly string _yaml = @"
    template_format: semantic-kernel
    template:        Say hello world to {{$name}} in {{$language}}
    description:     Say hello to the specified person using the specified language
    name:            SayHello
    input_parameters:
      - name:          name
        description:   The name of the person to greet
        default_value: John
      - name:          language
        description:   The language to generate the greeting in
        default_value: English
    model_settings:
      - model_id:          gpt-4
        temperature:       1.0
        top_p:             0.0
        presence_penalty:  0.0
        frequency_penalty: 0.0
        max_tokens:        256
        stop_sequences:    []
      - model_id:          gpt-3.5
        temperature:       1.0
        top_p:             0.0
        presence_penalty:  0.0
        frequency_penalty: 0.0
        max_tokens:        256
        stop_sequences:    [ ""foo"", ""bar"", ""baz"" ]
";
}
