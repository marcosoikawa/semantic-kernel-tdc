﻿#region HEADER

// Copyright (c) Microsoft. All rights reserved.

#endregion

using System;
using System.Net.Http;
using Microsoft.SemanticKernel.Connectors.Gemini.Abstract;
using Microsoft.SemanticKernel.Http;

namespace Microsoft.SemanticKernel.Connectors.Gemini.Core.VertexAI;

internal sealed class VertexAIGeminiHttpRequestFactory : IHttpRequestFactory
{
    private readonly string _apiKey;

    public VertexAIGeminiHttpRequestFactory(string apiKey)
    {
        Verify.NotNullOrWhiteSpace(apiKey);

        this._apiKey = apiKey;
    }

    public HttpRequestMessage CreatePost(object requestData, Uri endpoint)
    {
        var httpRequestMessage = HttpRequest.CreatePostRequest(endpoint, requestData);
        httpRequestMessage.Headers.Add("User-Agent", HttpHeaderValues.UserAgent);
        httpRequestMessage.Headers.Add("Authorization", $"Bearer {this._apiKey}");
        return httpRequestMessage;
    }
}
