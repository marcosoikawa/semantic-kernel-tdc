﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Search;

/// <summary>
/// Provides extension methods for interacting with <see cref="ITextSearch"/>.
/// </summary>
public static class TextSearchExtensions
{
    #region KernelPlugin factory methods
    /// <summary>
    /// Creates a plugin from an ITextSearch implementation.
    /// </summary>
    /// <remarks>
    /// The plugin will have a single function called `Search` which
    /// will return a <see cref="IEnumerable{String}"/>
    /// </remarks>
    /// <param name="textSearch">The instance of ITextSearch to be used by the plugin.</param>
    /// <param name="pluginName">The name for the plugin.</param>
    /// <param name="description">A description of the plugin.</param>
    /// <returns>A KernelPlugin instance whose functions correspond to the OpenAPI operations.</returns>
    public static KernelPlugin CreateWithSearch(this ITextSearch textSearch, string pluginName, string? description = null)
    {
        Verify.NotNull(textSearch);
        Verify.NotNull(pluginName);

        return KernelPluginFactory.CreateFromFunctions(pluginName, description, [textSearch.CreateSearch()]);
    }

    /// <summary>
    /// Creates a plugin from an ITextSearch implementation.
    /// </summary>
    /// <remarks>
    /// The plugin will have a single function called `GetSearchResults` which
    /// will return a <see cref="IEnumerable{TextSearchResult}"/>
    /// </remarks>
    /// <param name="textSearch">The instance of ITextSearch to be used by the plugin.</param>
    /// <param name="pluginName">The name for the plugin.</param>
    /// <param name="description">A description of the plugin.</param>
    /// <returns>A KernelPlugin instance whose functions correspond to the OpenAPI operations.</returns>
    public static KernelPlugin CreateWithGetTextSearchResults(this ITextSearch textSearch, string pluginName, string? description = null)
    {
        Verify.NotNull(textSearch);
        Verify.NotNull(pluginName);

        return KernelPluginFactory.CreateFromFunctions(pluginName, description, [textSearch.CreateGetTextSearchResults()]);
    }

    /// <summary>
    /// Creates a plugin from an ITextSearch implementation.
    /// </summary>
    /// <remarks>
    /// The plugin will have a single function called `GetSearchResults` which
    /// will return a <see cref="IEnumerable{TextSearchResult}"/>
    /// </remarks>
    /// <param name="textSearch">The instance of ITextSearch to be used by the plugin.</param>
    /// <param name="pluginName">The name for the plugin.</param>
    /// <param name="description">A description of the plugin.</param>
    /// <returns>A KernelPlugin instance whose functions correspond to the OpenAPI operations.</returns>
    public static KernelPlugin CreateWithGetSearchResults(this ITextSearch textSearch, string pluginName, string? description = null)
    {
        Verify.NotNull(textSearch);
        Verify.NotNull(pluginName);

        return KernelPluginFactory.CreateFromFunctions(pluginName, description, [textSearch.CreateGetSearchResults()]);
    }
    #endregion

    #region KernelFunction factory methods
    /// <summary>
    /// Create a <see cref="KernelFunction"/> which invokes <see cref="ITextSearch.SearchAsync(string, TextSearchOptions?, CancellationToken)"/>.
    /// </summary>
    /// <param name="textSearch">The ITextSearch instance to use.</param>
    /// <param name="options">Optional KernelFunctionFromMethodOptions which allow the KernelFunction metadata to be specified.</param>
    /// <param name="mapToString">Optional MapSearchResultToString delegate which modifies how the search result is converted to a string.</param>
    /// <param name="searchOptions">Optional TextSearchOptions which override the options provided when the function is invoked.</param>
    public static KernelFunction CreateSearch(this ITextSearch textSearch, KernelFunctionFromMethodOptions? options = null, MapSearchResultToString? mapToString = null, TextSearchOptions? searchOptions = null)
    {
        async Task<IEnumerable<string>> SearchAsync(Kernel kernel, KernelFunction function, KernelArguments arguments, CancellationToken cancellationToken)
        {
            arguments.TryGetValue("query", out var query);
            if (string.IsNullOrEmpty(query?.ToString()))
            {
                return [];
            }

            var parameters = function.Metadata.Parameters;

            searchOptions ??= new()
            {
                Count = GetArgumentValue(arguments, parameters, "count", 2),
                Offset = GetArgumentValue(arguments, parameters, "skip", 0)
            };

            var result = await textSearch.SearchAsync(query?.ToString()!, searchOptions, cancellationToken).ConfigureAwait(false);
            var resultList = await result.Results.ToListAsync(cancellationToken).ConfigureAwait(false);
            return MapToStrings(resultList, mapToString);
        }

        options ??= DefaultSearchMethodOptions();
        return KernelFunctionFactory.CreateFromMethod(
                SearchAsync,
                options);
    }

