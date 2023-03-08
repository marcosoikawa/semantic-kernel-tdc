﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Memory;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example15_MemorySkill
{
    private const string MemoryCollectionName = "aboutMe";

    public static async Task RunAsync()
    {
        var kernel = Kernel.Builder
            .WithLogger(ConsoleLogger.Log)
            .Configure(c =>
            {
                c.AddOpenAICompletionBackend("davinci", "text-davinci-003", Env.Var("OPENAI_API_KEY"));
                c.AddOpenAIEmbeddingsBackend("ada", "text-embedding-ada-002", Env.Var("OPENAI_API_KEY"));
            })
            .WithMemoryStorage(new VolatileMemoryStore())
            .Build();

        // ========= Store memories using the kernel =========

        await kernel.Memory.SaveInformationAsync(MemoryCollectionName, id: "info1", text: "My name is Andrea");
        await kernel.Memory.SaveInformationAsync(MemoryCollectionName, id: "info2", text: "I work as a tourist operator");
        await kernel.Memory.SaveInformationAsync(MemoryCollectionName, id: "info3", text: "I've been living in Seattle since 2005");
        await kernel.Memory.SaveInformationAsync(MemoryCollectionName, id: "info4", text: "I visited France and Italy five times since 2015");

        // ========= Store memories using semantic function =========

        // Add Memory as a skill for other functions
        var memorySkill = new TextMemorySkill();
        kernel.ImportSkill(new TextMemorySkill());

        // Build a semantic function that saves info to memory
        const string SAVE_FUNCTION_DEFINITION = @"{{save $info}}";
        var memorySaver = kernel.CreateSemanticFunction(SAVE_FUNCTION_DEFINITION);

        var context = kernel.CreateNewContext();
        context[TextMemorySkill.CollectionParam] = MemoryCollectionName;
        context[TextMemorySkill.KeyParam] = "info5";
        context["info"] = "My family is from New York";
        await memorySaver.InvokeAsync(context);

        // ========= Test memory remember =========
        Console.WriteLine("========= Example: Recalling a Memory =========");

        context[TextMemorySkill.KeyParam] = "info1";
        var answer = await memorySkill.RecallMemoryAsync(context);
        Console.WriteLine("Memory associated with 'info1': {0}", answer);
        /*
        Output:
        "Memory associated with 'info1': My name is Andrea
        */

        // ========= Test memory recall =========
        Console.WriteLine("========= Example: Recalling an Idea =========");

        context[TextMemorySkill.LimitParam] = "2";
        context[TextMemorySkill.JoinParam] = "\n";
        string ask = "where did I grow up?";
        answer = await memorySkill.RecallIdeaAsync(ask, context);
        Console.WriteLine("Ask: {0}", ask);
        Console.WriteLine("Answer:\n{0}", answer);

        ask = "where do I live?";
        answer = await memorySkill.RecallIdeaAsync(ask, context);
        Console.WriteLine("Ask: {0}", ask);
        Console.WriteLine("Answer:\n{0}", answer);

        /*
        Output:

            Ask: where did I grow up?
            Answer:
            My family is from New York
            I've been living in Seattle since 2005 

            Ask: where do I live?
            Answer:
            I've been living in Seattle since 200
            My family is from New York
        */

        // ========= Use memory in a semantic function =========
        Console.WriteLine("========= Example: Using Recall in a Semantic Function =========");

        // Build a semantic function that uses memory to find facts
        const string RECALL_FUNCTION_DEFINITION = @"
Consider only the facts below when answering questions.

About me: {{recallidea $fact1}}
About me: {{recallidea $fact2}}

Question: {{$query}}

Answer:
";

        var aboutMeOracle = kernel.CreateSemanticFunction(RECALL_FUNCTION_DEFINITION, maxTokens: 100);

        context["fact1"] = "where did I grow up?";
        context["fact2"] = "where do I live?";
        context["query"] = "Do I live in the same town where I grew up?";
        context[TextMemorySkill.RelevanceParam] = "0.8";

        var result = await aboutMeOracle.InvokeAsync(context);

        Console.WriteLine(context["query"] + "\n");
        Console.WriteLine(result);

        /*
        Output:

            Do I live in the same town where I grew up?

            No, I do not live in the same town where I grew up since my family is from New York and I have been living in Seattle since 2005.
        */

        // ========= Forget a memory =========
        Console.WriteLine("========= Example: Forgetting a Memory =========");

        context["fact1"] = "What is my name?";
        context["fact2"] = "What do I do for a living?";
        context["query"] = "Tell me a bit about myself";
        context[TextMemorySkill.RelevanceParam] = ".75";

        result = await aboutMeOracle.InvokeAsync(context);

        Console.WriteLine(context["query"] + "\n");
        Console.WriteLine(result);

        /*
        Output:
            Tell me a bit about myself

            I am Andrea and I come from a family from New York. I work as a tourist operator,
            helping people plan their trips and find the best places to visit.
        */

        context[TextMemorySkill.KeyParam] = "info1";
        await memorySkill.ForgetMemoryAsync(context);

        result = await aboutMeOracle.InvokeAsync(context);

        Console.WriteLine(context["query"] + "\n");
        Console.WriteLine(result);
        /*
        Output:
            Tell me a bit about myself

            I'm originally from New York and have been living in Seattle since 2005. I currently work as a
            tourist operator, helping people plan their trips and find the best places to visit.
        */

        // ========= Forget an idea =========
        Console.WriteLine("========= Example: Forgetting an Idea =========");
        await memorySkill.ForgetIdeaAsync("my location", context);

        context["fact1"] = "where did I grow up?";
        context["fact2"] = "where do I live?";
        context["query"] = "Do I live in the same town where I grew up?";
        context[TextMemorySkill.RelevanceParam] = "0.8";

        result = await aboutMeOracle.InvokeAsync(context);

        Console.WriteLine(context["query"] + "\n");
        Console.WriteLine(result);

        /*
        Output:

            Do I live in the same town where I grew up?

            No, since I have been living in Seattle since 2005, it is likely that I did not grow up in Seattle.
        */
    }
}
