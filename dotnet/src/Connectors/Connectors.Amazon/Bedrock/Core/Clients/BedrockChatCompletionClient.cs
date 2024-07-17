// Copyright (c) Microsoft. All rights reserved.
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Connectors.Amazon.Core.Requests;
using Connectors.Amazon.Core.Responses;
using Connectors.Amazon.Models;
using Connectors.Amazon.Models.AI21;
using Connectors.Amazon.Models.Amazon;
using Connectors.Amazon.Models.Anthropic;
using Connectors.Amazon.Models.Cohere;
using Connectors.Amazon.Models.Meta;
using Connectors.Amazon.Models.Mistral;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Diagnostics;

namespace Microsoft.SemanticKernel.Connectors.Amazon.Core;
/// <summary>
/// Represents a client for interacting with the chat completion through Bedrock.
/// </summary>
/// <typeparam name="TRequest"> Request object which is an IChatCompletionRequest. </typeparam>
/// <typeparam name="TResponse"> Response object which is an IChatCompletionResponse. </typeparam>
public class BedrockChatCompletionClient<TRequest, TResponse>
    where TRequest : IChatCompletionRequest
    where TResponse : IChatCompletionResponse
{
    private readonly string _modelId;
    private readonly string _modelProvider;
    private readonly IAmazonBedrockRuntime _bedrockApi;
    private readonly IBedrockModelIOService<IChatCompletionRequest, IChatCompletionResponse> _ioService;
    private readonly Uri _chatGenerationEndpoint;

    /// <summary>
    /// Builds the client object and registers the model input-output service given the user's passed in model ID.
    /// </summary>
    /// <param name="modelId"></param>
    /// <param name="bedrockApi"></param>
    /// <exception cref="ArgumentException"></exception>
    public BedrockChatCompletionClient(string modelId, IAmazonBedrockRuntime bedrockApi)
    {
        this._modelId = modelId;
        this._bedrockApi = bedrockApi;
        this._chatGenerationEndpoint = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com");
        int periodIndex = modelId.IndexOf('.'); //modelId looks like "amazon.titan-embed-text-v1:0"
        string modelProvider = periodIndex >= 0 ? modelId.Substring(0, periodIndex) : modelId;
        this._modelProvider = modelProvider;
        switch (modelProvider)
        {
            case "ai21":
                if (modelId.Contains("jamba"))
                {
                    this._ioService = new AI21JambaIOService();
                    break;
                }
                if (modelId.Contains("j2-"))
                {
                    this._ioService = new AI21JurassicIOService();
                    break;
                }
                throw new ArgumentException($"Unsupported AI21 model: {modelId}");
            case "amazon":
                this._ioService = new AmazonIOService();
                break;
            case "anthropic":
                this._ioService = new AnthropicIOService();
                break;
            case "cohere":
                if (modelId.Contains("command-r"))
                {
                    this._ioService = new CohereCommandRIOService();
                    break;
                }
                throw new ArgumentException($"Unsupported Cohere model: {modelId}");
            case "meta":
                this._ioService = new MetaIOService();
                break;
            case "mistral":
                this._ioService = new MistralIOService();
                break;
            default:
                throw new ArgumentException($"Unsupported model provider: {modelProvider}");
        }
    }
    /// <summary>
    /// Builds the convert request body given the model ID (as stored in ioService object) and calls the ConverseAsync Bedrock Runtime action to get the result.
    /// </summary>
    /// <param name="chatHistory"> The chat history containing the conversation data. </param>
    /// <param name="executionSettings"> Optional settings for prompt execution. </param>
    /// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
    /// <returns></returns>
    private async Task<ConverseResponse> ConverseBedrockModelAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, CancellationToken cancellationToken = default)
    {
        var converseRequest = this._ioService.GetConverseRequest(this._modelId, chatHistory, executionSettings);
        return await this._bedrockApi.ConverseAsync(converseRequest, cancellationToken).ConfigureAwait(true);
    }
    internal async Task<IReadOnlyList<ChatMessageContent>> GenerateChatMessageAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrEmpty(chatHistory);
        ConverseResponse response;
        using (var activity = ModelDiagnostics.StartCompletionActivity(
                   this._chatGenerationEndpoint, this._modelId, this._modelProvider, chatHistory, executionSettings))
        {
            try
            {
                response = await this.ConverseBedrockModelAsync(chatHistory, executionSettings ?? new PromptExecutionSettings(), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (activity is not null)
            {
                activity.SetError(ex);
                throw;
            }
            IEnumerable<ChatMessageContent> chat = ConvertToMessageContent(response);
            IReadOnlyList<ChatMessageContent> chatMessagesList = chat.ToList();
            // Per other sample Connector demos, we are letting the user add the response to the ChatHistory. Otherwise, it could be done as below:
            // foreach (var message in chat)
            // {
            //     chatHistory.AddMessage(AuthorRole.Assistant, message.Content);
            // }
            activity?.SetCompletionResponse(chatMessagesList);
            return chatMessagesList;
        }
    }
    /// <summary>
    /// Converts the ConverseResponse object as outputted by the Bedrock Runtime API call to a ChatMessageContent for the Semantic Kernel.
    /// </summary>
    /// <param name="response"> ConverseResponse object outputted by Bedrock. </param>
    /// <returns></returns>
    private static IEnumerable<ChatMessageContent> ConvertToMessageContent(ConverseResponse response)
    {
        if (response.Output.Message != null)
        {
            var message = response.Output.Message;
            return new[]
            {
                new ChatMessageContent
                {
                    Role = MapRole(message.Role.Value),
                    Items = CreateChatMessageContentItemCollection(message.Content)
                }
            };
        }
        return Enumerable.Empty<ChatMessageContent>();
    }
    private static AuthorRole MapRole(string role)
    {
        return role switch
        {
            "user" => AuthorRole.User,
            "assistant" => AuthorRole.Assistant,
            "system" => AuthorRole.System,
            _ => throw new ArgumentOutOfRangeException(nameof(role), $"Invalid role: {role}")
        };
    }
    private static ChatMessageContentItemCollection CreateChatMessageContentItemCollection(List<ContentBlock> contentBlocks)
    {
        var itemCollection = new ChatMessageContentItemCollection();
        foreach (var contentBlock in contentBlocks)
        {
            itemCollection.Add(new TextContent(contentBlock.Text));
        }
        return itemCollection;
    }

    // Order of operations:
    // 1. Start completion activity with semantic kernel
    // 2. Call converse stream async with bedrock API
    // 3. Convert output to semantic kernel's StreamingChatMessageContent
    // 4. Yield return the streamed contents
    // 5. End streaming activity with kernel
    internal async IAsyncEnumerable<StreamingChatMessageContent> StreamChatMessageAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        ConverseStreamResponse response;
        using var activity = ModelDiagnostics.StartCompletionActivity(
            this._chatGenerationEndpoint, this._modelId, this._modelProvider, chatHistory, executionSettings);
        try
        {
            try
            {
                response = await this.StreamConverseBedrockModelAsync(chatHistory, executionSettings ?? new PromptExecutionSettings(), cancellationToken).ConfigureAwait(false);
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine($"ERROR: Can't invoke '{this._modelId}'. Reason: {e.Message}");
                throw;
            }
        }
        catch (Exception ex) when (activity is not null)
        {
            activity.SetError(ex);
            throw;
        }

        List<StreamingChatMessageContent>? streamedContents = activity is not null ? [] : null;
        foreach (var chunk in response.Stream.AsEnumerable())
        {
            if (chunk is ContentBlockDeltaEvent)
            {
                var c = (chunk as ContentBlockDeltaEvent)?.Delta.Text;
                var content = new StreamingChatMessageContent(AuthorRole.Assistant, c);
                streamedContents?.Add(content);
                yield return content;
            }
        }
        activity?.EndStreaming(streamedContents);
    }
    private async Task<ConverseStreamResponse> StreamConverseBedrockModelAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings executionSettings,
        CancellationToken cancellationToken)
    {
        var converseRequest = this._ioService.GetConverseStreamRequest(this._modelId, chatHistory, executionSettings);
        return await this._bedrockApi.ConverseStreamAsync(converseRequest, cancellationToken).ConfigureAwait(true);
    }
}
