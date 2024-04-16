﻿// Copyright (c) Microsoft. All rights reserved.
using System.Text.Json;

namespace Microsoft.SemanticKernel.Agents.Chat;
/// <summary>
/// Supports parsing json from a text block that may contain literals delimiters:
/// <example>
/// [json]
/// </example>
/// <example>
/// ```
/// [json]
/// ```
/// </example>
/// <example>
/// ```json
/// [json]
/// ```
/// </example>
/// </summary>
public static class JsonResultTranslator
{
    private const string LiteralDelimiter = "```";
    private const string JsonPrefix = "json";

    /// <summary>
    /// %%%
    /// </summary>
    /// <param name="result"></param>
    /// <typeparam name="TResult">The target type of the <see cref="FunctionResult"/>.</typeparam>
    /// <returns></returns>
    public static TResult? Translate<TResult>(string result)
    {
        string rawJson = ExtractJson(result);

        return JsonSerializer.Deserialize<TResult>(rawJson);
    }

    private static string ExtractJson(string result)
    {
        // Search for initial literal delimiter: ```
        int startIndex = result.IndexOf(LiteralDelimiter, System.StringComparison.Ordinal);
        if (startIndex < 0)
        {
            // No initial delimiter, return entire expression.
            return result;
        }

        // Accommodate "json" prefix, if present.
        if (JsonPrefix.Equals(result.Substring(startIndex, JsonPrefix.Length), System.StringComparison.OrdinalIgnoreCase))
        {
            startIndex += JsonPrefix.Length;
        }

        // Locate final literal delimiter
        int endIndex = result.IndexOf(LiteralDelimiter, startIndex, System.StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0)
        {
            endIndex = result.Length - 1;
        }

        // Extract JSON
        return result.Substring(startIndex, endIndex - startIndex);
    }
}
