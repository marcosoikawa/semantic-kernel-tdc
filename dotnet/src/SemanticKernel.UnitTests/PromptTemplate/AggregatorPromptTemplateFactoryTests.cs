﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Xunit;

namespace SemanticKernel.UnitTests.PromptTemplate;

public sealed class AggregatorPromptTemplateFactoryTests
{
    [Fact]
    public void ItCreatesMyPromptTemplates()
    {
        // Arrange
        var templateString = "{{$input}}";
        var promptModel1 = new PromptTemplateConfig() { TemplateFormat = "my-format-1", Template = templateString };
        var promptModel2 = new PromptTemplateConfig() { TemplateFormat = "my-format-2", Template = templateString };
        var target = new AggregatorPromptTemplateFactory(new MyPromptTemplateFactory1(), new MyPromptTemplateFactory2());

        // Act
        var result1 = target.Create(promptModel1);
        var result2 = target.Create(promptModel2);

        // Assert
        Assert.NotNull(result1);
        Assert.True(result1 is MyPromptTemplate1);
        Assert.NotNull(result2);
        Assert.True(result2 is MyPromptTemplate2);
    }

    [Fact]
    public void ItThrowsExceptionForUnknowPromptTemplateFormat()
    {
        // Arrange
        var templateString = "{{$input}}";
        var promptModel = new PromptTemplateConfig() { TemplateFormat = "unknown-format", Template = templateString };
        var target = new AggregatorPromptTemplateFactory(new MyPromptTemplateFactory1(), new MyPromptTemplateFactory2());

        // Act
        // Assert
        Assert.Throws<KernelException>(() => target.Create(promptModel));
    }

    #region private
    private sealed class MyPromptTemplateFactory1 : IPromptTemplateFactory
    {
        public IPromptTemplate Create(PromptTemplateConfig promptModel)
        {
            if (promptModel.TemplateFormat.Equals("my-format-1", StringComparison.Ordinal))
            {
                return new MyPromptTemplate1(promptModel);
            }

            throw new KernelException($"Prompt template format {promptModel.TemplateFormat} is not supported.");
        }
    }

    private sealed class MyPromptTemplate1 : IPromptTemplate
    {
        private readonly PromptTemplateConfig _promptModel;

        public MyPromptTemplate1(PromptTemplateConfig promptModel)
        {
            this._promptModel = promptModel;
        }

        public IReadOnlyList<KernelParameterMetadata> Parameters => Array.Empty<KernelParameterMetadata>();

        public Task<string> RenderAsync(Kernel kernel, ContextVariables variables, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this._promptModel.Template);
        }
    }

    private sealed class MyPromptTemplateFactory2 : IPromptTemplateFactory
    {
        public IPromptTemplate Create(PromptTemplateConfig promptModel)
        {
            if (promptModel.TemplateFormat.Equals("my-format-2", StringComparison.Ordinal))
            {
                return new MyPromptTemplate2(promptModel);
            }

            throw new KernelException($"Prompt template format {promptModel.TemplateFormat} is not supported.");
        }
    }

    private sealed class MyPromptTemplate2 : IPromptTemplate
    {
        private readonly PromptTemplateConfig _promptModel;

        public MyPromptTemplate2(PromptTemplateConfig promptModel)
        {
            this._promptModel = promptModel;
        }

        public IReadOnlyList<KernelParameterMetadata> Parameters => Array.Empty<KernelParameterMetadata>();

        public Task<string> RenderAsync(Kernel kernel, ContextVariables variables, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this._promptModel.Template);
        }
    }
    #endregion
}
