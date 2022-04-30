using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DSharpPlus.VoiceNext.Enums
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DiscordVoiceProtocol
    {
        /// <summary>
        /// The nonce bytes are the RTP header
        /// </summary>
        /// <remarks>
        /// Nonce implementation: Copy the RTP header
        /// </remarks>
        [EnumMember(Value = "xsalsa20_poly1305")]
        Poly1305Normal,

        /// <summary>
        /// The nonce bytes are 24 bytes appended to the payload of the RTP packet
        /// </summary>
        /// <remarks>
        /// Nonce implementation: Generate 24 random bytes
        /// </remarks>
        [EnumMember(Value = "xsalsa20_poly1305_suffix")]
        Poly1305Suffix,

        /// <summary>
        /// The nonce bytes are 4 bytes appended to the payload of the RTP packet
        /// </summary>
        /// <remarks>
        /// Nonce implementation: Incremental 4 bytes (32bit) int value
        /// </remarks>
        [EnumMember(Value = "xsalsa20_poly1305_lite")]
        Poly1305Lite,

        /// <summary>
        /// Undocumented.
        /// </summary>
        [EnumMember(Value = "xsalsa20_poly1305_lite_rtpsize")]
        Poly1305LiteRTPSize,

        /// <summary>
        /// Undocumented.
        /// </summary>
        [EnumMember(Value = "aead_aes256_gcm_rtpsize")]
        AES256GCM,

        /// <summary>
        /// Undocumented.
        /// </summary>
        [EnumMember(Value = "aead_aes256_gcm")]
        AES256GCMNoRTP
    }
}
