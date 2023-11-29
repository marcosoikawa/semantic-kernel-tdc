﻿// Copyright (c) Microsoft. All rights reserved.

// ReSharper disable once InconsistentNaming

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example09_FunctionTypes
{
    public static async Task RunAsync()
    {
        Console.WriteLine("======== Native function types ========");

        var kernel = new KernelBuilder()
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .WithOpenAIChatCompletionService(TestConfiguration.OpenAI.ChatModelId, TestConfiguration.OpenAI.ApiKey)
            .Build();

        var arguments = new KernelFunctionArguments();

        // Load native plugin into the kernel function collection, sharing its functions with prompt templates
        var plugin = kernel.ImportPluginFromObject<LocalExamplePlugin>("test");

        string folder = RepoFiles.SamplePluginsPath();
        kernel.ImportPluginFromPromptDirectory(Path.Combine(folder, "SummarizePlugin"));

        // Using Kernel.InvokeAsync
        await kernel.InvokeAsync(plugin["type01"]);
        await kernel.InvokeAsync(kernel.Plugins["test"]["type01"]);

        await kernel.InvokeAsync(plugin["type02"]);
        await kernel.InvokeAsync(kernel.Plugins["test"]["type02"]);

        await kernel.InvokeAsync(plugin["type03"]);
        await kernel.InvokeAsync(kernel.Plugins["test"]["type03"]);

        await kernel.InvokeAsync(plugin["type04"]);
        await kernel.InvokeAsync(kernel.Plugins["test"]["type04"]);

        await kernel.InvokeAsync(plugin["type05"], arguments);
        await kernel.InvokeAsync(kernel.Plugins["test"]["type05"], arguments);

        await kernel.InvokeAsync(plugin["type06"], arguments);
        await kernel.InvokeAsync(kernel.Plugins["test"]["type06"], arguments);

        await kernel.InvokeAsync(plugin["type07"], arguments);
        await kernel.InvokeAsync(kernel.Plugins["test"]["type07"], arguments);

        await kernel.InvokeAsync(plugin["type08"], arguments);
        await kernel.InvokeAsync(kernel.Plugins["test"]["type08"], arguments);

        await kernel.InvokeAsync(plugin["type09"]);
        await kernel.InvokeAsync(kernel.Plugins["test"]["type09"]);
    }
}

public class LocalExamplePlugin
{
    [KernelFunction]
    public void Type01()
    {
        Console.WriteLine("Running function type 1");
    }

    [KernelFunction]
    public string Type02()
    {
        Console.WriteLine("Running function type 2");
        return "";
    }

    [KernelFunction]
    public async Task<string> Type03Async()
    {
        await Task.Delay(0);
        Console.WriteLine("Running function type 3");
        return "";
    }

    [KernelFunction]
    public async Task<string> Type04Async(Kernel kernel)
    {
        var summary = await kernel.InvokeAsync(kernel.Plugins["SummarizePlugin"]["Summarize"], new KernelFunctionArguments() { { "input", "blah blah blah" } });
        Console.WriteLine($"Running function type 4 [{summary?.GetValue<string>()}]");
        return "";
    }

    [KernelFunction]
    public void Type05(string x)
    {
        Console.WriteLine("Running function type 5");
    }

    [KernelFunction]
    public string Type06(string x)
    {
        Console.WriteLine("Running function type 6");
        return "";
    }

    [KernelFunction]
    public async Task<string> Type07Async(string x)
    {
        await Task.Delay(0);
        Console.WriteLine("Running function type 07");
        return "";
    }

    [KernelFunction]
    public async Task Type08Async(string x)
    {
        await Task.Delay(0);
        Console.WriteLine("Running function type 08");
    }

    [KernelFunction]
    public async Task Type09Async()
    {
        await Task.Delay(0);
        Console.WriteLine("Running function type 09");
    }
}
