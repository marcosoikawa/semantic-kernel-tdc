﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Planners.Handlebars;
using Plugins.DictionaryPlugin;
using RepoUtils;

/**
 * This example shows how to use the Handlebars sequential planner.
 */
public static class Example65_HandlebarsPlanner
{
    private static int s_sampleIndex;

    private const string CourseraPluginName = "CourseraPlugin";

    /// <summary>
    /// Show how to create a plan with Handlebars and execute it.
    /// </summary>
    public static async Task RunAsync()
    {
        s_sampleIndex = 1;
        bool shouldPrintPrompt = true;

        // Using primitive types as inputs and outputs
        await PlanNotPossibleSampleAsync();
        await RunDictionaryWithBasicTypesSampleAsync();
        await RunPoetrySampleAsync();
        await RunBookSampleAsync();

        // Using Complex Types as inputs and outputs
        await RunLocalDictionaryWithComplexTypesSampleAsync(shouldPrintPrompt);

        // TODO (@teresaqhoang): Enable this sample once complex types are supported for remote plugins return type.
        // await RunRemoteDictionaryWithComplexTypesSampleAsync();
    }

    private static void WriteSampleHeadingToConsole(string name)
    {
        Console.WriteLine($"======== [Handlebars Planner] Sample {s_sampleIndex++} - Create and Execute {name} Plan ========");
    }

    private static async Task RunSampleAsync(string goal, bool shouldPrintPrompt = false, params string[] pluginDirectoryNames)
    {
        string apiKey = TestConfiguration.AzureOpenAI.ApiKey;
        string chatDeploymentName = TestConfiguration.AzureOpenAI.ChatDeploymentName;
        string endpoint = TestConfiguration.AzureOpenAI.Endpoint;

        if (apiKey == null || chatDeploymentName == null || endpoint == null)
        {
            Console.WriteLine("Azure endpoint, apiKey, or deploymentName not found. Skipping example.");
            return;
        }

        var kernel = new KernelBuilder()
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .WithAzureOpenAIChatCompletionService(
                deploymentName: chatDeploymentName,
                endpoint: endpoint,
                serviceId: "AzureOpenAIChat",
                apiKey: apiKey)
            .Build();

        if (pluginDirectoryNames[0] == BasicDictionaryPlugin.PluginName)
        {
            kernel.ImportFunctions(new BasicDictionaryPlugin(), BasicDictionaryPlugin.PluginName);
        }
        else if (pluginDirectoryNames[0] == ComplexDictionaryPlugin.PluginName)
        {
            kernel.ImportFunctions(new ComplexDictionaryPlugin(), ComplexDictionaryPlugin.PluginName);
        }
        else if (pluginDirectoryNames[0] == CourseraPluginName)
        {
            await kernel.ImportOpenApiPluginFunctionsAsync(
                CourseraPluginName,
                new Uri("https://www.coursera.org/api/rest/v1/search/openapi.yaml")
                );
        }
        else
        {
            string folder = RepoFiles.SamplePluginsPath();
            kernel.ImportSemanticFunctionsFromDirectory(folder, pluginDirectoryNames);
        }

        // The gpt-35-turbo model does not handle loops well in the plans.
        var allowLoopsInPlan = !chatDeploymentName.Contains("gpt-35-turbo", StringComparison.OrdinalIgnoreCase);
        var planner = new HandlebarsPlanner(
            kernel,
            new HandlebarsPlannerConfig()
            {
                // Change this if you want to test with loops regardless of model selection.
                AllowLoops = allowLoopsInPlan
            });

        Console.WriteLine($"Goal: {goal}");

        // Create the plan
        var plan = await planner.CreatePlanAsync(goal);

        if (shouldPrintPrompt)
        {
            // Print the prompt template
            Console.WriteLine($"\nPrompt template:\n{plan.Prompt}");
        }

        Console.WriteLine($"\nOriginal plan:\n{plan}");

        // Execute the plan
        var result = plan.Invoke(kernel.CreateNewContext(), new Dictionary<string, object?>(), CancellationToken.None);
        Console.WriteLine($"\nResult:\n{result.GetValue<string>()}\n");
    }

    private static async Task PlanNotPossibleSampleAsync(bool shouldPrintPrompt = false)
    {
        WriteSampleHeadingToConsole("Plan Not Possible");

        try
        {
            // Load additional plugins to enable planner but not enough for the given goal.
            await RunSampleAsync("Send Mary an email with the list of meetings I have scheduled today.", shouldPrintPrompt, "SummarizePlugin");
        }
        catch (SKException e)
        {
            /*
                Unable to create plan for goal with available functions.
                Goal: Email me a list of meetings I have scheduled today.
                Available Functions: SummarizePlugin-Notegen, SummarizePlugin-Summarize, SummarizePlugin-MakeAbstractReadable, SummarizePlugin-Topics
                Planner output:
                I'm sorry, but it seems that the provided helpers do not include any helper to fetch or filter meetings scheduled for today. 
                Therefore, I cannot create a Handlebars template to achieve the specified goal with the available helpers. 
                Additional helpers may be required.
            */
            Console.WriteLine($"{e.Message}\n");
        }
    }

