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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DSharpPlus.Net
{
    /// <summary>
    /// Represents a rate limit bucket.
    /// </summary>
    internal class RateLimitBucket : IEquatable<RateLimitBucket>
    {
        /// <summary>
        /// Gets the Id of the guild bucket.
        /// </summary>
        public string GuildId { get; internal set; }

        /// <summary>
        /// Gets the Id of the channel bucket.
        /// </summary>
        public string ChannelId { get; internal set; }

        /// <summary>
        /// Gets the ID of the webhook bucket.
        /// </summary>
        public string WebhookId { get; internal set; }

        /// <summary>
        /// Gets the Id of the ratelimit bucket.
        /// </summary>
        public volatile string BucketId;

        /// <summary>
        /// Gets or sets the ratelimit hash of this bucket.
        /// </summary>
        public string Hash
        {
            get => Volatile.Read(ref _hash);

            internal set
            {
                _isUnlimited = value.Contains(UNLIMITED_HASH);

                if (BucketId != null && !BucketId.StartsWith(value))
                {
                    var id = GenerateBucketId(value, GuildId, ChannelId, WebhookId);
                    BucketId = id;
                    RouteHashes.Add(id);
                }

                Volatile.Write(ref _hash, value);
            }
        }

        internal string _hash;

        /// <summary>
        /// Gets the past route hashes associated with this bucket.
        /// </summary>
        public ConcurrentBag<string> RouteHashes { get; }

        /// <summary>
        /// Gets when this bucket was last called in a request.
        /// </summary>
        public DateTimeOffset LastAttemptAt { get; internal set; }

        /// <summary>
        /// Gets the number of uses left before pre-emptive rate limit is triggered.
        /// </summary>
        public int Remaining
            => _remaining;

        /// <summary>
        /// Gets the maximum number of uses within a single bucket.
        /// </summary>
        public int Maximum { get; set; }

        /// <summary>
        /// Gets the timestamp at which the rate limit resets.
        /// </summary>
        public DateTimeOffset Reset { get; internal set; }

        /// <summary>
        /// Gets the time interval to wait before the rate limit resets.
        /// </summary>
        public TimeSpan? ResetAfter { get; internal set; }

        internal DateTimeOffset ResetAfterOffset { get; set; }

        internal volatile int _remaining;

        /// <summary>
        /// Gets whether this bucket has it's ratelimit determined.
        /// <para>This will be <see langword="false"/> if the ratelimit is determined.</para>
        /// </summary>
        internal volatile bool _isUnlimited;

        /// <summary>
        /// If the initial request for this bucket that is deterternining the rate limits is currently executing
        /// This is a int because booleans can't be accessed atomically
        /// 0 => False, all other values => True
        /// </summary>
        internal volatile int _limitTesting;

        /// <summary>
        /// Task to wait for the rate limit test to finish
        /// </summary>
        internal volatile Task _limitTestFinished;

        /// <summary>
        /// If the rate limits have been determined
        /// </summary>
        internal volatile bool _limitValid;

        /// <summary>
        /// Rate limit reset in ticks, UTC on the next response after the rate limit has been reset
        /// </summary>
        internal long _nextReset;

        /// <summary>
        /// If the rate limit is currently being reset.
        /// This is a int because booleans can't be accessed atomically.
        /// 0 => False, all other values => True
        /// </summary>
        internal volatile int _limitResetting;

        private const string UNLIMITED_HASH = "unlimited";

        internal RateLimitBucket(string hash, string guild_id, string channel_id, string webhook_id)
        {
            Hash = hash;
            ChannelId = channel_id;
            GuildId = guild_id;
            WebhookId = webhook_id;

            BucketId = GenerateBucketId(hash, guild_id, channel_id, webhook_id);
            RouteHashes = new ConcurrentBag<string>();
        }

        /// <summary>
        /// Generates an ID for this request bucket.
        /// </summary>
        /// <param name="hash">Hash for this bucket.</param>
        /// <param name="guild_id">Guild Id for this bucket.</param>
        /// <param name="channel_id">Channel Id for this bucket.</param>
        /// <param name="webhook_id">Webhook Id for this bucket.</param>
        /// <returns>Bucket Id.</returns>
        public static string GenerateBucketId(string hash, string guild_id, string channel_id, string webhook_id)
            => $"{hash}:{guild_id}:{channel_id}:{webhook_id}";

        public static string GenerateHashKey(RestRequestMethod method, string route)
            => $"{method}:{route}";

        public static string GenerateUnlimitedHash(RestRequestMethod method, string route)
            => $"{GenerateHashKey(method, route)}:{UNLIMITED_HASH}";

        /// <summary>
        /// Returns a string representation of this bucket.
        /// </summary>
        /// <returns>String representation of this bucket.</returns>
        public override string ToString()
        {
            var guildId = GuildId != string.Empty ? GuildId : "guild_id";
            var channelId = ChannelId != string.Empty ? ChannelId : "channel_id";
            var webhookId = WebhookId != string.Empty ? WebhookId : "webhook_id";

            return $"rate limit bucket [{Hash}:{guildId}:{channelId}:{webhookId}] [{Remaining}/{Maximum}] {(ResetAfter.HasValue ? ResetAfterOffset : Reset)}";
        }

        /// <summary>
        /// Checks whether this <see cref="RateLimitBucket"/> is equal to another object.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns>Whether the object is equal to this <see cref="RateLimitBucket"/>.</returns>
        public override bool Equals(object obj)
            => Equals(obj as RateLimitBucket);

        /// <summary>
        /// Checks whether this <see cref="RateLimitBucket"/> is equal to another <see cref="RateLimitBucket"/>.
        /// </summary>
        /// <param name="e"><see cref="RateLimitBucket"/> to compare to.</param>
        /// <returns>Whether the <see cref="RateLimitBucket"/> is equal to this <see cref="RateLimitBucket"/>.</returns>
        public bool Equals(RateLimitBucket e)
        {
            if (e is null)
                return false;

            return ReferenceEquals(this, e) ? true : BucketId == e.BucketId;
        }

        /// <summary>
        /// Gets the hash code for this <see cref="RateLimitBucket"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="RateLimitBucket"/>.</returns>
        public override int GetHashCode()
            => BucketId.GetHashCode();

        /// <summary>
        /// Sets remaining number of requests to the maximum when the ratelimit is reset
        /// </summary>
        /// <param name="now"></param>
        internal async Task TryResetLimitAsync(DateTimeOffset now)
        {
            if (ResetAfter.HasValue)
                ResetAfter = ResetAfterOffset - now;

            if (_nextReset == 0)
                return;

            if (_nextReset > now.UtcTicks)
                return;

            while (Interlocked.CompareExchange(ref _limitResetting, 1, 0) != 0)
#pragma warning restore 420
                await Task.Yield();

            if (_nextReset != 0)
            {
                _remaining = Maximum;
                _nextReset = 0;
            }

            _limitResetting = 0;
        }

        internal void SetInitialValues(int max, int usesLeft, DateTimeOffset newReset)
        {
            Maximum = max;
            _remaining = usesLeft;
            _nextReset = newReset.UtcTicks;

            _limitValid = true;
            _limitTestFinished = null;
            _limitTesting = 0;
        }
    }
}
