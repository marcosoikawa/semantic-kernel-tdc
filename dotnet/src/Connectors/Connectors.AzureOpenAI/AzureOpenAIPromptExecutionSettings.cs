﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Text;
using OpenAI.Chat;

namespace Microsoft.SemanticKernel.Connectors.AzureOpenAI;

/// <summary>
/// Execution settings for an AzureOpenAI completion request.
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed class AzureOpenAIPromptExecutionSettings : PromptExecutionSettings
{
    /// <summary>
    /// Temperature controls the randomness of the completion.
    /// The higher the temperature, the more random the completion.
    /// Default is 1.0.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double Temperature
    {
        get => this._temperature;

        set
        {
            this.ThrowIfFrozen();
            this._temperature = value;
        }
    }

    /// <summary>
    /// TopP controls the diversity of the completion.
    /// The higher the TopP, the more diverse the completion.
    /// Default is 1.0.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double TopP
    {
        get => this._topP;

        set
        {
            this.ThrowIfFrozen();
            this._topP = value;
        }
    }

    /// <summary>
    /// Number between -2.0 and 2.0. Positive values penalize new tokens
    /// based on whether they appear in the text so far, increasing the
    /// model's likelihood to talk about new topics.
    /// </summary>
    [JsonPropertyName("presence_penalty")]
    public double PresencePenalty
    {
        get => this._presencePenalty;

        set
        {
            this.ThrowIfFrozen();
            this._presencePenalty = value;
        }
    }

    /// <summary>
    /// Number between -2.0 and 2.0. Positive values penalize new tokens
    /// based on their existing frequency in the text so far, decreasing
    /// the model's likelihood to repeat the same line verbatim.
    /// </summary>
    [JsonPropertyName("frequency_penalty")]
    public double FrequencyPenalty
    {
        get => this._frequencyPenalty;

        set
        {
            this.ThrowIfFrozen();
            this._frequencyPenalty = value;
        }
    }

    /// <summary>
    /// The maximum number of tokens to generate in the completion.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens
    {
        get => this._maxTokens;

        set
        {
            this.ThrowIfFrozen();
            this._maxTokens = value;
        }
    }

    /// <summary>
    /// Sequences where the completion will stop generating further tokens.
    /// </summary>
    [JsonPropertyName("stop_sequences")]
    public IList<string>? StopSequences
    {
        get => this._stopSequences;

        set
        {
            this.ThrowIfFrozen();
            this._stopSequences = value;
        }
    }

    /// <summary>
    /// If specified, the system will make a best effort to sample deterministically such that repeated requests with the
    /// same seed and parameters should return the same result. Determinism is not guaranteed.
    /// </summary>
    [JsonPropertyName("seed")]
    public long? Seed
    {
        get => this._seed;

        set
        {
            this.ThrowIfFrozen();
            this._seed = value;
        }
    }

    /// <summary>
    /// Gets or sets the response format to use for the completion.
    /// </summary>
    /// <remarks>
    /// Possible values are: "json_object", "text", <see cref="ChatResponseFormat"/> object.
    /// </remarks>
    [Experimental("SKEXP0010")]
    [JsonPropertyName("response_format")]
    public object? ResponseFormat
    {
        get => this._responseFormat;

        set
        {
            this.ThrowIfFrozen();
            this._responseFormat = value;
        }
    }

    /// <summary>
    /// The system prompt to use when generating text using a chat model.
    /// Defaults to "Assistant is a large language model."
    /// </summary>
    [JsonPropertyName("chat_system_prompt")]
    public string? ChatSystemPrompt
    {
        get => this._chatSystemPrompt;

        set
        {
            this.ThrowIfFrozen();
            this._chatSystemPrompt = value;
        }
    }

    /// <summary>
    /// Modify the likelihood of specified tokens appearing in the completion.
    /// </summary>
    [JsonPropertyName("token_selection_biases")]
    public IDictionary<int, int>? TokenSelectionBiases
    {
        get => this._tokenSelectionBiases;

        set
        {
            this.ThrowIfFrozen();
            this._tokenSelectionBiases = value;
        }
    }

    /// <summary>
    /// Gets or sets the behavior for how tool calls are handled.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>To disable all tool calling, set the property to null (the default).</item>
    /// <item>
    /// To request that the model use a specific function, set the property to an instance returned
    /// from <see cref="AzureToolCallBehavior.RequireFunction"/>.
    /// </item>
    /// <item>
    /// To allow the model to request one of any number of functions, set the property to an
    /// instance returned from <see cref="AzureToolCallBehavior.EnableFunctions"/>, called with
    /// a list of the functions available.
    /// </item>
    /// <item>
    /// To allow the model to request one of any of the functions in the supplied <see cref="Kernel"/>,
    /// set the property to <see cref="AzureToolCallBehavior.EnableKernelFunctions"/> if the client should simply
    /// send the information about the functions and not handle the response in any special manner, or
    /// <see cref="AzureToolCallBehavior.AutoInvokeKernelFunctions"/> if the client should attempt to automatically
    /// invoke the function and send the result back to the service.
    /// </item>
    /// </list>
    /// For all options where an instance is provided, auto-invoke behavior may be selected. If the service
    /// sends a request for a function call, if auto-invoke has been requested, the client will attempt to
    /// resolve that function from the functions available in the <see cref="Kernel"/>, and if found, rather
    /// than returning the response back to the caller, it will handle the request automatically, invoking
    /// the function, and sending back the result. The intermediate messages will be retained in the
    /// <see cref="ChatHistory"/> if an instance was provided.
    /// </remarks>
    public AzureToolCallBehavior? ToolCallBehavior
    {
        get => this._toolCallBehavior;

        set
        {
            this.ThrowIfFrozen();
            this._toolCallBehavior = value;
        }
    }

    /// <summary>
    /// A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse
    /// </summary>
    public string? User
    {
        get => this._user;

        set
        {
            this.ThrowIfFrozen();
            this._user = value;
        }
    }

    /// <summary>
    /// Whether to return log probabilities of the output tokens or not.
    /// If true, returns the log probabilities of each output token returned in the `content` of `message`.
    /// </summary>
    [Experimental("SKEXP0010")]
    [JsonPropertyName("logprobs")]
    public bool? Logprobs
    {
        get => this._logprobs;

        set
        {
            this.ThrowIfFrozen();
            this._logprobs = value;
        }
    }

    /// <summary>
    /// An integer specifying the number of most likely tokens to return at each token position, each with an associated log probability.
    /// </summary>
    [Experimental("SKEXP0010")]
    [JsonPropertyName("top_logprobs")]
    public int? TopLogprobs
    {
        get => this._topLogprobs;

        set
        {
            this.ThrowIfFrozen();
            this._topLogprobs = value;
        }
    }

    /// <summary>
    /// An abstraction of additional settings for chat completion, see https://learn.microsoft.com/en-us/dotnet/api/azure.ai.openai.azurechatextensionsoptions.
    /// This property is compatible only with Azure OpenAI.
    /// </summary>
    [Experimental("SKEXP0010")]
    [JsonIgnore]
    public AzureChatDataSource? AzureChatDataSource
    {
        get => this._azureChatDataSource;

        set
        {
            this.ThrowIfFrozen();
            this._azureChatDataSource = value;
        }
    }

    /// <inheritdoc/>
    public override void Freeze()
    {
        if (this.IsFrozen)
        {
            return;
        }

        base.Freeze();

        if (this._stopSequences is not null)
        {
            this._stopSequences = new ReadOnlyCollection<string>(this._stopSequences);
        }

        if (this._tokenSelectionBiases is not null)
        {
            this._tokenSelectionBiases = new ReadOnlyDictionary<int, int>(this._tokenSelectionBiases);
        }
    }

    /// <inheritdoc/>
    public override PromptExecutionSettings Clone()
    {
        return new AzureOpenAIPromptExecutionSettings()
        {
            ModelId = this.ModelId,
            ExtensionData = this.ExtensionData is not null ? new Dictionary<string, object>(this.ExtensionData) : null,
            Temperature = this.Temperature,
            TopP = this.TopP,
            PresencePenalty = this.PresencePenalty,
            FrequencyPenalty = this.FrequencyPenalty,
            MaxTokens = this.MaxTokens,
            StopSequences = this.StopSequences is not null ? new List<string>(this.StopSequences) : null,
            Seed = this.Seed,
            ResponseFormat = this.ResponseFormat,
            TokenSelectionBiases = this.TokenSelectionBiases is not null ? new Dictionary<int, int>(this.TokenSelectionBiases) : null,
            ToolCallBehavior = this.ToolCallBehavior,
            User = this.User,
            ChatSystemPrompt = this.ChatSystemPrompt,
            Logprobs = this.Logprobs,
            TopLogprobs = this.TopLogprobs,
            AzureChatDataSource = this.AzureChatDataSource,
        };
    }

    /// <summary>
    /// Default max tokens for a text generation
    /// </summary>
    internal static int DefaultTextMaxTokens { get; } = 256;

    /// <summary>
    /// Create a new settings object with the values from another settings object.
    /// </summary>
    /// <param name="executionSettings">Template configuration</param>
    /// <param name="defaultMaxTokens">Default max tokens</param>
    /// <returns>An instance of OpenAIPromptExecutionSettings</returns>
    public static AzureOpenAIPromptExecutionSettings FromExecutionSettings(PromptExecutionSettings? executionSettings, int? defaultMaxTokens = null)
    {
        if (executionSettings is null)
        {
            return new AzureOpenAIPromptExecutionSettings()
            {
                MaxTokens = defaultMaxTokens
            };
        }

        if (executionSettings is AzureOpenAIPromptExecutionSettings settings)
        {
            return settings;
        }

        var json = JsonSerializer.Serialize(executionSettings);

        var openAIExecutionSettings = JsonSerializer.Deserialize<AzureOpenAIPromptExecutionSettings>(json, JsonOptionsCache.ReadPermissive);
        if (openAIExecutionSettings is not null)
        {
            return openAIExecutionSettings;
        }

        throw new ArgumentException($"Invalid execution settings, cannot convert to {nameof(AzureOpenAIPromptExecutionSettings)}", nameof(executionSettings));
    }

    /// <summary>
    /// Create a new settings object with the values from another settings object.
    /// </summary>
    /// <param name="executionSettings">Template configuration</param>
    /// <param name="defaultMaxTokens">Default max tokens</param>
    /// <returns>An instance of OpenAIPromptExecutionSettings</returns>
    [Obsolete("This method is deprecated in favor of OpenAIPromptExecutionSettings.AzureChatExtensionsOptions")]
    public static AzureOpenAIPromptExecutionSettings FromExecutionSettingsWithData(PromptExecutionSettings? executionSettings, int? defaultMaxTokens = null)
    {
        var settings = FromExecutionSettings(executionSettings, defaultMaxTokens);

        if (settings.StopSequences?.Count == 0)
        {
            // Azure OpenAI WithData API does not allow to send empty array of stop sequences
            // Gives back "Validation error at #/stop/str: Input should be a valid string\nValidation error at #/stop/list[str]: List should have at least 1 item after validation, not 0"
            settings.StopSequences = null;
        }

        return settings;
    }

    #region private ================================================================================

    private double _temperature = 1;
    private double _topP = 1;
    private double _presencePenalty;
    private double _frequencyPenalty;
    private int? _maxTokens;
    private IList<string>? _stopSequences;
    private long? _seed;
    private object? _responseFormat;
    private IDictionary<int, int>? _tokenSelectionBiases;
    private AzureToolCallBehavior? _toolCallBehavior;
    private string? _user;
    private string? _chatSystemPrompt;
    private bool? _logprobs;
    private int? _topLogprobs;
    private AzureChatDataSource? _azureChatDataSource;

    #endregion
}
