﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Xunit;
using Xunit.Abstractions;

namespace Examples;

/* The files contains two examples about SK Semantic Memory.
 *
 * 1. Memory using Azure AI Search.
 * 2. Memory using a custom embedding generator and vector engine.
 *
 * Semantic Memory allows to store your data like traditional DBs,
 * adding the ability to query it using natural language.
 */
public class Example14_SemanticMemory : BaseTest
{
    private const string MemoryCollectionName = "SKGitHub";

    [Fact]
    public async Task RunAsync()
    {
        this._output.WriteLine("==============================================================");
        this._output.WriteLine("======== Semantic Memory using Azure AI Search ========");
        this._output.WriteLine("==============================================================");

        /* This example leverages Azure AI Search to provide SK with Semantic Memory.
         *
         * Azure AI Search automatically indexes your data semantically, so you don't
         * need to worry about embedding generation.
         */

        var memoryWithACS = new MemoryBuilder()
            .WithOpenAITextEmbeddingGeneration("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .WithMemoryStore(new AzureAISearchMemoryStore(TestConfiguration.AzureAISearch.Endpoint, TestConfiguration.AzureAISearch.ApiKey))
            .Build();

        await RunExampleAsync(memoryWithACS);

        this._output.WriteLine("====================================================");
        this._output.WriteLine("======== Semantic Memory (volatile, in RAM) ========");
        this._output.WriteLine("====================================================");

        /* You can build your own semantic memory combining an Embedding Generator
         * with a Memory storage that supports search by similarity (ie semantic search).
         *
         * In this example we use a volatile memory, a local simulation of a vector DB.
         *
         * You can replace VolatileMemoryStore with Qdrant (see QdrantMemoryStore connector)
         * or implement your connectors for Pinecone, Vespa, Postgres + pgvector, SQLite VSS, etc.
         */

        var memoryWithCustomDb = new MemoryBuilder()
            .WithOpenAITextEmbeddingGeneration("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .WithMemoryStore(new VolatileMemoryStore())
            .Build();

        await RunExampleAsync(memoryWithCustomDb);
    }

    [Theory]
    public async Task RunExampleAsync(ISemanticTextMemory memory)
    {
        await StoreMemoryAsync(memory);

        await SearchMemoryAsync(memory, "How do I get started?");

        /*
        Output:

        Query: How do I get started?

        Result 1:
          URL:     : https://github.com/microsoft/semantic-kernel/blob/main/README.md
          Title    : README: Installation, getting started, and how to contribute

        Result 2:
          URL:     : https://github.com/microsoft/semantic-kernel/blob/main/samples/dotnet-jupyter-notebooks/00-getting-started.ipynb
          Title    : Jupyter notebook describing how to get started with the Semantic Kernel

        */

        await SearchMemoryAsync(memory, "Can I build a chat with SK?");

        /*
        Output:

        Query: Can I build a chat with SK?

        Result 1:
          URL:     : https://github.com/microsoft/semantic-kernel/tree/main/samples/plugins/ChatPlugin/ChatGPT
          Title    : Sample demonstrating how to create a chat plugin interfacing with ChatGPT

        Result 2:
          URL:     : https://github.com/microsoft/semantic-kernel/blob/main/samples/apps/chat-summary-webapp-react/README.md
          Title    : README: README associated with a sample chat summary react-based webapp

        */
    }

    private async Task SearchMemoryAsync(ISemanticTextMemory memory, string query)
    {
        this._output.WriteLine("\nQuery: " + query + "\n");

        var memoryResults = memory.SearchAsync(MemoryCollectionName, query, limit: 2, minRelevanceScore: 0.5);

        int i = 0;
        await foreach (MemoryQueryResult memoryResult in memoryResults)
        {
            this._output.WriteLine($"Result {++i}:");
            this._output.WriteLine("  URL:     : " + memoryResult.Metadata.Id);
            this._output.WriteLine("  Title    : " + memoryResult.Metadata.Description);
            this._output.WriteLine("  Relevance: " + memoryResult.Relevance);
            this._output.WriteLine();
        }

        this._output.WriteLine("----------------------");
    }

    private async Task StoreMemoryAsync(ISemanticTextMemory memory)
    {
        /* Store some data in the semantic memory.
         *
         * When using Azure AI Search the data is automatically indexed on write.
         *
         * When using the combination of VolatileStore and Embedding generation, SK takes
         * care of creating and storing the index
         */

        this._output.WriteLine("\nAdding some GitHub file URLs and their descriptions to the semantic memory.");
        var githubFiles = SampleData();
        var i = 0;
        foreach (var entry in githubFiles)
        {
            await memory.SaveReferenceAsync(
                collection: MemoryCollectionName,
                externalSourceName: "GitHub",
                externalId: entry.Key,
                description: entry.Value,
                text: entry.Value);

            Console.Write($" #{++i} saved.");
        }

        this._output.WriteLine("\n----------------------");
    }

    private static Dictionary<string, string> SampleData()
    {
        return new Dictionary<string, string>
        {
            ["https://github.com/microsoft/semantic-kernel/blob/main/README.md"]
                = "README: Installation, getting started, and how to contribute",
            ["https://github.com/microsoft/semantic-kernel/blob/main/dotnet/notebooks/02-running-prompts-from-file.ipynb"]
                = "Jupyter notebook describing how to pass prompts from a file to a semantic plugin or function",
            ["https://github.com/microsoft/semantic-kernel/blob/main/dotnet/notebooks//00-getting-started.ipynb"]
                = "Jupyter notebook describing how to get started with the Semantic Kernel",
            ["https://github.com/microsoft/semantic-kernel/tree/main/samples/plugins/ChatPlugin/ChatGPT"]
                = "Sample demonstrating how to create a chat plugin interfacing with ChatGPT",
            ["https://github.com/microsoft/semantic-kernel/blob/main/dotnet/src/SemanticKernel/Memory/VolatileMemoryStore.cs"]
                = "C# class that defines a volatile embedding store",
        };
    }

    public Example14_SemanticMemory(ITestOutputHelper output) : base(output)
    {
    }
}
