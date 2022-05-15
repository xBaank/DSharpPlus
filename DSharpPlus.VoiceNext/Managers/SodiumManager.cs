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
using System.Security.Cryptography;
using DSharpPlus.VoiceNext.Interop.Sodium;

namespace DSharpPlus.VoiceNext.Managers
{
    public sealed class SodiumManager
    {
        public static readonly RNGCryptoServiceProvider CryptoServiceProvider = new();

        public static void Encrypt(ReadOnlySpan<byte> source, ReadOnlySpan<byte> key, Span<byte> nonce, Span<byte> target)
        {
            if (nonce.Length != SodiumBindings.SodiumNonceSize)
                throw new ArgumentException("Nonce must be of length " + SodiumBindings.SodiumNonceSize, nameof(nonce));
            else if (target.Length != SodiumBindings.SodiumMacSize + source.Length)
                throw new ArgumentException("Target must be of length " + (SodiumBindings.SodiumMacSize + source.Length), nameof(target));

            SodiumBindings.Encrypt(source, target, key, nonce);
        }

        public static void Decrypt(ReadOnlySpan<byte> source, ReadOnlySpan<byte> key, Span<byte> nonce, Span<byte> target)
        {
            if (nonce.Length != SodiumBindings.SodiumNonceSize)
                throw new ArgumentException("Nonce must be of length " + SodiumBindings.SodiumNonceSize, nameof(nonce));
            else if (target.Length != source.Length - SodiumBindings.SodiumMacSize)
                throw new ArgumentException("Target must be of length " + (source.Length - SodiumBindings.SodiumMacSize), nameof(target));

            SodiumBindings.Decrypt(source, target, key, nonce);
        }

        public static void GeneratePoly1305Nonce(byte[] rtpHeader, Span<byte> nonce)
        {
            if (rtpHeader.Length != 12)
                throw new ArgumentException("RTP header must be 12 bytes long.");
            else if (nonce.Length != SodiumBindings.SodiumNonceSize)
                throw new ArgumentException("Nonce must be of length " + SodiumBindings.SodiumNonceSize, nameof(nonce));

            // Copy the RTP Header to the beginning of the nonce
            rtpHeader.CopyTo(nonce.Slice(nonce.Length - 12));
        }

        public static void GeneratePoly1305LiteNonce(uint nonce, Span<byte> incrementedNonce)
        {
            if (incrementedNonce.Length != SodiumBindings.SodiumNonceSize)
                throw new ArgumentException("Incremented nonce must be a length of " + SodiumBindings.SodiumNonceSize, nameof(incrementedNonce));

            // Write the uint to memory
            BinaryPrimitives.WriteUInt32BigEndian(incrementedNonce.Slice(incrementedNonce.Length - 4), nonce);
        }

        public static void GeneratePoly1305SuffixNonce(Span<byte> nonce)
        {
            if (nonce.Length != SodiumBindings.SodiumNonceSize)
                throw new ArgumentException("Nonce must be of length " + SodiumBindings.SodiumNonceSize, nameof(nonce));

            var buffer = new byte[nonce.Length];
            CryptoServiceProvider.GetBytes(buffer);
            buffer.CopyTo(nonce);
        }
    }
}