    private static async Task RunDictionaryWithBasicTypesSampleAsync(bool shouldPrintPrompt = false)
    {
        WriteSampleHeadingToConsole("Dictionary");
        await RunSampleAsync("Get a random word and its definition.", shouldPrintPrompt, BasicDictionaryPlugin.PluginName);
        /*
            Original plan:
            {{!-- Step 1: Get a random word --}}
            {{set "randomWord" (DictionaryPlugin-GetRandomWord)}}

            {{!-- Step 2: Get the definition of the random word --}}
            {{set "definition" (DictionaryPlugin-GetDefinition word=(get "randomWord"))}}

            {{!-- Step 3: Output the random word and its definition --}}
            {{json (array (get "randomWord") (get "definition"))}}

            Result:
            ["book","a set of printed or written pages bound together along one edge"]
        */
    }

    private static async Task RunLocalDictionaryWithComplexTypesSampleAsync(bool shouldPrintPrompt = false)
    {
        // Test with Coursera APIs, which uses complex types as inputs and outputs. 
        WriteSampleHeadingToConsole("Complex Types with Local Dictionary Plugin");
        await RunSampleAsync("Teach me two random words and their definition.", shouldPrintPrompt, ComplexDictionaryPlugin.PluginName);
        /*
            Original Plan:
            {{!-- Step 1: Get two random dictionary entries --}}
            {{set "entry1" (DictionaryPlugin-GetRandomEntry)}}
            {{set "entry2" (DictionaryPlugin-GetRandomEntry)}}

            {{!-- Step 2: Extract words from the entries --}}
            {{set "word1" (DictionaryPlugin-GetWord entry=(get "entry1"))}}
            {{set "word2" (DictionaryPlugin-GetWord entry=(get "entry2"))}}

            {{!-- Step 3: Extract definitions for the words --}}
            {{set "definition1" (DictionaryPlugin-GetDefinition word=(get "word1"))}}
            {{set "definition2" (DictionaryPlugin-GetDefinition word=(get "word2"))}}

            {{!-- Step 4: Display the words and their definitions --}}
            Word 1: {{json (get "word1")}}
            Definition: {{json (get "definition1")}}

            Word 2: {{json (get "word2")}}
            Definition: {{json (get "definition2")}}

            Result:
            Word 1: apple
            Definition 1: a round fruit with red, green, or yellow skin and a white flesh

            Word 2: dog
            Definition 2: a domesticated animal with four legs, a tail, and a keen sense of smell that is often used for hunting or companionship
        */
    }

    private static async Task RunRemoteDictionaryWithComplexTypesSampleAsync(bool shouldPrintPrompt = false)
    {
        WriteSampleHeadingToConsole("Complex Types with Coursera OpenAPI Plugin");

        try
        {
            await RunSampleAsync("I'd like to learn about prompt engineering. Please recommend some free courses.", shouldPrintPrompt, CourseraPluginName);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("======== DONE RunRemoteDictionaryWithComplexTypesSampleAsync ========");
        }
    }

    private static async Task RunPoetrySampleAsync(bool shouldPrintPrompt = false)
    {
        WriteSampleHeadingToConsole("Poetry");
        await RunSampleAsync("Write a poem about John Doe, then translate it into Italian.", shouldPrintPrompt, "SummarizePlugin", "WriterPlugin");
        /*
            Original plan:
            {{!-- Step 1: Initialize the scenario for the poem --}}
            {{set "scenario" "John Doe, a mysterious and kind-hearted person"}}

            {{!-- Step 2: Generate a short poem about John Doe --}}
            {{set "poem" (WriterPlugin-ShortPoem input=(get "scenario"))}}

            {{!-- Step 3: Translate the poem into Italian --}}
            {{set "translatedPoem" (WriterPlugin-Translate input=(get "poem") language="Italian")}}

            {{!-- Step 4: Output the translated poem --}}
            {{json (get "translatedPoem")}}

            Result:
            C'era una volta un uomo di nome John Doe,
            La cui gentilezza si mostrava costantemente,
            Aiutava con un sorriso,
            E non si arrendeva mai,
            Al mistero che lo faceva brillare.
        */
    }

    private static async Task RunBookSampleAsync(bool shouldPrintPrompt = false)
    {
        WriteSampleHeadingToConsole("Book Creation");
        await RunSampleAsync("Create a book with 3 chapters about a group of kids in a club called 'The Thinking Caps.'", shouldPrintPrompt, "WriterPlugin", "MiscPlugin");
        /*
            Original plan:
            {{!-- Step 1: Initialize the book title and chapter count --}}
            {{set "bookTitle" "The Thinking Caps"}}
            {{set "chapterCount" 3}}

            {{!-- Step 2: Generate the novel outline with the given chapter count --}}
            {{set "novelOutline" (WriterPlugin-NovelOutline input=(get "bookTitle") chapterCount=(get "chapterCount"))}}

            {{!-- Step 3: Loop through the chapters and generate the content for each chapter --}}
            {{#each (range 1 (get "chapterCount"))}}
                {{set "chapterIndex" this}}
                {{set "chapterSynopsis" (MiscPlugin-ElementAtIndex input=(get "novelOutline") index=(get "chapterIndex"))}}
                {{set "previousChapterSynopsis" (MiscPlugin-ElementAtIndex input=(get "novelOutline") index=(get "chapterIndex" - 1))}}
                
                {{!-- Step 4: Write the chapter content using the WriterPlugin-NovelChapter helper --}}
                {{set "chapterContent" (WriterPlugin-NovelChapter input=(get "chapterSynopsis") theme=(get "bookTitle") previousChapter=(get "previousChapterSynopsis") chapterIndex=(get "chapterIndex"))}}
                
                {{!-- Step 5: Output the chapter content --}}
                {{json (get "chapterContent")}}
            {{/each}}
        */
    }
}
