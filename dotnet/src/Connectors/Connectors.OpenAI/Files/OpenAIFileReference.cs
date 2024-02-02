﻿// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// Specialization of <see cref="FileReference"/> used in association <see cref="OpenAIFileService"/>.
/// </summary>
public sealed class OpenAIFileReference
{
    /// <summary>
    /// The file identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp the file was uploaded.s
    /// </summary>
    public DateTime CreatedTimestamp { get; set; }

    /// <summary>
    /// The name of the file.s
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Describes the associated purpose of the file.
    /// </summary>
    public OpenAIFilePurpose Purpose { get; set; }

    /// <summary>
    /// The file size, in bytes.
    /// </summary>
    public int SizeInBytes { get; set; }
}
