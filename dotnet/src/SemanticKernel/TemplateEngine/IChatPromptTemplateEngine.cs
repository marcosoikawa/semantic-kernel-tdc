﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.TemplateEngine.Blocks;

namespace Microsoft.SemanticKernel.TemplateEngine;

/// <summary>
/// Prompt template engine interface.
/// </summary>
public interface IChatPromptTemplateEngine
{
    /// <summary>
    /// Given a prompt template string, extract all the blocks (text, variables, function calls)
    /// </summary>
    /// <param name="templateText">Prompt template (see skprompt.txt files)</param>
    /// <param name="validate">Whether to validate the blocks syntax, or just return the blocks found, which could contain invalid code</param>
    /// <returns>A list of all the blocks, ie the template tokenized in text, variables and function calls</returns>
    IList<Block> ExtractBlocks(
        string? templateText,
        bool validate = true);

    /// <summary>
    /// Given a prompt template, replace the variables with their values and execute the functions replacing their
    /// reference with the function result.
    /// </summary>
    /// <param name="templateText">Prompt template (see skprompt.txt files)</param>
    /// <param name="context">Access into the current kernel execution context</param>
    /// <returns>The prompt template ready to be used for an AI request</returns>
    Task<string> RenderAsync(
        string templateText,
        SKContext context);

    /// <summary>
    /// Given a list of blocks render each block and compose the final result
    /// </summary>
    /// <param name="blocks">Template blocks generated by ExtractBlocks</param>
    /// <param name="context">Access into the current kernel execution context</param>
    /// <returns>The prompt template ready to be used for an AI request</returns>
    Task<string> RenderAsync(
        IList<Block> blocks,
        SKContext context);

    /// <summary>
    /// Given a list of blocks, render the Variable Blocks, replacing placeholders with the actual value in memory
    /// </summary>
    /// <param name="blocks">List of blocks, typically all the blocks found in a template</param>
    /// <param name="variables">Container of all the temporary variables known to the kernel</param>
    /// <returns>An updated list of blocks where Variable Blocks have rendered to Text Blocks</returns>
    IList<Block> RenderVariables(
        IList<Block> blocks,
        ContextVariables? variables);

    /// <summary>
    /// Given a list of blocks, render the Code Blocks, executing the functions and replacing placeholders with the functions result
    /// </summary>
    /// <param name="blocks">List of blocks, typically all the blocks found in a template</param>
    /// <param name="executionContext">Access into the current kernel execution context</param>
    /// <returns>An updated list of blocks where Code Blocks have rendered to Text Blocks</returns>
    Task<IList<Block>> RenderCodeAsync(
        IList<Block> blocks,
        SKContext executionContext);
}
