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
using DSharpPlus.Core.Enums;
using Newtonsoft.Json;

namespace DSharpPlus.Core.Entities
{
    /// <summary>
    /// Represents a sticker that can be sent in messages.
    /// </summary>
    public sealed record DiscordSticker
    {
        /// <summary>
        /// The id of the sticker.
        /// </summary>
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public DiscordSnowflake Id { get; init; } = null!;

        /// <summary>
        /// For standard stickers, id of the pack the sticker is from.
        /// </summary>
        [JsonProperty("pack_id", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<DiscordSnowflake> PackId { get; init; }

        /// <summary>
        /// The name of the sticker.
        /// </summary>
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; init; } = null!;

        /// <summary>
        /// The description of the sticker.
        /// </summary>
        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string? Description { get; init; }

        /// <summary>
        /// Autocomplete/suggestion tags for the sticker (max 200 characters).
        /// </summary>
        [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
        public string Tags { get; init; } = null!;

        /// <summary>
        /// Deprecated previously the sticker asset hash, now an empty string
        /// </summary>
        [JsonProperty("asset", NullValueHandling = NullValueHandling.Ignore)]
        [Obsolete("Deprecated previously the sticker asset hash, now an empty string")]
        public Optional<string> Asset { get; set; } = null!;

        /// <summary>
        /// The type of sticker.
        /// </summary>
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public DiscordStickerType Type { get; init; }

        /// <summary>
        /// The type of sticker format.
        /// </summary>
        [JsonProperty("format_type", NullValueHandling = NullValueHandling.Ignore)]
        public DiscordStickerFormatType FormatType { get; init; }

        /// <summary>
        /// Whether this guild sticker can be used, may be false due to loss of Server Boosts.
        /// </summary>
        [JsonProperty("available", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<bool> Available { get; init; }

        /// <summary>
        /// The id of the guild that owns this sticker.
        /// </summary>
        [JsonProperty("guild_id", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<DiscordSnowflake?> GuildId { get; init; }

        /// <summary>
        /// The user that uploaded the guild sticker.
        /// </summary>
        [JsonProperty("user", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<DiscordUser> User { get; init; }

        /// <summary>
        /// The standard sticker's sort order within its pack.
        /// </summary>
        [JsonProperty("sort_value", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<int> SortValue { get; init; }
    }
}