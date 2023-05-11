﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel.AI.TextCompletion;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

internal class TextCompletionStreamingResultBase : ITextCompletionStreamingResult
{
    private readonly StreamingChoice _choice;

    public TextCompletionStreamingResultBase(StreamingChoice choice)
    {
        this._choice = choice;
    }

    public async Task<string> CompleteAsync(CancellationToken cancellationToken = default)
    {
        var fullMessage = new StringBuilder();
        await foreach (var message in this._choice.GetTextStreaming(cancellationToken).ConfigureAwait(false))
        {
            fullMessage.Append(message);
        }

        return fullMessage.ToString();
    }

    public IAsyncEnumerable<string> CompleteStreamAsync(CancellationToken cancellationToken = default)
    {
        return this._choice.GetTextStreaming(cancellationToken);
    }
}
