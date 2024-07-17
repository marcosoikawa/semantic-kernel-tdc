﻿// Copyright (c) Microsoft. All rights reserved.

using Amazon.BedrockRuntime;
using Connectors.Amazon.Core.Requests;
using Connectors.Amazon.Core.Responses;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Amazon.Core;
using Microsoft.SemanticKernel.Services;
using Microsoft.SemanticKernel.TextGeneration;

namespace Connectors.Amazon.Services;

/// <summary>
/// Represents a text generationservice using Amazon Bedrock API.
/// </summary>
public class BedrockTextGenerationService : BedrockTextGenerationClient<ITextGenerationRequest, ITextGenerationResponse>, ITextGenerationService
{
    private readonly Dictionary<string, object?> _attributesInternal = [];

    /// <summary>
    /// Initializes an instance of the BedrockTextGenerationService using an IAmazonBedrockRuntime object passed in by the user.
    /// </summary>
    /// <param name="modelId"></param>
    /// <param name="bedrockApi"></param>
    public BedrockTextGenerationService(string modelId, IAmazonBedrockRuntime bedrockApi)
        : base(modelId, bedrockApi)
    {
        this._attributesInternal.Add(AIServiceExtensions.ModelIdKey, modelId);
    }

    /// <summary>
    /// Initializes an instance of the BedrockTextGenerationService by creating a new AmazonBedrockRuntimeClient().
    /// </summary>
    /// <param name="modelId"></param>
    public BedrockTextGenerationService(string modelId)
        : base(modelId, new AmazonBedrockRuntimeClient())
    {
        this._attributesInternal.Add(AIServiceExtensions.ModelIdKey, modelId);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Attributes => this._attributesInternal;

    /// <inheritdoc />
    public Task<IReadOnlyList<TextContent>> GetTextContentsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
        => this.InvokeBedrockModelAsync(prompt, executionSettings, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
        => this.StreamTextAsync(prompt, executionSettings, kernel, cancellationToken);
}
