using System.Text.Json.Serialization;

namespace Talkward;

public class TalkwardConfig
{
    public static TalkwardConfig Default { get; } = new();

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = "ah1dcykia4chi6pz5f3z80sizxi5ba"; // not secret

    [JsonPropertyName("broadcasterId")]
    public string? BroadcasterId { get; set; }

    [JsonPropertyName("moderatorId")]
    public string? ModeratorId { get; set; }

    [JsonPropertyName("displayNameTransforms")]
    public DisplayNameTransform[]? DisplayNameTransforms { get; set; }

    [JsonPropertyName("drawTwitchAuthCode")]
    public bool DrawTwitchAuthCode { get; set; } = false;

    [JsonPropertyName("openTwitchAuthInBrowser")]
    public bool OpenTwitchAuthInBrowser { get; set; } = true;

    [JsonPropertyName("enoughChatters")]
    public int EnoughChatters { get; set; } = 512;
}