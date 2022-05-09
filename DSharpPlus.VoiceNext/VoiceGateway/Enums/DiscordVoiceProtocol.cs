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

using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DSharpPlus.VoiceNext.VoiceGateway.Enums
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DiscordVoiceProtocol
    {
        /// <summary>
        /// The nonce bytes are the RTP header
        /// </summary>
        /// <remarks>
        /// Nonce implementation: Copy the RTP header
        /// </remarks>
        [EnumMember(Value = "xsalsa20_poly1305")]
        Poly1305Normal,

        /// <summary>
        /// The nonce bytes are 24 bytes appended to the payload of the RTP packet
        /// </summary>
        /// <remarks>
        /// Nonce implementation: Generate 24 random bytes
        /// </remarks>
        [EnumMember(Value = "xsalsa20_poly1305_suffix")]
        Poly1305Suffix,

        /// <summary>
        /// The nonce bytes are 4 bytes appended to the payload of the RTP packet
        /// </summary>
        /// <remarks>
        /// Nonce implementation: Incremental 4 bytes (32bit) int value
        /// </remarks>
        [EnumMember(Value = "xsalsa20_poly1305_lite")]
        Poly1305Lite,

        /// <summary>
        /// Undocumented.
        /// </summary>
        [EnumMember(Value = "xsalsa20_poly1305_lite_rtpsize")]
        Poly1305LiteRTPSize,

        /// <summary>
        /// Undocumented.
        /// </summary>
        [EnumMember(Value = "aead_aes256_gcm_rtpsize")]
        AES256GCM,

        /// <summary>
        /// Undocumented.
        /// </summary>
        [EnumMember(Value = "aead_aes256_gcm")]
        AES256GCMNoRTP
    }
}
