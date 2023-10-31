﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Events;
using Microsoft.SemanticKernel.Functions;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.TemplateEngine;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the main namespace
namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

#pragma warning disable format

/// <summary>
/// A Semantic Kernel "Semantic" prompt function.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class SemanticFunction : ISKFunction, IDisposable
{
    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string PluginName { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <summary>
    /// List of function parameters
    /// </summary>
    public IReadOnlyList<ParameterView> Parameters => this._promptTemplate.Parameters;

    /// <summary>
    /// Create a semantic function instance, given a semantic function configuration.
    /// </summary>
    /// <param name="pluginName">Name of the plugin to which the function being created belongs.</param>
    /// <param name="functionName">Name of the function to create.</param>
    /// <param name="promptTemplateConfig">Prompt template configuration.</param>
    /// <param name="promptTemplate">Prompt template.</param>
    /// <param name="serviceSelector">AI service selector</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>SK function instance.</returns>
    public static ISKFunction FromSemanticConfig(
        string pluginName,
        string functionName,
        PromptTemplateConfig promptTemplateConfig,
        IPromptTemplate promptTemplate,
        IAIServiceSelector? serviceSelector = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(promptTemplateConfig);
        Verify.NotNull(promptTemplate);

        var func = new SemanticFunction(
            template: promptTemplate,
            description: promptTemplateConfig.Description,
            pluginName: pluginName,
            functionName: functionName,
            serviceSelector: serviceSelector,
            loggerFactory: loggerFactory
        )
        {
            _modelSettings = promptTemplateConfig.ModelSettings
        };

        return func;
    }

    /// <inheritdoc/>
    public FunctionView Describe()
    {
        return new FunctionView(this.Name, this.PluginName, this.Description) { Parameters = this.Parameters };
    }

    /// <inheritdoc/>
    public async Task<FunctionResult> InvokeAsync(
        SKContext context,
        AIRequestSettings? requestSettings = null,
        EventDelegateWrapper<FunctionInvokingEventArgs>? invokingHandlerWrapper = null,
        EventDelegateWrapper<FunctionInvokedEventArgs>? invokedHandlerWrapper = null,
        CancellationToken cancellationToken = default)
    {
        this.AddDefaultValues(context.Variables);

        (var textCompletion, var defaultRequestSettings) = this._serviceSelector.SelectAIService<ITextCompletion>(context.ServiceProvider, this._modelSettings);
        return await this.RunPromptAsync(textCompletion, requestSettings ?? defaultRequestSettings, context, invokingHandlerWrapper, invokedHandlerWrapper, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Dispose of resources.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// JSON serialized string representation of the function.
    /// </summary>
    public override string ToString()
        => this.ToString(false);

    /// <summary>
    /// JSON serialized string representation of the function.
    /// </summary>
    public string ToString(bool writeIndented)
        => JsonSerializer.Serialize(this, options: writeIndented ? s_toStringIndentedSerialization : s_toStringStandardSerialization);

    internal SemanticFunction(
        IPromptTemplate template,
        string pluginName,
        string functionName,
        string description,
        IAIServiceSelector? serviceSelector = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(template);
        Verify.ValidPluginName(pluginName);
        Verify.ValidFunctionName(functionName);

        this._logger = loggerFactory is not null ? loggerFactory.CreateLogger(typeof(SemanticFunction)) : NullLogger.Instance;

        this._promptTemplate = template;
        Verify.ParametersUniqueness(this.Parameters);

        this.Name = functionName;
        this.PluginName = pluginName;
        this.Description = description;

        this._serviceSelector = serviceSelector ?? new OrderedIAIServiceSelector();

        this._view = new(() => new(functionName, pluginName, description, this.Parameters));
    }

    #region private

    private static readonly JsonSerializerOptions s_toStringStandardSerialization = new();
    private static readonly JsonSerializerOptions s_toStringIndentedSerialization = new() { WriteIndented = true };
    private readonly ILogger _logger;
    private IAIServiceSelector _serviceSelector;
    public List<AIRequestSettings>? _modelSettings;
    private readonly Lazy<FunctionView> _view;
    public const string RenderedPromptMetadataKey = "RenderedPrompt";
    public IPromptTemplate _promptTemplate { get; }

    private static async Task<string> GetCompletionsResultContentAsync(IReadOnlyList<ITextResult> completions, CancellationToken cancellationToken = default)
    {
        // To avoid any unexpected behavior we only take the first completion result (when running from the Kernel)
        return await completions[0].GetCompletionAsync(cancellationToken).ConfigureAwait(false);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"{this.Name} ({this.Description})";

    /// <summary>Add default values to the context variables if the variable is not defined</summary>
    private void AddDefaultValues(ContextVariables variables)
    {
        foreach (var parameter in this.Parameters)
        {
            if (!variables.ContainsKey(parameter.Name) && parameter.DefaultValue != null)
            {
                variables[parameter.Name] = parameter.DefaultValue;
            }
        }
    }

    private async Task<FunctionResult> RunPromptAsync(
        ITextCompletion? client,
        AIRequestSettings? requestSettings,
        SKContext context,
        EventDelegateWrapper<FunctionInvokingEventArgs>? invokingHandlerWrapper = null,
        EventDelegateWrapper<FunctionInvokedEventArgs>? invokedHandlerWrapper = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(client);

        FunctionResult result;

        try
        {
            string renderedPrompt = await this._promptTemplate.RenderAsync(context, cancellationToken).ConfigureAwait(false);

            this.CallFunctionInvoking(context, invokingHandlerWrapper, renderedPrompt);
            if (this.ShouldStopInvocation(invokingHandlerWrapper?.EventArgs, out var stopReason))
            {
                return new StopFunctionResult(this.Name, this.PluginName, context, stopReason!.Value);
            }

            renderedPrompt = this.TryUpdatePromptFromEventArgsMetadata(renderedPrompt, invokingHandlerWrapper?.EventArgs);

            IReadOnlyList<ITextResult> completionResults = await client.GetCompletionsAsync(renderedPrompt, requestSettings, cancellationToken).ConfigureAwait(false);
            string completion = await GetCompletionsResultContentAsync(completionResults, cancellationToken).ConfigureAwait(false);

            // Update the result with the completion
            context.Variables.Update(completion);

            var modelResults = completionResults.Select(c => c.ModelResult).ToArray();

            result = new FunctionResult(this.Name, this.PluginName, context, completion);

            result.Metadata.Add(AIFunctionResultExtensions.ModelResultsMetadataKey, modelResults);
            result.Metadata.Add(SemanticFunction.RenderedPromptMetadataKey, renderedPrompt);

            this.CallFunctionInvoked(result, invokedHandlerWrapper, renderedPrompt);
            if (this.ShouldStopInvocation(invokedHandlerWrapper?.EventArgs, out stopReason))
            {
                return new StopFunctionResult(this.Name, this.PluginName, context, result.Value, stopReason!.Value);
            }
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            this._logger?.LogError(ex, "Semantic function {Plugin}.{Name} execution failed with error {Error}", this.PluginName, this.Name, ex.Message);
            throw;
        }

        return result;
    }
    private void CallFunctionInvoking(SKContext context, EventDelegateWrapper<FunctionInvokingEventArgs>? eventDelegateWrapper, string prompt)
    {
        if (eventDelegateWrapper?.Handler is null)
        {
            return;
        }

        eventDelegateWrapper.EventArgs = new FunctionInvokingEventArgs(this.Describe(), context)
        {
            Metadata = {
                [SemanticFunction.RenderedPromptMetadataKey] = prompt
            }
        };
        eventDelegateWrapper.Handler.Invoke(this, eventDelegateWrapper.EventArgs);
    }

    private void CallFunctionInvoked(FunctionResult result, EventDelegateWrapper<FunctionInvokedEventArgs>? eventDelegateWrapper, string prompt)
    {
        result.Metadata[RenderedPromptMetadataKey] = prompt;

        // Not handlers registered, return the result as is
        if (eventDelegateWrapper?.Handler is null)
        {
            return;
        }

        eventDelegateWrapper.EventArgs = new FunctionInvokedEventArgs(this.Describe(), result);
        eventDelegateWrapper.Handler.Invoke(this, eventDelegateWrapper.EventArgs);

        // Updates the eventArgs metadata during invoked handler execution
        // will reflect in the result metadata
        result.Metadata = eventDelegateWrapper.EventArgs.Metadata;
    }

    private bool ShouldStopInvocation(FunctionInvokingEventArgs? invokingEvent, out StopFunctionResult.StopReason? reason)
    {
        reason = null;

        // When no event handler is registered, the event args are null and
        // this should not stop the function execution.
        if (invokingEvent is null)
        {
            return false;
        }

        if (invokingEvent.IsSkipRequested)
        {
            reason = StopFunctionResult.StopReason.InvokingSkipped;
        }
        else if (invokingEvent.CancelToken.IsCancellationRequested)
        {
            reason = StopFunctionResult.StopReason.InvokingCancelled;
        }

        // Check any event flags that interrupt this function execution;
        return (reason is not null);
    }

    private bool ShouldStopInvocation(FunctionInvokedEventArgs? invokedEvent, out StopFunctionResult.StopReason? reason)
    {
        reason = null;

        // When no event handler is registered, the event args are null and
        // this should not stop the function execution.
        if (invokedEvent is null)
        {
            return false;
        }

        if (invokedEvent.CancelToken.IsCancellationRequested)
        {
            reason = StopFunctionResult.StopReason.InvokedCancelled;
        }

        // Check any event flags that interrupt this function execution;
        return (reason is not null);
    }

    private string TryUpdatePromptFromEventArgsMetadata(string renderedPrompt, FunctionInvokingEventArgs? eventArgs)
    {
        if (eventArgs is null)
        {
            return renderedPrompt;
        }

        eventArgs.Metadata.TryGetValue(RenderedPromptMetadataKey, out var renderedPromptFromMetadata);

        // If prompt was modified to null, default to a string.Empty
        return eventArgs.Metadata[SemanticFunction.RenderedPromptMetadataKey].ToString() ?? string.Empty;
    }

    #endregion

    #region Obsolete

    /// <inheritdoc/>
    [Obsolete("Use ISKFunction.ModelSettings instead. This will be removed in a future release.")]
    public AIRequestSettings? RequestSettings => this._modelSettings?.FirstOrDefault<AIRequestSettings>();

    /// <inheritdoc/>
    [Obsolete("Use ISKFunction.SetAIServiceFactory instead. This will be removed in a future release.")]
    public ISKFunction SetAIService(Func<ITextCompletion> serviceFactory)
    {
        Verify.NotNull(serviceFactory);

        if (this._serviceSelector is DelegatingAIServiceSelector delegatingProvider)
        {
            delegatingProvider.ServiceFactory = serviceFactory;
        }
        else
        {
            var serviceSelector = new DelegatingAIServiceSelector();
            serviceSelector.ServiceFactory = serviceFactory;
            this._serviceSelector = serviceSelector;
        }
        return this;
    }

    /// <inheritdoc/>
    [Obsolete("Use ISKFunction.SetAIRequestSettingsFactory instead. This will be removed in a future release.")]
    public ISKFunction SetAIConfiguration(AIRequestSettings? requestSettings)
    {
        if (this._serviceSelector is DelegatingAIServiceSelector delegatingProvider)
        {
            delegatingProvider.RequestSettings = requestSettings;
        }
        else
        {
            var configurationProvider = new DelegatingAIServiceSelector();
            configurationProvider.RequestSettings = requestSettings;
            this._serviceSelector = configurationProvider;
        }
        return this;
    }

    /// <inheritdoc/>
    [Obsolete("Methods, properties and classes which include Skill in the name have been renamed. Use ISKFunction.PluginName instead. This will be removed in a future release.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string SkillName => this.PluginName;

    /// <inheritdoc/>
    [Obsolete("Kernel no longer differentiates between Semantic and Native functions. This will be removed in a future release.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool IsSemantic => true;

    /// <inheritdoc/>
    [Obsolete("This method is a nop and will be removed in a future release.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ISKFunction SetDefaultSkillCollection(IReadOnlyFunctionCollection skills) => this;

    /// <inheritdoc/>
    [Obsolete("This method is a nop and will be removed in a future release.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ISKFunction SetDefaultFunctionCollection(IReadOnlyFunctionCollection functions) => this;

    #endregion
}
