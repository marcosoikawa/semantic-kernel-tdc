﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Planners.Handlebars.Extensions;
using Microsoft.SemanticKernel.Planners.Handlebars.Models;

namespace Microsoft.SemanticKernel.Planners.Handlebars;

/// <summary>
/// Represents a Handlebars planner.
/// </summary>
public sealed class HandlebarsPlanner
{
    /// <summary>
    /// The key for the available kernel functions.
    /// </summary>
    public const string AvailableKernelFunctionsKey = "AVAILABLE_KERNEL_FUNCTIONS";

    /// <summary>
    /// Gets the stopwatch used for measuring planning time.
    /// </summary>
    public Stopwatch Stopwatch { get; } = new();

    private readonly IKernel _kernel;

    private readonly HandlebarsPlannerConfig _config;

    private readonly HashSet<HandlebarsParameterTypeView> _parametersTypeView = new();

    private readonly Dictionary<string, string> _parametersSchemaView = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlebarsPlanner"/> class.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="config">The configuration.</param>
    public HandlebarsPlanner(IKernel kernel, HandlebarsPlannerConfig? config = default)
    {
        this._kernel = kernel;
        this._config = config ?? new HandlebarsPlannerConfig();
    }

    /// <summary>
    /// Create a plan for a goal.
    /// </summary>
    /// <param name="goal">The goal to create a plan for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="SKException">Thrown when the plan cannot be created.</exception>
    public async Task<HandlebarsPlan> CreatePlanAsync(string goal, CancellationToken cancellationToken = default)
    {
        var availableFunctions = this.GetAvailableFunctionsManual(cancellationToken);
        var createPlanPrompt = this.GetHandlebarsTemplate(this._kernel, goal, availableFunctions);
        var chatCompletion = this._kernel.GetService<IChatCompletion>();

        // Console.WriteLine($"\nTemplate:\n{createPlanPrompt}");

        // Extract the chat history from the rendered prompt
        string pattern = @"<(user~|system~|assistant~)>(.*?)<\/\1>";
        MatchCollection matches = Regex.Matches(createPlanPrompt, pattern, RegexOptions.Singleline);

        // Add the chat history to the chat
        ChatHistory chatMessages = this.GetChatHistoryFromPrompt(createPlanPrompt, chatCompletion);

        // Get the chat completion results
        var completionResults = await chatCompletion.GenerateMessageAsync(chatMessages, cancellationToken: cancellationToken).ConfigureAwait(false);
        var resultContext = this._kernel.CreateNewContext();
        resultContext.Variables.Update(completionResults);

        // Check if plan could not be created with available helpers
        if (resultContext.Result.IndexOf("Additional helpers may be required", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var functionNames = availableFunctions.ToList().Select(func => $"{func.PluginName}{HandlebarsTemplateEngineExtensions.ReservedNameDelimiter}{func.Name}");
            throw new SKException($"Unable to create plan for goal with available functions.\nGoal: {goal}\nAvailable Functions: {string.Join(", ", functionNames)}\nPlanner output:\n{resultContext.Result}");
        }

        // Extract the proposed plan as a handlesbar template from result
        Match match = Regex.Match(resultContext.Result, @"```\s*(handlebars)?\s*(.*)\s*```", RegexOptions.Singleline);
        if (!match.Success)
        {
            throw new SKException("Could not find the plan in the results");
        }

        var planTemplate = match.Groups[2].Value.Trim();

        planTemplate = planTemplate.Replace("compare.equal", "equal");
        planTemplate = planTemplate.Replace("compare.lessThan", "lessThan");
        planTemplate = planTemplate.Replace("compare.greaterThan", "greaterThan");
        planTemplate = planTemplate.Replace("compare.lessThanOrEqual", "lessThanOrEqual");
        planTemplate = planTemplate.Replace("compare.greaterThanOrEqual", "greaterThanOrEqual");
        planTemplate = planTemplate.Replace("compare.greaterThanOrEqual", "greaterThanOrEqual");

        planTemplate = MinifyHandlebarsTemplate(planTemplate);
        return new HandlebarsPlan(this._kernel, planTemplate, createPlanPrompt);
    }

    private List<FunctionView> GetAvailableFunctionsManual(CancellationToken cancellationToken = default)
    {
        var availableFunctions = this._kernel.Functions.GetFunctionViews()
            .Where(s => !this._config.ExcludedPlugins.Contains(s.PluginName, StringComparer.OrdinalIgnoreCase)
                && !this._config.ExcludedFunctions.Contains(s.Name, StringComparer.OrdinalIgnoreCase)
                && !s.Name.Contains("Planner_Excluded"))
            .ToList();

        var functionsView = new List<FunctionView>();
        foreach (var skFunction in availableFunctions)
        {
            // Extract any complex schemas for isolated render in prompt template
            var parametersView = new List<ParameterView>();
            foreach (var parameter in skFunction.Parameters)
            {
                var paramToAdd = this.SetComplexTypeDefinition(parameter);
                parametersView.Add(paramToAdd);
            }

            var returnParameter = skFunction.ReturnParameter.ToParameterView(skFunction.Name);
            returnParameter = this.SetComplexTypeDefinition(returnParameter);

            // Need to override function view in case parameter views changed (e.g., converted primitive types from schema objects)
            var functionView = new FunctionView(skFunction.Name, skFunction.PluginName, skFunction.Description, parametersView, returnParameter.ToReturnParameterView());
            functionsView.Add(functionView);
        }

        return functionsView;
    }

    // Extract any complex schemas for isolated render in prompt template
    private ParameterView SetComplexTypeDefinition(ParameterView parameter)
    {
        // TODO (@teresaqhoang): Handle case when schema and ParameterType can exist i.e., when ParameterType = RestApiResponse
        if (parameter.ParameterType is not null)
        {
            // Async return type - need to extract the actual return type and override ParameterType property
            var type = parameter.ParameterType;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                parameter = parameter with { ParameterType = type.GenericTypeArguments[0] }; // Actual Return Type
            }

            this._parametersTypeView.UnionWith(parameter.ParameterType.ToHandlebarsParameterTypeView());
        }
        else if (parameter.Schema is not null)
        {
            // Parse the schema to filter any primitive types and set in ParameterType property instead
            var parsedParameter = parameter.ParseJsonSchema();
            if (parsedParameter.Schema is not null)
            {
                this._parametersSchemaView[parameter.GetSchemaTypeName()] = parameter.Schema.RootElement.ToJsonString();
            }

            parameter = parsedParameter;
        }

        return parameter;
    }

