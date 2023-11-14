﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Experimental.Assistants.Internal;
using Microsoft.SemanticKernel.Experimental.Assistants.Models;

namespace Microsoft.SemanticKernel.Experimental.Assistants.Extensions;

/// <summary>
/// Supported OpenAI REST API actions for thread runs.
/// </summary>
internal static partial class OpenAIRestExtensions
{
    /// <summary>
    /// Create a new run.
    /// </summary>
    /// <param name="context">A context for accessing OpenAI REST endpoint</param>
    /// <param name="threadId">A thread identifier</param>
    /// <param name="assistantId">The assistant identifier</param>
    /// <param name="instructions">Optional instruction override</param>
    /// <param name="tools">The assistant tools</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A run definition</returns>
    public static Task<ThreadRunModel> CreateRunAsync(
        this OpenAIRestContext context,
        string threadId,
        string assistantId,
        string? instructions = null,
        IEnumerable<AssistantModel.ToolModel>? tools = null,
        CancellationToken cancellationToken = default)
    {
        var payload =
            new
            {
                assistant_id = assistantId,
                instructions,
                tools,
            };

        return
            context.ExecutePostAsync<ThreadRunModel>(
                GetRunUrl(threadId),
                payload,
                cancellationToken);
    }

    /// <summary>
    /// Retrieve an run by identifier.
    /// </summary>
    /// <param name="context">A context for accessing OpenAI REST endpoint</param>
    /// <param name="threadId">A thread identifier</param>
    /// <param name="runId">A run identifier</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A run definition</returns>
    public static Task<ThreadRunModel> GetRunAsync(
        this OpenAIRestContext context,
        string threadId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        return
            context.ExecuteGetAsync<ThreadRunModel>(
                GetRunUrl(threadId, runId),
                cancellationToken);
    }

    /// <summary>
    /// Retrieve run steps by identifier.
    /// </summary>
    /// <param name="context">A context for accessing OpenAI REST endpoint</param>
    /// <param name="threadId">A thread identifier</param>
    /// <param name="runId">A run identifier</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A set of run steps</returns>
    public static Task<ThreadRunStepListModel> GetRunStepsAsync(
        this OpenAIRestContext context,
        string threadId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        return
            context.ExecuteGetAsync<ThreadRunStepListModel>(
                GetRunStepsUrl(threadId, runId),
                cancellationToken);
    }

    /// <summary>
    /// Add a function result for a run.
    /// </summary>
    /// <param name="context">A context for accessing OpenAI REST endpoint</param>
    /// <param name="threadId">A thread identifier</param>
    /// <param name="runId">The run identifier</param>
    /// <param name="results">The function/tool results.</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A run definition</returns>
    public static Task<ThreadRunModel> AddToolOutputsAsync(
        this OpenAIRestContext context,
        string threadId,
        string runId,
        IList<ToolResultModel> results,
        CancellationToken cancellationToken = default)
    {
        var payload =
           new
           {
               tool_outputs = results
           };

        return
            context.ExecutePostAsync<ThreadRunModel>(
                GetRunToolOutput(threadId, runId),
                payload,
                cancellationToken);
    }

    private static string GetRunUrl(string threadId)
    {
        return $"{BaseThreadUrl}/{threadId}/runs";
    }

    private static string GetRunUrl(string threadId, string runId)
    {
        return $"{BaseThreadUrl}/{threadId}/runs/{runId}";
    }

    private static string GetRunStepsUrl(string threadId, string runId)
    {
        return $"{BaseThreadUrl}/{threadId}/runs/{runId}/steps";
    }

    private static string GetRunToolOutput(string threadId, string runId)
    {
        return $"{BaseThreadUrl}/{threadId}/runs/{runId}/submit_tool_outputs";
    }
}
