﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.SemanticKernel;

namespace Plugins.DictionaryPlugin;

/// <summary>
/// Plugin example with two native functions, where one function gets a random word and the other returns a definition for a given word.
/// </summary>
public sealed class BasicDictionaryPlugin
{
    public const string PluginName = nameof(BasicDictionaryPlugin);

    private readonly Dictionary<string, string> _dictionary = new()
    {
        {"apple", "a round fruit with red, green, or yellow skin and a white flesh"},
        {"book", "a set of printed or written pages bound together along one edge"},
        {"cat", "a small furry animal with whiskers and a long tail that is often kept as a pet"},
        {"dog", "a domesticated animal with four legs, a tail, and a keen sense of smell that is often used for hunting or companionship"},
        {"elephant", "a large gray mammal with a long trunk, tusks, and ears that lives in Africa and Asia"}
    };

    [SKFunction, SKName("GetRandomWord"), System.ComponentModel.Description("Gets a random word from a dictionary of common words and their definitions.")]
    public string GetRandomWord()
    {
        // Get random number
        var index = RandomNumberGenerator.GetInt32(0, this._dictionary.Count - 1);

        // Return the word at the random index
        return this._dictionary.ElementAt(index).Key;
    }

    [SKFunction, SKName("GetDefinition"), System.ComponentModel.Description("Gets the definition for a given word.")]
    public string GetDefinition([System.ComponentModel.Description("Word to get definition for.")] string word)
    {
        return this._dictionary.TryGetValue(word, out var definition)
            ? definition
            : "Word not found";
    }
}
