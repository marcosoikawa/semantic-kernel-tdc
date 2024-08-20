﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Search;

/// <summary>
/// A <see cref="FilterClause"/> which filters using equality of a field value with the specified field value.
/// </summary>
/// <remarks>
/// The <see cref="EqualityFilterClause"/> is used to request that the underlying search service should
/// filter search results based on the equality of a field value with the specified field value.
/// </remarks>
/// <param name="field">Field name.</param>
/// <param name="value">Field value.</param>
public sealed class EqualityFilterClause(string field, object value) : FilterClause(FilterClauseType.Equality)
{
    /// <summary>
    /// Field name to match.
    /// </summary>
    public string Field { get; private set; } = field;

    /// <summary>
    /// Field value to match.
    /// </summary>
    public object Value { get; private set; } = value;
}
