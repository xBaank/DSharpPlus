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
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.Net.Serialization;
using DSharpPlus.VoiceNext.VoiceGateway.Entities.Commands;
using DSharpPlus.VoiceNext.VoiceGateway.Enums;

namespace DSharpPlus.VoiceNext
{
    public sealed partial class VoiceNextConnection
    {
        public Task ConnectAsync()
        {
            var gatewayUri = new UriBuilder
            {
                Scheme = "wss",
                Host = this._webSocketEndpoint.Hostname,
                Query = "encoding=json&v=4"
            };

            return this._voiceWebsocket.ConnectAsync(gatewayUri.Uri);
        }

        public Task ReconnectAsync() => throw new NotImplementedException();

        public Task DisconnectAsync() => throw new NotImplementedException();

        public async Task SpeakAsync(Stream audioStream, DiscordVoiceSpeakingIndicators speakingIndicators)
        {
            if (this._isDisposed)
                throw new ObjectDisposedException(nameof(VoiceNextConnection));

            // You must send at least one Opcode 5 Speaking payload before sending voice data, or you will be disconnected with an invalid SSRC error. - DDocs
            await this._voiceWebsocket.SendMessageAsync(DiscordJson.SerializeObject(new GatewayPayload()
            {
                OpCode = (GatewayOpCode)DiscordVoiceOpCode.Speaking,
                Data = new DiscordVoiceSpeakingCommand()
                {
                    Speaking = speakingIndicators,
                    Delay = 0,
                    SSRC = this._voiceReadyPayload!.SSRC
                }
            }));

            var buffer = new byte[4096];
            var bytesRead = 0;
            while ((bytesRead = await audioStream.ReadAsync(buffer, bytesRead, Math.Min(int.TryParse(audioStream.Length.ToString(CultureInfo.InvariantCulture), out var audioStreamLength) ? audioStreamLength : int.MaxValue, buffer.Length))) != 0)
            {
                var voicePacket = this.AudioManager!.PrepareVoicePacket(buffer);
                await this._udpClient!.SendAsync(voicePacket, voicePacket.Length);
            }

            await this._voiceWebsocket.SendMessageAsync(DiscordJson.SerializeObject(new GatewayPayload()
            {
                OpCode = (GatewayOpCode)DiscordVoiceOpCode.Speaking,
                Data = new DiscordVoiceSpeakingCommand()
                {
                    Speaking = 0,
                    Delay = 0,
                    SSRC = this._voiceReadyPayload!.SSRC
                }
            }));
        }
    }
}
