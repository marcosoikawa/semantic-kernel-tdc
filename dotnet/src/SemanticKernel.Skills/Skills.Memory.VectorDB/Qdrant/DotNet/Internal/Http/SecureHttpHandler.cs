﻿// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;

namespace Qdrant.DotNet.Internal.Http;

internal class HttpHandlers
{
    public static HttpClientHandler CheckCertificateRevocation
    {
        get
        {
            var handler = new HttpClientHandler();
            handler.CheckCertificateRevocationList = true;
            return handler;
        }
    }
}
