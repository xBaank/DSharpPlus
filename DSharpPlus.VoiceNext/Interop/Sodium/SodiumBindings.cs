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
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DSharpPlus.VoiceNext.Interop.Sodium
{
    public sealed class SodiumBindings
    {
        /// <summary>
        /// Gets the Sodium key size for xsalsa20_poly1305 algorithm.
        /// </summary>
        public static int SodiumKeySize { get; } = (int)crypto_secretbox_xsalsa20poly1305_keybytes();

        /// <summary>
        /// Gets the Sodium nonce size for xsalsa20_poly1305 algorithm.
        /// </summary>
        public static int SodiumNonceSize { get; } = (int)crypto_secretbox_xsalsa20poly1305_noncebytes();

        /// <summary>
        /// Gets the Sodium MAC size for xsalsa20_poly1305 algorithm.
        /// </summary>
        public static int SodiumMacSize { get; } = (int)crypto_secretbox_xsalsa20poly1305_macbytes();

        // Used for properties
        [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr crypto_secretbox_xsalsa20poly1305_keybytes();

        [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr crypto_secretbox_xsalsa20poly1305_noncebytes();

        [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr crypto_secretbox_xsalsa20poly1305_macbytes();

        // Used for encryption
        [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int crypto_secretbox_easy(byte* buffer, byte* message, ulong messageLength, byte* nonce, byte* key);

        [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int crypto_secretbox_open_easy(byte* buffer, byte* encryptedMessage, ulong encryptedLength, byte* nonce, byte* key);

        /// <summary>
        /// Encrypts supplied buffer using xsalsa20_poly1305 algorithm, using supplied key and nonce to perform encryption.
        /// </summary>
        /// <param name="source">Contents to encrypt.</param>
        /// <param name="target">Buffer to encrypt to.</param>
        /// <param name="key">Key to use for encryption.</param>
        /// <param name="nonce">Nonce to use for encryption.</param>
        /// <returns>Encryption status.</returns>
        public static unsafe void Encrypt(ReadOnlySpan<byte> source, Span<byte> target, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce)
        {
            var status = 0;
            fixed (byte* sourcePtr = &source.GetPinnableReference())
            fixed (byte* targetPtr = &target.GetPinnableReference())
            fixed (byte* keyPtr = &key.GetPinnableReference())
            fixed (byte* noncePtr = &nonce.GetPinnableReference())
                status = crypto_secretbox_easy(targetPtr, sourcePtr, (ulong)source.Length, noncePtr, keyPtr);

            // https://stackoverflow.com/questions/42918869/does-libsodium-have-error-codes
            if (status != 0)
                throw new CryptographicException("Failed to encrypt buffer for unknown reasons.");
        }

        /// <summary>
        /// Decrypts supplied buffer using xsalsa20_poly1305 algorithm, using supplied key and nonce to perform decryption.
        /// </summary>
        /// <param name="source">Buffer to decrypt from.</param>
        /// <param name="target">Decrypted message buffer.</param>
        /// <param name="key">Key to use for decryption.</param>
        /// <param name="nonce">Nonce to use for decryption.</param>
        /// <returns>Decryption status.</returns>
        public static unsafe void Decrypt(ReadOnlySpan<byte> source, Span<byte> target, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce)
        {
            var status = 0;
            fixed (byte* sourcePtr = &source.GetPinnableReference())
            fixed (byte* targetPtr = &target.GetPinnableReference())
            fixed (byte* keyPtr = &key.GetPinnableReference())
            fixed (byte* noncePtr = &nonce.GetPinnableReference())
                status = crypto_secretbox_open_easy(targetPtr, sourcePtr, (ulong)source.Length, noncePtr, keyPtr);

            // https://stackoverflow.com/questions/42918869/does-libsodium-have-error-codes
            if (status != 0)
                throw new CryptographicException("Failed to decrypt buffer for unknown reasons.");
        }
    }
}
