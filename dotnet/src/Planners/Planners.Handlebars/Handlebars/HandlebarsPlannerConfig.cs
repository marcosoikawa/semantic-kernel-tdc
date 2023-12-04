﻿// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable IDE0130 // Namespace does not match folder structure
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Planning.Handlebars;

/// <summary>
/// Configuration for Handlebars planner instances.
/// </summary>
public sealed class HandlebarsPlannerConfig
{
    /// <summary>
    /// Delegate to get the prompt template string.
    /// </summary>
    public Func<string>? GetPromptTemplate { get; set; }

    /// <summary>
    /// A list of plugins to exclude from the plan creation request.
    /// </summary>
    public HashSet<string> ExcludedPlugins { get; } = new();

    /// <summary>
    /// A list of functions to exclude from the plan creation request.
    /// </summary>
    public HashSet<string> ExcludedFunctions { get; } = new();

    /// <summary>
    /// Callback to get the available functions for planning (optional).
    /// Use if you want to override the default function lookup behavior.
    /// If set, this function takes precedence over <see cref="Memory"/>.
    /// Setting <see cref="ExcludedPlugins"/>, <see cref="ExcludedFunctions"/> will be used to filter the results.
    /// </summary>
    public Func<HandlebarsPlannerConfig, string?, CancellationToken, Task<IEnumerable<KernelFunctionMetadata>>>? GetAvailableFunctionsAsync { get; set; }

    /// <summary>
    /// Callback to get a function by name (optional).
    /// Use if you want to override the default function lookup behavior.
    /// </summary>
    public Func<string, string, KernelFunction?>? GetFunctionCallback { get; set; }

    /// <summary>
    /// The maximum total number of tokens to allow in a completion request,
    /// which includes the tokens from the prompt and completion
    /// </summary>
    public int MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the last plan generated by the planner.
    /// </summary>
    public HandlebarsPlan? LastPlan { get; set; }

    /// <summary>
    /// Gets or sets the last error that occurred during planning.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether loops are allowed in the plan.
    /// </summary>
    public bool AllowLoops { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlebarsPlannerConfig"/> class.
    /// </summary>
    public HandlebarsPlannerConfig(
        HandlebarsPlan? lastPlan = default,
        string? lastError = default,
        bool allowLoops = true
    )
    {
        this.LastPlan = lastPlan;
        this.LastError = lastError;
        this.AllowLoops = allowLoops;
    }
}
