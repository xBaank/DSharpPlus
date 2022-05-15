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

using DSharpPlus.VoiceNext.Interop.Opus;

namespace DSharpPlus.VoiceNext.Opus
{
    public sealed class OpusAudioFormat
    {
        /// <summary>
        /// 24,000Hz sample rate with stereo. The human hearing range is between roughly 20kHz (20,000Hz) and 20Hz.
        /// </summary>
        public static readonly OpusAudioFormat BestSpeed = new(24000, 2, (int)OpusSignal.Music);

        /// <summary>
        /// 48,000Hz sample rate with stereo.
        /// </summary>
        public static readonly OpusAudioFormat BestQuality = new(48000, 2, (int)OpusSignal.Music);

        /// <inheritdoc cref="BestQuality" />
        /// <remarks>
        /// An alias to <see cref="BestQuality"/>. Preserved for backwards compatibility.
        /// </remarks>
        public static OpusAudioFormat Default => BestQuality;

        /// <summary>
        /// The sample rate of the audio. The minimum should be 24,000Hz per the human hearing range.
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// 1 for mono, 2 for stereo.
        /// </summary>
        public int ChannelCount { get; }

        /// <summary>
        /// What type of audio to encode. Auto will lead to Opus determining the best signal type.
        /// </summary>
        public int Application { get; }

        public OpusAudioFormat(OpusSampleRate sampleRate = OpusSampleRate.FullBand, OpusChannelType channelType = OpusChannelType.Stereo, OpusSignal audioType = OpusSignal.Music) : this((int)sampleRate, (int)channelType, (int)audioType) { }

        public OpusAudioFormat(int sampleRate, int channels, int application)
        {
            this.SampleRate = sampleRate;
            this.ChannelCount = channels;
            this.Application = application;
        }

        public override string ToString() => $"{this.SampleRate}Hz {this.ChannelCount}-channel {this.Application}";
    }
}
