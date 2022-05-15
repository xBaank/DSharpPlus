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
using System.Buffers.Binary;
using DSharpPlus.VoiceNext.Interop.Sodium;

namespace DSharpPlus.VoiceNext.Managers
{
    public sealed class RtpManager
    {
        public readonly string EncryptionType;
        public uint IncrementalNonce { get; set; }
        public ushort SequenceNumber { get; set; }

        public RtpManager(string encryptionType)
        {
            this.EncryptionType = encryptionType switch
            {
                "xsalsa20_poly1305" => "xsalsa20_poly1305",
                "xsalsa20_poly1305_suffix" => "xsalsa20_poly1305_suffix",
                "xsalsa20_poly1305_lite" => throw new ArgumentException($"xsalsa20_poly1305_lite should be using the 'RtpManager(uint)' constructor."),
                _ => throw new NotImplementedException($"Unknown encryption type: {encryptionType}")
            };
        }

        public RtpManager(uint incrementalNonce)
        {
            this.EncryptionType = "xsalsa20_poly1305_lite";
            this.IncrementalNonce = incrementalNonce;
        }

        public byte[] GetNonce(byte[]? rtpHeader = null)
        {
            Span<byte> nonce = stackalloc byte[SodiumBindings.SodiumNonceSize];
            switch (this.EncryptionType)
            {
                case "xsalsa20_poly1305":
                    if (rtpHeader == null)
                        throw new ArgumentNullException(nameof(rtpHeader));
                    SodiumManager.GeneratePoly1305Nonce(rtpHeader, nonce);
                    break;
                case "xsalsa20_poly1305_lite":
                    // Prevent a stack overflow from longstanding connections.
                    this.IncrementalNonce = this.IncrementalNonce == uint.MaxValue ? 0 : this.IncrementalNonce + 1;
                    SodiumManager.GeneratePoly1305LiteNonce(this.IncrementalNonce, nonce);
                    break;
                case "xsalsa20_poly1305_suffix":
                    SodiumManager.GeneratePoly1305SuffixNonce(nonce);
                    break;
                default:
                    throw new NotImplementedException($"Unknown encryption type: {this.EncryptionType}");
            }
            return nonce.ToArray();
        }

        public byte[] CreateRtpHeader(uint timestamp, uint ssrc, Span<byte> encodedAudio, Span<byte> rtpPacket)
        {
            if (rtpPacket.Length != encodedAudio.Length + 12)
            {
                throw new ArgumentException($"{nameof(rtpPacket)}.Length must be {nameof(encodedAudio.Length)} + 12 bytes long.");
            }
            else if (this.SequenceNumber == ushort.MaxValue)
            {
                // Prevent stack overflow from longstanding connections.
                this.SequenceNumber = 0;
            }

            rtpPacket[0] = 0x80;
            rtpPacket[1] = 0x78;
            BinaryPrimitives.WriteUInt16BigEndian(rtpPacket.Slice(2, 2), this.SequenceNumber);
            BinaryPrimitives.WriteUInt32BigEndian(rtpPacket.Slice(4, 4), timestamp);
            BinaryPrimitives.WriteUInt32BigEndian(rtpPacket.Slice(8, 4), ssrc);
            encodedAudio.CopyTo(rtpPacket.Slice(12));

            switch (this.EncryptionType)
            {
                case "xsalsa20_poly1305":
                    return rtpPacket.Slice(12).ToArray();
                case "xsalsa20_poly1305_lite":
                    if (rtpPacket.Length != 12 + encodedAudio.Length + 4)
                    {
                        throw new ArgumentException($"{nameof(rtpPacket)}.Length must be 12 + {nameof(encodedAudio.Length)} + 4 bytes long due to the appended 4 bytes from the xsalsa20_poly1305_lite encryption.", nameof(rtpPacket));
                    }
                    var nonce = this.GetNonce();
                    nonce.CopyTo(rtpPacket.Slice(rtpPacket.Length - 4, 4));
                    return nonce;
                case "xsalsa20_poly1305_suffix":
                    if (rtpPacket.Length != 12 + encodedAudio.Length + 24)
                    {
                        throw new ArgumentException($"{nameof(rtpPacket)}.Length must be 12 + {nameof(encodedAudio.Length)} + 24 bytes long due to the appended 24 bytes from the xsalsa20_poly1305_suffix encryption.", nameof(rtpPacket));
                    }
                    nonce = this.GetNonce();
                    nonce.CopyTo(rtpPacket.Slice(rtpPacket.Length - 24, 24));
                    return nonce;
                default:
                    throw new NotImplementedException($"Unknown encryption type: {this.EncryptionType}");
            }
        }
    }
}
