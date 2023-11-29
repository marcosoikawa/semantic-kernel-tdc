﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace - Using the main namespace
namespace Microsoft.SemanticKernel;

/// <summary>Provides extension methods for working with <see cref="IKernelPlugin"/>s and collections of them.</summary>
public static class IKernelPluginExtensions
{
    /// <summary>Gets whether the plugin contains a function with the specified name.</summary>
    /// <param name="plugin">The plugin.</param>
    /// <param name="functionName">The name of the function.</param>
    /// <returns>true if the plugin contains the specified function; otherwise, false.</returns>
    public static bool Contains(this IKernelPlugin plugin, string functionName)
    {
        Verify.NotNull(plugin);
        Verify.NotNull(functionName);

        return plugin.TryGetFunction(functionName, out _);
    }

    /// <summary>Gets whether the plugin contains a function.</summary>
    /// <param name="plugin">The plugin.</param>
    /// <param name="function">The function.</param>
    /// <returns>true if the plugin contains the specified function; otherwise, false.</returns>
    public static bool Contains(this IKernelPlugin plugin, KernelFunction function)
    {
        Verify.NotNull(plugin);
        Verify.NotNull(function);

        return plugin.TryGetFunction(function.Name, out KernelFunction? found) && found == function;
    }

    /// <summary>Gets whether the plugins collection contains a plugin with the specified name.</summary>
    /// <param name="plugins">The plugins collections.</param>
    /// <param name="pluginName">The name of the plugin.</param>
    /// <returns>true if the plugins contains a plugin with the specified name; otherwise, false.</returns>
    public static bool Contains(this IReadOnlyKernelPluginCollection plugins, string pluginName)
    {
        Verify.NotNull(plugins);
        Verify.NotNull(pluginName);

        return plugins.TryGetPlugin(pluginName, out _);
    }

    /// <summary>Gets a function from the collection by plugin and function names.</summary>
    /// <param name="plugins">The collection.</param>
    /// <param name="pluginName">The name of the plugin storing the function.</param>
    /// <param name="functionName">The name of the function.</param>
    /// <returns>The function from the collection.</returns>
    public static KernelFunction GetFunction(this IReadOnlyKernelPluginCollection plugins, string? pluginName, string functionName)
    {
        Verify.NotNull(plugins);
        Verify.NotNull(functionName);

        if (!TryGetFunction(plugins, pluginName, functionName, out KernelFunction? function))
        {
            throw new KeyNotFoundException("The plugin collection does not contain a plugin and/or function with the specified names.");
        }

        return function;
    }

    /// <summary>Gets a function from the collection by plugin and function names.</summary>
    /// <param name="plugins">The collection.</param>
    /// <param name="pluginName">The name of the plugin storing the function.</param>
    /// <param name="functionName">The name of the function.</param>
    /// <param name="func">The function, if found.</param>
    /// <returns>true if the specified plugin was found and the specified function was found in that plugin; otherwise, false.</returns>
    /// <remarks>
    /// If <paramref name="pluginName"/> is null or entirely whitespace, all plugins are searched for a function with the specified name,
    /// and the first one found is returned.
    /// </remarks>
    public static bool TryGetFunction(this IReadOnlyKernelPluginCollection plugins, string? pluginName, string functionName, [NotNullWhen(true)] out KernelFunction? func)
    {
        Verify.NotNull(plugins);
        Verify.NotNull(functionName);

        if (string.IsNullOrWhiteSpace(pluginName))
        {
            foreach (IKernelPlugin p in plugins)
            {
                if (p.TryGetFunction(functionName, out func))
                {
                    return true;
                }
            }
        }
        else
        {
            if (plugins.TryGetPlugin(pluginName!, out IKernelPlugin? plugin) &&
                plugin.TryGetFunction(functionName, out func))
            {
                return true;
            }
        }

        func = null;
        return false;
    }

    /// <summary>Gets a collection of <see cref="KernelPluginMetadata"/> instances, one for every plugin in the plugins collection.</summary>
    /// <param name="plugins">The plugins collection.</param>
    /// <returns>A list of metadata over every function in the plugins collection</returns>
    public static IList<KernelPluginMetadata> GetPluginsMetadata(this IEnumerable<IKernelPlugin> plugins)
    {
        Verify.NotNull(plugins);

        List<KernelPluginMetadata> pluginsMetadata = new();
        foreach (IKernelPlugin plugin in plugins)
        {
            List<KernelFunctionMetadata> functionsMetadata = new();
            var pluginMetadata = new KernelPluginMetadata(plugin.Name)
            {
                Description = plugin.Description,
                FunctionsMetadata = functionsMetadata,
            };
            foreach (KernelFunction function in plugin)
            {
                functionsMetadata.Add(new KernelFunctionMetadata(function.Metadata));
            }
        }

        return pluginsMetadata;
    }

    /// <summary>Gets a collection of <see cref="KernelFunctionMetadata"/> instances, one for every function in every plugin in the plugins collection.</summary>
    /// <param name="plugins">The plugins collection.</param>
    /// <returns>A list of metadata over every function in the plugins collection</returns>
    public static IList<KernelFunctionMetadata> GetFunctionsMetadata(this IEnumerable<IKernelPlugin> plugins)
    {
        Verify.NotNull(plugins);

        List<KernelFunctionMetadata> metadata = new();
        foreach (IKernelPlugin plugin in plugins)
        {
            foreach (KernelFunction function in plugin)
            {
                metadata.Add(new KernelFunctionMetadata(function.Metadata) { PluginName = plugin.Name });
            }
        }

        return metadata;
    }
}
