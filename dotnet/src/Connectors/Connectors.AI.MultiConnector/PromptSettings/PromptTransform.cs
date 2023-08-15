﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;

namespace Microsoft.SemanticKernel.Connectors.AI.MultiConnector.PromptSettings;

public enum PromptInterpolationType
{
    None,
    InterpolateKeys,
    InterpolateFormattable,
    InterpolateFormattableExpression,
    InterpolateDynamicLinqExpression
}

/// <summary>
/// Represents a transformation of an input prompt string that can be template based or customized
/// </summary>
public class PromptTransform
{
    private static readonly Regex s_interpolateRegex = new(@"{(\D.+?)}", RegexOptions.Compiled);

    public PromptTransform()
    {
        this.Template = Defaults.EmptyFormat;
        this.TransformFunction = this.DefaultTransform;
    }

    public string Template { get; set; }

    public PromptInterpolationType InterpolationType { get; set; }

    [JsonIgnore]
    public Func<string, Dictionary<string, object>?, string> TransformFunction { get; set; }

    /// <summary>
    /// The default transform does interpolation of the template with tokens {key} from a dictionary of values for keys, and then a string format to inject the input in token {0}
    /// </summary>
    public string DefaultTransform(string input, Dictionary<string, object>? context)
    {
        var processedTemplate = this.Template;

        if (context is { Count: > 0 })
        {
            switch (this.InterpolationType)
            {
                case PromptInterpolationType.None:
                    break;
                case PromptInterpolationType.InterpolateKeys:
                    processedTemplate = this.InterpolateKeys(processedTemplate, context);
                    break;
                case PromptInterpolationType.InterpolateFormattable:
                    processedTemplate = this.InterpolateFormattable(processedTemplate, context);
                    break;
                case PromptInterpolationType.InterpolateFormattableExpression:
                    processedTemplate = this.InterpolateFormattableExpression(processedTemplate, context);
                    break;
                case PromptInterpolationType.InterpolateDynamicLinqExpression:
                    processedTemplate = this.InterpolateDynamicLinqExpression(processedTemplate, context);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        var toReturn = string.Format(CultureInfo.InvariantCulture, processedTemplate, input);

        return toReturn;
    }

    /// <summary>
    /// Simple interpolation of a string with tokens {key} with a dictionary of values for keys
    /// </summary>
    public string InterpolateKeys(string value, Dictionary<string, object>? context)
    {
        return s_interpolateRegex.Replace(value, match =>
        {
            var key = match.Groups[1].Value;
            if (context?.TryGetValue(key, out var replacementValue) ?? false)
            {
                return string.Format(CultureInfo.InvariantCulture, Defaults.EmptyFormat, replacementValue);
            }

            return string.Empty;
        });
    }



    public string InterpolateFormattable(string format, Dictionary<string, object> context)
    {
        // Extract tokens from the format string.
        var matches = s_interpolateRegex.Matches(format);
        var tokens = matches.Cast<Match>().Select(m => m.Groups[1].Value).ToArray();

        // Replace tokens with indexed placeholders.
        for (int i = 0; i < tokens.Length; i++)
        {
            format = format.Replace($"{{{tokens[i]}}}", $"{{{i}}}");
        }

        // Create arguments array.
        var args = tokens.Select(t => context.ContainsKey(t) ? context[t] : null).ToArray();

        // Use FormattableStringFactory to create a FormattableString.
        FormattableString formattable = FormattableStringFactory.Create(format, args);

        // Finally, format the string.
        return formattable.ToString(CultureInfo.InvariantCulture);
    }

    private static readonly ConcurrentDictionary<string, Func<Dictionary<string, object>, string>> s_cachedInterpolationFormattableExpressions = new ();

    public string InterpolateFormattableExpression(string value, Dictionary<string, object> context)
    {
        return s_interpolateRegex.Replace(value, match =>
        {
            var matchToken = match.Groups[1].Value;
            var key = $"{value}/{matchToken}";
            if (!s_cachedInterpolationFormattableExpressions.TryGetValue(key, out var interpolationDelegate))
            {
                var dictionaryParam = Expression.Parameter(typeof(Dictionary<string, object>), nameof(context));

                // This fetches the value from the dictionary for the given token.
                var tokenValueExpression = Expression.Property(dictionaryParam, "Item", Expression.Constant(matchToken));

                var formattedStringExpression = Expression.Call(
                    typeof(FormattableStringFactory),
                    nameof(FormattableStringFactory.Create),
                    typeArguments: null,
                    arguments: new Expression[]
                    {
                        Expression.Constant($"{{{matchToken}}}"), // Template
                        Expression.NewArrayInit(typeof(object), Expression.Convert(tokenValueExpression, typeof(object))) // Args
                    });

                var toStringExpression = Expression.Call(formattedStringExpression, nameof(object.ToString), typeArguments: null);

                interpolationDelegate = Expression.Lambda<Func<Dictionary<string, object>, string>>(toStringExpression, dictionaryParam).Compile();

                s_cachedInterpolationFormattableExpressions[key] = interpolationDelegate;
            }

            return interpolationDelegate(context);
        });
    }

    private static ConcurrentDictionary<string, Delegate> s_cachedInterpolationLinqExpressions = new();

    public string InterpolateDynamicLinqExpression( string value, Dictionary<string, object> context)
    {
        return s_interpolateRegex.Replace(value,
            match =>
            {
                var matchToken = match.Groups[1].Value;
                var key = $"{value}/{matchToken}";
                if (!s_cachedInterpolationLinqExpressions.TryGetValue(key, out var tokenDelegate))
                {
                    var parameters = new List<ParameterExpression>(context.Count);
                    foreach (var contextObject in context)
                    {
                        var p = Expression.Parameter(contextObject.Value.GetType(), contextObject.Key);
                        parameters.Add(p);
                    }

                    ParsingConfig config = new ParsingConfig();
                    config.CustomTypeProvider = new CustomDynamicTypeProvider(context) { DefaultProvider = config.CustomTypeProvider };

                    var e = System.Linq.Dynamic.Core.DynamicExpressionParser.ParseLambda(config, parameters.ToArray(), null, matchToken);
                    tokenDelegate = e.Compile();
                    s_cachedInterpolationLinqExpressions[key] = tokenDelegate;
                }

                return (tokenDelegate.DynamicInvoke(context.Values.ToArray()) ?? "").ToString();
            });
    }

    private class CustomDynamicTypeProvider : IDynamicLinkCustomTypeProvider
    {
        private readonly Dictionary<string, object> _context;

        public CustomDynamicTypeProvider(Dictionary<string, object> context)
        {
            this._context = context;
        }

        public IDynamicLinkCustomTypeProvider DefaultProvider { get; set; }

        public HashSet<Type> GetCustomTypes()
        {
            HashSet<Type> types = this.DefaultProvider.GetCustomTypes();
            types.Add(typeof(string));
            types.Add(typeof(Regex));
            types.Add(typeof(RegexOptions));
            types.Add(typeof(CultureInfo));
            types.Add(typeof(HttpUtility));
            types.Add(typeof(Enumerable));
            foreach (var contextObject in this._context)
            {
                types.Add(contextObject.Value.GetType());
            }

            return types;
        }

        public Dictionary<Type, List<MethodInfo>> GetExtensionMethods()
        {
            return this.DefaultProvider.GetExtensionMethods();
        }

        public Type? ResolveType(string typeName)
        {
            return this.DefaultProvider.ResolveType(typeName);
        }

        public Type? ResolveTypeBySimpleName(string simpleTypeName)
        {
            return this.DefaultProvider.ResolveTypeBySimpleName(simpleTypeName);
        }
    }

}
