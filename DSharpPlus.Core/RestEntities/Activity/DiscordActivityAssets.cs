using Newtonsoft.Json;

namespace DSharpPlus.Core.RestEntities
{
    public sealed record DiscordActivityAssets
    {
        /// <summary>
        /// Activity asset images are arbitrary strings which usually contain snowflake IDs or prefixed image IDs. Treat data within this field carefully, as it is user-specifiable and not sanitized.
        /// To use an external image via media proxy, specify the URL as the field's value when sending. You will only receive the <c>mp:</c> prefix via the gateway.
        /// </summary>
        [JsonProperty("large_image", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<string> LargeImage { get; init; }

        /// <summary>
        /// Text displayed when hovering over the large image of the activity.
        /// </summary>
        [JsonProperty("large_text", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<string> LargeText { get; init; }

        /// <summary>
        /// Activity asset images are arbitrary strings which usually contain snowflake IDs or prefixed image IDs. Treat data within this field carefully, as it is user-specifiable and not sanitized.
        /// To use an external image via media proxy, specify the URL as the field's value when sending. You will only receive the <c>mp:</c> prefix via the gateway.
        /// </summary>
        [JsonProperty("small_image", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<string> SmallImage { get; init; }

        /// <summary>
        /// Text displayed when hovering over the small image of the activity.
        /// </summary>
        [JsonProperty("small_text", NullValueHandling = NullValueHandling.Ignore)]
        public Optional<string> SmallText { get; init; }
    }
}