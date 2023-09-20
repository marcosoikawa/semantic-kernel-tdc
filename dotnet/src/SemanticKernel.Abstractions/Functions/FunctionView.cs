﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the main namespace
namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

/// <summary>
/// Class used to copy and export data from the function collection.
/// The data is mutable, but changes do not affect the function collection.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class FunctionView
{
    /// <summary>
    /// Name of the function. The name is used by the function collection and in prompt templates e.g. {{pluginName.functionName}}
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Name of the plugin containing the function. The name is used by the function collection and in prompt templates e.g. {{pluginName.functionName}}
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// Function description. The description is used in combination with embeddings when searching relevant functions.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether the delegate points to a semantic function
    /// </summary>
    public bool IsSemantic { get; set; }

    /// <summary>
    /// Whether the delegate is an asynchronous function
    /// </summary>
    public bool IsAsynchronous { get; set; }

    /// <summary>
    /// List of function parameters
    /// </summary>
    public IList<ParameterView> Parameters { get; set; } = new List<ParameterView>();

    /// <summary>
    /// Constructor
    /// </summary>
    public FunctionView()
    {
    }

    /// <summary>
    /// Create a function view.
    /// </summary>
    /// <param name="name">Function name</param>
    /// <param name="pluginName">Plugin name, e.g. the function namespace</param>
    /// <param name="description">Function description</param>
    /// <param name="parameters">List of function parameters provided by the function developer</param>
    /// <param name="isSemantic">Whether the function is a semantic one (or native is False)</param>
    /// <param name="isAsynchronous">Whether the function is async. Note: all semantic functions are async.</param>
    public FunctionView(
        string name,
        string pluginName,
        string description,
        IList<ParameterView> parameters,
        bool isSemantic,
        bool isAsynchronous = true)
    {
        this.Name = name;
        this.PluginName = pluginName;
        this.Description = description;
        this.Parameters = parameters;
        this.IsSemantic = isSemantic;
        this.IsAsynchronous = isAsynchronous;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"{this.Name} ({this.Description})";
}
