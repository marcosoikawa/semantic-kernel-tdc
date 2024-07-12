﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.TextToImage;

/* Phase 02
- Breaking the current constructor parameter order to follow the same order as the other services.
- Added custom endpoint support, and removed ApiKey validation, as it is performed by the ClientCore when the Endpoint is not provided.
- Added custom OpenAIClient support.
- Updated "organization" parameter to "organizationId".
- "modelId" parameter is now required in the constructor.

- Added OpenAIClient breaking glass constructor.

Phase 08
- Removed OpenAIClient breaking glass constructor
- Reverted the order and parameter names.
*/

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// OpenAI text to image service.
/// </summary>
[Experimental("SKEXP0010")]
public class OpenAITextToImageService : ITextToImageService
{
    private readonly ClientCore _client;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Attributes => this._client.Attributes;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAITextToImageService"/> class.
    /// </summary>
    /// <param name="apiKey">OpenAI API key, see https://platform.openai.com/account/api-keys</param>
    /// <param name="organization">OpenAI organization id. This is usually optional unless your account belongs to multiple organizations.</param>
    /// <param name="modelId">The model to use for image generation.</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public OpenAITextToImageService(
        string apiKey,
        string? organization = null,
        string? modelId = null,
        HttpClient? httpClient = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNullOrWhiteSpace(modelId, nameof(modelId));
        this._client = new(modelId, apiKey, organization, null, httpClient, loggerFactory?.CreateLogger(this.GetType()));
    }

    /// <inheritdoc/>
    public Task<string> GenerateImageAsync(string description, int width, int height, Kernel? kernel = null, CancellationToken cancellationToken = default)
        => this._client.GenerateImageAsync(description, width, height, cancellationToken);
}