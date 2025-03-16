using System.Text.RegularExpressions;

namespace Talkward;

public static class TalkwardConfigHelpers
{
    public static void ApplyDefaults(this TalkwardConfig config)
    {
        var d = TalkwardConfig.Default;
        config.ClientId ??= d.ClientId;
        config.BroadcasterId ??= d.BroadcasterId;
        config.ModeratorId ??= d.ModeratorId;
        config.DisplayNameTransforms ??= d.DisplayNameTransforms;
        if (config.DrawTwitchAuthCode == default)
            config.DrawTwitchAuthCode = d.DrawTwitchAuthCode;
        if (config.OpenTwitchAuthInBrowser == default)
            config.OpenTwitchAuthInBrowser = d.OpenTwitchAuthInBrowser;
        config.EnoughChatters = d.EnoughChatters;
    }

    public static string? Transform(this TalkwardConfig? config, string? displayName)
    {
        if (config is null
            || string.IsNullOrEmpty(displayName)
            || config.DisplayNameTransforms is null)
            return displayName;

        foreach (var txf in config.DisplayNameTransforms)
            displayName = txf.Regex.Replace(
                displayName,
                txf.Replace,
                txf.MaxMatches // -1 for all
            );
        return displayName;
    }
}