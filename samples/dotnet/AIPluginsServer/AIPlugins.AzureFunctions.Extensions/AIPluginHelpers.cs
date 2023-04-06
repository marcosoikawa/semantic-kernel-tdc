﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;

namespace AIPlugins.AzureFunctions.SKExtensions;

public static class AIPluginHelpers
{
    public static async Task<HttpResponseData> GenerateAIPluginJsonResponseAsync(HttpRequestData req, string skillName, string description)
    {
        string skillUri = req.Url.GetLeftPart(UriPartial.Path);
        skillUri = skillUri.Remove(skillUri.IndexOf($"{skillName}/.well-known", StringComparison.InvariantCultureIgnoreCase));
        Uri openApiSpecUri = new(baseUri: new(skillUri), $"swagger.json?tag=CodingSkill");

        AIPluginModel aiPluginModel = new()
        {
            SchemaVersion = "1.0",
            NameForModel = skillName,
            NameForHuman = skillName,
            DescriptionForModel = description,
            DescriptionForHuman = description,
            Api = new()
            {
                Type = "openapi",
                Url = openApiSpecUri.ToString(),
                HasUserAuthentication = false
            }
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteAsJsonAsync(aiPluginModel);
        return response;
    }
}
