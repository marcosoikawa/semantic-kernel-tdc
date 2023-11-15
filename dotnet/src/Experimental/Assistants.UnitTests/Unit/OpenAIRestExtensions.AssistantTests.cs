﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Experimental.Assistants.Extensions;
using Microsoft.SemanticKernel.Experimental.Assistants.Internal;
using Microsoft.SemanticKernel.Experimental.Assistants.Models;
using Moq;
using Moq.Protected;
using Xunit;

namespace SemanticKernel.Experimental.Assistants.UnitTests.Unit;

[Trait("Category", "Unit Tests")]
public sealed class OpenAIRestExtensionsAssistantTests : IDisposable
{
    private const string BogusApiKey = "bogus";
    private const string TestAssistantId = "assistantId";

    private readonly AssistantModel _assistantModel = new();
    private readonly OpenAIRestContext _restContext;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler = new();
    private readonly HttpResponseMessage _emptyResponse = new()
    {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent("{}"),
    };

    public OpenAIRestExtensionsAssistantTests()
    {
        this._mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(this._emptyResponse);
        this._restContext = new(BogusApiKey, () => new HttpClient(this._mockHttpMessageHandler.Object));
    }

    [Fact]
    public async Task CreateAssistantModelAsync()
    {
        await this._restContext.CreateAssistantModelAsync(this._assistantModel).ConfigureAwait(true);

        this._mockHttpMessageHandler.VerifyMock(HttpMethod.Post, 1, OpenAIRestExtensions.BaseAssistantUrl);
    }

    [Fact]
    public async Task GetAssistantModelAsync()
    {
        await this._restContext.GetAssistantModelAsync(TestAssistantId).ConfigureAwait(true);

        this._mockHttpMessageHandler.VerifyMock(HttpMethod.Get, 1, OpenAIRestExtensions.GetAssistantUrl(TestAssistantId));
    }

    [Fact]
    public async Task ListAssistantModelsAsync()
    {
        await this._restContext.ListAssistantModelsAsync().ConfigureAwait(true);

        this._mockHttpMessageHandler.VerifyMock(HttpMethod.Get, 1, $"{OpenAIRestExtensions.BaseAssistantUrl}?limit=20&order=desc");
    }

    [Fact]
    public async Task DeleteAssistantModelAsync()
    {
        await this._restContext.DeleteAssistantModelAsync(TestAssistantId).ConfigureAwait(true);

        this._mockHttpMessageHandler.VerifyMock(HttpMethod.Delete, 1, OpenAIRestExtensions.GetAssistantUrl(TestAssistantId));
    }

    public void Dispose()
    {
        this._emptyResponse.Dispose();
    }
}
