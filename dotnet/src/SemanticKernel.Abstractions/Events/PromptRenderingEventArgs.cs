﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Events;

/// <summary>
/// Event arguments available to the Kernel.PromptRendering event.
/// </summary>
public class PromptRenderingEventArgs : SKEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PromptRenderingEventArgs"/> class.
    /// </summary>
    /// <param name="metadata">Function metadata</param>
    /// <param name="context">Context related to the event</param>
    /// <param name="requestSettings">request settings used by the AI service</param>
    public PromptRenderingEventArgs(SKFunctionMetadata metadata, SKContext context, AIRequestSettings? requestSettings) : base(metadata, context)
    {
        this.RequestSettings = requestSettings; // TODO clone these settings
    }

    /// <summary>
    /// Request settings for the AI service.
    /// </summary>
    public AIRequestSettings? RequestSettings { get; }
}
