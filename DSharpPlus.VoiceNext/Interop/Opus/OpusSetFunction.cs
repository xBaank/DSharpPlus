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
    // grep -r -E "#define OPUS_SET_.*_REQUEST" opus/include/opus_defines.h
    /// <summary>
    /// Each enum member represents an Opus function to use with <see cref="OpusBindings"/> (opus_encoder_ctl/opus_decoder_ctl).
    /// </summary>
    internal enum OpusSetFunction
    {
        Application = 4000,
        Bitrate = 4002,
        MaxBandwidth = 4004,
        Vbr = 4006,
        Bandwidth = 4008,
        Complexity = 4010,
        InbandFec = 4012,
        PacketLossPerc = 4014,
        Dtx = 4016,
        VbrConstraint = 4020,
        ForceChannels = 4022,
        Signal = 4024,
        Gain = 4034,
        LsbDepth = 4036,
        ExpertFrameDuration = 4040,
        PredictionDisabled = 4042,
        PhaseInversionDisabled = 4046
    }
}
