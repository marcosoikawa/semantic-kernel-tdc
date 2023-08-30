﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Reliability.Polly.Config;
using Xunit;

namespace SemanticKernel.Extensions.UnitTests.Reliability.Polly;

/// <summary>
/// Unit tests of <see cref="KernelConfig"/>.
/// </summary>
public class HttpRetryConfigTests
{
    [Fact]
    public async Task NegativeMaxRetryCountThrowsAsync()
    {
        // Act
        await Assert.ThrowsAsync<System.ArgumentOutOfRangeException>(() =>
        {
            var httpRetryConfig = new HttpRetryConfig() { MaxRetryCount = -1 };
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void SetDefaultHttpRetryConfig()
    {
        // Arrange
        var config = new KernelConfig();
        var httpRetryConfig = new HttpRetryConfig() { MaxRetryCount = 1 };

        // Act
        config.SetHttpRetryConfig(httpRetryConfig);

        // Assert
        Assert.IsType<DefaultHttpRetryHandlerFactory>(config.HttpHandlerFactory);
        var httpHandlerFactory = config.HttpHandlerFactory as DefaultHttpRetryHandlerFactory;
        Assert.NotNull(httpHandlerFactory);
        Assert.Equal(httpRetryConfig, httpHandlerFactory.DefaultConfig);
    }

    [Fact]
    public void SetDefaultHttpRetryConfigToDefaultIfNotSet()
    {
        // Arrange
        var config = new KernelConfig();
        var retryConfig = new HttpRetryConfig();

        // Act
        config.SetHttpRetryConfig(retryConfig);

        // Assert
        Assert.IsType<DefaultHttpRetryHandlerFactory>(config.HttpHandlerFactory);
        var httpHandlerFactory = config.HttpHandlerFactory as DefaultHttpRetryHandlerFactory;
        Assert.NotNull(httpHandlerFactory);
        Assert.Equal(retryConfig.MaxRetryCount, httpHandlerFactory.DefaultConfig.MaxRetryCount);
        Assert.Equal(retryConfig.MaxRetryDelay, httpHandlerFactory.DefaultConfig.MaxRetryDelay);
        Assert.Equal(retryConfig.MinRetryDelay, httpHandlerFactory.DefaultConfig.MinRetryDelay);
        Assert.Equal(retryConfig.MaxTotalRetryTime, httpHandlerFactory.DefaultConfig.MaxTotalRetryTime);
        Assert.Equal(retryConfig.UseExponentialBackoff, httpHandlerFactory.DefaultConfig.UseExponentialBackoff);
        Assert.Equal(retryConfig.RetryableStatusCodes, httpHandlerFactory.DefaultConfig.RetryableStatusCodes);
        Assert.Equal(retryConfig.RetryableExceptionTypes, httpHandlerFactory.DefaultConfig.RetryableExceptionTypes);
    }

    [Fact]
    public void SetDefaultHttpRetryConfigToDefaultIfNull()
    {
        // Arrange
        var config = new KernelConfig();
        var defaultConfig = new HttpRetryConfig();

        // Act
        config.SetHttpRetryConfig();

        // Assert
        Assert.IsType<DefaultHttpRetryHandlerFactory>(config.HttpHandlerFactory);
        var httpHandlerFactory = config.HttpHandlerFactory as DefaultHttpRetryHandlerFactory;

        Assert.NotNull(httpHandlerFactory);
        Assert.Equal(defaultConfig.MaxRetryCount, httpHandlerFactory.DefaultConfig.MaxRetryCount);
        Assert.Equal(defaultConfig.MaxRetryDelay, httpHandlerFactory.DefaultConfig.MaxRetryDelay);
        Assert.Equal(defaultConfig.MinRetryDelay, httpHandlerFactory.DefaultConfig.MinRetryDelay);
        Assert.Equal(defaultConfig.MaxTotalRetryTime, httpHandlerFactory.DefaultConfig.MaxTotalRetryTime);
        Assert.Equal(defaultConfig.UseExponentialBackoff, httpHandlerFactory.DefaultConfig.UseExponentialBackoff);
        Assert.Equal(defaultConfig.RetryableStatusCodes, httpHandlerFactory.DefaultConfig.RetryableStatusCodes);
        Assert.Equal(defaultConfig.RetryableExceptionTypes, httpHandlerFactory.DefaultConfig.RetryableExceptionTypes);
    }
}
