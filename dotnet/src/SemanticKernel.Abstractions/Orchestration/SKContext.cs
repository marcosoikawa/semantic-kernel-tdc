﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Microsoft.SemanticKernel.Orchestration;

/// <summary>
/// Semantic Kernel context.
/// </summary>
public abstract class SKContext
{
    /// <summary>
    /// Print the processed input, aka the current data after any processing occurred.
    /// </summary>
    /// <returns>Processed input, aka result</returns>
    public abstract string Result { get; }

    /// <summary>
    /// Whether an error occurred while executing functions in the pipeline.
    /// </summary>
    public abstract bool ErrorOccurred { get; }

    /// <summary>
    /// Error details.
    /// </summary>
    public abstract string LastErrorDescription { get; }

    /// <summary>
    /// When an error occurs, this is the most recent exception.
    /// </summary>
    public abstract Exception? LastException { get; }

    /// <summary>
    /// When a prompt is processed, aka the current data after any model results processing occurred.
    /// (One prompt can have multiple results).
    /// </summary>
    public abstract IReadOnlyCollection<ModelResult> ModelResults { get; }

    /// <summary>
    /// The culture currently associated with this context.
    /// </summary>
    public abstract CultureInfo Culture { get; }

    /// <summary>
    /// Call this method to signal when an error occurs.
    /// In the usual scenarios this is also how execution is stopped, e.g. to inform the user or take necessary steps.
    /// </summary>
    /// <param name="errorDescription">Error description</param>
    /// <param name="exception">If available, the exception occurred</param>
    /// <returns>The current instance</returns>
    public abstract void Fail(string errorDescription, Exception? exception = null);

    /// <summary>
    /// User variables
    /// </summary>
    public abstract ContextVariables Variables { get; }

    /// <summary>
    /// Read only skills collection
    /// </summary>
    public abstract IReadOnlySkillCollection Skills { get; }

    /// <summary>
    /// App logger
    /// </summary>
    public abstract ILogger Logger { get; }

    /// <summary>
    /// Print the processed input, aka the current data after any processing occurred.
    /// If an error occurred, prints the last exception message instead.
    /// </summary>
    /// <returns>Processed input, aka result, or last exception message if any</returns>
    public abstract override string ToString();

    /// <summary>
    /// Create a clone of the current context, using the same kernel references (memory, skills, logger)
    /// and a new set variables, so that variables can be modified without affecting the original context.
    /// </summary>
    /// <returns>A new context copied from the current one</returns>
    public abstract SKContext Clone();

    #region Obsolete

    /// <summary>
    /// The token to monitor for cancellation requests.
    /// </summary>
    [Obsolete("Add a CancellationToken param to SKFunction method signatures instead of retrieving it from SKContext.")]
    public CancellationToken CancellationToken { get; } = default;

    /// <summary>
    /// Shortcut into user data, access variables by name
    /// </summary>
    /// <param name="name">Variable name</param>
    [Obsolete("Indexer for variable access removed. Use context.Variables instead.")]
    public string this[string name] => this.Variables[name];

    /// <summary>
    /// Semantic memory
    /// </summary>
    [Obsolete("Memory no longer passed through SKContext. Instead, initialize your skill class with the memory provider it needs.")]
    public ISemanticTextMemory Memory
    {
        get => throw new InvalidOperationException(
            "Memory no longer passed through SKContext. Instead, initialize your skill class with the memory provider it needs.");
    }

    /// <summary>
    /// App logger (obsolete - use 'Logger')
    /// </summary>
    [Obsolete("Use 'SKContext.Logger' instead.")]
    public ILogger Log => this.Logger;

    /// <summary>
    /// Access registered functions by skill + name. Not case sensitive.
    /// The function might be native or semantic, it's up to the caller handling it.
    /// </summary>
    /// <param name="skillName">Skill name</param>
    /// <param name="functionName">Function name</param>
    /// <returns>Delegate to execute the function</returns>
    [Obsolete("This method has been removed from SKContext. Use SKContext.Skills collection instead.")]
    public ISKFunction Func(string skillName, string functionName)
        => this.Skills.GetFunction(skillName, functionName);

    #endregion
}
