﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.AI;

/// <summary>
/// Interface for text completion clients.
/// </summary>
public interface ITextCompletionClient
{
    /// <summary>
    /// Creates a completion for the prompt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <returns>Text generated by the remote model</returns>
    public Task<string> CompleteAsync(string text, RequestSettings requestSettings);
}
