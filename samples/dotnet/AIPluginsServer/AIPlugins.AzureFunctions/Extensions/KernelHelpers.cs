﻿// Copyright (c) Microsoft. All rights reserved.

using System.IO;
using System.Reflection;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;

namespace AIPlugins.AzureFunctions.Extensions;

internal static class KernelHelpers
{
    public static IKernel CreateKernel(ILogger logger)
    {
        KernelBuilder builder = Kernel.Builder;
        // Register AI Providers...
        IKernel kernel = builder.Build();

        kernel.RegisterSemanticSkills(SampleSkillsPath(), logger);
        kernel.RegisterPlanner();

        if (kernel.Config.DefaultTextEmbeddingGenerationServiceId != null)
        {
            kernel.RegisterTextMemory();
        }

        return kernel;
    }

    public static ContextVariables LoadContextVariablesFromRequest(HttpRequestData req)
    {
        ContextVariables contextVariables = new ContextVariables();
        foreach (string? key in req.Query.AllKeys)
        {
            if (!string.IsNullOrEmpty(key))
            {
                contextVariables.Set(key, req.Query[key]);
            }
        }

        // If "input" was not specified in the query string, then check the body
        if (string.IsNullOrEmpty(req.Query.Get("input")))
        {
            // Load the input from the body
            string? body = req.Body.ToString();
            if (!string.IsNullOrEmpty(body))
            {
                contextVariables.Update(body);
            }
        }

        return contextVariables;
    }

    /// <summary>
    /// Scan the local folders from the repo, looking for "samples/skills" folder.
    /// </summary>
    /// <returns>The full path to samples/skills</returns>
    private static string SampleSkillsPath()
    {
        const string PARENT = "samples";
        const string FOLDER = "skills";

        bool SearchPath(string pathToFind, out string result, int maxAttempts = 10)
        {
            var currentDir = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
            bool found;
            do
            {
                result = Path.Join(currentDir, pathToFind);
                found = Directory.Exists(result);
                currentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));
            } while (maxAttempts-- > 0 && !found);

            return found;
        }

        if (!SearchPath(PARENT + Path.DirectorySeparatorChar + FOLDER, out string path)
            && !SearchPath(FOLDER, out path))
        {
            throw new DirectoryNotFoundException("Skills directory not found. The app needs the skills from the repo to work.");
        }

        return path;
    }
}
