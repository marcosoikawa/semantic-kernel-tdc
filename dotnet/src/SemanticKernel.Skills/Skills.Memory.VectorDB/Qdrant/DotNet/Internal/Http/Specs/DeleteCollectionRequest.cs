﻿// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;
using Qdrant.DotNet.Internal.Diagnostics;

namespace Qdrant.DotNet.Internal.Http.Specs;

internal class DeleteCollectionRequest : IValidatable
{
    public static DeleteCollectionRequest Create(string collectionName)
    {
        return new DeleteCollectionRequest(collectionName);
    }

    public void Validate()
    {
        Verify.NotNullOrEmpty(this._collectionName, "The collection name is empty");
    }

    public HttpRequestMessage Build()
    {
        this.Validate();
        return HttpRequest.CreateDeleteRequest($"collections/{this._collectionName}?timeout=30");
    }

    #region private ================================================================================

    private readonly string _collectionName;

    private DeleteCollectionRequest(string collectionName)
    {
        this._collectionName = collectionName;
    }

    #endregion
}
