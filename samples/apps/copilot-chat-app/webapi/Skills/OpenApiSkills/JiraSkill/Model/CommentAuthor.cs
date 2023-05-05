﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace SemanticKernel.Service.Skills.OpenApiSkills.JiraSkill.Model;

public class CommentAuthor
{
    /// <summary>
    /// Gets or sets the ID of the label.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string displayName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentAuthor"/> class.
    /// </summary>
    /// <param name="displayName">Name of Author</param>
    public CommentAuthor(string displayName)
    {
        this.displayName = displayName;
    }
}
