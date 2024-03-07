﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.GoogleVertexAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using Xunit;
using Xunit.Abstractions;

namespace Examples;

/// <summary>
/// Represents an example class for Gemini Embedding Generation with volatile memory store.
/// </summary>
public sealed class Example99_GeminiEmbeddingGeneration : BaseTest
{
    private const string MemoryCollectionName = "aboutMe";

    [Fact]
    public async Task GoogleAIAsync()
    {
        this.WriteLine("============= Google AI - Gemini Embedding Generation =============");

        string googleAIApiKey = TestConfiguration.GoogleAI.ApiKey;
        string geminiModelId = TestConfiguration.GoogleAI.Gemini.ModelId;
        string embeddingModelId = TestConfiguration.GoogleAI.EmbeddingModelId;

        if (googleAIApiKey is null || geminiModelId is null || embeddingModelId is null)
        {
            this.WriteLine("GoogleAI credentials not found. Skipping example.");
            return;
        }

        Kernel kernel = Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(
                modelId: geminiModelId,
                apiKey: googleAIApiKey)
            .AddGoogleAIEmbeddingGeneration(
                modelId: embeddingModelId,
                apiKey: googleAIApiKey)
            .Build();

        await this.RunSimpleSampleAsync(kernel);
        await this.RunTextMemoryPluginSampleAsync(kernel);
    }

    [Fact]
    public async Task VertexAIAsync()
    {
        this.WriteLine("============= Vertex AI - Gemini Embedding Generation =============");

        string vertexBearerKey = TestConfiguration.VertexAI.BearerKey;
        string geminiModelId = TestConfiguration.VertexAI.Gemini.ModelId;
        string geminiLocation = TestConfiguration.VertexAI.Location;
        string geminiProject = TestConfiguration.VertexAI.ProjectId;
        string embeddingModelId = TestConfiguration.VertexAI.EmbeddingModelId;

        if (vertexBearerKey is null || geminiModelId is null || geminiLocation is null
            || geminiProject is null || embeddingModelId is null)
        {
            this.WriteLine("VertexAI credentials not found. Skipping example.");
            return;
        }

        Kernel kernel = Kernel.CreateBuilder()
            .AddVertexAIGeminiChatCompletion(
                modelId: geminiModelId,
                bearerKey: vertexBearerKey,
                location: geminiLocation,
                projectId: geminiProject)
            .AddVertexAIEmbeddingGeneration(
                modelId: embeddingModelId,
                bearerKey: vertexBearerKey,
                location: geminiLocation,
                projectId: geminiProject)
            .Build();

        await this.RunSimpleSampleAsync(kernel);
        await this.RunTextMemoryPluginSampleAsync(kernel);
    }

    private async Task RunSimpleSampleAsync(Kernel kernel)
    {
        this.WriteLine("== Simple Sample: Generating Embeddings ==");

        // Obtain an embedding generator.
        var embeddingGenerator = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

        var generatedEmbeddings = await embeddingGenerator.GenerateEmbeddingAsync("My name is Andrea");
        this.WriteLine($"Generated Embeddings count: {generatedEmbeddings.Length}, " +
                       $"First five: {string.Join(", ", generatedEmbeddings[..5].ToArray())}...");
        this.WriteLine();
    }

