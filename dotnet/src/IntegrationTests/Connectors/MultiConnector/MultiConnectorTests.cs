﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.MultiConnector;
using Microsoft.SemanticKernel.Connectors.AI.Oobabooga.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.Tokenizers;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Planning.Sequential;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.MultiConnector;

/// <summary>
/// Integration tests for <see cref=" OobaboogaTextCompletion"/>.
/// </summary>
public sealed class MultiConnectorTests : IDisposable
{
    private readonly ILogger _logger;
    private readonly IConfigurationRoot _configuration;
    private List<ClientWebSocket> _webSockets = new();
    private Func<ClientWebSocket> _webSocketFactory;
    private readonly RedirectOutput _testOutputHelper;
    private readonly Func<string, int> _gp3TokenCounter = s => GPT3Tokenizer.Encode(s).Count;
    private readonly Func<string, int> _wordCounter = s => s.Split(' ').Length;

    public MultiConnectorTests(ITestOutputHelper output)
    {
        //this._logger = NullLogger.Instance;
        this._logger = new XunitLogger<object>(output);
        this._testOutputHelper = new RedirectOutput(output);

        // Load configuration
        this._configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<MultiConnectorTests>()
            .Build();

        this._webSocketFactory = () =>
        {
            var toReturn = new ClientWebSocket();
            this._webSockets.Add(toReturn);
            return toReturn;
        };
    }

    private const string StartGoal = "The goal of this plan is to evaluate the capabilities of a smaller LLM model. Start by writing a text of about 100 words on a given topic, as the input parameter of the plan. Then use distinct functions from the available skills on the input text and/or the previous functions results, choosing parameters in such a way that you know you will succeed at running each function but a smaller model might not. Try to propose steps of distinct difficulties so that models of distinct capabilities might succeed on some functions and fail on others. In a second phase, you will be asked to evaluate the function answers from smaller models. Please beware of correct Xml tags, attributes, and parameter names when defined and when reused.";

