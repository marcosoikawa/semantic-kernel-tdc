// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.SemanticFunctions;

namespace Microsoft.SemanticKernel.AI;

/// <summary>
/// Settings for a request. The request can be a completion request or other type of requests.
/// </summary>
public class RequestSettings
{
    /// <summary>
    /// Settings to a completion request.
    /// </summary>
    public CompleteRequestSettings? CompleteRequestSettings { get; set; } = null;

    /// <summary>
    /// The number of seconds to wait before the request to the completion backend times out.
    /// </summary>
    public int HttpTimeoutInSeconds { get; set; } = 100;

    /// <summary>
    /// Create a new settings object containing a <see cref="CompleteRequestSettings"/> instance
    /// with the values from another settings object.
    /// </summary>
    /// <param name="config"></param>
    /// <returns>An instance of <see cref="RequestSettings"/> </returns>
    public static RequestSettings FromCompletionConfig(PromptTemplateConfig.CompletionConfig config)
    {
        return new RequestSettings
        {
            CompleteRequestSettings = new CompleteRequestSettings
            {
                Temperature = config.Temperature,
                TopP = config.TopP,
                PresencePenalty = config.PresencePenalty,
                FrequencyPenalty = config.FrequencyPenalty,
                MaxTokens = config.MaxTokens,
                StopSequences = config.StopSequences,
            },
            HttpTimeoutInSeconds = config.HttpTimeoutInSeconds
        };
    }
}