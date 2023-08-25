﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System;
using Microsoft.SemanticKernel.Connectors.AI.Oobabooga.Completion.ChatCompletion;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.SemanticKernel.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Connectors.AI.Oobabooga.Completion;

public class OobaboogaCompletionSettings
{
    protected internal readonly HttpClient HttpClient;
    public ILogger? Logger { get; }
    protected internal readonly Func<ClientWebSocket> WebSocketFactory;
    protected internal readonly bool UseWebSocketsPooling;

    private readonly int _maxNbConcurrentWebSockets;
    private readonly SemaphoreSlim? _concurrentSemaphore;
    private readonly ConcurrentBag<bool>? _activeConnections;
    internal readonly ConcurrentBag<ClientWebSocket> WebSocketPool = new();
    private readonly int _keepAliveWebSocketsDuration;

    private long _lastCallTicks = long.MaxValue;

    /// <summary>
    /// Determines whether or not to use the overlapping SK settings for the completion request. Prompt is still provided by SK.
    /// </summary>
    public bool OverrideSKSettings { get; set; }

    /// <summary>
    /// Controls the size of the buffer used to received websocket packets
    /// </summary>
    public int WebSocketBufferSize { get; set; } = 2048;

    /// <summary>
    ///  Initializes a new instance of the <see cref="OobaboogaCompletionSettings"/> class, which controls how oobabooga completion requests are made.
    /// </summary>
    /// <param name="concurrentSemaphore">You can optionally set a hard limit on the max number of concurrent calls to the either of the completion methods by providing a <see cref="SemaphoreSlim"/>. Calls in excess will wait for existing consumers to release the semaphore</param>
    /// <param name="useWebSocketsPooling">If true, websocket clients will be recycled in a reusable pool as long as concurrent calls are detected</param>
    /// <param name="webSocketsCleanUpCancellationToken">if websocket pooling is enabled, you can provide an optional CancellationToken to properly dispose of the clean up tasks when disposing of the connector</param>
    /// <param name="keepAliveWebSocketsDuration">When pooling is enabled, pooled websockets are flushed on a regular basis when no more connections are made. This is the time to keep them in pool before flushing</param>
    /// <param name="webSocketFactory">The WebSocket factory used for making streaming API requests. Note that only when pooling is enabled will websocket be recycled and reused for the specified duration. Otherwise, a new websocket is created for each call and closed and disposed afterwards, to prevent data corruption from concurrent calls.</param>
    /// <param name="httpClient">Optional. The HTTP client used for making blocking API requests. If not specified, a default client will be used.</param>
    /// <param name="logger">Application logger</param>
    public OobaboogaCompletionSettings(
        SemaphoreSlim? concurrentSemaphore = null,
        bool useWebSocketsPooling = true,
        CancellationToken? webSocketsCleanUpCancellationToken = default,
        int keepAliveWebSocketsDuration = 100,
        Func<ClientWebSocket>? webSocketFactory = null,
        HttpClient? httpClient = null,
        ILogger? logger = null)
    {
        this.HttpClient = httpClient ?? new HttpClient(NonDisposableHttpClientHandler.Instance, disposeHandler: false);
        this.Logger = logger;

        this.UseWebSocketsPooling = useWebSocketsPooling;
        this._keepAliveWebSocketsDuration = keepAliveWebSocketsDuration;
        if (webSocketFactory != null)
        {
            this.WebSocketFactory = () =>
            {
                var webSocket = webSocketFactory();
                this.SetWebSocketOptions(webSocket);
                return webSocket;
            };
        }
        else
        {
            this.WebSocketFactory = () =>
            {
                ClientWebSocket webSocket = new();
                this.SetWebSocketOptions(webSocket);
                return webSocket;
            };
        }

        // if a hard limit is defined, we use a semaphore to limit the number of concurrent calls, otherwise, we use a stack to track active connections
        if (concurrentSemaphore != null)
        {
            this._concurrentSemaphore = concurrentSemaphore;
            this._maxNbConcurrentWebSockets = concurrentSemaphore.CurrentCount;
        }
        else
        {
            this._activeConnections = new();
            this._maxNbConcurrentWebSockets = 0;
        }

        if (this.UseWebSocketsPooling)
        {
            this.StartCleanupTask(webSocketsCleanUpCancellationToken ?? CancellationToken.None);
        }
    }

    /// <summary>
    /// Sets the options for the <paramref name="clientWebSocket"/>, either persistent and provided by the ctor, or transient if none provided.
    /// </summary>
    private void SetWebSocketOptions(ClientWebSocket clientWebSocket)
    {
        clientWebSocket.Options.SetRequestHeader("User-Agent", Telemetry.HttpUserAgent);
    }

