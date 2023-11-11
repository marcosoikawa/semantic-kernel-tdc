﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Functions.Yaml.Functions;
using Microsoft.SemanticKernel.Models;
using Microsoft.SemanticKernel.TemplateEngine;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the namespace of IKernel
namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

/// <summary>
/// Class for extensions methods to define semantic functions using YAML format.
/// </summary>
public static class KernelSemanticFunctionYamlExtensions
{
    /// <summary>
    /// Creates an <see cref="ISKFunction"/> instance for a semantic function using the specified YAML resource.
    /// </summary>
    /// <param name="kernel">Kernel instance</param>
    /// <param name="resourceName">Resource containing the YAML representation of the <see cref="SemanticFunctionConfig"/> to use to create the semantic function</param>
    /// <param name="pluginName">The optional name of the plug-in associated with this method.</param>
    /// <param name="promptTemplateFactory">>Prompt template factory.</param>
    /// <returns>The created <see cref="ISKFunction"/>.</returns>
    public static ISKFunction CreateFromYamlResource(
        this IKernel kernel,
        string resourceName,
        string? pluginName = null,
        IPromptTemplateFactory? promptTemplateFactory = null)
    {
        return SKFunctionYaml.CreateFromResource(resourceName, pluginName, promptTemplateFactory, kernel.LoggerFactory);
    }

    /// <summary>
    /// Creates an <see cref="ISKFunction"/> instance for a semantic function using the specified YAML.
    /// </summary>
    /// <param name="kernel">Kernel instance</param>
    /// <param name="text">YAML representation of the <see cref="SemanticFunctionConfig"/> to use to create the semantic function</param>
    /// <param name="pluginName">The optional name of the plug-in associated with this method.</param>
    /// <param name="promptTemplateFactory">>Prompt template factory.</param>
    /// <returns>The created <see cref="ISKFunction"/>.</returns>
    public static ISKFunction CreateFromYaml(
        this IKernel kernel,
        string text,
        string? pluginName = null,
        IPromptTemplateFactory? promptTemplateFactory = null)
    {
        return SKFunctionYaml.CreateFromYaml(text, pluginName, promptTemplateFactory, kernel.LoggerFactory);
    }
}
