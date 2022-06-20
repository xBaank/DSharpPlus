// This file is part of the DSharpPlus project.
//
// Copyright (c) 2015 Mike Santiago
// Copyright (c) 2016-2022 DSharpPlus Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.EventArgs;
using Emzi0767.Utilities;

namespace DSharpPlus.Net.WebSocket
{
    // weebsocket
    // not even sure whether emzi or I posted this. much love, naam.
    /// <summary>
    /// The default, native-based WebSocket client implementation.
    /// </summary>
    public class WebSocketClient : IWebSocketClient
    {
        private const int OutgoingChunkSize = 8192; // 8 KiB
        private const int IncomingChunkSize = 32768; // 32 KiB

        /// <inheritdoc />
        public IWebProxy Proxy { get; }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> DefaultHeaders { get; }
        private readonly Dictionary<string, string> _defaultHeaders;

        private Task _receiverTask;
        private CancellationTokenSource _receiverTokenSource;
        private CancellationToken _receiverToken;
        private readonly SemaphoreSlim _senderLock;

        private CancellationTokenSource _socketTokenSource;
        private CancellationToken _socketToken;
        private ClientWebSocket _ws;

        private volatile bool _isClientClose;
        private volatile bool _isConnected;
        private bool _isDisposed;

        /// <summary>
        /// Instantiates a new WebSocket client with specified proxy settings.
        /// </summary>
        /// <param name="proxy">Proxy settings for the client.</param>
        private WebSocketClient(IWebProxy proxy)
        {
            _connected = new AsyncEvent<WebSocketClient, SocketEventArgs>("WS_CONNECT", TimeSpan.Zero, EventErrorHandler);
            _disconnected = new AsyncEvent<WebSocketClient, SocketCloseEventArgs>("WS_DISCONNECT", TimeSpan.Zero, EventErrorHandler);
            _messageReceived = new AsyncEvent<WebSocketClient, SocketMessageEventArgs>("WS_MESSAGE", TimeSpan.Zero, EventErrorHandler);
            _exceptionThrown = new AsyncEvent<WebSocketClient, SocketErrorEventArgs>("WS_ERROR", TimeSpan.Zero, null);

            Proxy = proxy;
            _defaultHeaders = new Dictionary<string, string>();
            DefaultHeaders = new ReadOnlyDictionary<string, string>(_defaultHeaders);

            _receiverTokenSource = null;
            _receiverToken = CancellationToken.None;
            _senderLock = new SemaphoreSlim(1);

            _socketTokenSource = null;
            _socketToken = CancellationToken.None;
        }

