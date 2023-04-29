﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.SemanticKernel;
using Xunit;

namespace SemanticKernel.UnitTests;

public class KernelExceptionTests
{
    [Fact]
    public void ItRoundtripsArgsToErrorCodeCtor()
    {
        // Arrange
        var e = new KernelException(KernelException.ErrorCodes.FunctionNotAvailable);

        // Assert
        Assert.Equal(KernelException.ErrorCodes.FunctionNotAvailable, e.ErrorCode);
        Assert.Contains("Function not available", e.Message, StringComparison.Ordinal);
        Assert.Null(e.InnerException);
    }

    [Fact]
    public void ItRoundtripsArgsToErrorCodeMessageCtor()
    {
        // Arrange
        const string MESSAGE = "this is a test";
        var e = new KernelException(KernelException.ErrorCodes.FunctionNotAvailable, MESSAGE);

        // Assert
        Assert.Equal(KernelException.ErrorCodes.FunctionNotAvailable, e.ErrorCode);
        Assert.Contains("Function not available", e.Message, StringComparison.Ordinal);
        Assert.Contains(MESSAGE, e.Message, StringComparison.Ordinal);
        Assert.Null(e.InnerException);
    }

    [Fact]
    public void ItRoundtripsArgsToErrorCodeMessageExceptionCtor()
    {
        // Arrange
        const string MESSAGE = "this is a test";
        var inner = new FormatException();
        var e = new KernelException(KernelException.ErrorCodes.FunctionNotAvailable, MESSAGE, inner);

        // Assert
        Assert.Equal(KernelException.ErrorCodes.FunctionNotAvailable, e.ErrorCode);
        Assert.Contains("Function not available", e.Message, StringComparison.Ordinal);
        Assert.Contains(MESSAGE, e.Message, StringComparison.Ordinal);
        Assert.Same(inner, e.InnerException);
    }

    [Fact]
    public void ItAllowsNullMessageAndInnerExceptionInCtors()
    {
        // Arrange
        var e = new KernelException(KernelException.ErrorCodes.FunctionNotAvailable, null, null);

        // Assert
        Assert.Equal(KernelException.ErrorCodes.FunctionNotAvailable, e.ErrorCode);
        Assert.Contains("Function not available", e.Message, StringComparison.Ordinal);
        Assert.Null(e.InnerException);
    }
}
