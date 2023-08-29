﻿// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System;
using Xunit;
using System.Net.Mime;
using System.Text;

namespace SemanticKernel.UnitTests.Utilities;

public sealed class HttpContentExtensionsTests : IDisposable
{
    /// <summary>
    /// An instance of HttpMessageHandlerStub class used to get access to various properties of HttpRequestMessage sent by HTTP client.
    /// </summary>
    private readonly HttpMessageHandlerStub _httpMessageHandlerStub;

    /// <summary>
    /// An instance of HttpClient class used by the tests.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates an instance of a <see cref="HttpClientExtensionsTests"/> class.
    /// </summary>
    public HttpContentExtensionsTests()
    {
        this._httpMessageHandlerStub = new HttpMessageHandlerStub();

        this._httpClient = new HttpClient(this._httpMessageHandlerStub);
    }

    [Fact]
    public async Task ShouldReturnHttpContentAsStringAsync()
    {
        //Arrange
        this._httpMessageHandlerStub.ResponseToReturn.Content = new StringContent("{\"details\": \"fake-response-content\"}", Encoding.UTF8, MediaTypeNames.Application.Json);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://fake-random-test-host");

        using var responseMessage = await this._httpClient.SendAsync(requestMessage, CancellationToken.None);

        //Act
        var result = await responseMessage.Content.ReadAsStringAndTranslateExceptionAsync();

        //Assert
        Assert.False(string.IsNullOrEmpty(result));

        Assert.Equal("{\"details\": \"fake-response-content\"}", result);
    }

    /// <summary>
    /// Disposes resources used by this class.
    /// </summary>
    public void Dispose()
    {
        this._httpMessageHandlerStub.Dispose();

        this._httpClient.Dispose();
    }
}
