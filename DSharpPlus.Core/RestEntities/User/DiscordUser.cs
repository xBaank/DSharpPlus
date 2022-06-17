using DSharpPlus.Core.Attributes;
using DSharpPlus.Core.Enums;
using Newtonsoft.Json;

namespace DSharpPlus.Core.RestEntities
{
    /// <summary>
    /// Implements a <see href="https://discord.com/developers/docs/resources/user#user-object-user-structure">Discord User</see>.
    /// </summary>
    [DiscordGatewayPayload("USER_UPDATE")]
    public sealed record DiscordUser
    {
        /// <summary>
        /// The user's id, used to identify the user across all of Discord.
        /// </summary>
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public DiscordSnowflake Id { get; init; } = null!;

        /// <summary>
        /// The user's username, not unique across the platform.
        /// </summary>
        [JsonProperty("username", NullValueHandling = NullValueHandling.Ignore)]
        public string Username { get; init; } = null!;

        /// <summary>
        /// The user's 4-digit discord-tag.
        /// </summary>
        [JsonProperty("discriminator", NullValueHandling = NullValueHandling.Ignore)]
        public string Discriminator { get; init; } = null!;

        /// <summary>
        /// The user's avatar hash.
        /// </summary>
        [JsonProperty("avatar", NullValueHandling = NullValueHandling.Ignore)]
        public string? Avatar { get; init; }

        /// <summary>
        /// Whether the user belongs to an OAuth2 application.
        /// </summary>
        [JsonProperty("bot", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<bool> Bot { get; init; }

        /// <summary>
        /// Whether the user is an Official Discord System user (part of the urgent message system).
        /// </summary>
        [JsonProperty("system", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<bool> System { get; init; }

        /// <summary>
        /// Whether the user has two factor enabled on their account.
        /// </summary>
        [JsonProperty("mfa_enabled", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<bool> MFAEnabled { get; init; }

        /// <summary>
        /// The user's banner hash.
        /// </summary>
        [JsonProperty("banner", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<string?> Banner { get; init; }

        /// <summary>
        /// The user's banner color.
        /// </summary>
        [JsonProperty("accent_color", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<int?> AccentColor { get; init; }

        /// <summary>
        /// The user's chosen language option.
        /// </summary>
        [JsonProperty("locale", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<string> Locale { get; init; }

        /// <summary>
        /// Whether the email on this account has been verified. Requires the <see cref="DiscordApplicationScopes.Email"/> scope.
        /// </summary>
        [JsonProperty("verified", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<bool> Verified { get; init; }

        /// <summary>
        /// The user's email. Requires the <see cref="DiscordApplicationScopes.Email"/> scope.
        /// </summary>
        [JsonProperty("email", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<string?> Email { get; init; }

        /// <summary>
        /// The user flags on a user's account.
        /// </summary>
        [JsonProperty("flags", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<DiscordUserFlags> Flags { get; init; }

        /// <summary>
        /// The type of Nitro subscription on a user's account.
        /// </summary>
        [JsonProperty("premium_type", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<DiscordPremiumType> PremiumType { get; init; }

        /// <summary>
        /// The public flags on a user's account.
        /// </summary>
        [JsonProperty("public_flags", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<DiscordUserFlags> PublicFlags { get; init; }

        /// <summary>
        /// Only set on the <c>MESSAGE_CREATE</c> and <c>MESSAGE_UPDATE</c> gateway payloads.
        /// </summary>
        [JsonProperty("member", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<DiscordGuildMember> Member { get; init; }

        public static implicit operator ulong(DiscordUser user) => user.Id;
        public static implicit operator DiscordSnowflake(DiscordUser user) => user.Id;
    }
}