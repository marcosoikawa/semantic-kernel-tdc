﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Experimental.Assistants.Extensions;
using Microsoft.SemanticKernel.Experimental.Assistants.Models;

namespace Microsoft.SemanticKernel.Experimental.Assistants.Internal;

/// <summary>
/// Represents an assistant that can call the model and use tools.
/// </summary>
internal sealed class Assistant : IAssistant
{
    /// <inheritdoc/>
    public string Id => this._model.Id;

    /// <inheritdoc/>
    public IKernel? Kernel { get; }

    /// <inheritdoc/>
#pragma warning disable CA1720 // Identifier contains type name - We don't control the schema
#pragma warning disable CA1716 // Identifiers should not match keywords
    public string Object => this._model.Object;
#pragma warning restore CA1720 // Identifier contains type name - We don't control the schema
#pragma warning restore CA1716 // Identifiers should not match keywords

    /// <inheritdoc/>
    public long CreatedAt => this._model.CreatedAt;

    /// <inheritdoc/>
    public string? Name => this._model.Name;

    /// <inheritdoc/>
    public string? Description => this._model.Description;

    /// <inheritdoc/>
    public string Model => this._model.Model;

    /// <inheritdoc/>
    public string Instructions => this._model.Instructions;

    private readonly IOpenAIRestContext _restContext;
    private readonly AssistantModel _model;

    /// <summary>
    /// Create a new assistant.
    /// </summary>
    /// <param name="restContext">A context for accessing OpenAI REST endpoint</param>
    /// <param name="assistantModel">The assistant definition</param>
    /// <param name="kernel">A semantic-kernel instance (for tool/function execution)</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>An initialized <see cref="Assistant"> instance.</see></returns>
    public static async Task<IAssistant> CreateAsync(
        IOpenAIRestContext restContext,
        AssistantModel assistantModel,
        IKernel? kernel,
        CancellationToken cancellationToken = default)
    {
        var resultModel =
            await restContext.CreateAssistantModelAsync(assistantModel, cancellationToken).ConfigureAwait(false) ??
            throw new SKException("Unexpected failure creating assisant: no result.");

        return new Assistant(resultModel, restContext, kernel);
    }

    /// <summary>
    /// Modify an existing Assistant
    /// </summary>
    /// <param name="openAiRestContext">Context to make calls to OpenAI</param>
    /// <param name="assistantModel">New properties for our instance</param>
    /// <param name="kernel">A semantic-kernel instance (for tool/function execution)</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The modified <see cref="Assistant"> instance.</see></returns>
    public static async Task<IAssistant> ModifyAsync(
        IOpenAIRestContext openAiRestContext,
        AssistantModel assistantModel,
        IKernel? kernel,
        CancellationToken cancellationToken = default)
    {
        var resultModel =
            await openAiRestContext.ModifyAssistantModelAsync(assistantModel, cancellationToken).ConfigureAwait(false) ??
            throw new SKException("Unexpected failure modifying assistant: no result.");

        return new Assistant(resultModel, openAiRestContext, kernel);
    }

    /// <summary>
    /// Retrieve an existing assistant, by identifier.
    /// </summary>
    /// <param name="restContext">A context for accessing OpenAI REST endpoint</param>
    /// <param name="assistantId">The assistant identifier</param>
    /// <param name="kernel">A semantic-kernel instance (for tool/function execution)</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>An initialized <see cref="Assistant"> instance.</see></returns>
    public static async Task<IAssistant> GetAsync(
        IOpenAIRestContext restContext,
        string assistantId,
        IKernel? kernel,
        CancellationToken cancellationToken = default)
    {
        var resultModel =
            await restContext.GetAssistantModelAsync(assistantId, cancellationToken).ConfigureAwait(false) ??
            throw new SKException($"Unexpected failure retrieving assisant: no result. ({assistantId})");

        return new Assistant(resultModel, restContext, kernel);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Assistant"/> class.
    /// </summary>
    internal Assistant(AssistantModel model, IOpenAIRestContext restContext, IKernel? kernel)
    {
        this._model = model;
        this._restContext = restContext;
        this.Kernel = kernel;
    }
}
