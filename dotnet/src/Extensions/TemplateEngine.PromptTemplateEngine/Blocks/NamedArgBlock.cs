﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.TemplateEngine.Prompt.Blocks;

/// <summary>
/// A <see cref="Block"/> that represents a named argument for a function call.
/// For example, in the template {{ MyPlugin.MyFunction var1="foo" }}, var1="foo" is a named arg block.
/// </summary>
internal sealed class NamedArgBlock : Block, ITextRendering
{
    /// <summary>
    /// Returns the <see cref="BlockTypes"/>.
    /// </summary>
    internal override BlockTypes Type => BlockTypes.NamedArg;

    /// <summary>
    /// Gets the name of the function argument.
    /// </summary>
    internal string Name { get; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedArgBlock"/> class.
    /// </summary>
    /// <param name="text">Raw text parsed from the prompt template.</param>
    /// <param name="logger">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <exception cref="SKException"></exception>
    public NamedArgBlock(string? text, ILoggerFactory? logger = null)
        : base(NamedArgBlock.TrimWhitespace(text), logger)
    {
        var argParts = this.Content.Split(Symbols.NamedArgBlockSeparator);
        if (argParts.Length != 2)
        {
            this.Logger.LogError("Invalid named argument `{Text}`", text);
            throw new SKException($"A function named argument must contain a name and value separated by a '{Symbols.NamedArgBlockSeparator}' character.");
        }

        this.Name = argParts[0];
        this._argNameAsVarBlock = new VarBlock($"{Symbols.VarPrefix}{argParts[0]}");
        var argValue = argParts[1];
        if (argValue.Length == 0)
        {
            this.Logger.LogError("Invalid named argument `{Text}`", text);
            throw new SKException($"A function named argument must contain a quoted value or variable after the '{Symbols.NamedArgBlockSeparator}' character.");
        }

        if (argValue[0] == Symbols.VarPrefix)
        {
            this._argValueAsVarBlock = new VarBlock(argValue);
        }
        else
        {
            this._valBlock = new ValBlock(argValue);
        }
    }

    /// <summary>
    /// Gets the rendered value of the function argument. If the value is a <see cref="ValBlock"/>, the value stays the same.
    /// If the value is a <see cref="VarBlock"/>, the value of the variable is determined by the context variables passed in.
    /// </summary>
    /// <param name="variables">Variables to use for rendering the named argument value when the value is a <see cref="VarBlock"/>.</param>
    /// <returns></returns>
    internal string GetValue(ContextVariables? variables)
    {
        var valueIsValidValBlock = this._valBlock != null && this._valBlock.IsValid(out var errorMessage);
        if (valueIsValidValBlock)
        {
            return this._valBlock!.Render(variables);
        }

        var valueIsValidVarBlock = this._argValueAsVarBlock != null && this._argValueAsVarBlock.IsValid(out var errorMessage2);
        if (valueIsValidVarBlock)
        {
            return this._argValueAsVarBlock!.Render(variables);
        }

        return string.Empty;
    }

    /// <summary>
    /// Renders the named arg block.
    /// </summary>
    /// <param name="variables"></param>
    /// <returns></returns>
    public string Render(ContextVariables? variables)
    {
        return this.Content;
    }

    /// <summary>
    /// Returns whether the named arg block has valid syntax.
    /// </summary>
    /// <param name="errorMsg">An error message that gets set when the named arg block is not valid.</param>
    /// <returns></returns>
#pragma warning disable CA2254 // error strings are used also internally, not just for logging
    public override bool IsValid(out string errorMsg)
    {
        errorMsg = string.Empty;
        if (string.IsNullOrEmpty(this.Name))
        {
            errorMsg = "A named argument must have a name";
            this.Logger.LogError(errorMsg);
            return false;
        }

        if (this._valBlock != null && !this._valBlock.IsValid(out var valErrorMsg))
        {
            errorMsg = $"There was an issue with the named argument value for '{this.Name}': {valErrorMsg}";
            this.Logger.LogError(errorMsg);
            return false;
        }
        else if (this._argValueAsVarBlock != null && !this._argValueAsVarBlock.IsValid(out var variableErrorMsg))
        {
            errorMsg = $"There was an issue with the named argument value for '{this.Name}': {variableErrorMsg}";
            this.Logger.LogError(errorMsg);
            return false;
        }
        else if (this._valBlock == null && this._argValueAsVarBlock == null)
        {
            errorMsg = "A named argument must have a value";
            this.Logger.LogError(errorMsg);
            return false;
        }

        // Argument names share the same validation as variables
        if (!this._argNameAsVarBlock.IsValid(out var argNameErrorMsg))
        {
            errorMsg = Regex.Replace(argNameErrorMsg, "a variable", "An argument", RegexOptions.IgnoreCase);
            errorMsg = Regex.Replace(errorMsg, "the variable", "The argument", RegexOptions.IgnoreCase);
            return false;
        }

        return true;
    }
#pragma warning restore CA2254

    #region private ================================================================================

    private readonly VarBlock _argNameAsVarBlock;
    private readonly ValBlock? _valBlock;
    private readonly VarBlock? _argValueAsVarBlock;

    private static string? TrimWhitespace(string? text)
    {
        if (text == null)
        {
            return text;
        }

        string[] trimmedParts = NamedArgBlock.GetTrimmedParts(text);
        switch (trimmedParts?.Length)
        {
            case (2):
                return $"{trimmedParts[0]}{Symbols.NamedArgBlockSeparator}{trimmedParts[1]}";
            case (1):
                return trimmedParts[0];
            default:
                return null;
        }
    }

    private static string[] GetTrimmedParts(string? text)
    {
        if (text == null)
        {
            return System.Array.Empty<string>();
        }

        string[] parts = text.Split(new char[] { Symbols.NamedArgBlockSeparator }, 2);
        string[] result = new string[parts.Length];
        if (parts.Length > 0)
        {
            result[0] = parts[0].Trim();
        }

        if (parts.Length > 1)
        {
            result[1] = parts[1].Trim();
        }

        return result;
    }

    #endregion
}
