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
using System.Text;
using DSharpPlus.VoiceNext.JsonConverters;
using Newtonsoft.Json;

namespace DSharpPlus.VoiceNext.VoiceGateway.Entities
{
    [JsonConverter(typeof(DiscordIPDiscoveryConverter))]
    public sealed record DiscordIPDiscovery
    {
        public ushort Type { get; internal set; }
        public ushort Length { get; internal set; } = 70;
        public uint SSRC { get; internal set; }
        public string Address { get; internal set; } = null!;
        public ushort Port { get; internal set; }

        public static implicit operator DiscordIPDiscovery(Span<byte> ipDiscoverySpan) => new()
        {
            Type = BinaryPrimitives.ReadUInt16BigEndian(ipDiscoverySpan.Slice(0, 2)),
            Length = BinaryPrimitives.ReadUInt16BigEndian(ipDiscoverySpan.Slice(2, 2)),
            SSRC = BinaryPrimitives.ReadUInt32BigEndian(ipDiscoverySpan.Slice(4, 4)),
            Address = Encoding.UTF8.GetString(ipDiscoverySpan.Slice(8, 64).ToArray()),
            Port = BinaryPrimitives.ReadUInt16BigEndian(ipDiscoverySpan.Slice(72, 2))
        };

        public static implicit operator byte[](DiscordIPDiscovery ipDiscovery)
        {
            var addressBytes = Encoding.UTF8.GetBytes(ipDiscovery.Address + "\0");
            Span<byte> ipDiscoverySpan = stackalloc byte[74];

            BinaryPrimitives.WriteUInt16BigEndian(ipDiscoverySpan.Slice(0, 2), ipDiscovery.Type);
            BinaryPrimitives.WriteUInt16BigEndian(ipDiscoverySpan.Slice(2, 2), (ushort)(6 + addressBytes.Length));
            BinaryPrimitives.WriteUInt32BigEndian(ipDiscoverySpan.Slice(4, 4), ipDiscovery.SSRC);
            addressBytes.CopyTo(ipDiscoverySpan.Slice(8, addressBytes.Length));
            BinaryPrimitives.WriteUInt16BigEndian(ipDiscoverySpan.Slice(addressBytes.Length + 2, 2), ipDiscovery.Port);

            return ipDiscoverySpan.ToArray();
        }
    }
}
