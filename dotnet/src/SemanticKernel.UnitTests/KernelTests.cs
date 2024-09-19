﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;
using Moq;
using Xunit;

#pragma warning disable CS0618 // Events are deprecated

namespace SemanticKernel.UnitTests;

public class KernelTests
{
    private const string InputParameterName = "input";

    [Fact]
    public void ItProvidesAccessToFunctionsViaFunctionCollection()
    {
        // Arrange
        Kernel kernel = new();
        kernel.Plugins.AddFromType<MyPlugin>("mySk");

        // Act & Assert - 3 functions, var name is not case sensitive
        Assert.NotNull(kernel.Plugins.GetFunction("mySk", "sayhello"));
        Assert.NotNull(kernel.Plugins.GetFunction("MYSK", "SayHello"));
        Assert.NotNull(kernel.Plugins.GetFunction("mySk", "ReadFunctionCollectionAsync"));
        Assert.NotNull(kernel.Plugins.GetFunction("MYSK", "ReadFunctionCollectionAsync"));
    }

    [Fact]
    public async Task InvokeAsyncDoesNotRunWhenCancelledAsync()
    {
        // Arrange
        var kernel = new Kernel();
        var functions = kernel.ImportPluginFromType<MyPlugin>();

        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => kernel.InvokeAsync(functions["GetAnyValue"], cancellationToken: cts.Token));
    }

    [Fact]
    public async Task InvokeAsyncRunsWhenNotCancelledAsync()
    {
        // Arrange
        var kernel = new Kernel();
        kernel.ImportPluginFromType<MyPlugin>("mySk");

        using CancellationTokenSource cts = new();

        // Act
        var result = await kernel.InvokeAsync(kernel.Plugins.GetFunction("mySk", "GetAnyValue"), cancellationToken: cts.Token);

        // Assert
        Assert.False(string.IsNullOrEmpty(result.GetValue<string>()));
    }

    [Fact]
    public void ItImportsPluginsNotCaseSensitive()
    {
        // Act
        KernelPlugin plugin = new Kernel().ImportPluginFromType<MyPlugin>();

        // Assert
        Assert.Equal(3, plugin.Count());
        Assert.True(plugin.Contains("GetAnyValue"));
        Assert.True(plugin.Contains("getanyvalue"));
        Assert.True(plugin.Contains("GETANYVALUE"));
    }

    [Fact]
    public void ItAllowsToImportTheSamePluginMultipleTimes()
    {
        // Arrange
        var kernel = new Kernel();

        // Act - Assert no exception occurs
        kernel.ImportPluginFromType<MyPlugin>();
        kernel.ImportPluginFromType<MyPlugin>("plugin1");
        kernel.ImportPluginFromType<MyPlugin>("plugin2");
        kernel.ImportPluginFromType<MyPlugin>("plugin3");
    }

    [Fact]
    public async Task InvokeAsyncHandlesPreInvocationAsync()
    {
        // Arrange
        var kernel = new Kernel();
        int functionInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var handlerInvocations = 0;
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            handlerInvocations++;
        };

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, functionInvocations);
        Assert.Equal(1, handlerInvocations);
    }

    [Fact]
    public async Task RunStreamingAsyncHandlesPreInvocationAsync()
    {
        // Arrange
        var kernel = new Kernel();
        int functionInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var handlerInvocations = 0;
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            handlerInvocations++;
        };

        // Act
        await foreach (var chunk in kernel.InvokeStreamingAsync(function)) { }

        // Assert
        Assert.Equal(1, functionInvocations);
        Assert.Equal(1, handlerInvocations);
    }

    [Fact]
    public async Task RunStreamingAsyncHandlesPreInvocationWasCancelledAsync()
    {
        // Arrange
        var kernel = new Kernel();
        int functionInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var handlerInvocations = 0;
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            handlerInvocations++;
            e.Cancel = true;
        };

        // Act
        IAsyncEnumerable<StreamingKernelContent> enumerable = kernel.InvokeStreamingAsync<StreamingKernelContent>(function);
        IAsyncEnumerator<StreamingKernelContent> enumerator = enumerable.GetAsyncEnumerator();
        var e = await Assert.ThrowsAsync<KernelFunctionCanceledException>(async () => await enumerator.MoveNextAsync());

        // Assert
        Assert.Equal(1, handlerInvocations);
        Assert.Equal(0, functionInvocations);
        Assert.Same(function, e.Function);
        Assert.Same(kernel, e.Kernel);
        Assert.Empty(e.Arguments);
    }

    [Fact]
    public async Task RunStreamingAsyncPreInvocationCancelationDontTriggerInvokedHandlerAsync()
    {
        // Arrange
        var kernel = new Kernel();
        var functions = kernel.ImportPluginFromType<MyPlugin>();

        var invoked = 0;
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            e.Cancel = true;
        };

        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            invoked++;
        };

        // Act
        IAsyncEnumerable<StreamingKernelContent> enumerable = kernel.InvokeStreamingAsync<StreamingKernelContent>(functions["GetAnyValue"]);
        IAsyncEnumerator<StreamingKernelContent> enumerator = enumerable.GetAsyncEnumerator();
        var e = await Assert.ThrowsAsync<KernelFunctionCanceledException>(async () => await enumerator.MoveNextAsync());

        // Assert
        Assert.Equal(0, invoked);
    }

    [Fact]
    public async Task InvokeStreamingAsyncDoesNotHandlePostInvocationAsync()
    {
        // Arrange
        var kernel = new Kernel();
        int functionInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        int handlerInvocations = 0;
        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            handlerInvocations++;
        };

        // Act
        await foreach (var chunk in kernel.InvokeStreamingAsync(function))
        {
        }

        // Assert
        Assert.Equal(1, functionInvocations);
        Assert.Equal(0, handlerInvocations);
    }

    [Fact]
    public async Task InvokeAsyncHandlesPreInvocationWasCancelledAsync()
    {
        // Arrange
        var kernel = new Kernel();
        int functionInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var handlerInvocations = 0;
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            handlerInvocations++;
            e.Cancel = true;
        };

        // Act
        KernelFunctionCanceledException ex = await Assert.ThrowsAsync<KernelFunctionCanceledException>(() => kernel.InvokeAsync(function));

        // Assert
        Assert.Equal(1, handlerInvocations);
        Assert.Equal(0, functionInvocations);
        Assert.Same(function, ex.Function);
        Assert.Null(ex.FunctionResult?.Value);
    }

    [Fact]
    public async Task InvokeAsyncHandlesPreInvocationCancelationDontRunSubsequentFunctionsInThePipelineAsync()
    {
        // Arrange
        var kernel = new Kernel();
        int functionInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        int handlerInvocations = 0;
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            handlerInvocations++;
            e.Cancel = true;
        };

        // Act
        KernelFunctionCanceledException ex = await Assert.ThrowsAsync<KernelFunctionCanceledException>(() => kernel.InvokeAsync(function));

        // Assert
        Assert.Equal(1, handlerInvocations);
        Assert.Equal(0, functionInvocations);
        Assert.Same(function, ex.Function);
        Assert.Null(ex.FunctionResult?.Value);
    }

    [Fact]
    public async Task InvokeAsyncPreInvocationCancelationDontTriggerInvokedHandlerAsync()
    {
        // Arrange
        var kernel = new Kernel();
        var functions = kernel.ImportPluginFromType<MyPlugin>();

        var invoked = 0;
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            e.Cancel = true;
        };

        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            invoked++;
        };

        // Act
        KernelFunctionCanceledException ex = await Assert.ThrowsAsync<KernelFunctionCanceledException>(() => kernel.InvokeAsync(functions["GetAnyValue"]));

        // Assert
        Assert.Equal(0, invoked);
        Assert.Same(functions["GetAnyValue"], ex.Function);
        Assert.Null(ex.FunctionResult?.Value);
    }

    [Fact]
    public async Task InvokeAsyncHandlesPostInvocationAsync()
    {
        // Arrange
        var kernel = new Kernel();
        int functionInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        int handlerInvocations = 0;
        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            handlerInvocations++;
        };

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, functionInvocations);
        Assert.Equal(1, handlerInvocations);
    }

    [Fact]
    public async Task InvokeAsyncHandlesPostInvocationWithServicesAsync()
    {
        // Arrange
        var (mockTextResult, mockTextCompletion) = this.SetupMocks();
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<ITextGenerationService>(mockTextCompletion.Object);
        Kernel kernel = builder.Build();

        var function = KernelFunctionFactory.CreateFromPrompt("Write a simple phrase about UnitTests");

        var invoked = 0;

        kernel.FunctionInvoked += (sender, e) =>
        {
            invoked++;
        };

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, invoked);
        mockTextCompletion.Verify(m => m.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
    }

    [Fact]
    public async Task InvokeAsyncForwardsProvidedSettingsToChatCompletionServiceAsync()
    {
        // Arrange
        var (mockChatMessage, mockChatCompletion) = this.SetupChatMocks();
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(mockChatCompletion.Object);
        Kernel kernel = builder.Build();
        var function = KernelFunctionFactory.CreateFromPrompt("Write a simple phrase about UnitTests");
        var expectedModelId = "model-id";

        // Act
        var specificSettings = new PromptExecutionSettings { ModelId = expectedModelId };
        var result = await kernel.InvokeAsync(function, new(specificSettings));

        // Assert
        mockChatCompletion.Verify(m => m.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.Is<PromptExecutionSettings>(settings => settings.ModelId == expectedModelId), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
    }

    [Fact]
    public async Task InvokeStreamingAsyncForwardsProvidedSettingsToChatCompletionServiceAsync()
    {
        // Arrange
        var (mockChatMessage, mockChatCompletion) = this.SetupChatMocks();
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(mockChatCompletion.Object);
        Kernel kernel = builder.Build();
        var function = KernelFunctionFactory.CreateFromPrompt("Write a simple phrase about UnitTests");
        var expectedModelId = "model-id";

        // Act
        var specificSettings = new PromptExecutionSettings { ModelId = expectedModelId };
        var enumerator = kernel.InvokeStreamingAsync(function, new(specificSettings)).GetAsyncEnumerator();
        await enumerator.MoveNextAsync();

        // Assert
        mockChatCompletion.Verify(m => m.GetStreamingChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.Is<PromptExecutionSettings>(settings => settings.ModelId == expectedModelId), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
    }

    [Fact]
    public async Task InvokeAsyncHandlesPostInvocationAndCancellationExceptionContainsResultAsync()
    {
        // Arrange
        var kernel = new Kernel();
        object result = 42;
        var function = KernelFunctionFactory.CreateFromMethod(() => result);
        var args = new KernelArguments() { { "a", "b" } };

        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            e.Cancel = true;
        };

        // Act
        KernelFunctionCanceledException ex = await Assert.ThrowsAsync<KernelFunctionCanceledException>(() => kernel.InvokeAsync(function, args));

        // Assert
        Assert.Same(kernel, ex.Kernel);
        Assert.Same(function, ex.Function);
        Assert.Same(args, ex.Arguments);
        Assert.NotNull(ex.FunctionResult);
        Assert.Same(result, ex.FunctionResult.GetValue<object>());
    }

    [Fact]
    public async Task InvokeAsyncHandlesPostInvocationAndCancellationExceptionContainsModifiedResultAsync()
    {
        // Arrange
        var kernel = new Kernel();
        object result = 42;
        object newResult = 84;
        var function = KernelFunctionFactory.CreateFromMethod(() => result);
        var args = new KernelArguments() { { "a", "b" } };

        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            e.SetResultValue(newResult);
            e.Cancel = true;
        };

        // Act
        KernelFunctionCanceledException ex = await Assert.ThrowsAsync<KernelFunctionCanceledException>(() => kernel.InvokeAsync(function, args));

        // Assert
        Assert.Same(kernel, ex.Kernel);
        Assert.Same(function, ex.Function);
        Assert.Same(args, ex.Arguments);
        Assert.NotNull(ex.FunctionResult);
        Assert.Same(newResult, ex.FunctionResult.GetValue<object>());
    }

    [Fact]
    public async Task InvokeAsyncChangeVariableInvokingHandlerAsync()
    {
        var kernel = new Kernel();
        var function = KernelFunctionFactory.CreateFromMethod((string originalInput) => originalInput);

        var originalInput = "Importance";
        var newInput = "Problems";

        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            e.Arguments["originalInput"] = newInput;
        };

        // Act
        var result = await kernel.InvokeAsync(function, new() { ["originalInput"] = originalInput });

        // Assert
        Assert.Equal(newInput, result.GetValue<string>());
    }

    [Fact]
    public async Task InvokeAsyncChangeVariableInvokedHandlerAsync()
    {
        var kernel = new Kernel();
        var function = KernelFunctionFactory.CreateFromMethod(() => { });

        var originalInput = "Importance";
        var newInput = "Problems";

        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            e.SetResultValue(newInput);
        };

        // Act
        var result = await kernel.InvokeAsync(function, new() { [InputParameterName] = originalInput });

        // Assert
        Assert.Equal(newInput, result.GetValue<string>());
    }

    [Fact]
    public async Task ItReturnsFunctionResultsCorrectlyAsync()
    {
        // Arrange
        var kernel = new Kernel();

        var function = KernelFunctionFactory.CreateFromMethod(() => "Result", "Function1");

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Result", result.GetValue<string>());
    }

    [Fact]
    public async Task ItReturnsChangedResultsFromFunctionInvokedEventsAsync()
    {
        var kernel = new Kernel();

        // Arrange
        var function1 = KernelFunctionFactory.CreateFromMethod(() => "Result1", "Function1");
        const string ExpectedValue = "new result";

        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs args) =>
        {
            args.SetResultValue(ExpectedValue);
        };

        // Act
        var result = await kernel.InvokeAsync(function1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ExpectedValue, result.GetValue<string>());
    }

    [Fact]
    public async Task ItReturnsChangedResultsFromFunctionInvokingEventsAsync()
    {
        // Arrange
        var kernel = new Kernel();

        var function1 = KernelFunctionFactory.CreateFromMethod((string injectedVariable) => injectedVariable, "Function1");
        const string ExpectedValue = "injected value";

        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs args) =>
        {
            args.Arguments["injectedVariable"] = ExpectedValue;
        };

        // Act
        var result = await kernel.InvokeAsync(function1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ExpectedValue, result.GetValue<string>());
    }

    [Fact]
    public async Task ItCanFindAndRunFunctionAsync()
    {
        //Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => "fake result", "function");

        var kernel = new Kernel();
        kernel.ImportPluginFromFunctions("plugin", [function]);

        //Act
        var result = await kernel.InvokeAsync("plugin", "function");

        //Assert
        Assert.NotNull(result);
        Assert.Equal("fake result", result.GetValue<string>());
    }

    [Fact]
    public void ItShouldBePossibleToSetAndGetCultureAssociatedWithKernel()
    {
        //Arrange
        var kernel = new Kernel();

        var culture = CultureInfo.GetCultureInfo(28);

        //Act
        kernel.Culture = culture;

        //Assert
        Assert.Equal(culture, kernel.Culture);
    }

    [Fact]
    public void ItDefaultsLoggerFactoryToNullLoggerFactory()
    {
        //Arrange
        var kernel = new Kernel();

        //Assert
        Assert.Same(NullLoggerFactory.Instance, kernel.LoggerFactory);
    }

    [Fact]
    public void ItDefaultsDataToEmptyDictionary()
    {
        //Arrange
        var kernel = new Kernel();

        //Assert
        Assert.Empty(kernel.Data);
    }

    [Fact]
    public void ItDefaultsPluginsToEmptyCollection()
    {
        //Arrange
        var kernel = new Kernel();

        //Assert
        Assert.Empty(kernel.Plugins);
    }

    [Fact]
    public void InvariantCultureShouldBeReturnedIfNoCultureWasAssociatedWithKernel()
    {
        //Arrange
        var kernel = new Kernel();

        //Act
        var culture = kernel.Culture;

        //Assert
        Assert.Same(CultureInfo.InvariantCulture, culture);
    }

    [Fact]
    public void ItDeepClonesAllRelevantStateInClone()
    {
        // Kernel with all properties set
        var serviceSelector = new Mock<IAIServiceSelector>();
        var loggerFactory = new Mock<ILoggerFactory>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(serviceSelector.Object)
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddSingleton(new HttpClient())
#pragma warning restore CA2000
            .AddSingleton(loggerFactory.Object)
            .AddSingleton<IFunctionInvocationFilter>(new MyFunctionFilter())
            .AddSingleton<IPromptRenderFilter>(new MyPromptFilter())
            .BuildServiceProvider();
        var plugin = KernelPluginFactory.CreateFromFunctions("plugin1");
        var plugins = new KernelPluginCollection() { plugin };
        Kernel kernel1 = new(serviceProvider, plugins);
        kernel1.Data["key"] = "value";

        // Clone and validate it
        Kernel kernel2 = kernel1.Clone();
        Assert.Same(kernel1.Services, kernel2.Services);
        Assert.Same(kernel1.Culture, kernel2.Culture);
        Assert.NotSame(kernel1.Data, kernel2.Data);
        Assert.Equal(kernel1.Data.Count, kernel2.Data.Count);
        Assert.Equal(kernel1.Data["key"], kernel2.Data["key"]);
        Assert.NotSame(kernel1.Plugins, kernel2.Plugins);
        Assert.Equal(kernel1.Plugins, kernel2.Plugins);
        this.AssertFilters(kernel1, kernel2);

        // Minimally configured kernel
        Kernel kernel3 = new();

        // Clone and validate it
        Kernel kernel4 = kernel3.Clone();
        Assert.Same(kernel3.Services, kernel4.Services);
        Assert.NotSame(kernel3.Data, kernel4.Data);
        Assert.Empty(kernel4.Data);
        Assert.NotSame(kernel1.Plugins, kernel2.Plugins);
        Assert.Empty(kernel4.Plugins);
        this.AssertFilters(kernel3, kernel4);
    }

    [Fact]
    public async Task InvokeStreamingAsyncCallsConnectorStreamingApiAsync()
    {
        // Arrange
        var mockTextCompletion = this.SetupStreamingMocks(
            new StreamingTextContent("chunk1"),
            new StreamingTextContent("chunk2"));
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<ITextGenerationService>(mockTextCompletion.Object);
        Kernel kernel = builder.Build();
        var prompt = "Write a simple phrase about UnitTests {{$input}}";
        var sut = KernelFunctionFactory.CreateFromPrompt(prompt);
        var variables = new KernelArguments() { [InputParameterName] = "importance" };

        var chunkCount = 0;
        // Act
        await foreach (var chunk in sut.InvokeStreamingAsync<StreamingKernelContent>(kernel, variables))
        {
            chunkCount++;
        }

        // Assert
        Assert.Equal(2, chunkCount);
        mockTextCompletion.Verify(m => m.GetStreamingTextContentsAsync(It.IsIn("Write a simple phrase about UnitTests importance"), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
    }

    [Fact]
    public async Task ValidateInvokeAsync()
    {
        // Arrange
        var kernel = new Kernel();
        var function = KernelFunctionFactory.CreateFromMethod(() => "ExpectedResult");

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal("ExpectedResult", result.Value);
    }

    [Fact]
    public async Task ValidateInvokePromptAsync()
    {
        // Arrange
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.Services.AddTransient<IChatCompletionService>((sp) => new FakeChatCompletionService("ExpectedResult"));
        Kernel kernel = builder.Build();

        // Act
        var result = await kernel.InvokePromptAsync("My Test Prompt");

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal("ExpectedResult", result.Value.ToString());
    }

    private sealed class FakeChatCompletionService(string result) : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatMessageContent>>([new(AuthorRole.Assistant, result)]);
        }

#pragma warning disable IDE0036 // Order modifiers
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore IDE0036 // Order modifiers
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, result);
        }
    }

    private (TextContent mockTextContent, Mock<ITextGenerationService> textCompletionMock) SetupMocks(string? completionResult = null)
    {
        var mockTextContent = new TextContent(completionResult ?? "LLM Result about UnitTests");

        var mockTextCompletion = new Mock<ITextGenerationService>();
        mockTextCompletion.Setup(m => m.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>())).ReturnsAsync([mockTextContent]);
        return (mockTextContent, mockTextCompletion);
    }

    private (ChatMessageContent mockChatContent, Mock<IChatCompletionService> chatCompletionMock) SetupChatMocks(string? completionResult = null)
    {
        var mockChatContent = new ChatMessageContent(AuthorRole.Assistant, completionResult ?? "LLM Result about UnitTests");

        var mockChatCompletion = new Mock<IChatCompletionService>();
        mockChatCompletion.Setup(m => m.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>())).ReturnsAsync([mockChatContent]);
        mockChatCompletion.Setup(m => m.GetStreamingChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .Returns(new List<StreamingChatMessageContent> 
            {
                new(AuthorRole.Assistant, "chunk1")
            }.ToAsyncEnumerable());

        return (mockChatContent, mockChatCompletion);
    }


    private Mock<ITextGenerationService> SetupStreamingMocks(params StreamingTextContent[] streamingContents)
    {
        var mockTextCompletion = new Mock<ITextGenerationService>();
        mockTextCompletion.Setup(m => m.GetStreamingTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>())).Returns(streamingContents.ToAsyncEnumerable());

        return mockTextCompletion;
    }

    private void AssertFilters(Kernel kernel1, Kernel kernel2)
    {
        var functionFilters1 = kernel1.GetAllServices<IFunctionInvocationFilter>().ToArray();
        var promptFilters1 = kernel1.GetAllServices<IPromptRenderFilter>().ToArray();

        var functionFilters2 = kernel2.GetAllServices<IFunctionInvocationFilter>().ToArray();
        var promptFilters2 = kernel2.GetAllServices<IPromptRenderFilter>().ToArray();

        Assert.Equal(functionFilters1.Length, functionFilters2.Length);

        for (var i = 0; i < functionFilters1.Length; i++)
        {
            Assert.Same(functionFilters1[i], functionFilters2[i]);
        }

        Assert.Equal(promptFilters1.Length, promptFilters2.Length);

        for (var i = 0; i < promptFilters1.Length; i++)
        {
            Assert.Same(promptFilters1[i], promptFilters2[i]);
        }
    }

    public class MyPlugin
    {
        [KernelFunction, Description("Return any value.")]
        public virtual string GetAnyValue()
        {
            return Guid.NewGuid().ToString();
        }

        [KernelFunction, Description("Just say hello")]
        public virtual void SayHello()
        {
            Console.WriteLine("Hello folks!");
        }

        [KernelFunction("ReadFunctionCollectionAsync"), Description("Export info.")]
        public async Task ReadFunctionCollectionAsync(Kernel kernel)
        {
            await Task.Delay(0);
            Assert.NotNull(kernel.Plugins);
        }
    }

    private sealed class MyFunctionFilter : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            await next(context);
        }
    }

    private sealed class MyPromptFilter : IPromptRenderFilter
    {
        public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
        {
            await next(context);
        }
    }
}
