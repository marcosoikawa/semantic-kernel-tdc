﻿// Copyright (c) Microsoft. All rights reserved.

using System.IO;

namespace SemanticKernel.Connectors.Azure.UnitTests;

/// <summary>
/// Helper for OpenAI test purposes.
/// </summary>
internal static class TestHelper
{
    /// <summary>
    /// Reads test response from file for mocking purposes.
    /// </summary>
    /// <param name="fileName">Name of the file with test response.</param>
    internal static string GetTestResponse(string fileName)
    {
        return File.ReadAllText($"./TestData/{fileName}");
    }
}
