﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Inference;
using Azure.Core.Pipeline;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureAIInference;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.AzureAIInference;

#pragma warning disable xUnit1004 // Contains test methods used in manual verification. Disable warning for this file only.

public sealed class AzureAIInferenceChatCompletionServiceTests(ITestOutputHelper output) : BaseIntegrationTest, IDisposable
{
    private const string InputParameterName = "input";
    private readonly XunitLogger<Kernel> _loggerFactory = new(output);
    private readonly RedirectOutput _testOutputHelper = new(output);
    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
        .AddJsonFile(path: "testsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddUserSecrets<AzureAIInferenceChatCompletionServiceTests>()
        .Build();

    [Theory]
    [InlineData("Where is the most famous fish market in Seattle, Washington, USA?", "Pike Place")]
    public async Task InvokeGetChatMessageContentsAsync(string prompt, string expectedAnswerContains)
    {
        // Arrange
        var config = this._configuration.GetSection("AzureAIInference").Get<AzureAIInferenceConfiguration>();
        Assert.NotNull(config);

        var sut = (config.ApiKey is not null)
                ? new AzureAIInferenceChatCompletionService(
                    endpoint: config.Endpoint,
                    apiKey: config.ApiKey,
                    loggerFactory: this._loggerFactory)
                : new AzureAIInferenceChatCompletionService(
                    modelId: null,
                    endpoint: config.Endpoint,
                    credential: new AzureCliCredential(),
                    loggerFactory: this._loggerFactory);

        ChatHistory chatHistory = [
            new ChatMessageContent(AuthorRole.User, prompt)
        ];

        // Act
        var result = await sut.GetChatMessageContentsAsync(chatHistory);

        // Assert
        Assert.Single(result);
        Assert.Contains(expectedAnswerContains, result[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Where is the most famous fish market in Seattle, Washington, USA?", "Pike Place")]
    public async Task InvokeGetStreamingChatMessageContentsAsync(string prompt, string expectedAnswerContains)
    {
        // Arrange
        var config = this._configuration.GetSection("AzureAIInference").Get<AzureAIInferenceConfiguration>();
        Assert.NotNull(config);

        var sut = (config.ApiKey is not null)
                ? new AzureAIInferenceChatCompletionService(
                    endpoint: config.Endpoint,
                    apiKey: config.ApiKey,
                    loggerFactory: this._loggerFactory)
                : new AzureAIInferenceChatCompletionService(
                    modelId: null,
                    endpoint: config.Endpoint,
                    credential: new AzureCliCredential(),
                    loggerFactory: this._loggerFactory);

        ChatHistory chatHistory = [
            new ChatMessageContent(AuthorRole.User, prompt)
        ];

        StringBuilder fullContent = new();

        // Act
        await foreach (var update in sut.GetStreamingChatMessageContentsAsync(chatHistory))
        {
            fullContent.Append(update.Content);
        }

        // Assert
        Assert.Contains(expectedAnswerContains, fullContent.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    //[Fact(Skip = "Skipping while we investigate issue with GitHub actions.")]
    public async Task ItCanUseChatForTextGenerationAsync()
    {
        // Arrange
        var kernel = this.CreateAndInitializeKernel();

        var func = kernel.CreateFunctionFromPrompt(
            "List the two planets after '{{$input}}', excluding moons, using bullet points.",
            new AzureAIInferencePromptExecutionSettings());

        // Act
        var result = await func.InvokeAsync(kernel, new() { [InputParameterName] = "Jupiter" });

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Saturn", result.GetValue<string>(), StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("Uranus", result.GetValue<string>(), StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact(Skip = "Skipping as Azure AI Studio Phi3 Deployments presents with a bug when passing 0 float parameters")]
    public async Task ItStreamingFromKernelTestAsync()
    {
        // Arrange
        var kernel = this.CreateAndInitializeKernel();

        var plugins = TestHelpers.ImportSamplePlugins(kernel, "ChatPlugin");

        StringBuilder fullResult = new();

        var prompt = "Where is the most famous fish market in Seattle, Washington, USA?";

        // Act
        await foreach (var content in kernel.InvokeStreamingAsync<StreamingKernelContent>(plugins["ChatPlugin"]["Chat"], new() { [InputParameterName] = prompt }))
        {
            fullResult.Append(content);
        }

        // Assert
        Assert.Contains("Pike Place", fullResult.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ItHttpRetryPolicyTestAsync()
    {
        // Arrange
        List<HttpStatusCode?> statusCodes = [];

        var config = this._configuration.GetSection("AzureAIInference").Get<AzureAIInferenceConfiguration>();
        Assert.NotNull(config);
        Assert.NotNull(config.Endpoint);

        var kernelBuilder = Kernel.CreateBuilder();

        kernelBuilder.AddAzureAIInferenceChatCompletion(endpoint: config.Endpoint, apiKey: null);

        kernelBuilder.Services.ConfigureHttpClientDefaults(c =>
        {
            // Use a standard resiliency policy, augmented to retry on 401 Unauthorized for this example
            c.AddStandardResilienceHandler().Configure(o =>
            {
                o.Retry.ShouldHandle = args => ValueTask.FromResult(args.Outcome.Result?.StatusCode is HttpStatusCode.Unauthorized);
                o.Retry.OnRetry = args =>
                {
                    statusCodes.Add(args.Outcome.Result?.StatusCode);
                    return ValueTask.CompletedTask;
                };
            });
        });

        var target = kernelBuilder.Build();

        var plugins = TestHelpers.ImportSamplePlugins(target, "SummarizePlugin");

        var prompt = "Where is the most famous fish market in Seattle, Washington, USA?";

        // Act
        var exception = await Assert.ThrowsAsync<HttpOperationException>(() => target.InvokeAsync(plugins["SummarizePlugin"]["Summarize"], new() { [InputParameterName] = prompt }));

        // Assert
        Assert.All(statusCodes, s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        Assert.Equal(HttpStatusCode.Unauthorized, ((HttpOperationException)exception).StatusCode);
    }

    [Fact(Skip = "Skipping as Azure AI Studio Phi3 Deployments presents with a bug when passing 0 float parameters")]
    public async Task ItShouldReturnInnerContentAsync()
    {
        // Arrange
        var config = this._configuration.GetSection("AzureAIInference").Get<AzureAIInferenceConfiguration>();
        using var myHandler = new MyHttpClientHandler();
        using var myClient = new HttpClient(myHandler);
        var myInferenceClient = new ChatCompletionsClient(new Uri("https://Phi-3-medium-4k-instruct-maas.swedencentral.models.ai.azure.com"), new AzureKeyCredential(config!.ApiKey!), new ChatCompletionsClientOptions { Transport = new HttpClientTransport(myClient) });

        var options = new ChatCompletionsOptions();
        options.Messages.Add(new ChatRequestUserMessage("Hey"));
        options.Temperature = 0;
        options.NucleusSamplingFactor = 0;

        await myInferenceClient.CompleteAsync(options);

        var kernel = this.CreateAndInitializeKernel(myClient);
        var plugins = TestHelpers.ImportSamplePlugins(kernel, "FunPlugin");

        // Act
        var result = await kernel.InvokeAsync(plugins["FunPlugin"]["Limerick"]);
        var content = result.GetValue<ChatMessageContent>();
        // Assert
        Assert.NotNull(content);
        Assert.NotNull(content.InnerContent);

        Assert.IsType<CompletionsUsage>(content.InnerContent);
        var usage = (CompletionsUsage)content.InnerContent;

        // Usage
        Assert.NotEqual(0, usage.PromptTokens);
        Assert.NotEqual(0, usage.CompletionTokens);
    }

    public class MyHttpClientHandler : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var response = await base.SendAsync(request, cancellationToken);
            return response;
        }
    }


    [Theory(Skip = "This test is for manual verification.")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task CompletionWithDifferentLineEndingsAsync(string lineEnding)
    {
        // Arrange
        var prompt =
            "Given a json input and a request. Apply the request on the json input and return the result. " +
            $"Put the result in between <result></result> tags{lineEnding}" +
            $$"""Input:{{lineEnding}}{"name": "John", "age": 30}{{lineEnding}}{{lineEnding}}Request:{{lineEnding}}name""";

        var kernel = this.CreateAndInitializeKernel();

        var plugins = TestHelpers.ImportSamplePlugins(kernel, "ChatPlugin");

        // Act
        FunctionResult actual = await kernel.InvokeAsync(plugins["ChatPlugin"]["Chat"], new() { [InputParameterName] = prompt });

        // Assert
        Assert.Contains("<result>John</result>", actual.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ItHasSemanticKernelVersionHeaderAsync()
    {
        // Arrange
        using var defaultHandler = new HttpClientHandler();
        using var httpHeaderHandler = new HttpHeaderHandler(defaultHandler);
        using var httpClient = new HttpClient(httpHeaderHandler);

        var kernel = this.CreateAndInitializeKernel(httpClient);

        // Act
        var result = await kernel.InvokePromptAsync("Where is the most famous fish market in Seattle, Washington, USA?");

        // Assert
        Assert.NotNull(httpHeaderHandler.RequestHeaders);
        Assert.True(httpHeaderHandler.RequestHeaders.TryGetValues("Semantic-Kernel-Version", out var values));
    }

    private Kernel CreateAndInitializeKernel(HttpClient? httpClient = null)
    {
        var config = this._configuration.GetSection("AzureAIInference").Get<AzureAIInferenceConfiguration>();
        Assert.NotNull(config);
        Assert.NotNull(config.ApiKey);
        Assert.NotNull(config.Endpoint);

        var kernelBuilder = base.CreateKernelBuilder();

        kernelBuilder.AddAzureAIInferenceChatCompletion(
            endpoint: config.Endpoint,
            apiKey: config.ApiKey,
            serviceId: config.ServiceId,
            httpClient: httpClient);

        return kernelBuilder.Build();
    }

    private sealed class HttpHeaderHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        public System.Net.Http.Headers.HttpRequestHeaders? RequestHeaders { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.RequestHeaders = request.Headers;
            return await base.SendAsync(request, cancellationToken);
        }
    }

    public void Dispose()
    {
        this._loggerFactory.Dispose();
        this._testOutputHelper.Dispose();
    }
}
