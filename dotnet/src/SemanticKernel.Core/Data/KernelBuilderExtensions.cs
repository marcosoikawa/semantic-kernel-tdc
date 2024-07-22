﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Data;

/// <summary>
/// Extension methods to register Data services on the <see cref="IKernelBuilder"/>.
/// </summary>
public static class KernelBuilderExtensions
{
    /// <summary>
    /// Register a Volatile <see cref="IVectorStore"/> with the specified service ID.
    /// </summary>
    /// <param name="builder">The builder to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <returns>The kernel builder.</returns>
    public static IKernelBuilder AddVolatileVectorStore(this IKernelBuilder builder, string? serviceId = default)
    {
        builder.Services.AddVolatileVectorStore(serviceId);
        return builder;
    }
}
