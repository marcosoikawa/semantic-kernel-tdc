﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.SemanticKernel.AI;

/// <summary>
/// Represents a single update to a streaming content.
/// </summary>
public abstract class StreamingContent
{
    /// <summary>
    /// In a scenario of multiple choices per request, this represents zero-based index of the choice in the streaming sequence
    /// </summary>
    public int ChoiceIndex { get; }

    /// <summary>
    /// Internal chunk object reference. (Breaking glass).
    /// Each connector will have its own internal object representing the content chunk.
    /// </summary>
    /// <remarks>
    /// The usage of this property is considered "unsafe". Use it only if strictly necessary.
    /// </remarks>
    public object? InnerContent { get; }

    /// <summary>
    /// The metadata associated with the content.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    /// <summary>
    /// Abstract string representation of the chunk in a way it could compose/append with previous chunks.
    /// </summary>
    /// <remarks>
    /// Depending on the nature of the underlying type, this method may be more efficient than <see cref="ToByteArray"/>.
    /// </remarks>
    /// <returns>String representation of the chunk</returns>
    public abstract override string ToString();

    /// <summary>
    /// Abstract byte[] representation of the chunk in a way it could be composed/appended with previous chunks.
    /// </summary>
    /// <remarks>
    /// Depending on the nature of the underlying type, this method may be more efficient than <see cref="ToString"/>.
    /// </remarks>
    /// <returns>Byte array representation of the chunk</returns>
    public abstract byte[] ToByteArray();

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingContent"/> class.
    /// </summary>
    /// <param name="innerContent">Inner content object reference</param>
    /// <param name="choiceIndex"></param>
    /// <param name="metadata"></param>
    protected StreamingContent(object? innerContent, int choiceIndex = 0, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        this.InnerContent = innerContent;
        this.ChoiceIndex = choiceIndex;
        this.Metadata = metadata;
    }

    /// <summary>
    /// Implicit conversion to string
    /// </summary>
    /// <param name="modelContent">model Content</param>
    public static implicit operator string(StreamingContent modelContent)
    {
        return modelContent.ToString();
    }
}
