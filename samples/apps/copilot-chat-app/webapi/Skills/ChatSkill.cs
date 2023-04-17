﻿// Copyright (c) Microsoft. All rights reserved.

using System.Globalization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using SemanticKernel.Service.Storage;

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

    private readonly ChatMessageRepository _chatMessageRepository;

    private readonly ChatSessionRepository _chatSessionRepository;

    public ChatSkill(IKernel kernel, ChatMessageRepository chatMessageRepository, ChatSessionRepository chatSessionRepository)
    {
        this._kernel = kernel;
        this._chatMessageRepository = chatMessageRepository;
        this._chatSessionRepository = chatSessionRepository;
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
        var intentExtractionContext = Utils.CopyContextWithVariablesClone(context);
        intentExtractionContext.Variables.Set("tokenLimit", historyTokenBudget.ToString(new NumberFormatInfo()));
        intentExtractionContext.Variables.Set("knowledgeCutoff", SystemPromptDefaults.KnowledgeCutoffDate);

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
        var chatId = context["chatId"];
        var tokenLimit = int.Parse(context["tokenLimit"], new NumberFormatInfo());
        var contextTokenLimit = int.Parse(context["contextTokenLimit"], new NumberFormatInfo());
        var remainingToken = Math.Min(
            tokenLimit,
            Math.Floor(contextTokenLimit * SystemPromptDefaults.MemoriesResponseContextWeight)
        );

        // Find the most recent message.
        var latestMessage = await this._chatMessageRepository.FindLastByChatIdAsync(chatId);

        string memoryText = "";
        var results = context.Memory.SearchAsync(
            SemanticMemoryExtractor.MemoryCollectionName(chatId, SystemPromptDefaults.LongTermMemoryName),
            latestMessage.ToString(),
            limit: 1000,
            minRelevanceScore: 0.8);
        await foreach (var memory in results)
        {
            var estimatedTokenCount = this.EstimateTokenCount(memory.Metadata.Text);
            if (remainingToken - estimatedTokenCount > 0)
            {
                memoryText += $"\n[{memory.Metadata.Description}] {memory.Metadata.Text}";
                remainingToken -= estimatedTokenCount;
            }
            else
            {
                break;
            }
        }

        return $"Past memories (format: [memory type] <label>: <details>):\n{memoryText.Trim()}";
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
        var chatId = context["chatId"];
        var tokenLimit = int.Parse(context["tokenLimit"], new NumberFormatInfo());

        // TODO: Use contextTokenLimit to determine how much of the chat history to return
        // TODO: relevant history

        var messages = await this._chatMessageRepository.FindByChatIdAsync(chatId);
        var sortedMessages = messages.OrderByDescending(m => m.Timestamp);

        var remainingToken = tokenLimit;
        string historyText = "";
        foreach (var chatMessage in sortedMessages)
        {
            var formattedMessage = chatMessage.ToFormattedString();
            var estimatedTokenCount = this.EstimateTokenCount(formattedMessage);
            if (remainingToken - estimatedTokenCount > 0)
            {
                historyText = $"{formattedMessage}\n{historyText}";
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
    [SKFunctionContextParameter(Name = "userName", Description = "Name of the user")]
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
        var userId = context["userId"];
        var userName = context["userName"];
        var chatId = context["chatId"];

        // TODO: check if user has access to the chat

        // Save this new message to memory such that subsequent chat responses can use it
        try
        {
            await this.SaveNewMessageAsync(message, userId, userName, chatId);
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            context.Log.LogError("Unable to save new message: {0}", ex.Message);
            context.Fail($"Unable to save new message: {ex.Message}", ex);
            return context;
        }

        // Clone the context to avoid modifying the original context variables.
        var chatContext = Utils.CopyContextWithVariablesClone(context);
        chatContext.Variables.Set("tokenLimit", remainingToken.ToString(new NumberFormatInfo()));
        chatContext.Variables.Set("contextTokenLimit", contextTokenLimit.ToString(new NumberFormatInfo()));
        chatContext.Variables.Set("knowledgeCutoff", SystemPromptDefaults.KnowledgeCutoffDate);
        chatContext.Variables.Set("audience", userName);

        // Extract user intent and update remaining token count
        var userIntent = await this.ExtractUserIntentAsync(chatContext);
        chatContext.Variables.Set("userIntent", userIntent);
        remainingToken -= this.EstimateTokenCount(userIntent);

        var completionFunction = this._kernel.CreateSemanticFunction(
            SystemPromptDefaults.SystemChatPrompt,
            skillName: nameof(ChatSkill),
            description: "Complete the prompt.");

        chatContext = await completionFunction.InvokeAsync(
            context: chatContext,
            settings: this.CreateChatResponseCompletionSettings()
        );

        // Save this response to memory such that subsequent chat responses can use it
        try
        {
            await this.SaveNewResponseAsync(chatContext.Result, chatId);
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            context.Log.LogError("Unable to save new response: {0}", ex.Message);
            context.Fail($"Unable to save new response: {ex.Message}", ex);
            return context;
        }

        // Extract semantic memory
        await this.ExtractSemanticMemoryAsync(chatId, chatContext);

        context.Variables.Update(chatContext.Result);
        context.Variables.Set("userId", "Bot");
        return context;
    }

    #region Private

    /// <summary>
    /// Save a new message to the chat history.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="userId">The user ID</param>
    /// <param name="userName"></param>
    /// <param name="chatId">The chat ID</param>
    private async Task SaveNewMessageAsync(string message, string userId, string userName, string chatId)
    {
        // Make sure the chat exists.
        await this._chatSessionRepository.FindByIdAsync(chatId);

        var chatMessage = new ChatMessage(userId, userName, chatId, message);
        await this._chatMessageRepository.CreateAsync(chatMessage);
    }

    /// <summary>
    /// Save a new response to the chat history.
    /// </summary>
    /// <param name="response"></param>
    /// <param name="chatId">The chat ID</param>
    private async Task SaveNewResponseAsync(string response, string chatId)
    {
        // Make sure the chat exists.
        await this._chatSessionRepository.FindByIdAsync(chatId);

        var chatMessage = ChatMessage.CreateBotResponseMessage(chatId, response);
        await this._chatMessageRepository.CreateAsync(chatMessage);
    }

    /// <summary>
    /// Save a new response to the chat history.
    /// This method will only modify the memory and the log of the context.
    /// </summary>
    /// <param name="chatId"></param>
    /// <param name="context"></param>
    private async Task ExtractSemanticMemoryAsync(string chatId, SKContext context)
    {
        foreach (var memoryName in SystemPromptDefaults.MemoryMap.Keys)
        {
            try
            {
                var semanticMemory = await SemanticMemoryExtractor.ExtractCognitiveMemoryAsync(
                    memoryName,
                    this._kernel,
                    context
                );
                foreach (var item in semanticMemory.Items)
                {
                    await this.CreateMemoryAsync(item, chatId, context, memoryName);
                }
            }
            catch (Exception ex) when (!ex.IsCriticalException())
            {
                // Skip semantic memory extraction for this item if it fails.
                // We cannot rely on the model to response with perfect Json each time.
                context.Log.LogInformation("Unable to extract semantic memory for {0}: {1}. Continuing...", memoryName, ex.Message);
                continue;
            }
        }
    }

    /// <summary>
    /// Create a memory item in the memory collection.
    /// </summary>
    /// <param name="item">A SemanticChatMemoryItem instance</param>
    /// <param name="chatId">The ID of the chat the memories belong to</param>
    /// <param name="context">The context that contains the memory</param>
    /// <param name="memoryName">Name of the memory</param>
    /// <returns></returns>
    private async Task CreateMemoryAsync(SemanticChatMemoryItem item, string chatId, SKContext context, string memoryName)
    {
        var memoryCollectionName = SemanticMemoryExtractor.MemoryCollectionName(chatId, memoryName);

        var memories = context.Memory.SearchAsync(
            collection: memoryCollectionName,
            query: item.ToFormattedString(),
            limit: 1,
            minRelevanceScore: 0.8,
            cancel: context.CancellationToken
        ).ToEnumerable();

        if (!memories.Any())
        {
            await context.Memory.SaveInformationAsync(
                collection: memoryCollectionName,
                text: item.ToFormattedString(),
                id: Guid.NewGuid().ToString(),
                description: memoryName,
                cancel: context.CancellationToken
            );
        }
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

    # endregion
}