    //[Theory(Skip = "This test is for manual verification.")]
    [Theory]
    [InlineData(1, 1, 1, "SummarizeSkill", "MiscSkill")]
    public async Task ChatGptOffloadsToOobaboogaAsync(double durationWeight = 1, double costWeight = 1, int nbPromptTests = 1, params string[] skillNames)
    {
        // Arrange

        var sw = Stopwatch.StartNew();

        using var cleanupToken = new CancellationTokenSource();

        var creditor = new CallRequestCostCreditor();

        //HttpRetryConfig httpRetryConfig = new() { MaxRetryCount = 0 };
        //DefaultHttpRetryHandlerFactory defaultHttpRetryHandlerFactory = new(httpRetryConfig);

        //We configure settings to enable analysis, and let the connector discover the best settings, updating on the fly and deleting analysis file 
        var settings = new MultiTextCompletionSettings()
        {
            Creditor = creditor,
            AnalysisSettings = new MultiCompletionAnalysisSettings()
            {
                EnableAnalysis = false,
                NbPromptTests = nbPromptTests,
                MaxInstanceNb = 1,
                SuggestionPeriod = TimeSpan.FromMilliseconds(1),
                AnalysisDelay = TimeSpan.FromMilliseconds(10),
                MaxDegreeOfParallelismConnectorTests = 1,
                UpdateSuggestedSettings = true,
                DeleteAnalysisFile = true,
                SaveSuggestedSettings = false
            },
            PromptTruncationLength = 11,
            LogResult = true,
            ConnectorComparer = MultiTextCompletionSettings.GetConnectorComparer(durationWeight, costWeight),
            GlobalPromptTransform = new PromptTransform()
            {
                TransformFunction = s => s.EndsWith("\n", StringComparison.OrdinalIgnoreCase) ? s : s + "\n",
            }
        };

        var kernel = this.InitializeKernel(settings, durationWeight: durationWeight, costWeight: costWeight, cancellationToken: cleanupToken.Token);

        //var multiConnector = (MultiTextCompletion)kernel.GetService<ITextCompletion>();

        // Import all sample skills available for demonstration purposes.
        //TestHelpers.ImportSampleSkills(kernel);

        var prepareKernelTimeElapsed = sw.Elapsed;

        var skills = TestHelpers.GetSkills(kernel, skillNames);
        var planner = new SequentialPlanner(kernel,
            new SequentialPlannerConfig { RelevancyThreshold = 0.65, MaxRelevantFunctions = 30, Memory = kernel.Memory });

        // Act

        // Create a plan
        //var plan = await planner.CreatePlanAsync(StartGoal, cleanupToken.Token);
        //var planPath = System.IO.Path.Combine(Environment.CurrentDirectory, ".\\Connectors\\MultiConnector\\VettingSequentialPlan_SummarizeSkill.json");
        var planPath = System.IO.Path.Combine(Environment.CurrentDirectory, ".\\Connectors\\MultiConnector\\VettingSequentialPlan_SummarizeSkill_Summarize.json");
        var planJson = await System.IO.File.ReadAllTextAsync(planPath, cleanupToken.Token);
        var ctx = kernel.CreateNewContext();
        var plan = Plan.FromJson(planJson, ctx, true);

        settings.AnalysisSettings.EnableAnalysis = true;

        // Create a task completion source to signal the completion of the optimization
        TaskCompletionSource<OptimizationCompletedEventArgs> optimizationCompletedTaskSource = new();

        // Subscribe to the OptimizationCompleted event
        settings.OptimizationCompleted += (sender, args) =>
        {
            // Signal the completion of the optimization
            optimizationCompletedTaskSource.SetResult(args);
        };

        settings.Creditor!.Reset();

        var planBuildingTimeElapsed = sw.Elapsed;

        // Execute the plan once with primary connector

        var firstResult = await kernel.RunAsync(ctx.Variables, cleanupToken.Token, plan).ConfigureAwait(false);

        var planRunOnceTimeElapsed = sw.Elapsed;

        var firstPassEffectiveCost = settings.Creditor.OngoingCost;

        //await optimizationCompletedTaskSource.Task.WaitAsync(cleanupToken.Token);

        // Get the optimization results
        var optimizationResults = await optimizationCompletedTaskSource.Task.ConfigureAwait(false);

        var optimizationDoneElapsed = sw.Elapsed;

        //Re execute plan with suggested settings

        settings.Creditor!.Reset();

        ctx = kernel.CreateNewContext();
        plan = Plan.FromJson(planJson, ctx, true);

        var secondResult = await kernel.RunAsync(ctx.Variables, cleanupToken.Token, plan).ConfigureAwait(false);

        var secondPassEffectiveCost = settings.Creditor.OngoingCost;

        var planRunTwiceElapsed = sw.Elapsed;

        // Assert

        Assert.True(firstPassEffectiveCost > secondPassEffectiveCost);

        //Assert.Contains(
        //    plan.Steps,
        //    step =>
        //        step.Name.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
        //        step.SkillName.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Configures a kernel with MultiTextCompletion comprising a primary OpenAI connector with parameters defined in main settings for OpenAI integration tests, and Oobabooga secondary connectors with parameters defined in the MultiConnector part of the settings file.
    /// </summary>
    private IKernel InitializeKernel(MultiTextCompletionSettings multiTextCompletionSettings, double durationWeight = 1, double costWeight = 1, CancellationToken? cancellationToken = null)
    {
        cancellationToken ??= CancellationToken.None;

        var openAIConfiguration = this._configuration.GetSection("OpenAI").Get<OpenAIConfiguration>();
        Assert.NotNull(openAIConfiguration);

        ITextCompletion openAiConnector;

        string testOrChatModelId;
        if (openAIConfiguration.ChatModelId != null)
        {
            testOrChatModelId = openAIConfiguration.ChatModelId;
            openAiConnector = new OpenAIChatCompletion(testOrChatModelId, openAIConfiguration.ApiKey, logger: this._logger);
        }
        else
        {
            testOrChatModelId = openAIConfiguration.ModelId;
            openAiConnector = new OpenAITextCompletion(testOrChatModelId, openAIConfiguration.ApiKey, logger: this._logger);
        }

        var openAiNamedCompletion = new NamedTextCompletion(testOrChatModelId, openAiConnector)
        {
            MaxTokens = 4096,
            CostPer1000Token = 0.0015m,
            TokenCountFunc = this._gp3TokenCounter
        };

        var multiConnectorConfiguration = this._configuration.GetSection("MultiConnector").Get<MultiConnectorConfiguration>();
        Assert.NotNull(multiConnectorConfiguration);

        var oobaboogaCompletions = new List<NamedTextCompletion>();

        foreach (var oobaboogaConnector in multiConnectorConfiguration.OobaboogaCompletions)
        {
            var oobaboogaCompletion = new OobaboogaTextCompletion(
                endpoint: new Uri(multiConnectorConfiguration.OobaboogaEndPoint),
                blockingPort: oobaboogaConnector.BlockingPort,
                streamingPort: oobaboogaConnector.StreamingPort,
                webSocketFactory: this._webSocketFactory,
                logger: this._logger);

            Func<string, int> tokeCountFunction;
            switch (oobaboogaConnector.TokenCountFunction)
            {
                case TokenCountFunction.Gpt3Tokenizer:
                    tokeCountFunction = this._gp3TokenCounter;
                    break;
                case TokenCountFunction.WordCount:
                    tokeCountFunction = this._wordCounter;
                    break;
                default:
                    throw new InvalidOperationException("token count function not supported");
            }

            var oobaboogaNamedCompletion = new NamedTextCompletion(oobaboogaConnector.Name, oobaboogaCompletion)
            {
                CostPerRequest = oobaboogaConnector.CostPerRequest,
                CostPer1000Token = oobaboogaConnector.CostPer1000Token,
                TokenCountFunc = tokeCountFunction,
                TemperatureTransform = d => d == 0 ? 0.01 : d,
                PromptTransform = oobaboogaConnector.PromptTransform
                //RequestSettingsTransform = requestSettings =>
                //{
                //    var newRequestSettings = new CompleteRequestSettings()
                //    {
                //        MaxTokens = requestSettings.MaxTokens,
                //        ResultsPerPrompt = requestSettings.ResultsPerPrompt,
                //        ChatSystemPrompt = requestSettings.ChatSystemPrompt,
                //        FrequencyPenalty = requestSettings.FrequencyPenalty,
                //        PresencePenalty = 0.3,
                //        StopSequences = requestSettings.StopSequences,
                //        Temperature = 0.7,
                //        TokenSelectionBiases = requestSettings.TokenSelectionBiases,
                //        TopP = 0.9,
                //    };
                //    return newRequestSettings;
                //}
            };
            oobaboogaCompletions.Add(oobaboogaNamedCompletion);
        }

        var builder = Kernel.Builder
            .WithLogger(this._logger);
        //.WithMemoryStorage(new VolatileMemoryStore());

        builder.WithMultiConnectorCompletionService(
            serviceId: null,
            settings: multiTextCompletionSettings,
            mainTextCompletion: openAiNamedCompletion,
            setAsDefault: true,
            analysisTaskCancellationToken: cancellationToken,
            otherCompletions: oobaboogaCompletions.ToArray());

        var kernel = builder.Build();

        return kernel;
    }

    public void Dispose()
    {
        foreach (ClientWebSocket clientWebSocket in this._webSockets)
        {
            clientWebSocket.Dispose();
        }

        this._testOutputHelper.Dispose();
    }
}
