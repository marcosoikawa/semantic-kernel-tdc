﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.SemanticKernel.Functions;

/// <summary>
/// Extensions for logging <see cref="KernelFunction"/> invocations.
/// This extension uses the <see cref="LoggerMessageAttribute"/> to
/// generate logging code at compile time to achieve optimized code.
/// </summary>
internal static partial class KernelFunctionLogExtensions
{
    /// <summary>
    /// Logs invocation of a <see cref="KernelFunction"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Trace,
        Message = "Function {FunctionName} invoking with arguments {Arguments}.")]
    public static partial void LogFunctionInvokingWithArguments(
        this ILogger logger,
        string functionName,
        KernelArguments arguments);

    /// <summary>
    /// Logs cancellation of a <see cref="KernelFunction"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Trace,
        Message = "Function canceled prior to invocation.")]
    public static partial void LogFunctionCanceledPriorToInvoking(this ILogger logger);

    /// <summary>
    /// Logs successful invocation of a <see cref="KernelFunction"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Trace,
        Message = "Function succeeded.")]
    public static partial void LogFunctionInvokedSuccess(this ILogger logger);

    /// <summary>
    /// Logs post-invocation status of a <see cref="KernelFunction"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Trace,
        Message = "Function invocation {Completion}")]
    public static partial void LogFunctionInvokedStatus(
        this ILogger logger,
        string completion);

    /// <summary>
    /// Logs result of a <see cref="KernelFunction"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Trace,
        Message = "Function invocation {Result}")]
    public static partial void LogFunctionInvokedResult(
        this ILogger logger,
        object? result);

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
        Level = LogLevel.Trace,
        Message = "Function completed. Duration: {Duration}ms")]
    public static partial void LogFunctionComplete(
        this ILogger logger,
        double duration);

    /// <summary>
    /// Logs streaming invocation of a <see cref="KernelFunction"/>.
    /// </summary>
    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Trace,
        Message = "Function {FunctionName} streaming invoking with arguments {Arguments}.")]
    public static partial void LogFunctionStreamingInvokingWithArguments(
        this ILogger logger,
        string functionName,
        KernelArguments arguments);
}
