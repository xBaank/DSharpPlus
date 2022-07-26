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
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Net;
using DSharpPlus.Net.Serialization;
using DSharpPlus.Net.Udp;
using DSharpPlus.Net.WebSocket;
using DSharpPlus.VoiceNext.Codec;
using DSharpPlus.VoiceNext.Entities;
using DSharpPlus.VoiceNext.EventArgs;
using Emzi0767.Utilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DSharpPlus.VoiceNext
{
    public delegate Task VoiceDisconnectedEventHandler(DiscordGuild guild);

    /// <summary>
    ///     VoiceNext connection to a voice channel.
    /// </summary>
    public sealed class VoiceNextConnection : IDisposable
    {
        private readonly AsyncEvent<VoiceNextConnection, VoiceUserLeaveEventArgs> _userLeft;
        private readonly AsyncEvent<VoiceNextConnection, UserSpeakingEventArgs> _userSpeaking;
        private readonly AsyncEvent<VoiceNextConnection, VoiceReceiveEventArgs> _voiceReceived;
        private readonly AsyncEvent<VoiceNextConnection, SocketErrorEventArgs> _voiceSocketError;

        private volatile bool _isSpeaking;
        private ulong _lastKeepalive;

        private int _queueCount;
        private int _udpPing;
        private int _wsPing;

        internal VoiceNextConnection(DiscordClient client, DiscordGuild guild, DiscordChannel channel, VoiceNextConfiguration config, VoiceServerUpdatePayload server, VoiceStateUpdatePayload state)
        {
            Discord = client;
            Guild = guild;
            TargetChannel = channel;
            TransmittingSSRCs = new ConcurrentDictionary<uint, AudioSender>();

            _userSpeaking = new AsyncEvent<VoiceNextConnection, UserSpeakingEventArgs>("VNEXT_USER_SPEAKING", TimeSpan.Zero, Discord.EventErrorHandler);
            _userLeft = new AsyncEvent<VoiceNextConnection, VoiceUserLeaveEventArgs>("VNEXT_USER_LEFT", TimeSpan.Zero, Discord.EventErrorHandler);
            _voiceReceived = new AsyncEvent<VoiceNextConnection, VoiceReceiveEventArgs>("VNEXT_VOICE_RECEIVED", TimeSpan.Zero, Discord.EventErrorHandler);
            _voiceSocketError = new AsyncEvent<VoiceNextConnection, SocketErrorEventArgs>("VNEXT_WS_ERROR", TimeSpan.Zero, Discord.EventErrorHandler);
            TokenSource = new CancellationTokenSource();

            Configuration = config;
            Opus = new Opus(AudioFormat);
            //this.Sodium = new Sodium();
            Rtp = new Rtp();

            ServerData = server;
            StateData = state;

            var eps = ServerData.Endpoint;
            var epi = eps.LastIndexOf(':');
            var eph = string.Empty;
            var epp = 443;
            if (epi != -1)
            {
                eph = eps.Substring(0, epi);
                epp = int.Parse(eps.Substring(epi + 1));
            }
            else
                eph = eps;
            WebSocketEndpoint = new ConnectionEndpoint { Hostname = eph, Port = epp };

            ReadyWait = new TaskCompletionSource<bool>();
            IsInitialized = false;
            IsDisposed = false;

            PlayingWait = null;
            TransmitChannel = Channel.CreateBounded<RawVoicePacket>(new BoundedChannelOptions(Configuration.PacketQueueSize));
            KeepaliveTimestamps = new ConcurrentDictionary<ulong, long>();
            PauseEvent = new AsyncManualResetEvent(true);

            UdpClient = Discord.Configuration.UdpClientFactory();
            VoiceWs = Discord.Configuration.WebSocketClientFactory(Discord.Configuration.Proxy);
            VoiceWs.Disconnected += VoiceWS_SocketClosed;
            VoiceWs.MessageReceived += VoiceWS_SocketMessage;
            VoiceWs.Connected += VoiceWS_SocketOpened;
            VoiceWs.ExceptionThrown += VoiceWs_SocketException;
        }

        public bool IsConnected => !IsDisposed;

        private static DateTimeOffset UnixEpoch { get; } = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private DiscordClient Discord { get; }

        private DiscordGuild Guild { get; }

        private ConcurrentDictionary<uint, AudioSender> TransmittingSSRCs { get; }

        private BaseUdpClient UdpClient { get; set; }

        private IWebSocketClient VoiceWs { get; set; }

        private Task HeartbeatTask { get; set; }

        private int HeartbeatInterval { get; set; }

        private DateTimeOffset LastHeartbeat { get; set; }

        private CancellationTokenSource TokenSource { get; set; }

        private CancellationToken Token
            => TokenSource.Token;

        internal VoiceServerUpdatePayload ServerData { get; set; }

        internal VoiceStateUpdatePayload StateData { get; set; }

        internal bool Resume { get; set; }

        private VoiceNextConfiguration Configuration { get; }

        private Opus Opus { get; set; }

        private Sodium Sodium { get; set; }

        private Rtp Rtp { get; set; }

        private EncryptionMode SelectedEncryptionMode { get; set; }

        private uint Nonce { get; set; }

        private ushort Sequence { get; set; }

        private uint Timestamp { get; set; }

        private uint SSRC { get; set; }

        private byte[] Key { get; set; }

        private IpEndpoint DiscoveredEndpoint { get; set; }

        internal ConnectionEndpoint WebSocketEndpoint { get; set; }

        internal ConnectionEndpoint UdpEndpoint { get; set; }

        private TaskCompletionSource<bool> ReadyWait { get; set; }

        private bool IsInitialized { get; set; }

        private bool IsDisposed { get; set; }

        private TaskCompletionSource<bool> PlayingWait { get; set; }

        private AsyncManualResetEvent PauseEvent { get; }

        private VoiceTransmitSink TransmitStream { get; set; }

        private Channel<RawVoicePacket> TransmitChannel { get; }

        private ConcurrentDictionary<ulong, long> KeepaliveTimestamps { get; }

        private Task SenderTask { get; set; }

        private CancellationTokenSource SenderTokenSource { get; set; }

        private CancellationToken SenderToken
            => SenderTokenSource.Token;

        private Task ReceiverTask { get; set; }

        private CancellationTokenSource ReceiverTokenSource { get; set; }

        private CancellationToken ReceiverToken
            => ReceiverTokenSource.Token;

        private Task KeepaliveTask { get; set; }

        private CancellationTokenSource KeepaliveTokenSource { get; set; }

        private CancellationToken KeepaliveToken
            => KeepaliveTokenSource.Token;

        /// <summary>
        ///     Gets the audio format used by the Opus encoder.
        /// </summary>
        public AudioFormat AudioFormat => Configuration.AudioFormat;

        /// <summary>
        ///     Gets whether this connection is still playing audio.
        /// </summary>
        public bool IsPlaying
            => PlayingWait != null && !PlayingWait.Task.IsCompleted;

        /// <summary>
        ///     Gets the websocket round-trip time in ms.
        /// </summary>
        public int WebSocketPing
            => Volatile.Read(ref _wsPing);

        /// <summary>
        ///     Gets the UDP round-trip time in ms.
        /// </summary>
        public int UdpPing
            => Volatile.Read(ref _udpPing);

        /// <summary>
        ///     Gets the channel this voice client is connected to.
        /// </summary>
        public DiscordChannel TargetChannel { get; internal set; }

        /// <summary>
        ///     Disconnects and disposes this voice connection.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            IsInitialized = false;
            TokenSource.Cancel();
            SenderTokenSource.Cancel();
            ReceiverTokenSource?.Cancel();
            KeepaliveTokenSource.Cancel();

            TokenSource.Dispose();
            SenderTokenSource.Dispose();
            ReceiverTokenSource?.Dispose();
            KeepaliveTokenSource.Dispose();

            try
            {
                VoiceWs.DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                UdpClient.Close();
            }
            catch { }

            Opus?.Dispose();
            Opus = null;
            Sodium?.Dispose();
            Sodium = null;
            Rtp?.Dispose();
            Rtp = null;

            VoiceDisconnected?.Invoke(Guild);
        }

        /// <summary>
        ///     Triggered whenever a user speaks in the connected voice channel.
        /// </summary>
        public event AsyncEventHandler<VoiceNextConnection, UserSpeakingEventArgs> UserSpeaking
        {
            add => _userSpeaking.Register(value);
            remove => _userSpeaking.Unregister(value);
        }

        /// <summary>
        ///     Triggered whenever a user leaves voice in the connected guild.
        /// </summary>
        public event AsyncEventHandler<VoiceNextConnection, VoiceUserLeaveEventArgs> UserLeft
        {
            add => _userLeft.Register(value);
            remove => _userLeft.Unregister(value);
        }

        /// <summary>
        ///     Triggered whenever voice data is received from the connected voice channel.
        /// </summary>
        public event AsyncEventHandler<VoiceNextConnection, VoiceReceiveEventArgs> VoiceReceived
        {
            add => _voiceReceived.Register(value);
            remove => _voiceReceived.Unregister(value);
        }

        /// <summary>
        ///     Triggered whenever voice WebSocket throws an exception.
        /// </summary>
        public event AsyncEventHandler<VoiceNextConnection, SocketErrorEventArgs> VoiceSocketErrored
        {
            add => _voiceSocketError.Register(value);
            remove => _voiceSocketError.Unregister(value);
        }

        public event VoiceDisconnectedEventHandler VoiceDisconnected;

        ~VoiceNextConnection()
        {
            Dispose();
        }

        /// <summary>
        ///     Connects to the specified voice channel.
        /// </summary>
        /// <returns>A task representing the connection operation.</returns>
        internal Task ConnectAsync()
        {
            var gwuri = new UriBuilder { Scheme = "wss", Host = WebSocketEndpoint.Hostname, Query = "encoding=json&v=4" };

            return VoiceWs.ConnectAsync(gwuri.Uri);
        }

        internal Task ReconnectAsync() => VoiceWs.DisconnectAsync();

        internal async Task StartAsync()
        {
            // Let's announce our intentions to the server
            var vdp = new VoiceDispatch();

            if (!Resume)
            {
                vdp.OpCode = 0;
                vdp.Payload = new VoiceIdentifyPayload
                {
                    ServerId = ServerData.GuildId, UserId = StateData.UserId.Value, SessionId = StateData.SessionId, Token = ServerData.Token,
                };
                Resume = true;
            }
            else
            {
                vdp.OpCode = 7;
                vdp.Payload = new VoiceIdentifyPayload { ServerId = ServerData.GuildId, SessionId = StateData.SessionId, Token = ServerData.Token };
            }
            var vdj = JsonConvert.SerializeObject(vdp, Formatting.None);
            await WsSendAsync(vdj).ConfigureAwait(false);
        }

        internal Task WaitForReadyAsync()
            => ReadyWait.Task;

        internal async Task EnqueuePacketAsync(RawVoicePacket packet, CancellationToken token = default)
        {
            await TransmitChannel.Writer.WriteAsync(packet, token).ConfigureAwait(false);
            _queueCount++;
        }

        internal bool PreparePacket(ReadOnlySpan<byte> pcm, out byte[] target, out int length, bool isOpusPacket)
        {
            target = null;
            length = 0;

            if (IsDisposed)
                return false;

            var audioFormat = AudioFormat;

            var packetArray = ArrayPool<byte>.Shared.Rent(Rtp.CalculatePacketSize(audioFormat.SampleCountToSampleSize(audioFormat.CalculateMaximumFrameSize()), SelectedEncryptionMode));
            var packet = packetArray.AsSpan();

            Rtp.EncodeHeader(Sequence, Timestamp, SSRC, packet);
            var opus = packet.Slice(Rtp.HeaderSize, pcm.Length);

            if (!isOpusPacket)
                Opus.Encode(pcm, ref opus);
            else
                pcm.CopyTo(opus);

            //Todo calculate for opus Packets

            Sequence++;
            Timestamp += isOpusPacket ? 960 : (uint)audioFormat.CalculateFrameSize(audioFormat.CalculateSampleDuration(pcm.Length));

            Span<byte> nonce = stackalloc byte[Sodium.NonceSize];
            switch (SelectedEncryptionMode)
            {
                case EncryptionMode.XSalsa20_Poly1305:
                    Sodium.GenerateNonce(packet.Slice(0, Rtp.HeaderSize), nonce);
                    break;

                case EncryptionMode.XSalsa20_Poly1305_Suffix:
                    Sodium.GenerateNonce(nonce);
                    break;

                case EncryptionMode.XSalsa20_Poly1305_Lite:
                    Sodium.GenerateNonce(Nonce++, nonce);
                    break;

                default:
                    ArrayPool<byte>.Shared.Return(packetArray);
                    throw new Exception("Unsupported encryption mode.");
            }

            Span<byte> encrypted = stackalloc byte[Sodium.CalculateTargetSize(opus)];
            Sodium.Encrypt(opus, encrypted, nonce);
            encrypted.CopyTo(packet.Slice(Rtp.HeaderSize));
            packet = packet.Slice(0, Rtp.CalculatePacketSize(encrypted.Length, SelectedEncryptionMode));
            Sodium.AppendNonce(nonce, packet, SelectedEncryptionMode);


            target = packetArray;
            length = packet.Length;
            return true;
        }

        private async Task VoiceSenderTask()
        {
            var token = SenderToken;
            var client = UdpClient;
            var reader = TransmitChannel.Reader;

            byte[] data = null;
            var length = 0;

            var synchronizerTicks = (double)Stopwatch.GetTimestamp();
            var synchronizerResolution = Stopwatch.Frequency * 0.005;
            var tickResolution = 10_000_000.0 / Stopwatch.Frequency;
            Discord.Logger.LogDebug(VoiceNextEvents.Misc, "Timer accuracy: {Frequency}/{Resolution} (high resolution? {IsHighRes})", Stopwatch.Frequency, synchronizerResolution, Stopwatch.IsHighResolution);

            while (!token.IsCancellationRequested)
            {
                await PauseEvent.WaitAsync().ConfigureAwait(false);

                var hasPacket = reader.TryRead(out var rawPacket);
                if (hasPacket)
                {
                    _queueCount--;

                    if (PlayingWait == null || PlayingWait.Task.IsCompleted)
                        PlayingWait = new TaskCompletionSource<bool>();
                }

                // Provided by Laura#0090 (214796473689178133); this is Python, but adaptable:
                //
                // delay = max(0, self.delay + ((start_time + self.delay * loops) + - time.time()))
                //
                // self.delay
                //   sample size
                // start_time
                //   time since streaming started
                // loops
                //   number of samples sent
                // time.time()
                //   DateTime.Now

                if (hasPacket)
                {
                    hasPacket = PreparePacket(rawPacket.Bytes.Span, out data, out length, rawPacket.IsOpusPacket);
                    if (rawPacket.RentedBuffer != null)
                        ArrayPool<byte>.Shared.Return(rawPacket.RentedBuffer);
                }

                var durationModifier = hasPacket ? rawPacket.Duration / 5 : 4;
                var cts = Math.Max(Stopwatch.GetTimestamp() - synchronizerTicks, 0);
                if (cts < synchronizerResolution * durationModifier)
                    await Task.Delay(TimeSpan.FromTicks((long)((synchronizerResolution * durationModifier - cts) * tickResolution))).ConfigureAwait(false);

                synchronizerTicks += synchronizerResolution * durationModifier;

                if (!hasPacket)
                    continue;

                await SendSpeakingAsync().ConfigureAwait(false);
                await client.SendAsync(data, length).ConfigureAwait(false);
                ArrayPool<byte>.Shared.Return(data);

                if (!rawPacket.Silence && _queueCount == 0)
                {
                    var nullpcm = new byte[AudioFormat.CalculateSampleSize(20)];
                    for (var i = 0; i < 3; i++)
                    {
                        var nullpacket = new byte[nullpcm.Length];
                        var nullpacketmem = nullpacket.AsMemory();
                        await EnqueuePacketAsync(new RawVoicePacket(nullpacketmem, 20, true)).ConfigureAwait(false);
                    }
                }
                else if (_queueCount == 0)
                {
                    await SendSpeakingAsync(false).ConfigureAwait(false);
                    PlayingWait?.SetResult(true);
                }
            }
        }

        private bool ProcessPacket(ReadOnlySpan<byte> data, ref Memory<byte> opus, ref Memory<byte> pcm, IList<ReadOnlyMemory<byte>> pcmPackets, out AudioSender voiceSender, out AudioFormat outputFormat)
        {
            voiceSender = null;
            outputFormat = default;

            if (!Rtp.IsRtpHeader(data))
                return false;

            Rtp.DecodeHeader(data, out var sequence, out var timestamp, out var ssrc, out var hasExtension);

            if (!TransmittingSSRCs.TryGetValue(ssrc, out var vtx))
            {
                var decoder = Opus.CreateDecoder();

                vtx = new AudioSender(ssrc, decoder)
                {
                    // user isn't present as we haven't received a speaking event yet.
                    User = null,
                };
            }

            voiceSender = vtx;
            if (sequence <= vtx.LastSequence) // out-of-order packet; discard
                return false;
            var gap = vtx.LastSequence != 0 ? sequence - 1 - vtx.LastSequence : 0;

            if (gap >= 5)
                Discord.Logger.LogWarning(VoiceNextEvents.VoiceReceiveFailure, "5 or more voice packets were dropped when receiving");

            Span<byte> nonce = stackalloc byte[Sodium.NonceSize];
            Sodium.GetNonce(data, nonce, SelectedEncryptionMode);
            Rtp.GetDataFromPacket(data, out var encryptedOpus, SelectedEncryptionMode);

            var opusSize = Sodium.CalculateSourceSize(encryptedOpus);
            opus = opus.Slice(0, opusSize);
            var opusSpan = opus.Span;
            try
            {
                Sodium.Decrypt(encryptedOpus, opusSpan, nonce);

                // Strip extensions, if any
                if (hasExtension)
                    // RFC 5285, 4.2 One-Byte header
                    // http://www.rfcreader.com/#rfc5285_line186
                    if (opusSpan[0] == 0xBE && opusSpan[1] == 0xDE)
                    {
                        var headerLen = opusSpan[2] << 8 | opusSpan[3];
                        var i = 4;
                        for (; i < headerLen + 4; i++)
                        {
                            var @byte = opusSpan[i];

                            // ID is currently unused since we skip it anyway
                            //var id = (byte)(@byte >> 4);
                            var length = (byte)(@byte & 0x0F) + 1;

                            i += length;
                        }

                        // Strip extension padding too
                        while (opusSpan[i] == 0)
                            i++;

                        opusSpan = opusSpan.Slice(i);
                    }

                // TODO: consider implementing RFC 5285, 4.3. Two-Byte Header

                if (opusSpan[0] == 0x90)
                    // I'm not 100% sure what this header is/does, however removing the data causes no
                    // real issues, and has the added benefit of removing a lot of noise.
                    opusSpan = opusSpan.Slice(2);

                if (gap == 1)
                {
                    var lastSampleCount = Opus.GetLastPacketSampleCount(vtx.Decoder);
                    var fecpcm = new byte[AudioFormat.SampleCountToSampleSize(lastSampleCount)];
                    var fecpcmMem = fecpcm.AsSpan();
                    Opus.Decode(vtx.Decoder, opusSpan, ref fecpcmMem, true, out _);
                    pcmPackets.Add(fecpcm.AsMemory(0, fecpcmMem.Length));
                }
                else if (gap > 1)
                {
                    var lastSampleCount = Opus.GetLastPacketSampleCount(vtx.Decoder);
                    for (var i = 0; i < gap; i++)
                    {
                        var fecpcm = new byte[AudioFormat.SampleCountToSampleSize(lastSampleCount)];
                        var fecpcmMem = fecpcm.AsSpan();
                        Opus.ProcessPacketLoss(vtx.Decoder, lastSampleCount, ref fecpcmMem);
                        pcmPackets.Add(fecpcm.AsMemory(0, fecpcmMem.Length));
                    }
                }

                var pcmSpan = pcm.Span;
                Opus.Decode(vtx.Decoder, opusSpan, ref pcmSpan, false, out outputFormat);
                pcm = pcm.Slice(0, pcmSpan.Length);
            }
            finally
            {
                vtx.LastSequence = sequence;
            }

            return true;
        }

        private async Task ProcessVoicePacket(byte[] data)
        {
            if (data.Length < 13) // minimum packet length
                return;

            try
            {
                var pcm = new byte[AudioFormat.CalculateMaximumFrameSize()];
                var pcmMem = pcm.AsMemory();
                var opus = new byte[pcm.Length];
                var opusMem = opus.AsMemory();
                var pcmFillers = new List<ReadOnlyMemory<byte>>();
                if (!ProcessPacket(data, ref opusMem, ref pcmMem, pcmFillers, out var vtx, out var audioFormat))
                    return;

                foreach (var pcmFiller in pcmFillers)
                    await _voiceReceived.InvokeAsync(this, new VoiceReceiveEventArgs
                    {
                        SSRC = vtx.SSRC,
                        User = vtx.User,
                        PcmData = pcmFiller,
                        OpusData = new byte[0].AsMemory(),
                        AudioFormat = audioFormat,
                        AudioDuration = audioFormat.CalculateSampleDuration(pcmFiller.Length),
                    }).ConfigureAwait(false);

                await _voiceReceived.InvokeAsync(this, new VoiceReceiveEventArgs
                {
                    SSRC = vtx.SSRC,
                    User = vtx.User,
                    PcmData = pcmMem,
                    OpusData = opusMem,
                    AudioFormat = audioFormat,
                    AudioDuration = audioFormat.CalculateSampleDuration(pcmMem.Length),
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Discord.Logger.LogError(VoiceNextEvents.VoiceReceiveFailure, ex, "Exception occurred when decoding incoming audio data");
            }
        }

        private void ProcessKeepalive(byte[] data)
        {
            try
            {
                var keepalive = BinaryPrimitives.ReadUInt64LittleEndian(data);

                if (!KeepaliveTimestamps.TryRemove(keepalive, out var timestamp))
                    return;

                var tdelta = (int)((Stopwatch.GetTimestamp() - timestamp) / (double)Stopwatch.Frequency * 1000);
                Discord.Logger.LogDebug(VoiceNextEvents.VoiceKeepalive, "Received UDP keepalive {KeepAlive} (ping {Ping}ms)", keepalive, tdelta);
                Volatile.Write(ref _udpPing, tdelta);
            }
            catch (Exception ex)
            {
                Discord.Logger.LogError(VoiceNextEvents.VoiceKeepalive, ex, "Exception occurred when handling keepalive");
            }
        }

        private async Task UdpReceiverTask()
        {
            var token = ReceiverToken;
            var client = UdpClient;

            while (!token.IsCancellationRequested)
            {
                var data = await client.ReceiveAsync().ConfigureAwait(false);
                if (data.Length == 8)
                    ProcessKeepalive(data);
                else if (Configuration.EnableIncoming)
                    await ProcessVoicePacket(data).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Sends a speaking status to the connected voice channel.
        /// </summary>
        /// <param name="speaking">Whether the current user is speaking or not.</param>
        /// <returns>A task representing the sending operation.</returns>
        public async Task SendSpeakingAsync(bool speaking = true)
        {
            if (!IsInitialized)
                throw new InvalidOperationException("The connection is not initialized");

            if (_isSpeaking != speaking)
            {
                _isSpeaking = speaking;
                var pld = new VoiceDispatch { OpCode = 5, Payload = new VoiceSpeakingPayload { Speaking = speaking, Delay = 0 } };

                var plj = JsonConvert.SerializeObject(pld, Formatting.None);
                await WsSendAsync(plj).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Gets a transmit stream for this connection, optionally specifying a packet size to use with the stream. If a stream
        ///     is already configured, it will return the existing one.
        /// </summary>
        /// <param name="sampleDuration">Duration, in ms, to use for audio packets.</param>
        /// <returns>Transmit stream.</returns>
        public VoiceTransmitSink GetTransmitSink(int sampleDuration = 20)
        {
            if (!AudioFormat.AllowedSampleDurations.Contains(sampleDuration))
                throw new ArgumentOutOfRangeException(nameof(sampleDuration), "Invalid PCM sample duration specified.");

            if (TransmitStream == null)
                TransmitStream = new VoiceTransmitSink(this, sampleDuration);

            return TransmitStream;
        }

        /// <summary>
        ///     Asynchronously waits for playback to be finished. Playback is finished when speaking = false is signalled.
        /// </summary>
        /// <returns>A task representing the waiting operation.</returns>
        public async Task WaitForPlaybackFinishAsync()
        {
            if (PlayingWait != null)
                await PlayingWait.Task.ConfigureAwait(false);
        }

        /// <summary>
        ///     Pauses playback.
        /// </summary>
        public void Pause()
            => PauseEvent.Reset();

        /// <summary>
        ///     Asynchronously resumes playback.
        /// </summary>
        /// <returns></returns>
        public async Task ResumeAsync()
            => await PauseEvent.SetAsync().ConfigureAwait(false);

        /// <summary>
        ///     Disconnects and disposes this voice connection.
        /// </summary>
        public void Disconnect()
            => Dispose();

        private async Task HeartbeatAsync()
        {
            await Task.Yield();

            var token = Token;
            while (true)
                try
                {
                    token.ThrowIfCancellationRequested();

                    var dt = DateTime.Now;
                    Discord.Logger.LogTrace(VoiceNextEvents.VoiceHeartbeat, "Sent heartbeat");

                    var hbd = new VoiceDispatch { OpCode = 3, Payload = UnixTimestamp(dt) };
                    var hbj = JsonConvert.SerializeObject(hbd);
                    await WsSendAsync(hbj).ConfigureAwait(false);

                    LastHeartbeat = dt;
                    await Task.Delay(HeartbeatInterval).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
        }

        private async Task KeepaliveAsync()
        {
            await Task.Yield();

            var token = KeepaliveToken;
            var client = UdpClient;

            while (!token.IsCancellationRequested)
            {
                var timestamp = Stopwatch.GetTimestamp();
                var keepalive = Volatile.Read(ref _lastKeepalive);
                Volatile.Write(ref _lastKeepalive, keepalive + 1);
                KeepaliveTimestamps.TryAdd(keepalive, timestamp);

                var packet = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(packet, keepalive);

                await client.SendAsync(packet, packet.Length).ConfigureAwait(false);

                await Task.Delay(5000, token).ConfigureAwait(false);
            }
        }

        private async Task Stage1(VoiceReadyPayload voiceReady)
        {
            UdpClient?.Close();
            UdpClient = Discord.Configuration.UdpClientFactory();

            // IP Discovery
            UdpClient.Setup(UdpEndpoint);

            var pck = new byte[70];
            PreparePacket(pck);
            await UdpClient.SendAsync(pck, pck.Length).ConfigureAwait(false);

            var ipd = await UdpClient.ReceiveAsync().ConfigureAwait(false);
            ReadPacket(ipd, out var ip, out var port);
            DiscoveredEndpoint = new IpEndpoint { Address = ip, Port = port };
            Discord.Logger.LogTrace(VoiceNextEvents.VoiceHandshake, "Endpoint dicovery finished - discovered endpoint is {Ip}:{Port}", ip, port);

            void PreparePacket(byte[] packet)
            {
                var ssrc = SSRC;
                var packetSpan = packet.AsSpan();
                MemoryMarshal.Write(packetSpan, ref ssrc);
                Helpers.ZeroFill(packetSpan);
            }

            void ReadPacket(byte[] packet, out IPAddress decodedIp, out ushort decodedPort)
            {
                var packetSpan = packet.AsSpan();

                var ipString = Utilities.UTF8.GetString(packet, 4, 64 /* 70 - 6 */).TrimEnd('\0');
                decodedIp = IPAddress.Parse(ipString);

                decodedPort = BinaryPrimitives.ReadUInt16LittleEndian(packetSpan.Slice(68 /* 70 - 2 */));
            }

            // Select voice encryption mode
            var selectedEncryptionMode = Sodium.SelectMode(voiceReady.Modes);
            SelectedEncryptionMode = selectedEncryptionMode.Value;

            // Ready
            Discord.Logger.LogTrace(VoiceNextEvents.VoiceHandshake, "Selected encryption mode is {EncryptionMode}", selectedEncryptionMode.Key);
            var vsp = new VoiceDispatch { OpCode = 1, Payload = new VoiceSelectProtocolPayload { Protocol = "udp", Data = new VoiceSelectProtocolPayloadData { Address = DiscoveredEndpoint.Address.ToString(), Port = (ushort)DiscoveredEndpoint.Port, Mode = selectedEncryptionMode.Key } } };
            var vsj = JsonConvert.SerializeObject(vsp, Formatting.None);
            await WsSendAsync(vsj).ConfigureAwait(false);

            SenderTokenSource = new CancellationTokenSource();
            SenderTask = Task.Run(VoiceSenderTask, SenderToken);

            ReceiverTokenSource = new CancellationTokenSource();
            ReceiverTask = Task.Run(UdpReceiverTask, ReceiverToken);
        }

        private async Task Stage2(VoiceSessionDescriptionPayload voiceSessionDescription)
        {
            SelectedEncryptionMode = Sodium.SupportedModes[voiceSessionDescription.Mode.ToLowerInvariant()];
            Discord.Logger.LogTrace(VoiceNextEvents.VoiceHandshake, "Discord updated encryption mode - new mode is {EncryptionMode}", SelectedEncryptionMode);

            // start keepalive
            KeepaliveTokenSource = new CancellationTokenSource();
            KeepaliveTask = KeepaliveAsync();

            // send 3 packets of silence to get things going
            var nullpcm = new byte[AudioFormat.CalculateSampleSize(20)];
            for (var i = 0; i < 3; i++)
            {
                var nullPcm = new byte[nullpcm.Length];
                var nullpacketmem = nullPcm.AsMemory();
                await EnqueuePacketAsync(new RawVoicePacket(nullpacketmem, 20, true)).ConfigureAwait(false);
            }

            IsInitialized = true;
            ReadyWait.SetResult(true);
        }

        private async Task HandleDispatch(JObject jo)
        {
            var opc = (int)jo["op"];
            var opp = jo["d"] as JObject;

            switch (opc)
            {
                case 2: // READY
                    Discord.Logger.LogTrace(VoiceNextEvents.VoiceDispatch, "Received READY (OP2)");
                    var vrp = opp.ToDiscordObject<VoiceReadyPayload>();
                    SSRC = vrp.SSRC;
                    UdpEndpoint = new ConnectionEndpoint(vrp.Address, vrp.Port);

                    //Reset this for packets headers
                    Sequence = 0;
                    Timestamp = 0;
                    _isSpeaking = false;

                    // this is not the valid interval
                    // oh, discord
                    //this.HeartbeatInterval = vrp.HeartbeatInterval;
                    HeartbeatTask = Task.Run(HeartbeatAsync);
                    await Stage1(vrp).ConfigureAwait(false);
                    break;

                case 4: // SESSION_DESCRIPTION
                    Discord.Logger.LogTrace(VoiceNextEvents.VoiceDispatch, "Received SESSION_DESCRIPTION (OP4)");
                    var vsd = opp.ToDiscordObject<VoiceSessionDescriptionPayload>();
                    Key = vsd.SecretKey;
                    Sodium = new Sodium(Key.AsMemory());
                    await Stage2(vsd).ConfigureAwait(false);
                    break;

                case 5: // SPEAKING
                    // Don't spam OP5
                    // No longer spam, Discord supposedly doesn't send many of these
                    Discord.Logger.LogTrace(VoiceNextEvents.VoiceDispatch, "Received SPEAKING (OP5)");
                    var spd = opp.ToDiscordObject<VoiceSpeakingPayload>();
                    var foundUserInCache = Discord.TryGetCachedUserInternal(spd.UserId.Value, out var resolvedUser);
                    var spk = new UserSpeakingEventArgs { Speaking = spd.Speaking, SSRC = spd.SSRC.Value, User = resolvedUser };

                    if (foundUserInCache && TransmittingSSRCs.TryGetValue(spk.SSRC, out var txssrc5) && txssrc5.Id == 0)
                        txssrc5.User = spk.User;
                    else
                    {
                        var opus = Opus.CreateDecoder();
                        var vtx = new AudioSender(spk.SSRC, opus) { User = await Discord.GetUserAsync(spd.UserId.Value).ConfigureAwait(false) };

                        if (!TransmittingSSRCs.TryAdd(spk.SSRC, vtx))
                            Opus.DestroyDecoder(opus);
                    }

                    await _userSpeaking.InvokeAsync(this, spk).ConfigureAwait(false);
                    break;

                case 6: // HEARTBEAT ACK
                    var dt = DateTime.Now;
                    var ping = (int)(dt - LastHeartbeat).TotalMilliseconds;
                    Volatile.Write(ref _wsPing, ping);
                    Discord.Logger.LogTrace(VoiceNextEvents.VoiceDispatch, "Received HEARTBEAT_ACK (OP6, {Heartbeat}ms)", ping);
                    LastHeartbeat = dt;
                    break;

                case 8: // HELLO
                    // this sends a heartbeat interval that we need to use for heartbeating
                    Discord.Logger.LogTrace(VoiceNextEvents.VoiceDispatch, "Received HELLO (OP8)");
                    HeartbeatInterval = opp["heartbeat_interval"].ToDiscordObject<int>();
                    break;

                case 9: // RESUMED
                    Discord.Logger.LogTrace(VoiceNextEvents.VoiceDispatch, "Received RESUMED (OP9)");
                    HeartbeatTask = Task.Run(HeartbeatAsync);
                    break;

                case 13: // CLIENT_DISCONNECTED
                    Discord.Logger.LogTrace(VoiceNextEvents.VoiceDispatch, "Received CLIENT_DISCONNECTED (OP13)");
                    var ulpd = opp.ToDiscordObject<VoiceUserLeavePayload>();
                    var txssrc = TransmittingSSRCs.FirstOrDefault(x => x.Value.Id == ulpd.UserId);
                    if (TransmittingSSRCs.ContainsKey(txssrc.Key))
                    {
                        TransmittingSSRCs.TryRemove(txssrc.Key, out var txssrc13);
                        Opus.DestroyDecoder(txssrc13.Decoder);
                    }

                    var usrl = await Discord.GetUserAsync(ulpd.UserId).ConfigureAwait(false);
                    await _userLeft.InvokeAsync(this, new VoiceUserLeaveEventArgs { User = usrl, SSRC = txssrc.Key }).ConfigureAwait(false);
                    break;

                default:
                    Discord.Logger.LogTrace(VoiceNextEvents.VoiceDispatch, "Received unknown voice opcode (OP{Op})", opc);
                    break;
            }
        }

        private async Task VoiceWS_SocketClosed(IWebSocketClient client, SocketCloseEventArgs e)
        {
            Discord.Logger.LogDebug(VoiceNextEvents.VoiceConnectionClose, "Voice WebSocket closed ({CloseCode}, '{CloseMessage}')", e.CloseCode, e.CloseMessage);

            // generally this should not be disposed on all disconnects, only on requested ones
            // or something
            // otherwise problems happen
            //this.Dispose();

            if (e.CloseCode is 4006 or 4009)
                Resume = false;

            //4014 means it has been moved, kicked or channel was deleted, we check if the target channel is not null to know it has moved from channel
            if (e.CloseCode is 4014 && TargetChannel is not null)
            {
                Resume = false;

                KeepaliveTokenSource.Cancel();
                KeepaliveTokenSource = new CancellationTokenSource();
                ReceiverTokenSource.Cancel();
                ReceiverTokenSource = new CancellationTokenSource();
                SenderTokenSource.Cancel();
                SenderTokenSource = new CancellationTokenSource();

                KeepaliveTimestamps.Clear();

                ReadyWait = new TaskCompletionSource<bool>();
            }

            if (!IsDisposed)
            {
                //Cancel all tasks
                TokenSource.Cancel();
                TokenSource = new CancellationTokenSource();

                VoiceWs = Discord.Configuration.WebSocketClientFactory(Discord.Configuration.Proxy);
                VoiceWs.Disconnected += VoiceWS_SocketClosed;
                VoiceWs.MessageReceived += VoiceWS_SocketMessage;
                VoiceWs.Connected += VoiceWS_SocketOpened;

                if (Resume) // emzi you dipshit
                    await ConnectAsync().ConfigureAwait(false);
            }
        }

        private Task VoiceWS_SocketMessage(IWebSocketClient client, SocketMessageEventArgs e)
        {
            if (e is not SocketTextMessageEventArgs et)
            {
                Discord.Logger.LogCritical(VoiceNextEvents.VoiceGatewayError, "Discord Voice Gateway sent binary data - unable to process");
                return Task.CompletedTask;
            }

            Discord.Logger.LogTrace(VoiceNextEvents.VoiceWsRx, et.Message);
            return HandleDispatch(JObject.Parse(et.Message));
        }

        private Task VoiceWS_SocketOpened(IWebSocketClient client, SocketEventArgs e)
            => StartAsync();

        private Task VoiceWs_SocketException(IWebSocketClient client, SocketErrorEventArgs e)
            => _voiceSocketError.InvokeAsync(this, new SocketErrorEventArgs { Exception = e.Exception });

        private async Task WsSendAsync(string payload)
        {
            Discord.Logger.LogTrace(VoiceNextEvents.VoiceWsTx, payload);
            await VoiceWs.SendMessageAsync(payload).ConfigureAwait(false);
        }

        private static uint UnixTimestamp(DateTime dt)
        {
            var ts = dt - UnixEpoch;
            var sd = ts.TotalSeconds;
            var si = (uint)sd;
            return si;
        }
    }
}

// Naam you still owe me those noodles :^)
// I remember
// Alexa, how much is shipping to emzi
// NL -> PL is 18.50€ for packages <=2kg it seems (https://www.postnl.nl/en/mail-and-parcels/parcels/international-parcel/)
