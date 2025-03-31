using System.Collections.Concurrent;
using UnityEngine;

namespace Talkward;

public sealed class UnityTwitchIntegration : TwitchIntegration
{
    public UnityTwitchIntegration(TwitchConfig twitchConfig)
        : base(twitchConfig)
    {
    }

    protected override void OpenUri(string uri)
        => UnityThreadHelper.Post(static uri => Application.OpenURL((string) uri), uri);
}