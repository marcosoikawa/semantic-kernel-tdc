﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Connectors.AI.MultiConnector;

/// <summary>
/// Represents the collection of evaluations of connectors against prompt types, to be saved and analyzed.
/// </summary>
public class MultiCompletionAnalysis : TestEvent
{
    public List<ConnectorTest> OriginalTests { get; set; } = new();

    public DateTime TestTimestamp { get; set; } = DateTime.MinValue;

    public List<ConnectorTest> Tests { get; set; } = new();

    public DateTime EvaluationTimestamp { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Gets or sets the list of connector prompt evaluations saved in this analysis.
    /// </summary>
    public List<ConnectorPromptEvaluation> Evaluations { get; set; } = new();

    public DateTime SuggestionTimestamp { get; set; } = DateTime.MinValue;
}
