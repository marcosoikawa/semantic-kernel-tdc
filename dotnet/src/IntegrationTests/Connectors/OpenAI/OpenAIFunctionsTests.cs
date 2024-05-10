﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernel.IntegrationTests.Planners.Stepwise;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.OpenAI;

public sealed class OpenAIFunctionsTests : BaseIntegrationTest
{
    [Fact]
    public async Task CanAutoInvokeKernelFunctionsAsync()
    {
        // Arrange
        Kernel kernel = this.InitializeKernel();
        kernel.ImportPluginFromType<TimeInformation>();

        var invokedFunctions = new List<string>();

        var filter = new FakeFunctionFilter(async (context, next) =>
        {
            invokedFunctions.Add(context.Function.Name);
            await next(context);
        });

        kernel.FunctionInvocationFilters.Add(filter);

        // Act
        OpenAIPromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice() };
        var result = await kernel.InvokePromptAsync("How many days until Christmas? Explain your thinking.", new(settings));

        // Assert
        Assert.NotNull(result);
        Assert.Contains("GetCurrentUtcTime", invokedFunctions);
    }

    [Fact]
    public async Task CanAutoInvokeKernelFunctionsStreamingAsync()
    {
        // Arrange
        Kernel kernel = this.InitializeKernel();
        kernel.ImportPluginFromType<TimeInformation>();

        var invokedFunctions = new List<string>();

#pragma warning disable CS0618 // Events are deprecated
        void MyInvokingHandler(object? sender, FunctionInvokingEventArgs e)
        {
            invokedFunctions.Add($"{e.Function.Name}({string.Join(", ", e.Arguments)})");
        }

        kernel.FunctionInvoking += MyInvokingHandler;
#pragma warning restore CS0618 // Events are deprecated

        // Act
        OpenAIPromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice() };
        string result = "";
        await foreach (string c in kernel.InvokePromptStreamingAsync<string>(
            $"How much older is John than Jim? Compute that value and pass it to the {nameof(TimeInformation)}.{nameof(TimeInformation.InterpretValue)} function, then respond only with its result.",
            new(settings)))
        {
            result += c;
        }

        // Assert
        Assert.Contains("6", result, StringComparison.InvariantCulture);
        Assert.Contains("GetAge([personName, John])", invokedFunctions);
        Assert.Contains("GetAge([personName, Jim])", invokedFunctions);
        Assert.Contains("InterpretValue([value, 3])", invokedFunctions);
    }

    [Fact]
    public async Task CanAutoInvokeKernelFunctionsWithComplexTypeParametersAsync()
    {
        // Arrange
        Kernel kernel = this.InitializeKernel();
        kernel.ImportPluginFromType<WeatherPlugin>();

        // Act
        OpenAIPromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice() };
        var result = await kernel.InvokePromptAsync("What is the current temperature in Dublin, Ireland, in Fahrenheit?", new(settings));

        // Assert
        Assert.NotNull(result);
        Assert.Contains("42.8", result.GetValue<string>(), StringComparison.InvariantCulture); // The WeatherPlugin always returns 42.8 for Dublin, Ireland.
    }

    [Fact]
    public async Task CanAutoInvokeKernelFunctionsWithPrimitiveTypeParametersAsync()
    {
        // Arrange
        Kernel kernel = this.InitializeKernel();
        kernel.ImportPluginFromType<WeatherPlugin>();

        // Act
        OpenAIPromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice() };

        var result = await kernel.InvokePromptAsync("Convert 50 degrees Fahrenheit to Celsius.", new(settings));

        // Assert
        Assert.NotNull(result);
        Assert.Contains("10", result.GetValue<string>(), StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task CanAutoInvokeKernelFunctionFromPromptAsync()
    {
        // Arrange
        Kernel kernel = this.InitializeKernel();

        var promptFunction = KernelFunctionFactory.CreateFromPrompt(
            "Your role is always to return this text - 'A Game-Changer for the Transportation Industry'. Don't ask for more details or context.",
            functionName: "FindLatestNews",
            description: "Searches for the latest news.");

        kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions(
            "NewsProvider",
            "Delivers up-to-date news content.",
            [promptFunction]));

        // Act
        OpenAIPromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice() };
        var result = await kernel.InvokePromptAsync("Show me the latest news as they are.", new(settings));

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Transportation", result.GetValue<string>(), StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task CanAutoInvokeKernelFunctionFromPromptStreamingAsync()
    {
        // Arrange
        Kernel kernel = this.InitializeKernel();

        var promptFunction = KernelFunctionFactory.CreateFromPrompt(
            "Your role is always to return this text - 'A Game-Changer for the Transportation Industry'. Don't ask for more details or context.",
            functionName: "FindLatestNews",
            description: "Searches for the latest news.");

        kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions(
            "NewsProvider",
            "Delivers up-to-date news content.",
            [promptFunction]));

        // Act
        OpenAIPromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice() };
        var streamingResult = kernel.InvokePromptStreamingAsync("Show me the latest news as they are.", new(settings));

        var builder = new StringBuilder();

        await foreach (var update in streamingResult)
        {
            builder.Append(update.ToString());
        }

        var result = builder.ToString();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Transportation", result, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task ConnectorSpecificChatMessageContentClassesCanBeUsedForManualFunctionCallingAsync()
    {
        // Arrange
        var kernel = this.InitializeKernel(importHelperPlugin: true);

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Given the current time of day and weather, what is the likely color of the sky in Boston?");

        var settings = new OpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice(autoInvoke: false) };

        var sut = kernel.GetRequiredService<IChatCompletionService>();

        // Act
        var result = await sut.GetChatMessageContentAsync(chatHistory, settings, kernel);

        // Current way of handling function calls manually using connector specific chat message content class.
        var toolCalls = ((OpenAIChatMessageContent)result).ToolCalls.OfType<ChatCompletionsFunctionToolCall>().ToList();

        while (toolCalls.Count > 0)
        {
            // Adding LLM function call request to chat history
            chatHistory.Add(result);

            // Iterating over the requested function calls and invoking them
            foreach (var toolCall in toolCalls)
            {
                string content = kernel.Plugins.TryGetFunctionAndArguments(toolCall, out KernelFunction? function, out KernelArguments? arguments) ?
                    JsonSerializer.Serialize((await function.InvokeAsync(kernel, arguments)).GetValue<object>()) :
                    "Unable to find function. Please try again!";

                // Adding the result of the function call to the chat history
                chatHistory.Add(new ChatMessageContent(
                    AuthorRole.Tool,
                    content,
                    metadata: new Dictionary<string, object?>(1) { { OpenAIChatMessageContent.ToolIdProperty, toolCall.Id } }));
            }

            // Sending the functions invocation results back to the LLM to get the final response
            result = await sut.GetChatMessageContentAsync(chatHistory, settings, kernel);
            toolCalls = ((OpenAIChatMessageContent)result).ToolCalls.OfType<ChatCompletionsFunctionToolCall>().ToList();
        }

        // Assert
        Assert.Contains("rain", result.Content, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task ConnectorAgnosticFunctionCallingModelClassesCanBeUsedForManualFunctionCallingAsync()
    {
        // Arrange
        var kernel = this.InitializeKernel(importHelperPlugin: true);

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Given the current time of day and weather, what is the likely color of the sky in Boston?");

        var settings = new OpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice(autoInvoke: false) };

        var sut = kernel.GetRequiredService<IChatCompletionService>();

        // Act
        var messageContent = await sut.GetChatMessageContentAsync(chatHistory, settings, kernel);

        var functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();

        while (functionCalls.Length != 0)
        {
            // Adding function call request from LLM to chat history
            chatHistory.Add(messageContent);

            // Iterating over the requested function calls and invoking them
            foreach (var functionCall in functionCalls)
            {
                var result = await functionCall.InvokeAsync(kernel);

                chatHistory.Add(result.ToChatMessage());
            }

            // Sending the functions invocation results to the LLM to get the final response
            messageContent = await sut.GetChatMessageContentAsync(chatHistory, settings, kernel);
            functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();
        }

        // Assert
        Assert.Contains("rain", messageContent.Content, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task ConnectorAgnosticFunctionCallingModelClassesCanPassFunctionExceptionToConnectorAsync()
    {
        // Arrange
        var kernel = this.InitializeKernel(importHelperPlugin: true);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage("If you are unable to answer the question for whatever reason, please add the 'error' keyword to the response.");
        chatHistory.AddUserMessage("Given the current time of day and weather, what is the likely color of the sky in Boston?");

        var settings = new OpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice(autoInvoke: false) };

        var completionService = kernel.GetRequiredService<IChatCompletionService>();

        // Act
        var messageContent = await completionService.GetChatMessageContentAsync(chatHistory, settings, kernel);

        var functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();

        while (functionCalls.Length != 0)
        {
            // Adding function call request from LLM to chat history
            chatHistory.Add(messageContent);

            // Iterating over the requested function calls and invoking them
            foreach (var functionCall in functionCalls)
            {
                // Simulating an exception
                var exception = new OperationCanceledException("The operation was canceled due to timeout.");

                chatHistory.Add(new FunctionResultContent(functionCall, exception).ToChatMessage());
            }

            // Sending the functions execution results back to the LLM to get the final response
            messageContent = await completionService.GetChatMessageContentAsync(chatHistory, settings, kernel);
            functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();
        }

        // Assert
        Assert.NotNull(messageContent.Content);

        Assert.Contains("error", messageContent.Content, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task ConnectorAgnosticFunctionCallingModelClassesSupportSimulatedFunctionCallsAsync()
    {
        // Arrange
        var kernel = this.InitializeKernel(importHelperPlugin: true);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage("if there's a tornado warning, please add the 'tornado' keyword to the response.");
        chatHistory.AddUserMessage("Given the current time of day and weather, what is the likely color of the sky in Boston?");

        var settings = new OpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice(autoInvoke: false) };

        var completionService = kernel.GetRequiredService<IChatCompletionService>();

        // Act
        var messageContent = await completionService.GetChatMessageContentAsync(chatHistory, settings, kernel);

        var functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();

        while (functionCalls.Length > 0)
        {
            // Adding function call request from LLM to chat history
            chatHistory.Add(messageContent);

            // Iterating over the requested function calls and invoking them
            foreach (var functionCall in functionCalls)
            {
                var result = await functionCall.InvokeAsync(kernel);

                chatHistory.AddMessage(AuthorRole.Tool, new ChatMessageContentItemCollection() { result });
            }

            // Adding a simulated function call to the connector response message
            var simulatedFunctionCall = new FunctionCallContent("weather-alert", id: "call_123");
            messageContent.Items.Add(simulatedFunctionCall);

            // Adding a simulated function result to chat history
            var simulatedFunctionResult = "A Tornado Watch has been issued, with potential for severe thunderstorms causing unusual sky colors like green, yellow, or dark gray. Stay informed and follow safety instructions from authorities.";
            chatHistory.Add(new FunctionResultContent(simulatedFunctionCall, simulatedFunctionResult).ToChatMessage());

            // Sending the functions invocation results back to the LLM to get the final response
            messageContent = await completionService.GetChatMessageContentAsync(chatHistory, settings, kernel);
            functionCalls = FunctionCallContent.GetFunctionCalls(messageContent).ToArray();
        }

        // Assert
        Assert.Contains("tornado", messageContent.Content, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task ItFailsIfNoFunctionResultProvidedAsync()
    {
        // Arrange
        var kernel = this.InitializeKernel(importHelperPlugin: true);

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Given the current time of day and weather, what is the likely color of the sky in Boston?");

        var settings = new OpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice(autoInvoke: false) };

        var completionService = kernel.GetRequiredService<IChatCompletionService>();

        // Act
        var result = await completionService.GetChatMessageContentAsync(chatHistory, settings, kernel);

        chatHistory.Add(result);

        var exception = await Assert.ThrowsAsync<HttpOperationException>(() => completionService.GetChatMessageContentAsync(chatHistory, settings, kernel));

        // Assert
        Assert.Contains("'tool_calls' must be followed by tool", exception.Message, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task ConnectorAgnosticFunctionCallingModelClassesCanBeUsedForAutoFunctionCallingAsync()
    {
        // Arrange
        var kernel = this.InitializeKernel(importHelperPlugin: true);

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Given the current time of day and weather, what is the likely color of the sky in Boston?");

        var settings = new OpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice() };

        var sut = kernel.GetRequiredService<IChatCompletionService>();

        // Act
        await sut.GetChatMessageContentAsync(chatHistory, settings, kernel);

        // Assert
        Assert.Equal(5, chatHistory.Count);

        var userMessage = chatHistory[0];
        Assert.Equal(AuthorRole.User, userMessage.Role);

        // LLM requested the current time.
        var getCurrentTimeFunctionCallMessage = chatHistory[1];
        Assert.Equal(AuthorRole.Assistant, getCurrentTimeFunctionCallMessage.Role);

        var getCurrentTimeFunctionCall = getCurrentTimeFunctionCallMessage.Items.OfType<FunctionCallContent>().Single();
        Assert.Equal("GetCurrentUtcTime", getCurrentTimeFunctionCall.FunctionName);
        Assert.Equal("HelperFunctions", getCurrentTimeFunctionCall.PluginName);
        Assert.NotNull(getCurrentTimeFunctionCall.Id);

        // Connector invoked the GetCurrentUtcTime function and added result to chat history.
        var getCurrentTimeFunctionResultMessage = chatHistory[2];
        Assert.Equal(AuthorRole.Tool, getCurrentTimeFunctionResultMessage.Role);
        Assert.Single(getCurrentTimeFunctionResultMessage.Items.OfType<TextContent>()); // Current function calling model adds TextContent item representing the result of the function call.

        var getCurrentTimeFunctionResult = getCurrentTimeFunctionResultMessage.Items.OfType<FunctionResultContent>().Single();
        Assert.Equal("GetCurrentUtcTime", getCurrentTimeFunctionResult.FunctionName);
        Assert.Equal("HelperFunctions", getCurrentTimeFunctionResult.PluginName);
        Assert.Equal(getCurrentTimeFunctionCall.Id, getCurrentTimeFunctionResult.Id);
        Assert.NotNull(getCurrentTimeFunctionResult.Result);

        // LLM requested the weather for Boston.
        var getWeatherForCityFunctionCallMessage = chatHistory[3];
        Assert.Equal(AuthorRole.Assistant, getWeatherForCityFunctionCallMessage.Role);

        var getWeatherForCityFunctionCall = getWeatherForCityFunctionCallMessage.Items.OfType<FunctionCallContent>().Single();
        Assert.Equal("Get_Weather_For_City", getWeatherForCityFunctionCall.FunctionName);
        Assert.Equal("HelperFunctions", getWeatherForCityFunctionCall.PluginName);
        Assert.NotNull(getWeatherForCityFunctionCall.Id);

        // Connector invoked the Get_Weather_For_City function and added result to chat history.
        var getWeatherForCityFunctionResultMessage = chatHistory[4];
        Assert.Equal(AuthorRole.Tool, getWeatherForCityFunctionResultMessage.Role);
        Assert.Single(getWeatherForCityFunctionResultMessage.Items.OfType<TextContent>()); // Current function calling model adds TextContent item representing the result of the function call.

        var getWeatherForCityFunctionResult = getWeatherForCityFunctionResultMessage.Items.OfType<FunctionResultContent>().Single();
        Assert.Equal("Get_Weather_For_City", getWeatherForCityFunctionResult.FunctionName);
        Assert.Equal("HelperFunctions", getWeatherForCityFunctionResult.PluginName);
        Assert.Equal(getWeatherForCityFunctionCall.Id, getWeatherForCityFunctionResult.Id);
        Assert.NotNull(getWeatherForCityFunctionResult.Result);
    }

    [Fact]
    public async Task SubsetOfFunctionsCanBeUsedForFunctionCallingAsync()
    {
        // Arrange
        var kernel = this.InitializeKernel(importHelperPlugin: false);

        var function = kernel.CreateFunctionFromMethod(() => DayOfWeek.Friday, "GetDayOfWeek", "Retrieves the current day of the week.");
        kernel.ImportPluginFromFunctions("HelperFunctions", [function]);

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("What day is today?");

        var settings = new OpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice([function], autoInvoke: true) };

        var sut = kernel.GetRequiredService<IChatCompletionService>();

        // Act
        var result = await sut.GetChatMessageContentAsync(chatHistory, settings, kernel);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Friday", result.Content, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task RequiredFunctionShouldBeCalledAsync()
    {
        // Arrange
        var kernel = this.InitializeKernel(importHelperPlugin: false);

        var function = kernel.CreateFunctionFromMethod(() => DayOfWeek.Friday, "GetDayOfWeek", "Retrieves the current day of the week.");
        kernel.ImportPluginFromFunctions("HelperFunctions", [function]);

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("What day is today?");

        var settings = new PromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.RequiredFunctionChoice([function], true) };

        var sut = kernel.GetRequiredService<IChatCompletionService>();

        // Act
        var result = await sut.GetChatMessageContentAsync(chatHistory, settings, kernel);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Friday", result.Content, StringComparison.InvariantCulture);
    }

    private Kernel InitializeKernel(bool importHelperPlugin = false)
    {
        OpenAIConfiguration? openAIConfiguration = this._configuration.GetSection("Planners:OpenAI").Get<OpenAIConfiguration>();
        Assert.NotNull(openAIConfiguration);

        IKernelBuilder builder = this.CreateKernelBuilder()
            .AddOpenAIChatCompletion(
                modelId: openAIConfiguration.ModelId,
                apiKey: openAIConfiguration.ApiKey);

        var kernel = builder.Build();

        if (importHelperPlugin)
        {
            kernel.ImportPluginFromFunctions("HelperFunctions", new[]
            {
                kernel.CreateFunctionFromMethod(() => DateTime.UtcNow.ToString("R"), "GetCurrentUtcTime", "Retrieves the current time in UTC."),
                kernel.CreateFunctionFromMethod((string cityName) =>
                    cityName switch
                    {
                        "Boston" => "61 and rainy",
                        _ => "31 and snowing",
                    }, "Get_Weather_For_City", "Gets the current weather for the specified city"),
            });
        }

        return kernel;
    }

    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
        .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddUserSecrets<FunctionCallingStepwisePlannerTests>()
        .Build();

    /// <summary>
    /// A plugin that returns the current time.
    /// </summary>
    public class TimeInformation
    {
        [KernelFunction]
        [Description("Retrieves the current time in UTC.")]
        public string GetCurrentUtcTime() => DateTime.UtcNow.ToString("R");

        [KernelFunction]
        [Description("Gets the age of the specified person.")]
        public int GetAge(string personName)
        {
            if ("John".Equals(personName, StringComparison.OrdinalIgnoreCase))
            {
                return 33;
            }

            if ("Jim".Equals(personName, StringComparison.OrdinalIgnoreCase))
            {
                return 30;
            }

            return -1;
        }

        [KernelFunction]
        public int InterpretValue(int value) => value * 2;
    }

    public class WeatherPlugin
    {
        [KernelFunction, Description("Get current temperature.")]
        public Task<double> GetCurrentTemperatureAsync(WeatherParameters parameters)
        {
            if (parameters.City.Name == "Dublin" && (parameters.City.Country == "Ireland" || parameters.City.Country == "IE"))
            {
                return Task.FromResult(42.8); // 42.8 Fahrenheit.
            }

            throw new NotSupportedException($"Weather in {parameters.City.Name} ({parameters.City.Country}) is not supported.");
        }

        [KernelFunction, Description("Convert temperature from Fahrenheit to Celsius.")]
        public Task<double> ConvertTemperatureAsync(double temperatureInFahrenheit)
        {
            double temperatureInCelsius = (temperatureInFahrenheit - 32) * 5 / 9;
            return Task.FromResult(temperatureInCelsius);
        }
    }

    public record WeatherParameters(City City);

    public class City
    {
        public string Name { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    #region private

    private sealed class FakeFunctionFilter : IFunctionInvocationFilter
    {
        private readonly Func<FunctionInvocationContext, Func<FunctionInvocationContext, Task>, Task>? _onFunctionInvocation;

        public FakeFunctionFilter(
            Func<FunctionInvocationContext, Func<FunctionInvocationContext, Task>, Task>? onFunctionInvocation = null)
        {
            this._onFunctionInvocation = onFunctionInvocation;
        }

        public Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next) =>
            this._onFunctionInvocation?.Invoke(context, next) ?? Task.CompletedTask;
    }

    #endregion
}
