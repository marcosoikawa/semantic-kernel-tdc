﻿// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using System.Net;
using System.Reflection;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Connectors.Amazon.Core;

/// <summary>
/// Utility functions for the Bedrock clients.
/// </summary>
internal sealed class BedrockClientUtilities
{
    public const string UserAgentHeader = "User-Agent";
    public static readonly string UserAgentString = $"lib/semantic-kernel#{Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty}";

    /// <summary>
    /// Convert the Http Status Code in Converse Response to the Activity Status Code for Semantic Kernel activity.
    /// </summary>
    /// <param name="httpStatusCode"></param>
    /// <returns></returns>
    internal ActivityStatusCode ConvertHttpStatusCodeToActivityStatusCode(HttpStatusCode httpStatusCode)
    {
        if ((int)httpStatusCode >= 200 && (int)httpStatusCode < 300)
        {
            // 2xx status codes represent success
            return ActivityStatusCode.Ok;
        }
        else if ((int)httpStatusCode >= 400 && (int)httpStatusCode < 600)
        {
            // 4xx and 5xx status codes represent errors
            return ActivityStatusCode.Error;
        }
        else
        {
            // Any other status code is considered unset
            return ActivityStatusCode.Unset;
        }
    }
    /// <summary>
    /// Map Conversation role (value) to author role to build message content for semantic kernel output.
    /// </summary>
    /// <param name="role"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    internal AuthorRole MapConversationRoleToAuthorRole(string role)
    {
        return role.ToUpperInvariant() switch
        {
            "USER" => AuthorRole.User,
            "ASSISTANT" => AuthorRole.Assistant,
            "SYSTEM" => AuthorRole.System,
            _ => throw new ArgumentOutOfRangeException(nameof(role), $"Invalid role: {role}")
        };
    }
}
