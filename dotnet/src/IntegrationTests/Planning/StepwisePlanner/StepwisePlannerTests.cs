﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Planning.Stepwise;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Planning.StepwisePlanner;

public sealed class StepwisePlannerTests : IDisposable
{
    private readonly string _bingApiKey;

    public StepwisePlannerTests(ITestOutputHelper output)
    {
        this._loggerFactory = NullLoggerFactory.Instance;
        this._testOutputHelper = new RedirectOutput(output);

        // Load configuration
        this._configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<StepwisePlannerTests>()
            .Build();

        string? bingApiKeyCandidate = this._configuration["Bing:ApiKey"];
        Assert.NotNull(bingApiKeyCandidate);
        this._bingApiKey = bingApiKeyCandidate;
    }

    [Theory]
    [InlineData(false, "Who is the current president of the United States? What is his current age divided by 2", "ExecutePlan", "StepwisePlanner")]
    [InlineData(true, "Who is the current president of the United States? What is his current age divided by 2", "ExecutePlan", "StepwisePlanner")]
    public void CanCreateStepwisePlan(bool useChatModel, string prompt, string expectedFunction, string expectedSkill)
    {
        // Arrange
        bool useEmbeddings = false;
        IKernel kernel = this.InitializeKernel(useEmbeddings, useChatModel);
        var bingConnector = new BingConnector(this._bingApiKey);
        var webSearchEnginePlugin = new WebSearchEnginePlugin(bingConnector);
        kernel.ImportSkill(webSearchEnginePlugin, "WebSearch");
        kernel.ImportSkill(new TimePlugin(), "time");

        var planner = new Microsoft.SemanticKernel.Planning.StepwisePlanner(kernel, new StepwisePlannerConfig() { MaxIterations = 10 });

        // Act
        var plan = planner.CreatePlan(prompt);

        // Assert
        Assert.Contains(
            plan.Steps,
            step =>
                step.Name.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
                step.SkillName.Contains(expectedSkill, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(false, "What is the tallest mountain on Earth? How tall is it divided by 2", "Everest")]
    [InlineData(true, "What is the tallest mountain on Earth? How tall is it divided by 2", "Everest")]
    [InlineData(false, "What is the weather in Seattle?", "Seattle")]
    [InlineData(true, "What is the weather in Seattle?", "Seattle")]
    public async Task CanExecuteStepwisePlanAsync(bool useChatModel, string prompt, string partialExpectedAnswer)
    {
        // Arrange
        bool useEmbeddings = false;
        IKernel kernel = this.InitializeKernel(useEmbeddings, useChatModel);
        var bingConnector = new BingConnector(this._bingApiKey);
        var webSearchEnginePlugin = new WebSearchEnginePlugin(bingConnector);
        kernel.ImportSkill(webSearchEnginePlugin, "WebSearch");
        kernel.ImportSkill(new TimePlugin(), "time");

        var planner = new Microsoft.SemanticKernel.Planning.StepwisePlanner(kernel, new StepwisePlannerConfig() { MaxIterations = 10 });

        // Act
        var plan = planner.CreatePlan(prompt);
        var result = await plan.InvokeAsync(kernel);

        // Assert - should contain the expected answer
        Assert.Contains(partialExpectedAnswer, result.GetValue<string>(), StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task ExecutePlanFailsWithTooManyFunctionsAsync()
    {
        // Arrange
        IKernel kernel = this.InitializeKernel();
        var bingConnector = new BingConnector(this._bingApiKey);
        var webSearchEnginePlugin = new WebSearchEnginePlugin(bingConnector);
        kernel.ImportSkill(webSearchEnginePlugin, "WebSearch");
        kernel.ImportSkill(new TextPlugin(), "text");
        kernel.ImportSkill(new ConversationSummaryPlugin(kernel), "ConversationSummary");
        kernel.ImportSkill(new MathPlugin(), "Math");
        kernel.ImportSkill(new FileIOPlugin(), "FileIO");
        kernel.ImportSkill(new HttpPlugin(), "Http");

        var planner = new Microsoft.SemanticKernel.Planning.StepwisePlanner(kernel, new() { MaxTokens = 1000 });

        // Act
        var plan = planner.CreatePlan("I need to buy a new brush for my cat. Can you show me options?");

        // Assert
        var ex = await Assert.ThrowsAsync<SKException>(async () => await kernel.RunAsync(plan));
        Assert.Equal("ChatHistory is too long to get a completion. Try reducing the available functions.", ex.Message);
    }

    [Fact]
    public async Task ExecutePlanSucceedsWithAlmostTooManyFunctionsAsync()
    {
        // Arrange
        IKernel kernel = this.InitializeKernel();

        _ = await kernel.ImportAIPluginAsync("Klarna", new Uri("https://www.klarna.com/.well-known/ai-plugin.json"), new OpenApiPluginExecutionParameters(enableDynamicOperationPayload: true));

        var planner = new Microsoft.SemanticKernel.Planning.StepwisePlanner(kernel);

        // Act
        var plan = planner.CreatePlan("I need to buy a new brush for my cat. Can you show me options?");
        var result = (await kernel.RunAsync(plan)).GetValue<string>();

        // Assert - should contain results, for now just verify it didn't fail
        Assert.NotNull(result);
        Assert.DoesNotContain("Result not found, review 'stepsTaken' to see what happened", result, StringComparison.OrdinalIgnoreCase);
    }

    private IKernel InitializeKernel(bool useEmbeddings = false, bool useChatModel = false)
    {
        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        AzureOpenAIConfiguration? azureOpenAIEmbeddingsConfiguration = this._configuration.GetSection("AzureOpenAIEmbeddings").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIEmbeddingsConfiguration);

        var builder = Kernel.Builder.WithLoggerFactory(this._loggerFactory);

        if (useChatModel)
        {
            builder.WithAzureChatCompletionService(
                deploymentName: azureOpenAIConfiguration.ChatDeploymentName!,
                endpoint: azureOpenAIConfiguration.Endpoint,
                apiKey: azureOpenAIConfiguration.ApiKey);
        }
        else
        {
            builder.WithAzureTextCompletionService(
                deploymentName: azureOpenAIConfiguration.DeploymentName,
                endpoint: azureOpenAIConfiguration.Endpoint,
                apiKey: azureOpenAIConfiguration.ApiKey);
        }

        if (useEmbeddings)
        {
            builder.WithAzureTextEmbeddingGenerationService(
                    deploymentName: azureOpenAIEmbeddingsConfiguration.DeploymentName,
                    endpoint: azureOpenAIEmbeddingsConfiguration.Endpoint,
                    apiKey: azureOpenAIEmbeddingsConfiguration.ApiKey)
                .WithMemoryStorage(new VolatileMemoryStore());
        }

        var kernel = builder.Build();

        return kernel;
    }

    private readonly ILoggerFactory _loggerFactory;
    private readonly RedirectOutput _testOutputHelper;
    private readonly IConfigurationRoot _configuration;

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~StepwisePlannerTests()
    {
        this.Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (this._loggerFactory is IDisposable ld)
            {
                ld.Dispose();
            }

            this._testOutputHelper.Dispose();
        }
    }
}
