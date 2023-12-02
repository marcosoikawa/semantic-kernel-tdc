﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Plugins.OpenAPI.Model;

/// <summary>
/// The REST API operation parameter style.
/// </summary>
public enum RestApiOperationParameterStyle
{
    /// <summary>
    /// Path-style parameters.
    /// </summary>
    Matrix,

    /// <summary>
    /// Label style parameters.
    /// </summary>
    Label,

    /// <summary>
    /// Form style parameters.
    /// </summary>
    Form,

    /// <summary>
    /// Simple style parameters.
    /// </summary>
    Simple,

    /// <summary>
    /// Space separated array values.
    /// </summary>
    SpaceDelimited,

    /// <summary>
    /// Pipe separated array values.
    /// </summary>
    PipeDelimited,

    /// <summary>
    /// Provides a simple way of rendering nested objects using form parameters.
    /// </summary>
    DeepObject
}
