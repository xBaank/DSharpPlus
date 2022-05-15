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
using DSharpPlus.VoiceNext.VoiceGateway.Entities.Payloads;

namespace DSharpPlus.VoiceNext.Managers
{
    public sealed class AudioManager
    {
        private readonly OpusManager _opusManager;
        private readonly RtpManager _rtpManager;
        private readonly DiscordVoiceSessionDescriptionPayload _voiceSessionDescriptionPayload;
        private readonly uint _ssrc;
        private uint _timestamp;

        public AudioManager(OpusManager opusManager, RtpManager rtpManager, DiscordVoiceSessionDescriptionPayload voiceSessionDescriptionPayload, uint ssrc)
        {
            this._opusManager = opusManager;
            this._rtpManager = rtpManager;
            this._voiceSessionDescriptionPayload = voiceSessionDescriptionPayload;
            this._ssrc = ssrc;
        }

        public byte[] PrepareVoicePacket(byte[] audioData, byte[]? rtpHeader = null)
        {
            var encodedAudio = new byte[audioData.Length];
            _timestamp += this._opusManager.Encode(audioData, ref encodedAudio);

            var rtpPacket = new byte[this._rtpManager.EncryptionType switch
            {
                "xsalsa20_poly1305" => encodedAudio.Length + 12,
                "xsalsa20_poly1305_lite" => encodedAudio.Length + 12 + 4,
                "xsalsa20_poly1305_suffix" => encodedAudio.Length + 12 + 24,
                _ => throw new NotImplementedException($"Unknown encryption type: {this._rtpManager.EncryptionType}")
            }];
            var nonce = this._rtpManager.CreateRtpHeader(this._timestamp, this._ssrc, encodedAudio, rtpPacket);

            var encryptedAudio = new byte[encodedAudio.Length];
            SodiumManager.Encrypt(encodedAudio, this._voiceSessionDescriptionPayload.SecretKey, nonce, encryptedAudio);

            return rtpPacket;
        }
    }
}
