﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Extensions for logging <see cref="KernelFunction"/> invocations.
/// This extension uses the <see cref="LoggerMessageAttribute"/> to
/// generate logging code at compile time to achieve optimized code.
/// </summary>
internal static partial class KernelFunctionLogMessages
{
    /// <summary>
    /// Logs invocation of a <see cref="KernelFunction"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Function {FunctionName} invoking.")]
    public static partial void LogFunctionInvoking(
        this ILogger logger,
        string functionName);

    /// <summary>
    /// Logs arguments to a <see cref="KernelFunction"/>.
    /// The action provides the benefit of caching the template parsing result for better performance.
    /// And the public method is a helper to serialize the arguments.
    /// </summary>
    private static readonly Action<ILogger, string, Exception?> s_logFunctionArguments =
        LoggerMessage.Define<string>(
            logLevel: LogLevel.Trace,   // Sensitive data, logging as trace, disabled by default
            eventId: 1,
            "Function arguments: {Arguments}");
    public static void LogFunctionArguments(this ILogger logger, KernelArguments arguments)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            s_logFunctionArguments(
                logger,
                JsonSerializer.Serialize(arguments),
                null);
        }
    }

    /// <summary>
    /// Logs cancellation of a <see cref="KernelFunction"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Function canceled prior to invocation.")]
    public static partial void LogFunctionCanceledPriorToInvoking(this ILogger logger);

    /// <summary>
    /// Logs successful invocation of a <see cref="KernelFunction"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Function {FunctionName} succeeded.")]
    public static partial void LogFunctionInvokedSuccess(this ILogger logger, string functionName);

    /// <summary>
    /// Logs result of a <see cref="KernelFunction"/>.
    /// The action provides the benefit of caching the template parsing result for better performance.
    /// And the public method is a helper to serialize the result.
    /// </summary>
    private static readonly Action<ILogger, string, Exception?> s_logFunctionResult =
        LoggerMessage.Define<string>(
            logLevel: LogLevel.Trace,   // Sensitive data, logging as trace, disabled by default
            eventId: 1,
            "Function result: {Result}");
    public static void LogFunctionResult(this ILogger logger, object? result)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            s_logFunctionResult(
                logger,
                JsonSerializer.Serialize(result),
                null);
        }
    }

    /// <summary>
    /// Logs <see cref="KernelFunction"/> error.
    /// </summary>
    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Function failed. Error: {Message}")]
    public static partial void LogFunctionError(
        this ILogger logger,
        Exception exception,
        string message);

    /// <summary>
    /// Logs <see cref="KernelFunction"/> complete.
    /// </summary>
    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Function completed. Duration: {Duration}s")]
    public static partial void LogFunctionComplete(
        this ILogger logger,
        double duration);

    /// <summary>
    /// Logs streaming invocation of a <see cref="KernelFunction"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Function {FunctionName} streaming.")]
    public static partial void LogFunctionStreamingInvoking(
        this ILogger logger,
        string functionName);

    /// <summary>
    /// Logs <see cref="KernelFunction"/> streaming complete.
    /// </summary>
    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = "Function streaming completed. Duration: {Duration}s.")]
    public static partial void LogFunctionStreamingComplete(
        this ILogger logger,
        double duration);
}
