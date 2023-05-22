﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextCompletion;

/// <summary>
/// OpenAI text completion service.
/// TODO: forward ETW logging to ILogger, see https://learn.microsoft.com/en-us/dotnet/azure/sdk/logging
/// </summary>
public sealed class OpenAITextCompletion : OpenAIClientBase, ITextCompletion
{
    /// <summary>
    /// Create an instance of the OpenAI text completion connector
    /// </summary>
    /// <param name="modelId">Model name</param>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="organization">OpenAI Organization Id (usually optional)</param>
    /// <param name="logger">Application logger</param>
    public OpenAITextCompletion(
        string modelId,
        string apiKey,
        HttpClient httpClient,
        string? organization = null,
        ILogger? logger = null
    ) : base(modelId, apiKey, httpClient, organization, logger)
    {
    }

    /// <inheritdoc/>
    public Task<string> CompleteAsync(
        string text,
        CompleteRequestSettings requestSettings,
        CancellationToken cancellationToken = default)
    {
        return this.InternalCompleteTextAsync(text, requestSettings, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> CompleteStreamAsync(
        string text,
        CompleteRequestSettings requestSettings,
        CancellationToken cancellationToken = default)
    {
        return this.InternalCompletionStreamAsync(text, requestSettings, cancellationToken);
    }
}