    private ChatHistory GetChatHistoryFromPrompt(string prompt, IChatCompletion chatCompletion)
    {
        // Extract the chat history from the rendered prompt
        string pattern = @"<(user~|system~|assistant~)>(.*?)<\/\1>";
        MatchCollection matches = Regex.Matches(prompt, pattern, RegexOptions.Singleline);

        // Add the chat history to the chat
        ChatHistory chatMessages = chatCompletion.CreateNewChat();
        foreach (Match m in matches.Cast<Match>())
        {
            string role = m.Groups[1].Value;
            string message = m.Groups[2].Value;

            switch (role)
            {
                case "user~":
                    chatMessages.AddUserMessage(message);
                    break;
                case "system~":
                    chatMessages.AddSystemMessage(message);
                    break;
                case "assistant~":
                    chatMessages.AddAssistantMessage(message);
                    break;
            }
        }

        return chatMessages;
    }

    private string GetHandlebarsTemplate(IKernel kernel, string goal, List<FunctionView> availableFunctions)
    {
        var plannerTemplate = this.ReadPrompt("CreatePlanPrompt.handlebars");
        var variables = new Dictionary<string, object?>()
            {
                { "functions", availableFunctions},
                { "goal", goal },
                { "reservedNameDelimiter", HandlebarsTemplateEngineExtensions.ReservedNameDelimiter},
                { "allowLoops", this._config.AllowLoops },
                { "complexTypeDefinitions", this._parametersTypeView.Count > 0 && this._parametersTypeView.Any(p => p.IsComplexType) ? this._parametersTypeView.Where(p => p.IsComplexType) : null},
                { "complexSchemaDefinitions", this._parametersSchemaView.Count > 0 ? this._parametersSchemaView : null},
                { "lastPlan", this._config.LastPlan },
                { "lastError", this._config.LastError }
            };

        return HandlebarsTemplateEngineExtensions.Render(kernel, kernel.CreateNewContext(), plannerTemplate, variables);
    }

    private static string MinifyHandlebarsTemplate(string template)
    {
        // This regex pattern matches '{{', then any characters including newlines (non-greedy), then '}}'
        string pattern = @"(\{\{[\s\S]*?}})";

        // Replace all occurrences of the pattern in the input template
        return Regex.Replace(template, pattern, m =>
        {
            // For each match, remove the whitespace within the handlebars, except for spaces
            // that separate different items (e.g., 'json' and '(get')
            return Regex.Replace(m.Value, @"\s+", " ").Replace(" {", "{").Replace(" }", "}").Replace(" )", ")");
        });
    }
}
