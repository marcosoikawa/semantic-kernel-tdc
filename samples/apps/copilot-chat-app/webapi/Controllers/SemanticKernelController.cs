﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Orchestration;
using SemanticKernel.Service.Model;
using SemanticKernel.Service.Skills;
using SemanticKernel.Service.Storage;

namespace SemanticKernel.Service.Controllers;

[ApiController]
public class SemanticKernelController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SemanticKernelController> _logger;
    private readonly PromptSettings _promptSettings;

    public SemanticKernelController(IServiceProvider serviceProvider, IConfiguration configuration, PromptSettings promptSettings, ILogger<SemanticKernelController> logger)
    {
        this._serviceProvider = serviceProvider;
        this._configuration = configuration;
        this._promptSettings = promptSettings;
        this._logger = logger;
    }

    /// <summary>
    /// Invoke a Semantic Kernel function on the server.
    /// </summary>
    /// <remarks>
    /// We create and use a new kernel for each request.
    /// We feed the kernel the ask received via POST from the client
    /// and attempt to invoke the function with the given name.
    /// </remarks>
    /// <param name="kernel">Semantic kernel obtained through dependency injection</param>
    /// <param name="chatRepository">Storage repository to store chat sessions</param>
    /// <param name="chatMessageRepository">Storage repository to store chat messages</param>
    /// <param name="ask">Prompt along with its parameters</param>
    /// <param name="skillName">Skill in which function to invoke resides</param>
    /// <param name="functionName">Name of function to invoke</param>
    /// <returns>Results consisting of text generated by invoked function along with the variable in the SK that generated it</returns>
    [Authorize]
    [Route("skills/{skillName}/functions/{functionName}/invoke")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AskResult>> InvokeFunctionAsync(
        [FromServices] Kernel kernel,
        [FromServices] ChatSessionRepository chatRepository,
        [FromServices] ChatMessageRepository chatMessageRepository,
        [FromBody] Ask ask,
        string skillName, string functionName)
    {
        if (this._logger.IsEnabled(LogLevel.Debug))
        {
            this._logger.LogDebug("Received call to invoke {SkillName}/{FunctionName}", skillName, functionName);
        }

        if (string.IsNullOrWhiteSpace(ask.Input))
        {
            return this.BadRequest("Input is required.");
        }

        string semanticSkillsDirectory = this._configuration.GetSection(Constants.SemanticSkillsDirectoryConfigKey).Get<string>();
        if (!string.IsNullOrWhiteSpace(semanticSkillsDirectory))
        {
            kernel.RegisterSemanticSkills(semanticSkillsDirectory, this._logger);
        }

        kernel.RegisterNativeSkills(chatRepository, chatMessageRepository, this._promptSettings, this._logger);

        ISKFunction? function = null;
        try
        {
            function = kernel.Skills.GetFunction(skillName, functionName);
        }
        catch (KernelException)
        {
            return this.NotFound($"Failed to find {skillName}/{functionName} on server");
        }

        // Put ask's variables in the context we will use
        var contextVariables = new ContextVariables(ask.Input);
        foreach (var input in ask.Variables)
        {
            contextVariables.Set(input.Key, input.Value);
        }

        // Run function
        SKContext result = await kernel.RunAsync(contextVariables, function!);
        if (result.ErrorOccurred)
        {
            if (result.LastException is AIException aiException && aiException.Detail is not null)
            {
                return this.BadRequest(string.Concat(aiException.Message, " - Detail: " + aiException.Detail));
            }

            return this.BadRequest(result.LastErrorDescription);
        }

        return this.Ok(new AskResult { Value = result.Result, Variables = result.Variables.Select(v => new KeyValuePair<string, string>(v.Key, v.Value)) });
    }
}
