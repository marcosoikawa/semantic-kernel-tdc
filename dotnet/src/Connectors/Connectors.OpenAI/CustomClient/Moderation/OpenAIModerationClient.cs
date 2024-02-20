﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// Represents a client for interacting with the openai moderation models.
/// </summary>
internal sealed class OpenAIModerationClient : CustomClientBase, IOpenAIModerationClient
{
    private readonly string _modelId;

    /// <summary>
    /// Represents a client for interacting with the openai moderation models.
    /// </summary>
    /// <param name="httpClient">HttpClient instance used to send HTTP requests</param>
    /// <param name="modelId">Id of the model supporting moderation endpoint</param>
    /// <param name="httpRequestFactory">Request factory</param>
    /// <param name="endpointProvider">Endpoints provider</param>
    /// <param name="logger">Logger instance used for logging (optional)</param>
    public OpenAIModerationClient(
        HttpClient httpClient,
        string modelId,
        IHttpRequestFactory httpRequestFactory,
        IEndpointProvider endpointProvider,
        ILogger? logger = null)
        : base(
            httpClient: httpClient,
            httpRequestFactory: httpRequestFactory,
            endpointProvider: endpointProvider,
            logger: logger)
    {
        Verify.NotNullOrWhiteSpace(modelId);

        this._modelId = modelId;
    }

    /// <inheritdoc/>
    public async Task<ClassificationContent> ClassifyTextAsync(
        string text,
        PromptExecutionSettings? executionSettings = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(text);

        var endpoint = this.EndpointProvider.ModerationEndpoint;
        var geminiRequest = this.CreateRequest(text);
        using var httpRequestMessage = this.HttpRequestFactory.CreatePost(geminiRequest, endpoint);

        string body = await this.SendRequestAndGetStringBodyAsync(httpRequestMessage, cancellationToken)
            .ConfigureAwait(false);

        return ParseAndProcessClassificationResponse(body);
    }

    private static ClassificationContent ParseAndProcessClassificationResponse(string body)
        => GetClassificationContentFromResponse(DeserializeResponse<OpenAIModerationResponse>(body));

    private static ClassificationContent GetClassificationContentFromResponse(OpenAIModerationResponse response)
    {
        var responseResult = response.Results[0];
        var classificationResult = new OpenAIClassificationResult(
            flagged: responseResult.Flagged,
            entries: GetClassificationEntriesFromResponseResult(responseResult));
        return new ClassificationContent(innerContent: response, result: classificationResult, modelId: response.ModelId);
    }

    private static List<OpenAIClassificationEntry> GetClassificationEntriesFromResponseResult(
        OpenAIModerationResponse.ModerationResult responseResult)
    {
        var classificationEntries =
            from flags in responseResult.CategoryFlags
            join score in responseResult.CategoryScores on flags.Label equals score.Label
            select new OpenAIClassificationEntry(
                category: new OpenAIClassificationCategory(flags.Label),
                flagged: flags.Flagged,
                score: score.Score);
        return classificationEntries.ToList();
    }

    private OpenAIModerationRequest CreateRequest(string text)
        => OpenAIModerationRequest.FromText(text, this._modelId);
}
