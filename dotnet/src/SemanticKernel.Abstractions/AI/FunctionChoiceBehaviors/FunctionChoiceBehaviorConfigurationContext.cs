﻿// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel;

/// <summary>
/// The context to be provided by the choice behavior consumer in order to obtain the choice behavior configuration.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class FunctionChoiceBehaviorConfigurationContext
{
    /// <summary>
    /// Creates a new instance of <see cref="FunctionChoiceBehaviorConfigurationContext"/>.
    /// </summary>
    /// <param name="chatHistory">The chat history.</param>
    public FunctionChoiceBehaviorConfigurationContext(ChatHistory chatHistory)
    {
        this.ChatHistory = chatHistory;
    }

    /// <summary>
    /// The chat history.
    /// </summary>
    public ChatHistory ChatHistory { get; }

    /// <summary>
    /// The <see cref="Kernel"/> to be used for function calling.
    /// </summary>
    public Kernel? Kernel { get; init; }

    /// <summary>
    /// Request sequence index of automatic function invocation process. Starts from 0.
    /// </summary>
    public int RequestSequenceIndex { get; init; }
}
