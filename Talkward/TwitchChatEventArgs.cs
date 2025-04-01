//using TwitchLib.Client.Models;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Core.Models.Chat;

namespace Talkward;

public class TwitchChatEventArgs : EventArgs
{
    public ChannelChatMessage Data { get; }

    public TwitchChatEventArgs(ChannelChatMessage msg)
        => Data = msg;

    public string? UserName => Data.ChatterUserLogin;
    public string? DisplayName => Data.ChatterUserName;
    public string? Message => Data.Message.Text;
    public string? MessageId => Data.MessageId;
    public string? Channel => Data.SourceBroadcasterUserLogin;
    public string? UserId => Data.ChatterUserId;
    public string? Color => Data.Color;

    // paid messages
    public int Bits => Data.Cheer?.Bits ?? 0;
    public bool IsBitsReward => Bits > 0;

    // channel points
    public bool IsChannelPointsReward => IsHighlighted
                                         || IsSkippingSubMode
                                         || IsCustomReward;

    public bool IsHighlighted => false; // TODO
    public bool IsSkippingSubMode => false; // TODO
    public bool IsCustomReward => string.IsNullOrWhiteSpace(Data.ChannelPointsCustomRewardId);
    public string? CustomReward => IsCustomReward ? Data.ChannelPointsCustomRewardId : null;

    public bool IsMe => Data.BroadcasterUserId == Data.ChatterUserId;
    public bool IsBroadcaster => Data.IsBroadcaster;
    public bool IsSubscriber => Data.IsSubscriber;
    public bool IsModerator => Data.IsModerator;
    public bool IsTurbo => Data.Badges.Any(x => x.SetId.Equals("turbo", StringComparison.OrdinalIgnoreCase)); // TODO: check if this is correct
    public bool IsStaff => Data.IsStaff;
    public bool IsReply => Data.Reply is not null;
    public bool IsVip => Data.IsVip;
    public bool IsPartner => Data.Badges.Any(x => x.SetId.Equals("partner", StringComparison.OrdinalIgnoreCase)); // TODO: check if this is correct
    public bool IsFirstMessage => false; // TODO

    private ChatReply? RepliedTo => Data.Reply;
}