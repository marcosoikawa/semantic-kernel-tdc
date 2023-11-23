﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Events;
using Microsoft.SemanticKernel.Orchestration;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the main namespace
namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

/// <summary>
/// Represents a function that can be invoked as part of a Semantic Kernel workload.
/// </summary>
public abstract class KernelFunction
{
    /// <summary><see cref="ActivitySource"/> for function-related activities.</summary>
    private static readonly ActivitySource s_activitySource = new("Microsoft.SemanticKernel");

    /// <summary><see cref="Meter"/> for function-related metrics.</summary>
    private static readonly Meter s_meter = new("Microsoft.SemanticKernel");

    /// <summary><see cref="Histogram{T}"/> to record function invocation duration.</summary>
    private static readonly Histogram<double> s_invocationDuration = s_meter.CreateHistogram<double>(
        name: "sk.function.duration",
        unit: "s",
        description: "Measures the duration of a function’s execution");

    /// <summary>
    /// Gets the name of the function.
    /// </summary>
    /// <remarks>
    /// The name is used anywhere the function needs to be identified, such as in plans describing what functions
    /// should be invoked when, or as part of lookups in a plugin's function collection. Function names are generally
    /// handled in an ordinal case-insensitive manner.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets a description of the function.
    /// </summary>
    /// <remarks>
    /// The description may be supplied to a model in order to elaborate on the function's purpose,
    /// in case it may be beneficial for the model to recommend invoking the function.
    /// </remarks>
    public string Description { get; }

    /// <summary>
    /// Gets the model request settings.
    /// </summary>
    internal IEnumerable<AIRequestSettings> ModelSettings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelFunction"/> class.
    /// </summary>
    /// <param name="name">Name of the function.</param>
    /// <param name="description">Function description.</param>
    /// <param name="modelSettings">Model request settings.</param>
    /// <param name="invokeEventHandlers">Flad to control whether or not event handlers are invoked</param>
    internal KernelFunction(string name, string description, IEnumerable<AIRequestSettings>? modelSettings = null, bool invokeEventHandlers = true)
    {
        this.Name = name;
        this.Description = description;
        this.ModelSettings = modelSettings ?? Enumerable.Empty<AIRequestSettings>();

        this._invokeEventHandlers = invokeEventHandlers;
    }

    /// <summary>
    /// Invoke the <see cref="KernelFunction"/>.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="context">SK context</param>
    /// <param name="requestSettings">LLM completion settings (for semantic functions only)</param>
    /// <returns>The updated context, potentially a new one if context switching is implemented.</returns>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    public async Task<FunctionResult> InvokeAsync(
        Kernel kernel,
        SKContext context,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity(this.Name);
        ILogger logger = kernel.LoggerFactory.CreateLogger(this.Name);

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Function {Name} invoking.", this.Name);
        }

        TagList tags = new() { { "sk.function.name", this.Name } };
        long startingTimestamp = Stopwatch.GetTimestamp();
        try
        {
            // Invoke pre hook, and stop if skipping requested.
            if (this._invokeEventHandlers)
            {
                var invokingEventArgs = this.CallFunctionInvoking(kernel, context);
                if (invokingEventArgs.IsSkipRequested || invokingEventArgs.CancelToken.IsCancellationRequested)
                {
                    if (logger.IsEnabled(LogLevel.Trace))
                    {
                        logger.LogTrace("Function {Name} canceled or skipped prior to invocation.", this.Name);
                    }

                    return new FunctionResult(this.Name, context)
                    {
                        IsCancellationRequested = invokingEventArgs.CancelToken.IsCancellationRequested,
                        IsSkipRequested = invokingEventArgs.IsSkipRequested
                    };
                }
            }

            var result = await this.InvokeCoreAsync(kernel, context, requestSettings, cancellationToken).ConfigureAwait(false);

            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Function succeeded. Result: {Result}", result.GetValue<object>());
            }

            if (this._invokeEventHandlers)
            {
                // Invoke the post hook.
                (var invokedEventArgs, result) = this.CallFunctionInvoked(kernel, context, result);

                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("Function {Name} invocation {Completion}: {Result}",
                        this.Name,
                        invokedEventArgs.CancelToken.IsCancellationRequested ? "canceled" : "completed",
                        result.Value);
                }

                result.IsCancellationRequested = invokedEventArgs.CancelToken.IsCancellationRequested;
                result.IsRepeatRequested = invokedEventArgs.IsRepeatRequested;
            }

            return result;
        }
        catch (Exception ex)
        {
            tags.Add("error.type", ex.GetType().FullName);
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Function failed. Error: {Message}", ex.Message);
            }
            throw;
        }
        finally
        {
            TimeSpan duration = new((long)((Stopwatch.GetTimestamp() - startingTimestamp) * (10_000_000.0 / Stopwatch.Frequency)));
            s_invocationDuration.Record(duration.TotalSeconds, in tags);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Function completed. Duration: {Duration}ms", duration.TotalMilliseconds);
            }
        }
    }

    /// <summary>
    /// Invoke the <see cref="KernelFunction"/>.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="context">SK context</param>
    /// <param name="requestSettings">LLM completion settings (for semantic functions only)</param>
    /// <returns>The updated context, potentially a new one if context switching is implemented.</returns>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    protected abstract Task<FunctionResult> InvokeCoreAsync(
        Kernel kernel,
        SKContext context,
        AIRequestSettings? requestSettings,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the metadata describing the function.
    /// </summary>
    /// <returns>An instance of <see cref="SKFunctionMetadata"/> describing the function</returns>
    public SKFunctionMetadata GetMetadata()
    {
        return this.GetMetadataCore();
    }

    /// <summary>
    /// Gets the metadata describing the function.
    /// </summary>
    /// <returns>An instance of <see cref="SKFunctionMetadata"/> describing the function</returns>
    protected abstract SKFunctionMetadata GetMetadataCore();

    #region private
    private readonly bool _invokeEventHandlers;

    private FunctionInvokingEventArgs CallFunctionInvoking(Kernel kernel, SKContext context)
    {
        var eventArgs = new FunctionInvokingEventArgs(this.GetMetadata(), context);
        kernel.OnFunctionInvoking(eventArgs);
        return eventArgs;
    }

    private (FunctionInvokedEventArgs, FunctionResult) CallFunctionInvoked(Kernel kernel, SKContext context, FunctionResult result)
    {
        var eventArgs = new FunctionInvokedEventArgs(this.GetMetadata(), result);
        if (kernel.OnFunctionInvoked(eventArgs))
        {
            // Apply any changes from the event handlers to final result.
            result = new FunctionResult(this.Name, eventArgs.SKContext, eventArgs.SKContext.Variables.Input)
            {
                // Updates the eventArgs metadata during invoked handler execution
                // will reflect in the result metadata
                Metadata = eventArgs.Metadata
            };
        }

        return (eventArgs, result);
    }
    #endregion
}
