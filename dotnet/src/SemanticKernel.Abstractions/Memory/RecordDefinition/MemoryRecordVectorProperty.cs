﻿// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// A description of a vector property for storage in a memory store.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class MemoryRecordVectorProperty : MemoryRecordProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryRecordVectorProperty"/> class.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    public MemoryRecordVectorProperty(string propertyName)
        : base(propertyName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryRecordVectorProperty"/> class by cloning the given source.
    /// </summary>
    /// <param name="source">The source to clone</param>
    public MemoryRecordVectorProperty(MemoryRecordVectorProperty source)
        : base(source.PropertyName)
    {
    }
}
