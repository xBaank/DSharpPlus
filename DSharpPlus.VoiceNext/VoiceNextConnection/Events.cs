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

using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext.EventArgs;
using Emzi0767.Utilities;

namespace DSharpPlus.VoiceNext
{
    public sealed partial class VoiceNextConnection
    {
        public event AsyncEventHandler<VoiceNextConnection, UserSpeakingEventArgs> UserJoined { add => this._userJoined.Register(value); remove => this._userJoined.Unregister(value); }
        private readonly AsyncEvent<VoiceNextConnection, UserSpeakingEventArgs> _userJoined;

        public event AsyncEventHandler<VoiceNextConnection, UserSpeakingEventArgs> UserLeft { add => this._userLeft.Register(value); remove => this._userLeft.Unregister(value); }
        private readonly AsyncEvent<VoiceNextConnection, UserSpeakingEventArgs> _userLeft;

        public event AsyncEventHandler<VoiceNextConnection, UserSpeakingEventArgs> VoiceReceived { add => this._voiceReceived.Register(value); remove => this._voiceReceived.Unregister(value); }
        private readonly AsyncEvent<VoiceNextConnection, UserSpeakingEventArgs> _voiceReceived;

        public event AsyncEventHandler<VoiceNextConnection, VoiceReadyEventArgs> VoiceReady { add => this._voiceReady.Register(value); remove => this._voiceReady.Unregister(value); }
        private readonly AsyncEvent<VoiceNextConnection, VoiceReadyEventArgs> _voiceReady;

        public event AsyncEventHandler<VoiceNextConnection, SocketErrorEventArgs> WebsocketError { add => this._websocketError.Register(value); remove => this._websocketError.Unregister(value); }
        private readonly AsyncEvent<VoiceNextConnection, SocketErrorEventArgs> _websocketError;
    }
}
