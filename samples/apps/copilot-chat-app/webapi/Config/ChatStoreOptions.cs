﻿// Copyright (c) Microsoft. All rights reserved.

using static SemanticKernel.Service.Config.AuthorizationOptions;

namespace SemanticKernel.Service.Config;

/// <summary>
/// Configuration settings for the chat store.
/// </summary>
public class ChatStoreOptions
{
    public const string PropertyName = "ChatStore";

    /// <summary>
    /// The type of chat store to use.
    /// </summary>
    public enum ChatStoreType
    {
        /// <summary>
        /// Non-persistent chat store
        /// </summary>
        Volatile,

        /// <summary>
        /// File-system based persistent chat store.
        /// </summary>
        Filesystem,

        /// <summary>
        /// Azure CosmosDB based persistent chat store.
        /// </summary>
        Cosmos
    }

    /// <summary>
    /// Gets or sets the type of chat store to use.
    /// </summary>
    public ChatStoreType Type { get; set; } = ChatStoreType.Volatile;

    /// <summary>
    /// Gets or sets the configuration for the file system chat store.
    /// </summary>
    [RequiredOnPropertyValue(nameof(Type), ChatStoreType.Filesystem)]
    public FileSystemOptions? Filesystem { get; set; }

    /// <summary>
    /// Gets or sets the configuration for the Azure CosmosDB chat store.
    /// </summary>
    [RequiredOnPropertyValue(nameof(Type), ChatStoreType.Cosmos)]
    public CosmosConfig? Cosmos { get; set; }
}
