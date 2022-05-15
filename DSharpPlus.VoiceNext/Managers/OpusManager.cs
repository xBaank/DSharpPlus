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
using DSharpPlus.VoiceNext.Interop.Opus;
using DSharpPlus.VoiceNext.Opus;

namespace DSharpPlus.VoiceNext.Managers
{
    public sealed class OpusManager : IDisposable
    {
        private readonly OpusAudioFormat _opusAudioFormat;
        private readonly IntPtr _encoderPtr;
        private OpusAudioFormat _decoderAudioFormat;
        private IntPtr _decoderPtr;
        private bool _disposed;

        public OpusManager(OpusAudioFormat opusAudioFormat)
        {
            this._opusAudioFormat = opusAudioFormat;
            this._decoderAudioFormat = opusAudioFormat;
            this._encoderPtr = OpusBindings.CreateEncoder(opusAudioFormat.SampleRate, opusAudioFormat.ChannelCount, opusAudioFormat.Application);
            this._decoderPtr = OpusBindings.CreateDecoder(opusAudioFormat.SampleRate, opusAudioFormat.ChannelCount);
        }

        public uint Encode(byte[] pcm, ref byte[] encodedPcm) => this.Encode(pcm, ref encodedPcm);

        public uint Encode(ReadOnlySpan<byte> pcm, ref Span<byte> encodedPcm)
        {
            if (this._disposed)
                throw new ObjectDisposedException(nameof(OpusManager));
            else if (pcm.Length != encodedPcm.Length)
                throw new ArgumentException("PCM and Opus buffer lengths need to be equal.", nameof(encodedPcm));

            var packetMetrics = OpusBindings.GetPacketMetrics(pcm, this._opusAudioFormat.SampleRate);
            OpusBindings.Encode(this._encoderPtr, pcm, packetMetrics.FrameSize, ref encodedPcm);
            return (uint)OpusBindings.GetPacketMetrics(encodedPcm, this._opusAudioFormat.SampleRate).FrameSize;
        }

        public void Decode(ReadOnlySpan<byte> encodedPcm, ref Span<byte> pcm, bool useFec, out OpusAudioFormat opusAudioFormat)
        {
            if (this._disposed)
                throw new ObjectDisposedException(nameof(OpusManager));
            else if (encodedPcm.Length != pcm.Length)
                throw new ArgumentException("PCM and Opus buffer lengths need to be equal.", nameof(pcm));

            // This method uses it's own audio format property due to the encoded pcm audio data format could change between any invocations.
            // Think of it as a cache. Never null due to initialization in the constructor.
            var packetMetrics = OpusBindings.GetPacketMetrics(encodedPcm, this._decoderAudioFormat.SampleRate);
            opusAudioFormat = this._decoderAudioFormat.ChannelCount != packetMetrics.ChannelCount ? new OpusAudioFormat(this._decoderAudioFormat.SampleRate, packetMetrics.ChannelCount, this._decoderAudioFormat.Application) : this._decoderAudioFormat;

            if (this._decoderAudioFormat.ChannelCount != packetMetrics.ChannelCount)
            {
                OpusBindings.DestroyDecoder(this._decoderPtr);
                this._decoderAudioFormat = opusAudioFormat;
                this._decoderPtr = OpusBindings.CreateDecoder(opusAudioFormat.SampleRate, opusAudioFormat.ChannelCount);
            }

            var sampleCount = OpusBindings.Decode(this._decoderPtr, encodedPcm, packetMetrics.FrameSize, pcm, useFec);
            var sampleSize = sampleCount * this._decoderAudioFormat.ChannelCount * 2; // 2 is the size of ushort
            pcm.Slice(0, sampleSize);
        }

        ~OpusManager()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose();
        }

        public void Dispose()
        {
            if (!this._disposed)
            {
                OpusBindings.DestroyEncoder(this._encoderPtr);
                OpusBindings.DestroyDecoder(this._decoderPtr);
                this._disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
