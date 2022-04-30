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
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.EventArgs;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.Net.Serialization;
using DSharpPlus.Net.WebSocket;
using DSharpPlus.VoiceNext.Enums;
using DSharpPlus.VoiceNext.VoiceGatewayEntities;
using DSharpPlus.VoiceNext.VoiceGatewayEntities.Commands;
using DSharpPlus.VoiceNext.VoiceGatewayEntities.Payloads;
using Emzi0767;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace DSharpPlus.VoiceNext
{
    public sealed partial class VoiceNextConnection
    {
        public Task WebsocketOpenedAsync(IWebSocketClient socketClient, SocketEventArgs eventArgs)
        {
            if (this._shouldResume)
            {

            }

            // Send identify command
            var identifyCommand = new GatewayPayload()
            {
                OpCode = (GatewayOpCode)DiscordVoiceOpCode.Identify,
                Data = new DiscordVoiceIdentifyCommand()
                {
                    ServerId = this.Guild.Id,
                    UserId = this.Client.CurrentUser.Id,
                    SessionId = this._voiceStateUpdate.SessionId,
                    Token = this._voiceServerUpdate.Token
                }
            };
            return socketClient.SendMessageAsync(DiscordJson.SerializeObject(identifyCommand));
        }

        public async Task WebsocketMessageAsync(IWebSocketClient socketClient, SocketMessageEventArgs eventArgs)
        {
            if (eventArgs is not SocketTextMessageEventArgs et)
            {
                // This shouldn't happen, as Discord is a stable platform™️
                this.Client.Logger.LogCritical("Discord Voice Gateway sent binary data - unable to process.");
                return;
            }

            var payload = JObject.Parse(et.Message);
            switch ((DiscordVoiceOpCode)(int)payload["op"]!)
            {
                case DiscordVoiceOpCode.Ready:
                    this.Client.Logger.LogInformation("Discord Voice Gateway sent ready payload");
                    this._voiceReadyPayload = payload["d"].ToDiscordObject<DiscordVoiceReadyPayload>();
                    this.IsConnected = true;

                    this._udpClient = new(this._voiceReadyPayload.Ip, this._voiceReadyPayload.Port);
                    var ipDiscovery = new DiscordIPDiscovery()
                    {
                        Type = 0x01,
                        Length = (ushort)(6 + Encoding.UTF8.GetByteCount(this._voiceReadyPayload.Ip)), // SSRC is 2 bytes, Port is 4 bytes, Address is variable
                        SSRC = this._voiceReadyPayload.SSRC,
                        Port = this._voiceReadyPayload.Port,
                        Address = this._voiceReadyPayload.Ip
                    };

                    await this._udpClient.SendAsync(ipDiscovery, ipDiscovery.Length);
                    DiscordIPDiscovery reply = (await this._udpClient.ReceiveAsync()).Buffer.AsSpan();
                    this._webSocketEndpoint = new(reply.Address, reply.Port, true);
                    await this._voiceWebsocket.SendMessageAsync(DiscordJson.SerializeObject(new GatewayPayload()
                    {
                        OpCode = (GatewayOpCode)DiscordVoiceOpCode.SelectProtocol,
                        Data = new DiscordVoiceSelectProtocolCommand()
                        {
                            Protocol = "udp",
                            Data = new DiscordVoiceSelectProtocolCommandData()
                            {
                                Address = this._webSocketEndpoint.Hostname,
                                Port = (ushort)this._webSocketEndpoint.Port, // Why is this an int...
                                Mode = this._selectedProtocol!
                            }
                        }
                    }));

                    //TODO: Launch an event whenever sending or receiving udp data
                    return;
                // This is separate from identify/ready/select protocol. Should be started as soon as possible
                case DiscordVoiceOpCode.Hello:
                    this.Client.Logger.LogInformation("Discord Voice Gateway sent hello payload");
                    this._voiceHelloPayload = payload["d"].ToDiscordObject<DiscordVoiceHelloPayload>();

                    // Start heartbeat task. Fire and forget
                    _ = this.HeartbeatLoopAsync();
                    break;
                case DiscordVoiceOpCode.SessionDescription:
                    this.Client.Logger.LogInformation("Discord Voice Gateway sent session description payload");
                    this._voiceSessionDescriptionPayload = payload["d"].ToDiscordObject<DiscordVoiceSessionDescriptionPayload>();

                    // TODO: Send "ready" payload.
                    break;
                default:
                    break;
            }
        }

        public async Task HeartbeatLoopAsync()
        {
            var secureRandom = new SecureRandom();
            while (this.IsConnected)
            {
                await this._voiceWebsocket.SendMessageAsync(DiscordJson.SerializeObject(new GatewayPayload()
                {
                    OpCode = (GatewayOpCode)DiscordVoiceOpCode.Heartbeat,
                    Data = secureRandom.GetInt32()
                }));

                // This is executed after the heartbeat because:
                // - OpCode Hello expects the heartbeat to start ASAP.
                // - The while loop still checks if we're still connected before sending another heartbeat.
                await Task.Delay(this._voiceHelloPayload!.HeartbeatInterval);
            }
        }

        public Task WebsocketClosedAsync(IWebSocketClient socketClient, SocketCloseEventArgs eventArgs)
        {
            this.IsConnected = false;
            if ((DiscordVoiceCloseEventCode)eventArgs.CloseCode is DiscordVoiceCloseEventCode.SessionNoLongerValid or DiscordVoiceCloseEventCode.SessionTimeout)
            {
                this.Client.Logger.LogWarning("Discord Voice Gateway closed with session no longer valid or session timeout: {0}", eventArgs.CloseMessage);
                this._shouldResume = false;
            }

            if (!this._disposedValue)
            {
                this._cancellationTokenSource.Cancel();
                this._cancellationTokenSource = new();

                // Setup websocket
                this._voiceWebsocket = this.Client.Configuration.WebSocketClientFactory(this.Client.Configuration.Proxy);
                this._voiceWebsocket.Connected += this.WebsocketOpenedAsync;
                this._voiceWebsocket.Disconnected += this.WebsocketClosedAsync;
                this._voiceWebsocket.MessageReceived += this.WebsocketMessageAsync;
                this._voiceWebsocket.ExceptionThrown += this.WebsocketExceptionAsync;

                if (this._shouldResume) // emzi you dipshit
                    return this.ConnectAsync();
            }

            return Task.CompletedTask;
        }

        public Task WebsocketExceptionAsync(IWebSocketClient socketClient, SocketErrorEventArgs eventArgs)
        {
            this.Client.Logger.LogError(eventArgs.Exception, "Discord Voice Gateway exception: {0}", eventArgs.Exception.Message);
            return Task.CompletedTask;
        }
    }
}
