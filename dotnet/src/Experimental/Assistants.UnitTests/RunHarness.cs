﻿// Copyright (c) Microsoft. All rights reserved.

//#define DISABLEHOST // Comment line to enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Experimental.Assistants;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.Experimental.Assistants.UnitTests;

/// <summary>
/// Dev harness for manipulating runs.
/// </summary>
public sealed class RunHarness
{
#if DISABLEHOST
    private const string SkipReason = "Harness only for local/dev environment";
#else
    private const string SkipReason = null;
#endif

    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Test contructor.
    /// </summary>
    public RunHarness(ITestOutputHelper output)
    {
        this._output = output;
    }

    /// <summary>
    /// Verify creation of run.
    /// </summary>
    [Fact(Skip = SkipReason)]
    public async Task VerifyRunLifecycleAsync()
    {
        using var httpClient = new HttpClient();
        var context = OpenAIRestContext.CreateFromConfig(httpClient);

        var assistant =
            await context.CreateAssistantAsync(
                model: "gpt-3.5-turbo-1106",
                instructions: "say something funny",
                name: "Fred",
                description: "funny assistant").ConfigureAwait(true);

        var thread = await context.CreateThreadAsync().ConfigureAwait(true);

        await this.ChatAsync(
            thread,
            assistant.Id,
            "I was on my way to the store this morning and...",
            "That was great!  Tell me another.").ConfigureAwait(true);

        var copy = await context.GetThreadAsync(thread.Id).ConfigureAwait(true);
        this.DumpMessages(copy);

        Assert.Equal(4, copy.Messages.Count);
    }

    /// <summary>
    /// Verify creation of run.
    /// </summary>
    [Fact(Skip = SkipReason)]
    public async Task VerifyFunctionLifecycleAsync()
    {
        using var httpClient = new HttpClient();
        var context = OpenAIRestContext.CreateFromConfig(httpClient);

        var kernel = new KernelBuilder()
            //.WithLoggerFactory(ConsoleLogger.LoggerFactory) TODO: @chris - ???
            //.WithOpenAIChatCompletionService("gpt-3.5-turbo-1106", context.ApiKey, serviceId: "chat")
            .Build();

        kernel.ImportFunctions(new GuessingGame(), nameof(GuessingGame));

        var assistant =
            await context.CreateAssistant()
                .WithModel("gpt-3.5-turbo-1106")
                .WithInstructions("Run a guessing game where the user tries to guess the answer to a question.")
                .WithName("Fred")
                .WithTools(kernel)
                .BuildAsync().ConfigureAwait(true);

        var thread = await context.CreateThreadAsync().ConfigureAwait(true);
        await this.ChatAsync(
            thread,
            assistant.Id,
            "What is the question for the guessing game?",
            "Is it 'RED'?",
            "What is the answer?").ConfigureAwait(true);

        var copy = await context.GetThreadAsync(thread.Id).ConfigureAwait(true);
        this.DumpMessages(copy);

        Assert.Equal(6, copy.Messages.Count);
    }

    private async Task ChatAsync(IChatThread thread, string assistantId, params string[] messages)
    {
        foreach (var message in messages)
        {
            var messageUser = await thread.AddUserMessageAsync(message).ConfigureAwait(true);
            this.LogMessage(messageUser);

            var assistantMessages = await thread.InvokeAsync(assistantId).ConfigureAwait(true);
            this.LogMessages(assistantMessages);
        }
    }

    private void DumpMessages(IChatThread thread)
    {
        foreach (var message in thread.Messages)
        {
            if (string.IsNullOrWhiteSpace(message.AssistantId))
            {
                this._output.WriteLine($"{message.Role}: {message.Content}");
            }
            else
            {
                this._output.WriteLine($"{message.Role}: {message.Content} [{message.AssistantId}]");
            }
        }
    }

    private void LogMessages(IEnumerable<IChatMessage> messages)
    {
        foreach (var message in messages)
        {
            this.LogMessage(message);
        }
    }

    private void LogMessage(IChatMessage message)
    {
        this._output.WriteLine($"# {message.Id}");
        this._output.WriteLine($"# {message.Content}");
        this._output.WriteLine($"# {message.Role}");
        this._output.WriteLine($"# {message.AssistantId}");
    }

    private sealed class GuessingGame
    {
        /// <summary>
        /// Get the question
        /// </summary>
        [SKFunction, Description("Get the guessing game question")]
        public string GetQuestion() => "What color am I thinking of?";

        /// <summary>
        /// Get the answer
        /// </summary>
        [SKFunction, Description("Get the guessing game answer")]
        public string GetAnswer() => "Blue";
    }
}
