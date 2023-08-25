﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI.TextCompletion;

namespace Microsoft.SemanticKernel.Connectors.AI.Oobabooga.Completion.TextCompletion;

public class OobaboogaTextCompletion : OobaboogaCompletionBase<string, CompleteRequestSettings, OobaboogaCompletionParameters, OobaboogaCompletionRequest, TextCompletionResponse, TextCompletionResult, TextCompletionStreamingResult>, ITextCompletion
{
    private const string BlockingUriPath = "/api/v1/generate";
    private const string StreamingUriPath = "/api/v1/stream";

    /// <summary>
    /// Initializes a new instance of the <see cref="OobaboogaTextCompletion"/> class.
    /// </summary>
    /// <param name="completionRequestSettings">An instance of <see cref="OobaboogaCompletionSettings"/>, which are text completion settings specific to Oobabooga api</param>
    /// <param name="endpoint">The service API endpoint to which requests should be sent.</param>
    /// <param name="blockingPort">The port used for handling blocking requests. Default value is 5000</param>
    /// <param name="streamingPort">The port used for handling streaming requests. Default value is 5005</param>
    public OobaboogaTextCompletion(OobaboogaTextCompletionSettings completionRequestSettings, Uri endpoint,
        int blockingPort = 5000,
        int streamingPort = 5005) : base(endpoint, BlockingUriPath, StreamingUriPath, blockingPort, streamingPort, completionRequestSettings)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ITextResult>> GetCompletionsAsync(
        string text,
        CompleteRequestSettings requestSettings,
        CancellationToken cancellationToken = default)
    {
        this.LogActionDetails();
        return await this.GetCompletionsBaseAsync(text, requestSettings, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ITextStreamingResult> GetStreamingCompletionsAsync(
        string text,
        CompleteRequestSettings requestSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chatCompletionStreamingResult in this.GetStreamingCompletionsBaseAsync(text, requestSettings, cancellationToken))
        {
            yield return chatCompletionStreamingResult;
        }
    }

    /// <summary>
    /// Creates an Oobabooga request, mapping CompleteRequestSettings fields to their Oobabooga API counter parts
    /// </summary>
    /// <param name="input">The text to complete.</param>
    /// <param name="requestSettings">The request settings.</param>
    /// <returns>An Oobabooga TextCompletionRequest object with the text and completion parameters.</returns>
    protected override OobaboogaCompletionRequest CreateCompletionRequest(string input, CompleteRequestSettings? requestSettings)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentNullException(nameof(input));
        }

        requestSettings ??= new CompleteRequestSettings();

        // Prepare the request using the provided parameters.
        var toReturn = OobaboogaCompletionRequest.Create(input, (OobaboogaCompletionSettings<OobaboogaCompletionParameters>)this.OobaboogaSettings, requestSettings);
        return toReturn;
    }

    protected override IReadOnlyList<TextCompletionResult> GetCompletionResults(TextCompletionResponse completionResponse)
    {
        return completionResponse.Results.Select(completionText => new TextCompletionResult(completionText)).ToList();
    }

    protected override CompletionStreamingResponseBase? GetResponseObject(string messageText)
    {
        return JsonSerializer.Deserialize<TextCompletionStreamingResponse>(messageText);
    }
}
