namespace Talkward;

public class TwitchConfig
{
    public static TwitchConfig Default { get; } = new();

    public string ClientId { get; set; } = "ah1dcykia4chi6pz5f3z80sizxi5ba"; // not secret

    public string? BroadcasterId { get; set; }

    public string? ModeratorId { get; set; }

    public DisplayNameTransform[]? DisplayNameTransforms { get; set; }

    public bool DrawAuthCode { get; set; } = false;

    public bool OpenAuthInBrowser { get; set; } = true;

    public int EnoughChatters { get; set; } = 512;
}