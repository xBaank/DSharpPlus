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

namespace DSharpPlus.VoiceNext.Interop.Opus
{
    // grep -r -E "#define OPUS_GET_.*_REQUEST" opus/include/opus_defines.h
    /// <summary>
    /// Each enum member represents an Opus function to use with <see cref="OpusBindings"/> (opus_decoder_ctl).
    /// </summary>
    internal enum OpusGetFunction
    {
        Application = 4001,
        Bitrate = 4003,
        MaxBandwidth = 4005,
        Vbr = 4007,
        Bandwidth = 4009,
        Complexity = 4011,
        InbandFec = 4013,
        PacketLossPerc = 4015,
        Dtx = 4017,
        VbrConstraint = 4021,
        ForceChannels = 4023,
        Signal = 4025,
        Lookahead = 4027,
        SampleRate = 4029,
        FinalRange = 4031,
        Pitch = 4033,
        /// <remark>
        /// Should have been 4035.
        /// </remark>
        Gain = 4045,
        LsbDepth = 4037,
        LastPacketDuration = 4039,
        ExpertFrameDuration = 4041,
        PredictionDisabled = 4043,
        PhaseInversionDisabled = 4047,
        InDtx = 4049
    }
}
