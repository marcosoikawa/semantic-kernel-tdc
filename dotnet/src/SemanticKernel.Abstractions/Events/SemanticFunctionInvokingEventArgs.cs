﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Microsoft.SemanticKernel.Events;

/// <summary>
/// Semantic Function Event arguments available to the Kernel.FunctionInvoking event.
/// </summary>
public sealed class SemanticFunctionInvokingEventArgs : FunctionInvokingEventArgs
{
    internal SemanticFunctionInvokingEventArgs(FunctionView functionView, SKContext context, string? renderedPrompt) : base(functionView, context)
    {
        Verify.NotNull(context);

        this.RenderedPrompt = renderedPrompt;
    }

    /// <summary>
    /// Prompt rendered from template prior to the function execution.
    /// </summary>
    public string? RenderedPrompt { get; }
}
