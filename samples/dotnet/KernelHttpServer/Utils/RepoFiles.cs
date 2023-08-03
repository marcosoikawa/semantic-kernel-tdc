﻿// Copyright (c) Microsoft. All rights reserved.

using System.IO;
using System.Reflection;

namespace KernelHttpServer.Utils;

internal static class RepoFiles
{
    /// <summary>
    /// Scan the local folders from the repo, looking for "prompts/samples" folder.
    /// </summary>
    /// <returns>The full path to prompts/samples</returns>
    internal static string SampleSkillsPath()
    {
        const string Parent = "prompts";
        const string Folder = "samples";

        bool SearchPath(string pathToFind, out string result, int maxAttempts = 10)
        {
            var currDir = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
            bool found;
            do
            {
                result = Path.Join(currDir, pathToFind);
                found = Directory.Exists(result);
                currDir = Path.GetFullPath(Path.Combine(currDir, ".."));
            } while (maxAttempts-- > 0 && !found);

            return found;
        }

        if (!SearchPath(Parent + Path.DirectorySeparatorChar + Folder, out string path)
            && !SearchPath(Folder, out path))
        {
            throw new YourAppException("Skills directory not found. The app needs the skills from the repo to work.");
        }

        return path;
    }
}
