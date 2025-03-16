using System.Text.Json.Serialization;

namespace Talkward;

public class TalkwardConfig
{
    [JsonPropertyName("twitch")]
    public TwitchConfig Twitch { get; set; } = TwitchConfig.Default;
}