    private async Task RunTextMemoryPluginSampleAsync(Kernel kernel)
    {
        this.WriteLine("== Complex Sample: TextMemoryPlugin ==");

        var memoryStore = new VolatileMemoryStore();

        // Obtain an embedding generator to use for semantic memory.
        var embeddingGenerator = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

        // The combination of the text embedding generator and the memory store makes up the 'SemanticTextMemory' object used to
        // store and retrieve memories.
        SemanticTextMemory textMemory = new(memoryStore, embeddingGenerator);

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 1: Store and retrieve memories using the ISemanticTextMemory (textMemory) object.
        //
        // This is a simple way to store memories from a code perspective, without using the Kernel.
        /////////////////////////////////////////////////////////////////////////////////////////////////////
        WriteLine("== PART 1: Saving Memories through the ISemanticTextMemory object ==");

        WriteLine("Saving memory with key 'info1': \"My name is Andrea\"");
        await textMemory.SaveInformationAsync(MemoryCollectionName, id: "info1", text: "My name is Andrea");

        WriteLine("Saving memory with key 'info2': \"I work as a tourist operator\"");
        await textMemory.SaveInformationAsync(MemoryCollectionName, id: "info2", text: "I work as a tourist operator");

        WriteLine("Saving memory with key 'info3': \"I've been living in Seattle since 2005\"");
        await textMemory.SaveInformationAsync(MemoryCollectionName, id: "info3", text: "I've been living in Seattle since 2005");

        WriteLine("Saving memory with key 'info4': \"I visited France and Italy five times since 2015\"");
        await textMemory.SaveInformationAsync(MemoryCollectionName, id: "info4", text: "I visited France and Italy five times since 2015");

        this.WriteLine();

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 2: Create TextMemoryPlugin, store memories through the Kernel.
        //
        // This enables prompt functions and the AI (via Planners) to access memories
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        WriteLine("== PART 2: Saving Memories through the Kernel with TextMemoryPlugin and the 'Save' function ==");

        // Import the TextMemoryPlugin into the Kernel for other functions
        var memoryPlugin = kernel.ImportPluginFromObject(new TextMemoryPlugin(textMemory));

        // Save a memory with the Kernel
        WriteLine("Saving memory with key 'info5': \"My family is from New York\"");
        await kernel.InvokeAsync(memoryPlugin["Save"], new()
        {
            [TextMemoryPlugin.InputParam] = "My family is from New York",
            [TextMemoryPlugin.CollectionParam] = MemoryCollectionName,
            [TextMemoryPlugin.KeyParam] = "info5",
        });

        this.WriteLine();

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 3: Recall similar ideas with semantic search
        //
        // Uses AI Embeddings for fuzzy lookup of memories based on intent, rather than a specific key.
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        WriteLine("== PART 3: Recall (similarity search) with AI Embeddings ==");

        WriteLine("== PART 3a: Recall (similarity search) with ISemanticTextMemory ==");
        WriteLine("Ask: live in Seattle?");

        await foreach (var answer in textMemory.SearchAsync(
                           collection: MemoryCollectionName,
                           query: "live in Seattle?",
                           limit: 2,
                           minRelevanceScore: 0.79,
                           withEmbeddings: true))
        {
            WriteLine($"Answer: {answer.Metadata.Text}");
        }

        /* Possible output:
         Answer: I've been living in Seattle since 2005
        */

        WriteLine("== PART 3b: Recall (similarity search) with Kernel and TextMemoryPlugin 'Recall' function ==");
        WriteLine("Ask: my family is from?");

        var result = await kernel.InvokeAsync(memoryPlugin["Recall"], new()
        {
            [TextMemoryPlugin.InputParam] = "Ask: my family is from?",
            [TextMemoryPlugin.CollectionParam] = MemoryCollectionName,
            [TextMemoryPlugin.LimitParam] = "2",
            [TextMemoryPlugin.RelevanceParam] = "0.79",
        });

        WriteLine($"Answer: {result.GetValue<string>()}");
        WriteLine();

        /* Possible output:
         Answer: ["My family is from New York"]
        */

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 4: TextMemoryPlugin Recall in a Prompt Function
        //
        // Looks up related memories when rendering a prompt template, then sends the rendered prompt to
        // the text generation model to answer a natural language query.
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        WriteLine("== PART 4: Using TextMemoryPlugin 'Recall' function in a Prompt Function ==");

        // Build a prompt function that uses memory to find facts
        const string RecallFunctionDefinition = @"
Consider only the facts below when answering questions:

BEGIN FACTS
About me: {{recall 'live in Seattle?'}}
About me: {{recall 'my family is from?'}}
END FACTS

Question: {{$input}}

Answer:
";

        var aboutMeOracle = kernel.CreateFunctionFromPrompt(RecallFunctionDefinition, new GeminiPromptExecutionSettings() { MaxTokens = 1000 });

        result = await kernel.InvokeAsync(aboutMeOracle, new()
        {
            [TextMemoryPlugin.InputParam] = "Where are my family from?",
            [TextMemoryPlugin.CollectionParam] = MemoryCollectionName,
            [TextMemoryPlugin.LimitParam] = "2",
            [TextMemoryPlugin.RelevanceParam] = "0.79",
        });

        WriteLine("Ask: Where are my family from?");
        WriteLine($"Answer: {result.GetValue<string>()}");

        /* Possible output:
         Answer: New York
        */

        this.WriteLine();

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // PART 5: Cleanup, deleting database collection
        //
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        WriteLine("== PART 5: Cleanup, deleting database collection ==");

        WriteLine("Printing Collections in DB...");
        var collections = memoryStore.GetCollectionsAsync();
        await foreach (var collection in collections)
        {
            WriteLine(collection);
        }

        WriteLine();

        WriteLine($"Removing Collection {MemoryCollectionName}");
        await memoryStore.DeleteCollectionAsync(MemoryCollectionName);
        WriteLine();

        WriteLine($"Printing Collections in DB (after removing {MemoryCollectionName})...");
        collections = memoryStore.GetCollectionsAsync();
        await foreach (var collection in collections)
        {
            WriteLine(collection);
        }
    }

    public Example99_GeminiEmbeddingGeneration(ITestOutputHelper output) : base(output) { }
}