        /// <inheritdoc />
        public async Task ConnectAsync(Uri uri)
        {
            // Disconnect first
            try { await DisconnectAsync().ConfigureAwait(false); } catch { }

            // Disallow sending messages
            await _senderLock.WaitAsync().ConfigureAwait(false);

            try
            {
                // This can be null at this point
                _receiverTokenSource?.Dispose();
                _socketTokenSource?.Dispose();

                _ws?.Dispose();
                _ws = new ClientWebSocket();
                _ws.Options.Proxy = Proxy;
                _ws.Options.KeepAliveInterval = TimeSpan.Zero;
                if (_defaultHeaders != null)
                    foreach (var (k, v) in _defaultHeaders)
                        _ws.Options.SetRequestHeader(k, v);

                _receiverTokenSource = new CancellationTokenSource();
                _receiverToken = _receiverTokenSource.Token;

                _socketTokenSource = new CancellationTokenSource();
                _socketToken = _socketTokenSource.Token;

                _isClientClose = false;
                _isDisposed = false;
                await _ws.ConnectAsync(uri, _socketToken).ConfigureAwait(false);
                _receiverTask = Task.Run(ReceiverLoopAsync, _receiverToken);
            }
            finally
            {
                _senderLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task DisconnectAsync(int code = 1000, string message = "")
        {
            // Ensure that messages cannot be sent
            await _senderLock.WaitAsync().ConfigureAwait(false);

            try
            {
                _isClientClose = true;
                if (_ws != null && (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived))
                    await _ws.CloseOutputAsync((WebSocketCloseStatus)code, message, CancellationToken.None).ConfigureAwait(false);

                if (_receiverTask != null)
                    await _receiverTask.ConfigureAwait(false); // Ensure that receiving completed

                if (_isConnected)
                    _isConnected = false;

                if (!_isDisposed)
                {
                    // Cancel all running tasks
                    if (_socketToken.CanBeCanceled)
                        _socketTokenSource?.Cancel();
                    _socketTokenSource?.Dispose();

                    if (_receiverToken.CanBeCanceled)
                        _receiverTokenSource?.Cancel();
                    _receiverTokenSource?.Dispose();

                    _isDisposed = true;
                }
            }
            catch { }
            finally
            {
                _senderLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task SendMessageAsync(string message)
        {
            if (_ws == null)
                return;

            if (_ws.State != WebSocketState.Open && _ws.State != WebSocketState.CloseReceived)
                return;

            var bytes = Utilities.UTF8.GetBytes(message);
            await _senderLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var len = bytes.Length;
                var segCount = len / OutgoingChunkSize;
                if (len % OutgoingChunkSize != 0)
                    segCount++;

                for (var i = 0; i < segCount; i++)
                {
                    var segStart = OutgoingChunkSize * i;
                    var segLen = Math.Min(OutgoingChunkSize, len - segStart);

                    await _ws.SendAsync(new ArraySegment<byte>(bytes, segStart, segLen), WebSocketMessageType.Text, i == segCount - 1, CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                _senderLock.Release();
            }
        }

        /// <inheritdoc />
        public bool AddDefaultHeader(string name, string value)
        {
            _defaultHeaders[name] = value;
            return true;
        }

        /// <inheritdoc />
        public bool RemoveDefaultHeader(string name)
            => _defaultHeaders.Remove(name);

        /// <summary>
        /// Disposes of resources used by this WebSocket client instance.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            _receiverTokenSource?.Dispose();
            _socketTokenSource?.Dispose();
        }

        internal async Task ReceiverLoopAsync()
        {
            await Task.Yield();

            var token = _receiverToken;
            var buffer = new ArraySegment<byte>(new byte[IncomingChunkSize]);

            try
            {
                using var bs = new MemoryStream();
                while (!token.IsCancellationRequested)
                {
                    // See https://github.com/RogueException/Discord.Net/commit/ac389f5f6823e3a720aedd81b7805adbdd78b66d 
                    // for explanation on the cancellation token

                    WebSocketReceiveResult result;
                    byte[] resultBytes;
                    do
                    {
                        result = await _ws.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        bs.Write(buffer.Array, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    resultBytes = new byte[bs.Length];
                    bs.Position = 0;
                    bs.Read(resultBytes, 0, resultBytes.Length);
                    bs.Position = 0;
                    bs.SetLength(0);

                    if (!_isConnected && result.MessageType != WebSocketMessageType.Close)
                    {
                        _isConnected = true;
                        await _connected.InvokeAsync(this, new SocketEventArgs()).ConfigureAwait(false);
                    }

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        await _messageReceived.InvokeAsync(this, new SocketBinaryMessageEventArgs(resultBytes)).ConfigureAwait(false);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        await _messageReceived.InvokeAsync(this, new SocketTextMessageEventArgs(Utilities.UTF8.GetString(resultBytes))).ConfigureAwait(false);
                    }
                    else // close
                    {
                        if (!_isClientClose)
                        {
                            var code = result.CloseStatus.Value;
                            code = code == WebSocketCloseStatus.NormalClosure || code == WebSocketCloseStatus.EndpointUnavailable
                                ? (WebSocketCloseStatus)4000
                                : code;

                            await _ws.CloseOutputAsync(code, result.CloseStatusDescription, CancellationToken.None).ConfigureAwait(false);
                        }

                        await _disconnected.InvokeAsync(this, new SocketCloseEventArgs { CloseCode = (int)result.CloseStatus, CloseMessage = result.CloseStatusDescription }).ConfigureAwait(false);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                await _exceptionThrown.InvokeAsync(this, new SocketErrorEventArgs { Exception = ex }).ConfigureAwait(false);
                await _disconnected.InvokeAsync(this, new SocketCloseEventArgs { CloseCode = -1, CloseMessage = "" }).ConfigureAwait(false);
            }

            // Don't await or you deadlock
            // DisconnectAsync waits for this method
            _ = DisconnectAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new instance of <see cref="WebSocketClient"/>.
        /// </summary>
        /// <param name="proxy">Proxy to use for this client instance.</param>
        /// <returns>An instance of <see cref="WebSocketClient"/>.</returns>
        public static IWebSocketClient CreateNew(IWebProxy proxy)
            => new WebSocketClient(proxy);

        #region Events
        /// <summary>
        /// Triggered when the client connects successfully.
        /// </summary>
        public event AsyncEventHandler<IWebSocketClient, SocketEventArgs> Connected
        {
            add => _connected.Register(value);
            remove => _connected.Unregister(value);
        }
        private readonly AsyncEvent<WebSocketClient, SocketEventArgs> _connected;

        /// <summary>
        /// Triggered when the client is disconnected.
        /// </summary>
        public event AsyncEventHandler<IWebSocketClient, SocketCloseEventArgs> Disconnected
        {
            add => _disconnected.Register(value);
            remove => _disconnected.Unregister(value);
        }
        private readonly AsyncEvent<WebSocketClient, SocketCloseEventArgs> _disconnected;

        /// <summary>
        /// Triggered when the client receives a message from the remote party.
        /// </summary>
        public event AsyncEventHandler<IWebSocketClient, SocketMessageEventArgs> MessageReceived
        {
            add => _messageReceived.Register(value);
            remove => _messageReceived.Unregister(value);
        }
        private readonly AsyncEvent<WebSocketClient, SocketMessageEventArgs> _messageReceived;

        /// <summary>
        /// Triggered when an error occurs in the client.
        /// </summary>
        public event AsyncEventHandler<IWebSocketClient, SocketErrorEventArgs> ExceptionThrown
        {
            add => _exceptionThrown.Register(value);
            remove => _exceptionThrown.Unregister(value);
        }
        private readonly AsyncEvent<WebSocketClient, SocketErrorEventArgs> _exceptionThrown;

        private void EventErrorHandler<TArgs>(AsyncEvent<WebSocketClient, TArgs> asyncEvent, Exception ex, AsyncEventHandler<WebSocketClient, TArgs> handler, WebSocketClient sender, TArgs eventArgs)
            where TArgs : AsyncEventArgs
            => _exceptionThrown.InvokeAsync(this, new SocketErrorEventArgs { Exception = ex }).ConfigureAwait(false).GetAwaiter().GetResult();
        #endregion
    }
}
