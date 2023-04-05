﻿using System.Globalization;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using SKWebApi.Skills;

namespace SemanticKernel.Service.Skills;

/// <summary>
/// ChatSkill offers a more coherent chat experience by using memories
/// to extract conversation history and user intentions.
/// </summary>
public class ChatSkill
{
    /// <summary>
    /// A kernel instance to create a completion function since each invocation
    /// of the <see cref="ChatAsync"/> function will generate a new prompt dynamically.
    /// </summary>
    private readonly IKernel _kernel;

    public ChatSkill(IKernel kernel)
    {
        this._kernel = kernel;
    }

    /// <summary>
    /// Extract user intent from the conversation history.
    /// </summary>
    /// <param name="context">Contains the 'audience' indicating the name of the user.</param>
    [SKFunction("Extract user intent")]
    [SKFunctionName("ExtractUserIntent")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Chat ID to extract history from")]
    [SKFunctionContextParameter(Name = "audience", Description = "The audience the chat bot is interacting with.")]
    public async Task<string> ExtractUserIntentAsync(SKContext context)
    {
        var tokenLimit = SystemPromptDefaults.CompletionTokenLimit;
        var historyTokenBudget =
            tokenLimit -
            SystemPromptDefaults.ResponseTokenLimit -
            this.EstimateTokenCount(string.Join("\n", new string[]
                {
                    SystemPromptDefaults.SystemDescriptionPrompt,
                    SystemPromptDefaults.SystemIntentPrompt,
                    SystemPromptDefaults.SystemIntentContinuationPrompt
                })
            );

        // Clone the context to avoid modifying the original context variables.
        var intentExtractionVariables = context.Variables.Clone();
        intentExtractionVariables.Set("tokenLimit", historyTokenBudget.ToString(new NumberFormatInfo()));
        intentExtractionVariables.Set("knowledgeCutoff", SystemPromptDefaults.KnowledgeCutoffDate);

        var intentExtractionContext = new SKContext(
            intentExtractionVariables,
            context.Memory,
            context.Skills,
            context.Log,
            context.CancellationToken
        );

        var completionFunction = this._kernel.CreateSemanticFunction(
            SystemPromptDefaults.SystemIntentExtractionPrompt,
            skillName: nameof(ChatSkill),
            description: "Complete the prompt.");

        var result = await completionFunction.InvokeAsync(
            intentExtractionContext,
            settings: this.CreateIntentCompletionSettings()
        );

        return $"User intent: {result}";
    }

    /// <summary>
    /// Extract relevant memories based on the latest message.
    /// </summary>
    /// <param name="context">Contains the 'tokenLimit' and the 'contextTokenLimit' controlling the length of the prompt.</param>
    [SKFunction("Extract user memories")]
    [SKFunctionName("ExtractUserMemories")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Chat ID to extract history from")]
    [SKFunctionContextParameter(Name = "tokenLimit", Description = "Maximum number of tokens")]
    [SKFunctionContextParameter(Name = "contextTokenLimit", Description = "Maximum number of context tokens")]
    public async Task<string> ExtractUserMemoriesAsync(SKContext context)
    {
        var tokenLimit = int.Parse(context["tokenLimit"], new NumberFormatInfo());
        var contextTokenLimit = int.Parse(context["contextTokenLimit"], new NumberFormatInfo());

        var remainingToken = Math.Min(
            tokenLimit,
            Math.Floor(contextTokenLimit * SystemPromptDefaults.MemoriesResponseContextWeight)
        );

        var chatMemorySkill = new ChatMemorySkill();
        var latestMessageContext = await chatMemorySkill.GetLatestChatMessageAsync(context["chatId"], context);
        var latestChatMessage = JsonSerializer.Deserialize<ChatMessage>(latestMessageContext.Result);
        if (latestChatMessage == null)
        {
            context.Log.LogError("Failed to deserialize the latest chat message");
            return string.Empty;
        }

        string memoryText = "";
        var results = context.Memory.SearchAsync(
            ChatMemorySkill.MessageCollectionName(context["chatId"]), latestChatMessage.ToString(), limit: 1000);
        await foreach (var memory in results)
        {
            var estimatedTokenCount = this.EstimateTokenCount(memory.Metadata.Text);
            if (remainingToken - estimatedTokenCount > 0)
            {
                memoryText += $"\n{memory.Metadata.Text}";
                remainingToken -= estimatedTokenCount;
            }
            else
            {
                break;
            }
        }

        return $"Past memories:\n{memoryText.Trim()}";
    }

    /// <summary>
    /// Extract chat history.
    /// </summary>
    /// <param name="context">Contains the 'tokenLimit' and the 'contextTokenLimit' controlling the length of the prompt.</param>
    [SKFunction("Extract chat history")]
    [SKFunctionName("ExtractChatHistory")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Chat ID to extract history from")]
    [SKFunctionContextParameter(Name = "tokenLimit", Description = "Maximum number of tokens")]
    [SKFunctionContextParameter(Name = "contextTokenLimit", Description = "Maximum number of context tokens")]
    public async Task<string> ExtractChatHistoryAsync(SKContext context)
    {
        var tokenLimit = int.Parse(context["tokenLimit"], new NumberFormatInfo());

        // TODO: Use contextTokenLimit to determine how much of the chat history to return
        // TODO: relevant history

        var remainingToken = tokenLimit;
        string historyText = "";

        var chatMemorySkill = new ChatMemorySkill();
        var chatMessagesContext = await chatMemorySkill.GetAllChatMessagesAsync(context["chatId"], context);
        if (chatMessagesContext.ErrorOccurred)
        {
            context.Log.LogError("Failed to get chat messages");
            return string.Empty;
        }

        var chatMessages = JsonSerializer.Deserialize<List<ChatMessage>>(chatMessagesContext.Result);
        if (chatMessages == null)
        {
            context.Log.LogError("Failed to deserialize chat messages");
            return string.Empty;
        }

        foreach (var chatMessage in chatMessages)
        {
            var formattedMessage = chatMessage.ToString();
            var estimatedTokenCount = this.EstimateTokenCount(formattedMessage);
            if (remainingToken - estimatedTokenCount > 0)
            {
                historyText += $"\n{formattedMessage}";
                remainingToken -= estimatedTokenCount;
            }
            else
            {
                break;
            }
        }

        return $"Chat history:\n{historyText.Trim()}";
    }

    /// <summary>
    /// This is the entry point for getting a chat response. It manages the token limit, saves
    /// messages to memory, and fill in the necessary context variables for completing the
    /// prompt that will be rendered by the template engine.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="context">Contains the 'tokenLimit' and the 'contextTokenLimit' controlling the length of the prompt.</param>
    [SKFunction("Get chat response")]
    [SKFunctionName("Chat")]
    [SKFunctionInput(Description = "The new message")]
    [SKFunctionContextParameter(Name = "userId", Description = "Unique and persistent identifier for the user")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Unique and persistent identifier for the chat")]
    public async Task<SKContext> ChatAsync(string message, SKContext context)
    {
        var tokenLimit = SystemPromptDefaults.CompletionTokenLimit;
        var remainingToken =
            tokenLimit -
            SystemPromptDefaults.ResponseTokenLimit -
            this.EstimateTokenCount(string.Join("\n", new string[]
                {
                    SystemPromptDefaults.SystemDescriptionPrompt,
                    SystemPromptDefaults.SystemResponsePrompt,
                    SystemPromptDefaults.SystemChatContinuationPrompt
                })
            );
        var contextTokenLimit = remainingToken;
        var chatMemorySkill = new ChatMemorySkill();

        var chatUser = await chatMemorySkill.GetChatUserAsync(context["userId"], context);
        if (chatUser == null)
        {
            context.Log.LogError("Failed to get chat user");
            context.Fail("Failed to get chat user");
            return context;
        }

        // Save this new message to memory such that subsequent chat responses can use it
        try
        {
            await chatMemorySkill.SaveNewMessageAsync(message, context);
            if (context.ErrorOccurred)
            {
                return context;
            }
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            context.Log.LogError($"Unable to save new message: {ex.Message}");
            context.Fail($"Unable to save new message: {ex.Message}", ex);
            return context;
        }

        // Clone the context to avoid modifying the original context variables.
        var chatVariables = context.Variables.Clone();
        chatVariables.Set("tokenLimit", remainingToken.ToString(new NumberFormatInfo()));
        chatVariables.Set("contextTokenLimit", contextTokenLimit.ToString(new NumberFormatInfo()));
        chatVariables.Set("knowledgeCutoff", SystemPromptDefaults.KnowledgeCutoffDate);
        chatVariables.Set("audience", chatUser.FullName);
        var chatContext = new SKContext(
            chatVariables,
            context.Memory,
            context.Skills,
            context.Log,
            context.CancellationToken
        );

        // Extract user intent and update remaining token count
        var userIntent = await this.ExtractUserIntentAsync(chatContext);
        chatContext.Variables.Set("userIntent", userIntent);
        remainingToken -= this.EstimateTokenCount(userIntent);

        var completionFunction = this._kernel.CreateSemanticFunction(
            SystemPromptDefaults.SystemChatPrompt,
            skillName: nameof(ChatSkill),
            description: "Complete the prompt.");

        context = await completionFunction.InvokeAsync(
            context: chatContext,
            settings: this.CreateChatResponseCompletionSettings()
        );

        // Save this response to memory such that subsequent chat responses can use it
        try
        {
            chatContext.Variables.Set("userId", ChatMemorySkill.ChatBotID(context["chatId"]));
            await chatMemorySkill.SaveNewMessageAsync(context.Result, chatContext);
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            context.Log.LogError($"Unable to save new message: {ex.Message}");
            context.Fail($"Unable to save new response: {ex.Message}", ex);
            return context;
        }

        return context;
    }

    /// <summary>
    /// Create a completion settings object for chat response. Parameters are read from the SystemPromptDefaults class.
    /// </summary>
    private CompleteRequestSettings CreateChatResponseCompletionSettings()
    {
        var completionSettings = new CompleteRequestSettings
        {
            MaxTokens = SystemPromptDefaults.ResponseTokenLimit,
            Temperature = SystemPromptDefaults.ResponseTemperature,
            TopP = SystemPromptDefaults.ResponseTopP,
            FrequencyPenalty = SystemPromptDefaults.ResponseFrequencyPenalty,
            PresencePenalty = SystemPromptDefaults.ResponsePresencePenalty
        };

        return completionSettings;
    }

    /// <summary>
    /// Create a completion settings object for intent response. Parameters are read from the SystemPromptDefaults class.
    /// </summary>
    private CompleteRequestSettings CreateIntentCompletionSettings()
    {
        var completionSettings = new CompleteRequestSettings
        {
            MaxTokens = SystemPromptDefaults.ResponseTokenLimit,
            Temperature = SystemPromptDefaults.IntentTemperature,
            TopP = SystemPromptDefaults.IntentTopP,
            FrequencyPenalty = SystemPromptDefaults.IntentFrequencyPenalty,
            PresencePenalty = SystemPromptDefaults.IntentPresencePenalty,
            StopSequences = new string[] { "] bot:" }
        };

        return completionSettings;
    }

    /// <summary>
    /// Estimate the number of tokens in a string.
    /// TODO: This is a very naive implementation. We should use the new implementation that is available in the kernel.
    /// </summary>
    private int EstimateTokenCount(string text)
    {
        return (int)Math.Floor(text.Length / SystemPromptDefaults.TokenEstimateFactor);
    }
}
