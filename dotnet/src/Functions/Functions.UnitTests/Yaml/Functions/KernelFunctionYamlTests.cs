﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemanticKernel.Functions.UnitTests.Yaml;

public class KernelFunctionYamlTests
{
    private readonly ISerializer _serializer;

    public KernelFunctionYamlTests()
    {
        this._serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    [Fact]
    public void ItShouldCreateFunctionFromPromptYamlWithNoExecutionSettings()
    {
        // Arrange
        // Act
        var function = KernelFunctionYaml.FromPromptYaml(this._yamlNoExecutionSettings);

        // Assert
        Assert.NotNull(function);
        Assert.Equal("SayHello", function.Name);
        Assert.Equal("Say hello to the specified person using the specified language", function.Description);
        Assert.Equal(2, function.Metadata.Parameters.Count);
        //Assert.Equal(0, function.ExecutionSettings.Count);
    }

    [Fact]
    public void ItShouldCreateFunctionFromPromptYaml()
    {
        // Arrange
        // Act
        var function = KernelFunctionYaml.FromPromptYaml(this._yaml);

        // Assert
        Assert.NotNull(function);
        Assert.Equal("SayHello", function.Name);
        Assert.Equal("Say hello to the specified person using the specified language", function.Description);
    }

    [Fact]
    public void ItShouldCreateFunctionFromPromptYamlWithCustomExecutionSettings()
    {
        // Arrange
        // Act
        var function = KernelFunctionYaml.FromPromptYaml(this._yamlWithCustomSettings);

        // Assert
        Assert.NotNull(function);
        Assert.Equal("SayHello", function.Name);
        Assert.Equal("Say hello to the specified person using the specified language", function.Description);
        Assert.Equal(2, function.Metadata.Parameters.Count);
    }

    [Fact]
    public void ItShouldSupportCreatingOpenAIExecutionSettings()
    {
        // Arrange
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithNodeDeserializer(new PromptExecutionSettingsNodeDeserializer())
            .Build();
        var promptFunctionModel = deserializer.Deserialize<PromptTemplateConfig>(this._yaml);

        // Act
        var executionSettings = OpenAIPromptExecutionSettings.FromExecutionSettings(promptFunctionModel.ExecutionSettings["service1"]);

        // Assert
        Assert.NotNull(executionSettings);
        Assert.Equal("gpt-4", executionSettings.ModelId);
        Assert.Equal(1.0, executionSettings.Temperature);
        Assert.Equal(0.0, executionSettings.TopP);
    }

    [Fact]
    public void ItShouldCreateFunctionWithDefaultValueOfStringType()
    {
        // Act
        var function = KernelFunctionYaml.FromPromptYaml(this._yamlWithCustomSettings);

        // Assert
        Assert.NotNull(function?.Metadata?.Parameters);
        Assert.Equal("John", function?.Metadata?.Parameters[0].DefaultValue);
        Assert.Equal("English", function?.Metadata?.Parameters[1].DefaultValue);
    }

    [Fact]
    // This test checks that the logic of imposing a temporary limitation on the default value being a string is in place and works as expected.
    public void ItShouldThrowExceptionWhileCreatingFunctionWithDefaultValueOtherThanString()
    {
        string CreateYaml(object defaultValue)
        {
            var obj = new
            {
                description = "function description",
                input_variables = new[]
                {
                    new
                    {
                        name = "name",
                        description = "description",
                        @default = defaultValue,
                        isRequired = true
                    }
                }
            };

            return this._serializer.Serialize(obj);
        }

        // Act
        Assert.Throws<NotSupportedException>(() => KernelFunctionYaml.FromPromptYaml(CreateYaml(new { p1 = "v1" })));
    }

    private readonly string _yamlNoExecutionSettings = @"
        template_format: semantic-kernel
        template:        Say hello world to {{$name}} in {{$language}}
        description:     Say hello to the specified person using the specified language
        name:            SayHello
        input_variables:
          - name:          name
            description:   The name of the person to greet
            default:       John
          - name:          language
            description:   The language to generate the greeting in
            default: English
        ";

    private readonly string _yaml = """
        template_format: semantic-kernel
        template:        Say hello world to {{$name}} in {{$language}}
        description:     Say hello to the specified person using the specified language
        name:            SayHello
        input_variables:
          - name:          name
            description:   The name of the person to greet
            default:       John
          - name:          language
            description:   The language to generate the greeting in
            default: English
        execution_settings:
          service1:
            model_id:          gpt-4
            temperature:       1.0
            top_p:             0.0
            presence_penalty:  0.0
            frequency_penalty: 0.0
            max_tokens:        256
            stop_sequences:    []
          service2:
            model_id:          gpt-3.5
            temperature:       1.0
            top_p:             0.0
            presence_penalty:  0.0
            frequency_penalty: 0.0
            max_tokens:        256
            stop_sequences:    [ "foo", "bar", "baz" ]
        """;

    private readonly string _yamlWithCustomSettings = """
        template_format: semantic-kernel
        template:        Say hello world to {{$name}} in {{$language}}
        description:     Say hello to the specified person using the specified language
        name:            SayHello
        input_variables:
          - name:          name
            description:   The name of the person to greet
            default:       John
          - name:          language
            description:   The language to generate the greeting in
            default:       English
        execution_settings:
          service1:
            model_id:          gpt-4
            temperature:       1.0
            top_p:             0.0
            presence_penalty:  0.0
            frequency_penalty: 0.0
            max_tokens:        256
            stop_sequences:    []
          service2:
            model_id:          random-model
            temperaturex:      1.0
            top_q:             0.0
            rando_penalty:     0.0
            max_token_count:   256
            stop_sequences:    [ "foo", "bar", "baz" ]
        """;

    [Fact]
    public void ItShouldCreateFunctionFromMethodYaml()
    {
        // Arrange
        var target = new ValidatorPlugin();

        // Act
        var function = KernelFunctionYaml.FromMethodYaml(this._yamlFunctionConfig, target);

        // Assert
        Assert.NotNull(function);
        Assert.Equal("ValidateTaskId", function.Name);
        Assert.Equal("Validate a task id.", function.Description);
    }

    [Fact]
    public async Task ItShouldImportPluginFromMethodYamlConfigAsync()
    {
        // Arrange
        var kernel = new Kernel();
        var target = new ValidatorPlugin();

        // Act
        var functions = new List<KernelFunction>();
        functions.Add(KernelFunctionYaml.FromMethodYaml(this._yamlFunctionConfig, target));
        var plugin = kernel.ImportPluginFromFunctions("ValidatorPlugin", functions);
        var function = plugin["ValidateTaskId"];
        var result = await kernel.InvokeAsync(function, new() { { "taskId", "1234" } });

        // Assert
        Assert.NotNull(function);
        Assert.Equal("Validate a task id.", function.Metadata.Description);
        Assert.Equal("Kernel instance.", function.Metadata.Parameters[0].Description);
        Assert.Equal("Task identifier.", function.Metadata.Parameters[1].Description);
        Assert.Equal("Valid task id", result.GetValue<string>());
    }

    /// <summary>
    /// Plugin example with no KernelFunction or Description attributes.
    /// </summary>
    private sealed class ValidatorPlugin
    {
        public string ValidateTaskId(Kernel kernel, string taskId)
        {
            return taskId.Equals("1234", StringComparison.Ordinal) ? "Valid task id" : "Invalid task id";
        }
    }

    private readonly string _yamlFunctionConfig = """
        name: ValidateTaskId
        description: Validate a task id.
        input_variables:
          - name: kernel
            description: Kernel instance.
          - name: taskId
            description: Task identifier.
            is_required: true
        output_variable:
          description: String indicating whether or not the task id is valid.
        """;
}
