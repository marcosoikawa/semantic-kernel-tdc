﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Moq;
using Xunit;

namespace SemanticKernel.UnitTests.Functions;

public class SKContextTests
{
    private readonly Mock<IReadOnlyFunctionCollection> _functions = new();

    [Fact]
    public void ItHasHelpersForContextVariables()
    {
        // Arrange
        var variables = new ContextVariables();
        var target = new SKContext(new Mock<IFunctionExecutor>().Object, variables);
        variables.Set("foo1", "bar1");

        // Act
        target.Variables["foo2"] = "bar2";
        target.Variables["INPUT"] = Guid.NewGuid().ToString("N");

        // Assert
        Assert.Equal("bar1", target.Variables["foo1"]);
        Assert.Equal("bar1", target.Variables["foo1"]);
        Assert.Equal("bar2", target.Variables["foo2"]);
        Assert.Equal("bar2", target.Variables["foo2"]);
        Assert.Equal(target.Variables["INPUT"], target.Result);
        Assert.Equal(target.Variables["INPUT"], target.ToString());
        Assert.Equal(target.Variables["INPUT"], target.Variables.Input);
        Assert.Equal(target.Variables["INPUT"], target.Variables.ToString());
    }

    [Fact]
    public async Task ItHasHelpersForFunctionCollectionAsync()
    {
        // Arrange
        IDictionary<string, ISKFunction> functions = KernelBuilder.Create().ImportFunctions(new Parrot(), "test");
        this._functions.Setup(x => x.GetFunction("func")).Returns(functions["say"]);
        var (kernel, kernelContext) = this.SetupKernelMock(this._functions.Object);
        var target = new SKContext(kernelContext.Object, new ContextVariables(), this._functions.Object);
        Assert.NotNull(target.Functions);

        // Act
        var say = target.Functions.GetFunction("func");

        FunctionResult result = await say.InvokeAsync("ciao", kernel.Object);

        // Assert
        Assert.Equal("ciao", result.Context.Result);
        Assert.Equal("ciao", result.GetValue<string>());
    }

    private (Mock<IKernel> kernelMock, Mock<IFunctionExecutor> kernelContextMock) SetupKernelMock(IReadOnlyFunctionCollection? functions = null)
    {
        functions ??= new Mock<IFunctionCollection>().Object;

        var kernel = new Mock<IKernel>();
        var kernelContext = new Mock<IFunctionExecutor>();

        kernel.SetupGet(x => x.Functions).Returns(functions);
        kernel.Setup(k => k.CreateNewContext(It.IsAny<ContextVariables>(), It.IsAny<IReadOnlyFunctionCollection>(), It.IsAny<ILoggerFactory>(), It.IsAny<CultureInfo>()))
            .Returns<ContextVariables, IReadOnlyFunctionCollection, ILoggerFactory, CultureInfo>((contextVariables, skills, loggerFactory, culture) =>
        {
            return new SKContext(kernelContext.Object, contextVariables);
        });

        return (kernel, kernelContext);
    }

    private sealed class Parrot
    {
        [SKFunction, Description("say something")]
        // ReSharper disable once UnusedMember.Local
        public string Say(string input)
        {
            return input;
        }
    }
}
