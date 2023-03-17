// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Skills.Memory.Qdrant.DataModels;
internal class CollectionInfo
{
    internal string CollectionStatus { get; set; } = string.Empty;
    internal string OptimizerStatus { get; set; } = string.Empty;
    internal int VectorsCount { get; set; }
    internal int IndexedVectorsCount { get; set; }
    internal int PointsCount { get; set; }
    internal int SegmentsCount { get; set; }
    internal int VectorsSize { get; set; }
    internal string Distance { get; set; } = string.Empty;
}
