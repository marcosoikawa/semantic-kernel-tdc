﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Skills.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Skills.OpenAPI.Skills;
using RepoUtils;

namespace KernelSyntaxExamples;

internal class Example20_OpenApiSkill
{
    public static async Task RunAsync()
    {
        await DisplayKlarnaSuggestionsAsync();
    }

    private static async Task DisplayKlarnaSuggestionsAsync()
    {
        var kernel = new KernelBuilder().WithLogger(ConsoleLogger.Log).Build();

        string folder = RepoFiles.SampleSkillsPath();
        var skill = kernel.ImportOpenApiSkillFromDirectory(folder, "Wolframalpha");
        //var skill = await kernel.ImportChatGptPluginSkillFromUrlAsync("Klarna", new Uri("https://www.klarna.com/.well-known/ai-plugin.json"));

        //Add arguments
        var contextVariables = new ContextVariables();
        contextVariables.Set("query", "test query");

        //Run
        var result = await kernel.RunAsync(contextVariables, skill["getWolframCloudResults"]);

        Console.WriteLine("Klarna skill response: {0}", result);
        Console.ReadLine();
    }

    public static async Task GetSecretFromAzureKeyVaultAsync()
    {
        var kernel = new KernelBuilder().WithLogger(ConsoleLogger.Log).Build();

        //Import
        var skill = kernel.ImportOpenApiSkillFromResource(SkillResourceNames.AzureKeyVault, AuthenticateWithBearerToken);

        //Add arguments
        var contextVariables = new ContextVariables();
        contextVariables.Set("server-url", "https://<keyvault-name>.vault.azure.net");
        contextVariables.Set("secret-name", "<secret-name>");
        contextVariables.Set("api-version", "7.0");

        //Run
        var result = await kernel.RunAsync(contextVariables, skill["GetSecret"]);

        Console.WriteLine("GetSecret skill response: {0}", result);
    }

    public static async Task AddSecretToAzureKeyVaultAsync()
    {
        var kernel = new KernelBuilder().WithLogger(ConsoleLogger.Log).Build();

        //Import
        var skill = kernel.ImportOpenApiSkillFromResource(SkillResourceNames.AzureKeyVault, AuthenticateWithBearerToken);

        //Add arguments
        var contextVariables = new ContextVariables();
        contextVariables.Set("server-url", "https://<keyvault-name>.vault.azure.net");
        contextVariables.Set("secret-name", "<secret-name>");
        contextVariables.Set("api-version", "7.0");
        contextVariables.Set("enabled", "true");
        contextVariables.Set("value", "<secret>");

        //Run
        var result = await kernel.RunAsync(contextVariables, skill["SetSecret"]);

        Console.WriteLine("SetSecret skill response: {0}", result);
    }

    private static Task AuthenticateWithBearerToken(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Env.Var("AZURE_KEYVAULT_TOKEN"));
        return Task.CompletedTask;
    }
}
