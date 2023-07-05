﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using RepoUtils;

/**
 * These examples show how to use HttpClient and HttpClientFactory within SK SDK.
 */

public static class Example41_HttpClientUsage
{
    private static string openAIApiKey = config.GetValue<string>("OpenAI__ApiKey");
    private static string openAIModelId = config.GetValue<string>("OpenAI__ModelId");

    public static void Run(IConfigurationRoot config)
    {
        //Examples showing how to use HttpClient.
        UseDefaultHttpClientAsync();

        UseCustomHttpClientAsync();

        //Examples showing how to use HttpClientFactory.
        UseBasicRegistrationWithHttpClientFactoryAsync();

        UseNamedRegistrationWitHttpClientFactoryAsync();
    }

    /// <summary>
    /// Demonstrates the usage of the default HttpClient provided by the SK SDK.
    /// </summary>
    private static void UseDefaultHttpClientAsync()
    {
        var kernel = Kernel.Builder
            .WithOpenAITextCompletionService(
                modelId: openAIModelId,
                apiKey: openAIApiKey) // If you need to use the default HttpClient from the SK SDK, simply omit the argument for the httpMessageInvoker parameter.
            .Build();
    }

    /// <summary>
    /// Demonstrates the usage of a custom HttpClient.
    /// </summary>
    private static void UseCustomHttpClientAsync()
    {
        using var httpClient = new HttpClient();

        // If you need to use a custom HttpClient, simply pass it as an argument for the httpClient parameter.
        var kernel = Kernel.Builder
            .WithOpenAITextCompletionService(openAIModelId, openAIApiKey, httpClient: httpClient)
            .Build();
    }

    /// <summary>
    /// Demonstrates the "basic usage" approach for HttpClientFactory.
    /// </summary>
    private static void UseBasicRegistrationWithHttpClientFactoryAsync()
    {
        //More details - https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory#basic-usage
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddHttpClient();

        var kernel = serviceCollection.AddTransient<IKernel>((sp) =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();

            var kernel = Kernel.Builder
            .WithOpenAITextCompletionService(
                modelId: openAIModelId,
                apiKey: openAIApiKey,
                httpClient: factory.CreateClient())
            .Build();

            return kernel;
        });
    }

    /// <summary>
    /// Demonstrates the "named clients" approach for HttpClientFactory.
    /// </summary>
    private static void UseNamedRegistrationWitHttpClientFactoryAsync()
    {
        // More details https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory#named-clients

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddHttpClient();

        //Registration of a named HttpClient.
        serviceCollection.AddHttpClient("test-client", (client) =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/v1/", UriKind.Absolute);
        });

        var kernel = serviceCollection.AddTransient<IKernel>((sp) =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();

            var kernel = Kernel.Builder
            .WithOpenAITextCompletionService(
                modelId: openAIModelId,
                apiKey: openAIApiKey,
                httpClient: factory.CreateClient("test-client"))
            .Build();

            return kernel;
        });
    }
}