    /// <summary>
    /// Create a <see cref="KernelFunction"/> which invokes <see cref="ITextSearch.GetTextSearchResultsAsync(string, TextSearchOptions?, CancellationToken)"/>.
    /// </summary>
    /// <param name="textSearch">The ITextSearch instance to use.</param>
    /// <param name="options">Optional KernelFunctionFromMethodOptions which allow the KernelFunction metadata to be specified.</param>
    /// <param name="searchOptions">Optional TextSearchOptions which override the options provided when the function is invoked.</param>
    public static KernelFunction CreateGetTextSearchResults(this ITextSearch textSearch, KernelFunctionFromMethodOptions? options = null, TextSearchOptions? searchOptions = null)
    {
        async Task<IEnumerable<TextSearchResult>> GetTextSearchResultAsync(Kernel kernel, KernelFunction function, KernelArguments arguments, CancellationToken cancellationToken)
        {
            arguments.TryGetValue("query", out var query);
            if (string.IsNullOrEmpty(query?.ToString()))
            {
                return [];
            }

            var parameters = function.Metadata.Parameters;

            searchOptions ??= new()
            {
                Count = GetArgumentValue(arguments, parameters, "count", 2),
                Offset = GetArgumentValue(arguments, parameters, "skip", 0)
            };

            var result = await textSearch.GetTextSearchResultsAsync(query?.ToString()!, searchOptions, cancellationToken).ConfigureAwait(false);
            return await result.Results.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        options ??= DefaultGetTextSearchResultsMethodOptions();
        return KernelFunctionFactory.CreateFromMethod(
                GetTextSearchResultAsync,
                options);
    }

    /// <summary>
    /// Create a <see cref="KernelFunction"/> which invokes <see cref="ITextSearch.GetSearchResultsAsync(string, TextSearchOptions?, CancellationToken)"/>.
    /// </summary>
    /// <param name="textSearch">The ITextSearch instance to use.</param>
    /// <param name="options">Optional KernelFunctionFromMethodOptions which allow the KernelFunction metadata to be specified.</param>
    /// <param name="searchOptions">Optional TextSearchOptions which override the options provided when the function is invoked.</param>
    public static KernelFunction CreateGetSearchResults(this ITextSearch textSearch, KernelFunctionFromMethodOptions? options = null, TextSearchOptions? searchOptions = null)
    {
        async Task<IEnumerable<object>> GetSearchResultAsync(Kernel kernel, KernelFunction function, KernelArguments arguments, CancellationToken cancellationToken)
        {
            arguments.TryGetValue("query", out var query);
            if (string.IsNullOrEmpty(query?.ToString()))
            {
                return [];
            }

            var parameters = function.Metadata.Parameters;

            searchOptions ??= new()
            {
                Count = GetArgumentValue(arguments, parameters, "count", 2),
                Offset = GetArgumentValue(arguments, parameters, "skip", 0)
            };

            var result = await textSearch.GetSearchResultsAsync(query?.ToString()!, searchOptions, cancellationToken).ConfigureAwait(false);
            return await result.Results.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        options ??= DefaultGetSearchResultsMethodOptions();
        return KernelFunctionFactory.CreateFromMethod(
                GetSearchResultAsync,
                options);
    }
    #endregion

    #region private
    /// <summary>
    /// Get the argument value from <see cref="KernelArguments"/> or users default value from
    /// <see cref="KernelReturnParameterMetadata"/> or default to the provided value.
    /// </summary>
    /// <param name="arguments">KernelArguments instance.</param>
    /// <param name="parameters">List of KernelReturnParameterMetadata.</param>
    /// <param name="name">Name of the argument.</param>
    /// <param name="defaultValue">Default value of the argument.</param>
    private static int GetArgumentValue(KernelArguments arguments, IReadOnlyList<KernelParameterMetadata> parameters, string name, int defaultValue)
    {
        arguments.TryGetValue(name, out var value);
        return (value as int?) ?? GetDefaultValue(parameters, "count", defaultValue);
    }

    /// <summary>
    /// Get the argument value <see cref="KernelReturnParameterMetadata"/> or default to the provided value.
    /// </summary>
    /// <param name="parameters">List of KernelReturnParameterMetadata.</param>
    /// <param name="name">Name of the argument.</param>
    /// <param name="defaultValue">Default value of the argument.</param>
    private static int GetDefaultValue(IReadOnlyList<KernelParameterMetadata> parameters, string name, int defaultValue)
    {
        var value = parameters.FirstOrDefault(parameter => parameter.Name == name)?.DefaultValue;
        return value is int intValue ? intValue : defaultValue;
    }

    /// <summary>
    /// Utility method to map a collection of arbitrary search results to a list of strings.
    /// </summary>
    /// <param name="resultList">Collection of search results.</param>
    /// <param name="mapToString">Optional mapper function to convert a search result to a string.</param>
    /// <returns></returns>
    private static List<string> MapToStrings(IEnumerable<object> resultList, MapSearchResultToString? mapToString = null)
    {
        mapToString ??= DefaultMapSearchResultToString;

        return resultList.Select(result => mapToString(result)).ToList();
    }

    /// <summary>
    /// Default mapper which converts an arbitrary search result to a string using JSON serialization.
    /// </summary>
    /// <param name="result">Search result.</param>
    private static string DefaultMapSearchResultToString(object result)
    {
        if (result is string stringValue)
        {
            return stringValue;
        }
        return JsonSerializer.Serialize(result);
    }

    /// <summary>
    /// Create the default <see cref="KernelFunctionFromMethodOptions"/> for <see cref="ITextSearch.SearchAsync(string, TextSearchOptions?, CancellationToken)"/>.
    /// </summary>
    private static KernelFunctionFromMethodOptions DefaultSearchMethodOptions() =>
        new()
        {
            FunctionName = "Search",
            Description = "Perform a search for content related to the specified query and return string results",
            Parameters =
            [
                new KernelParameterMetadata("query") { Description = "What to search for", IsRequired = true },
                new KernelParameterMetadata("count") { Description = "Number of results", IsRequired = false, DefaultValue = 2 },
                new KernelParameterMetadata("skip") { Description = "Number of results to skip", IsRequired = false, DefaultValue = 0 },
            ],
            ReturnParameter = new() { ParameterType = typeof(KernelSearchResults<string>) },
        };

    /// <summary>
    /// Create the default <see cref="KernelFunctionFromMethodOptions"/> for <see cref="ITextSearch.GetTextSearchResultsAsync(string, TextSearchOptions?, CancellationToken)"/>.
    /// </summary>
    private static KernelFunctionFromMethodOptions DefaultGetTextSearchResultsMethodOptions() =>
        new()
        {
            FunctionName = "GetTextSearchResults",
            Description = "Perform a search for content related to the specified query. The search will return the name, value and link for the related content.",
            Parameters =
            [
                new KernelParameterMetadata("query") { Description = "What to search for", IsRequired = true },
                new KernelParameterMetadata("count") { Description = "Number of results", IsRequired = false, DefaultValue = 2 },
                new KernelParameterMetadata("skip") { Description = "Number of results to skip", IsRequired = false, DefaultValue = 0 },
            ],
            ReturnParameter = new() { ParameterType = typeof(KernelSearchResults<TextSearchResult>) },
        };

    /// <summary>
    /// Create the default <see cref="KernelFunctionFromMethodOptions"/> for <see cref="ITextSearch.GetSearchResultsAsync(string, TextSearchOptions?, CancellationToken)"/>.
    /// </summary>
    private static KernelFunctionFromMethodOptions DefaultGetSearchResultsMethodOptions() =>
        new()
        {
            FunctionName = "GetSearchResults",
            Description = "Perform a search for content related to the specified query.",
            Parameters =
            [
                new KernelParameterMetadata("query") { Description = "What to search for", IsRequired = true },
                new KernelParameterMetadata("count") { Description = "Number of results", IsRequired = false, DefaultValue = 2 },
                new KernelParameterMetadata("skip") { Description = "Number of results to skip", IsRequired = false, DefaultValue = 0 },
            ],
            ReturnParameter = new() { ParameterType = typeof(KernelSearchResults<TextSearchResult>) },
        };
    #endregion
}
