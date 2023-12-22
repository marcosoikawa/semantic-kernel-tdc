﻿#region HEADER
// Copyright (c) Microsoft. All rights reserved.
#endregion

using System.Linq;
using System.Text.Json.Nodes;
using Json.More;
using Microsoft.SemanticKernel.Connectors.Gemini.Settings;

namespace Microsoft.SemanticKernel.Connectors.Gemini;

internal static class TextGenerationRequest
{
    public static JsonObject GenerateJsonFromPromptExecutionSettings(string prompt, GeminiPromptExecutionSettings executionSettings)
    {
        JsonObject obj = CreatePromptObject(prompt);
        AddSafetySetting(executionSettings, obj);
        AddConfiguration(executionSettings, obj);
        return obj;
    }

    private static void AddConfiguration(GeminiPromptExecutionSettings executionSettings, JsonObject obj)
    {
        obj["configuration"] = new JsonObject()
        {
            ["temperature"] = executionSettings.Temperature,
            ["topP"] = executionSettings.TopP,
            ["topK"] = executionSettings.TopK,
            ["maxTokens"] = executionSettings.MaxTokens,
            ["stopSequences"] = executionSettings.StopSequences?.Select(s => JsonValue.Create(s)).ToJsonArray()
        };
    }

    private static void AddSafetySetting(GeminiPromptExecutionSettings executionSettings, JsonObject obj)
    {
        if (executionSettings.SafetySettings is { } safety)
        {
            obj["safetySettings"] = safety.Select(s => new JsonObject()
            {
                ["category"] = s.Category,
                ["threshold"] = s.Threshold
            }).ToJsonArray();
        }
    }

    private static JsonObject CreatePromptObject(string prompt)
    {
        var obj = new JsonObject
        {
            ["contents"] = new JsonArray()
            {
                new JsonObject()
                {
                    ["parts"] = new JsonArray()
                    {
                        new JsonObject()
                        {
                            ["text"] = prompt
                        }
                    }
                }
            }
        };
        return obj;
    }
}
