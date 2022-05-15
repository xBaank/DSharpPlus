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
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using DSharpPlus.Entities;
using DSharpPlus.Net;
using DSharpPlus.Net.WebSocket;
using DSharpPlus.VoiceNext.Managers;
using DSharpPlus.VoiceNext.VoiceGateway.Entities;
using DSharpPlus.VoiceNext.VoiceGateway.Entities.Payloads;

namespace DSharpPlus.VoiceNext
{
    public sealed partial class VoiceNextConnection : IDisposable
    {
        public DiscordClient Client { get; }
        public DiscordGuild Guild { get; }
        public DiscordChannel Channel { get; internal set; }
        public VoiceNextConfiguration Configuration { get; }
        public bool IsConnected = false;
        public CancellationToken CancellationToken => this._cancellationTokenSource.Token;
        public AudioManager? AudioManager { get; private set; }

        internal CancellationTokenSource _cancellationTokenSource { get; set; } = new();
        internal DiscordVoiceStateUpdate _voiceStateUpdate { get; }
        internal DiscordVoiceServerUpdatePayload _voiceServerUpdate { get; set; }
        internal ConnectionEndpoint _webSocketEndpoint { get; set; }
        internal IWebSocketClient _voiceWebsocket { get; set; }
        internal bool _shouldResume = false;

        private bool _isDisposed;
        private DiscordVoiceReadyPayload? _voiceReadyPayload { get; set; }
        private DiscordVoiceHelloPayload? _voiceHelloPayload { get; set; }
        private string? _selectedProtocol => this._voiceReadyPayload?.Modes.FirstOrDefault(x => x == "xsalsa20_poly1305" || x == "xsalsa20_poly1305_suffix" || x == "xsalsa20_poly1305_lite") ?? this._voiceReadyPayload?.Modes[0];
        private UdpClient? _udpClient { get; set; }
        private DiscordVoiceSessionDescriptionPayload? _voiceSessionDescriptionPayload { get; set; }

        internal VoiceNextConnection(DiscordClient client, DiscordChannel voiceChannel, VoiceNextConfiguration configuration, DiscordVoiceStateUpdate voiceStateUpdate, DiscordVoiceServerUpdatePayload voiceServerUpdatePayload)
        {
            this.Client = client;
            this.Guild = voiceChannel.Guild;
            this.Channel = voiceChannel;
            this.Configuration = configuration;

            // We're not supposed to cache these, however they're required when authenticating/resuming to a session in other methods. As such, don't use them otherwise.
            this._voiceStateUpdate = voiceStateUpdate;
            this._voiceServerUpdate = voiceServerUpdatePayload;

            // Setup endpoint
            if (this._voiceServerUpdate.Endpoint == null)
            {
                throw new InvalidOperationException($"The {nameof(this._voiceServerUpdate.Endpoint)} argument is null. A null endpoint means that the voice server allocated has gone away and is trying to be reallocated. You should attempt to disconnect from the currently connected voice server, and not attempt to reconnect until a new voice server is allocated.");
            }
            var endpointIndex = this._voiceServerUpdate.Endpoint.LastIndexOf(':');
            var endpointPort = 443;
            string? endpointHost;
            if (endpointIndex != -1) // Determines if the endpoint is a ip address or a hostname
            {
                endpointHost = this._voiceServerUpdate.Endpoint.Substring(0, endpointIndex);
                endpointPort = int.Parse(this._voiceServerUpdate.Endpoint.Substring(endpointIndex + 1));
            }
            else
            {
                endpointHost = this._voiceServerUpdate.Endpoint;
            }

            this._webSocketEndpoint = new ConnectionEndpoint
            {
                Hostname = endpointHost,
                Port = endpointPort
            };

            // Setup websocket
            this._voiceWebsocket = this.Client.Configuration.WebSocketClientFactory(this.Client.Configuration.Proxy);
            this._voiceWebsocket.Connected += this.WebsocketOpenedAsync;
            this._voiceWebsocket.Disconnected += this.WebsocketClosedAsync;
            this._voiceWebsocket.MessageReceived += this.WebsocketMessageAsync;
            this._voiceWebsocket.ExceptionThrown += this.WebsocketExceptionAsync;

            // Setup events
            this._userJoined = new("VOICENEXT_USER_JOINED", TimeSpan.Zero, this.Client.EventErrorHandler);
            this._userLeft = new("VOICENEXT_USER_LEFT", TimeSpan.Zero, this.Client.EventErrorHandler);
            this._voiceReceived = new("VOICENEXT_VOICE_RECEIVED", TimeSpan.Zero, this.Client.EventErrorHandler);
            this._voiceReady = new("VOICENEXT_VOICE_READY", TimeSpan.Zero, this.Client.EventErrorHandler);
            this._websocketError = new("VOICENEXT_WEBSOCKET_ERROR", TimeSpan.Zero, this.Client.EventErrorHandler);
        }

        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    this._cancellationTokenSource.Cancel();
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