    private void StartCleanupTask(CancellationToken cancellationToken)
    {
        Task.Factory.StartNew<Task>(
            async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await this.FlushWebSocketClientsAsync(cancellationToken).ConfigureAwait(false);
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Flushes the web socket clients that have been idle for too long
    /// </summary>
    private async Task FlushWebSocketClientsAsync(CancellationToken cancellationToken)
    {
        // In the cleanup task, make sure you handle OperationCanceledException appropriately
        // and make frequent checks on whether cancellation is requested.
        try
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(this._keepAliveWebSocketsDuration, cancellationToken).ConfigureAwait(false);

                // If another call was made during the delay, do not proceed with flushing
                if (DateTime.UtcNow.Ticks - Interlocked.Read(ref this._lastCallTicks) < TimeSpan.FromMilliseconds(this._keepAliveWebSocketsDuration).Ticks)
                {
                    return;
                }

                while (this.GetCurrentConcurrentCallsNb() == 0 && this.WebSocketPool.TryTake(out ClientWebSocket clientToDispose))
                {
                    await this.DisposeClientGracefullyAsync(clientToDispose).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException exception)
        {
            this.Logger?.LogTrace(message: "FlushWebSocketClientsAsync cleaning task was cancelled", exception: exception);
            while (this.WebSocketPool.TryTake(out ClientWebSocket clientToDispose))
            {
                await this.DisposeClientGracefullyAsync(clientToDispose).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Gets the number of concurrent calls, either by reading the semaphore count or by reading the active connections stack count
    /// </summary>
    private int GetCurrentConcurrentCallsNb()
    {
        if (this._concurrentSemaphore != null)
        {
            return this._maxNbConcurrentWebSockets - this._concurrentSemaphore!.CurrentCount;
        }

        return this._activeConnections!.Count;
    }

    /// <summary>
    /// Starts a concurrent call, either by taking a semaphore slot or by pushing a value on the active connections stack
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected internal async Task StartConcurrentCallAsync(CancellationToken cancellationToken)
    {
        if (this._concurrentSemaphore != null)
        {
            await this._concurrentSemaphore!.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            this._activeConnections!.Add(true);
        }
    }

    /// <summary>
    /// Ends a concurrent call, either by releasing a semaphore slot or by popping a value from the active connections stack
    /// </summary>
    protected internal void FinishConcurrentCall()
    {
        if (this._concurrentSemaphore != null)
        {
            this._concurrentSemaphore!.Release();
        }
        else
        {
            this._activeConnections!.TryTake(out _);
        }

        Interlocked.Exchange(ref this._lastCallTicks, DateTime.UtcNow.Ticks);
    }

    /// <summary>
    /// Closes and disposes of a client web socket after use
    /// </summary>
    protected internal async Task DisposeClientGracefullyAsync(ClientWebSocket clientWebSocket)
    {
        try
        {
            if (clientWebSocket.State == WebSocketState.Open)
            {
                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing client before disposal", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException exception)
        {
            this.Logger?.LogTrace(message: "Closing client web socket before disposal was cancelled", exception: exception);
        }
        catch (WebSocketException exception)
        {
            this.Logger?.LogTrace(message: "Closing client web socket before disposal raised web socket exception", exception: exception);
        }
        finally
        {
            clientWebSocket.Dispose();
        }
    }
}

public class OobaboogaCompletionSettings<TParameters> : OobaboogaCompletionSettings where TParameters : OobaboogaCompletionParameters, new()
{
    /// <inheritdoc/>
    public OobaboogaCompletionSettings(
        SemaphoreSlim? concurrentSemaphore = null,
        bool useWebSocketsPooling = true,
        CancellationToken? webSocketsCleanUpCancellationToken = default,
        int keepAliveWebSocketsDuration = 100,
        Func<ClientWebSocket>? webSocketFactory = null,
        HttpClient? httpClient = null,
        ILogger? logger = null) : base(concurrentSemaphore, useWebSocketsPooling, webSocketsCleanUpCancellationToken, keepAliveWebSocketsDuration, webSocketFactory, httpClient, logger)
    {
    }

    public TParameters OobaboogaParameters { get; set; } = new();
}

public class OobaboogaTextCompletionSettings : OobaboogaCompletionSettings<OobaboogaCompletionParameters>
{
    /// <inheritdoc/>
    public OobaboogaTextCompletionSettings(
        SemaphoreSlim? concurrentSemaphore = null,
        bool useWebSocketsPooling = true,
        CancellationToken? webSocketsCleanUpCancellationToken = default,
        int keepAliveWebSocketsDuration = 100,
        Func<ClientWebSocket>? webSocketFactory = null,
        HttpClient? httpClient = null,
        ILogger? logger = null) : base(concurrentSemaphore, useWebSocketsPooling, webSocketsCleanUpCancellationToken, keepAliveWebSocketsDuration, webSocketFactory, httpClient, logger)
    {
    }
}

public class OobaboogaChatCompletionSettings : OobaboogaCompletionSettings<OobaboogaChatCompletionParameters>
{
    /// <inheritdoc/>
    public OobaboogaChatCompletionSettings(
        SemaphoreSlim? concurrentSemaphore = null,
        bool useWebSocketsPooling = true,
        CancellationToken? webSocketsCleanUpCancellationToken = default,
        int keepAliveWebSocketsDuration = 100,
        Func<ClientWebSocket>? webSocketFactory = null,
        HttpClient? httpClient = null,
        ILogger? logger = null) : base(concurrentSemaphore, useWebSocketsPooling, webSocketsCleanUpCancellationToken, keepAliveWebSocketsDuration, webSocketFactory, httpClient, logger)
    {
    }
}
