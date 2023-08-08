﻿// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Newtonsoft.Json.Linq;

namespace Plugins.OrchestratorPlugin;

public class Orchestrator
{
    private IKernel _kernel;

    public Orchestrator(IKernel kernel)
    {
        this._kernel = kernel;
    }

    [SKFunction, Description("Routes the request to the appropriate function.")]
    public async Task<string> RouteRequest(SKContext context)
    {
        // Save the original user request
        string request = context.Variables["input"];

        // Add the list of available functions to the context
        context.Variables["options"] = "Sqrt, Add";

        // Retrieve the intent from the user request
        var GetIntent = this._kernel.Skills.GetFunction("OrchestratorPlugin", "GetIntent");
        var getIntentVariables = new ContextVariables
        {
            ["input"] = context.Variables["input"],
            ["options"] = "Sqrt, Add"
        };
        string intent = (await this._kernel.RunAsync(getIntentVariables, GetIntent)).Result.Trim();

        // Call the appropriate function
        ISKFunction MathFunction;
        switch (intent)
        {
            case "Sqrt":
                MathFunction = this._kernel.Skills.GetFunction("MathPlugin", "Sqrt");
                break;
            case "Add":
                MathFunction = this._kernel.Skills.GetFunction("MathPlugin", "Add");
                break;
            default:
                return "I'm sorry, I don't understand.";
        }

        // Get remaining functions
        var CreateResponse = this._kernel.Skills.GetFunction("OrchestratorPlugin", "CreateResponse");
        var GetNumbers = this._kernel.Skills.GetFunction("OrchestratorPlugin", "GetNumbers");
        var ExtractNumbersFromJson = this._kernel.Skills.GetFunction("OrchestratorPlugin", "ExtractNumbersFromJson");

        // Create a new context with the original request
        var pipelineContext = new ContextVariables(request);
        pipelineContext["original_request"] = request;

        // Run the pipeline
        var output = await this._kernel.RunAsync(
            pipelineContext,
            GetNumbers,
            ExtractNumbersFromJson,
            MathFunction,
            CreateResponse);

        return output.Variables["input"];
    }

    [SKFunction, Description("Extracts numbers from JSON")]
    public SKContext ExtractNumbersFromJson(SKContext context)
    {
        JObject numbers = JObject.Parse(context.Variables["input"]);

        // otherwise, loop through numbers and add them to the context
        foreach (var number in numbers)
        {
            if (number.Key == "number1")
            {
                context.Variables["input"] = number.Value!.ToString();
                continue;
            }

            context.Variables[number.Key] = number.Value!.ToString();
        }
        return context;
    }
}
