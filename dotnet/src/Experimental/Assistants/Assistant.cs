﻿//// Copyright (c) Microsoft. All rights reserved.

//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Microsoft.SemanticKernel.Experimental.Assistants.Extensions;
//using Microsoft.SemanticKernel.Experimental.Assistants.Models;

//namespace Microsoft.SemanticKernel.Experimental.Assistants;

///// <summary>
///// Assistant - Customizable entity that can be configured to respond to users’ messages
///// </summary>
//public sealed class Assistant
//{
//    private AssistantModel _assistantModel;
//    private readonly IOpenAIRestContext _openAiRestContext;

//    /// <summary>
//    /// Properties of this Assistant
//    /// </summary>
//    public AssistantModel Properties => this._assistantModel;

//    /// <summary>
//    /// Name for this Assistant
//    /// </summary>
//    public string Name => this._assistantModel.Name;

//    /// <summary>
//    /// List of Semantic Kernel functions that can be invoked from this
//    /// Assistant if it is used as a plugin.
//    /// </summary>
//    public IEnumerable<ISKFunction> Functions
//    {
//        /*this._functions = new List<ISKFunction>
//        {
//            NativeFunction.Create(
//                this.AskAsync,
//                null,
//                null,
//                this.Name,
//                "Ask",
//                this.Description,
//                new List<ParameterView>
//                {
//                    new ParameterView("ask", "The question to ask the assistant"),
//                },
//                null
//            )
//        };*/
//        get { return new List<ISKFunction>(); }
//    }

//    /// <summary>
//    /// Creates a new Assistant
//    /// </summary>
//    /// <param name="openAiRestContext">Context to make calls to OpenAI</param>
//    /// <param name="properties">Properties of instance to create.</param>
//    /// <returns>Created Assistant instance, or null on failure</returns>
//    public static async Task<Assistant?> CreateAsync(IOpenAIRestContext openAiRestContext, AssistantModel properties)
//    {
//        var response = await openAiRestContext.CreateAssistantAsync(properties).ConfigureAwait(false);
//        if (response is null)
//        {
//            return null;
//        }

//        return new Assistant(openAiRestContext, response);
//    }

//    /// <summary>
//    /// Retrieve an existing Assistant from OpenAI
//    /// </summary>
//    /// <param name="openAiRestContext">Context to make calls to OpenAI</param>
//    /// <param name="id">Identifier of Assistant to retrieve</param>
//    /// <returns>Retrieved Assistant, or null if it isn't found</returns>
//    public static async Task<Assistant?> RetrieveAsync(IOpenAIRestContext openAiRestContext, string id)
//    {
//        var response = await openAiRestContext.GetAssistantAsync(id).ConfigureAwait(false);
//        if (response is null)
//        {
//            return null;
//        }

//        return new Assistant(openAiRestContext, response);
//    }

//    /// <summary>
//    /// List existing Assistant instances from OpenAI
//    /// </summary>
//    /// <param name="openAiRestContext">Context to make calls to OpenAI</param>
//    /// <param name="limit">A limit on the number of objects to be returned.
//    /// Limit can range between 1 and 100, and the default is 20.</param>
//    /// <param name="ascending">Set to true to sort by ascending created_at timestamp
//    /// instead of descending.</param>
//    /// <param name="after">A cursor for use in pagination. This is an object ID that defines
//    /// your place in the list. For instance, if you make a list request and receive 100 objects,
//    /// ending with obj_foo, your subsequent call can include after=obj_foo in order to
//    /// fetch the next page of the list.</param>
//    /// <param name="before">A cursor for use in pagination. This is an object ID that defines
//    /// your place in the list. For instance, if you make a list request and receive 100 objects,
//    /// ending with obj_foo, your subsequent call can include before=obj_foo in order to
//    /// fetch the previous page of the list.</param>
//    /// <returns>List of retrieved Assistants</returns>
//    public static Task<List<AssistantModel>> ListAsync(
//        IOpenAIRestContext openAiRestContext,
//        int limit = 20,
//        bool ascending = false,
//        string? after = null,
//        string? before = null)
//    {
//        return openAiRestContext.GetAssistantsAsync(limit, ascending, after, before);
//    }

//    /// <summary>
//    /// Modify an existing Assistant
//    /// </summary>
//    /// <param name="properties">New properties for our instance</param>
//    public async Task ModifyAsync(AssistantModel properties)
//    {
//        var requestDataFromGivenProperties = new { };
//        /*using var response = await this._openAiRestContext.MakePretendAssistantRestCallAsync(requestDataFromGivenProperties).ConfigureAwait(false);
//        response.EnsureSuccessStatusCode();

//        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
//        AssistantModel? assistantModel = JsonSerializer.Deserialize<AssistantModel>(responseBody) ?? throw new JsonException();
//        this._assistantModel = assistantModel;*/
//    }

//    /// <summary>
//    /// Delete an existing Assistant
//    /// </summary>
//    /// <param name="id">Identifier of Assistant to retrieve</param>
//    public async Task DeleteAsync(string id)
//    {
//        var requestDataFromGivenProperties = new { };
//        //using var response = await this._openAiRestContext.MakePretendAssistantRestCallAsync(requestDataFromGivenProperties).ConfigureAwait(false);
//        //response.EnsureSuccessStatusCode();

//        // Clear our properties so further operations are not possible on this instance
//        this._assistantModel = new AssistantModel();
//    }

//    /// <summary>
//    /// Private constructor
//    /// </summary>
//    private Assistant(IOpenAIRestContext openAiRestContext, AssistantModel properties)
//    {
//        this._assistantModel = properties;
//        this._openAiRestContext = openAiRestContext;
//    }
//}
