//using TwitchLib.Client.Models;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Core.Models.Chat;

namespace Talkward;

public class TwitchChatEventArgs : EventArgs
{
    public DateTime Time { get; }

    public TwitchChatEventArgs(ChannelChatMessage msg, DateTime time)
    {
        Time = time;
        UserName = msg.ChatterUserLogin;
        DisplayName = msg.ChatterUserName;
        Message = msg.Message.Text;
        MessageId = msg.MessageId;
        Channel = msg.SourceBroadcasterUserLogin;
        UserId = msg.ChatterUserId;
        Color = msg.Color;
        Bits = msg.Cheer?.Bits ?? 0;
        IsHighlighted = false; // TODO
        IsSkippingSubMode = false; // TODO
        IsCustomReward = !string.IsNullOrWhiteSpace(msg.ChannelPointsCustomRewardId);
        CustomReward = IsCustomReward ? msg.ChannelPointsCustomRewardId : null;
        IsMe = msg.SourceBroadcasterUserId == msg.ChatterUserId;
        IsBroadcaster = msg.IsBroadcaster;
        IsSubscriber = msg.IsSubscriber;
        IsModerator = msg.IsModerator;
        IsTurbo = msg.Badges.Any(x => x.SetId.Equals("turbo", StringComparison.OrdinalIgnoreCase)); // TODO: check if this is correct
        IsStaff = msg.IsStaff;
        IsReply = msg.Reply is not null;
        IsVip = msg.IsVip;
        IsPartner = msg.Badges.Any(x => x.SetId.Equals("partner", StringComparison.OrdinalIgnoreCase)); // TODO: check if this is correct
        IsFirstMessage = false; // TODO
        RepliedTo = msg.Reply;
        IsAnonymous = false;
    }

    public TwitchChatEventArgs(ChannelPointsCustomRewardRedemption msg, DateTime time)
    {
        Time = time;
        UserName = msg.UserLogin;
        DisplayName = msg.UserName;
        Message = msg.UserInput;
        MessageId = msg.Id;
        Channel = msg.BroadcasterUserLogin;
        UserId = msg.UserId;
        Color = "white";
        Bits = 0;
        IsHighlighted = msg.Reward.Title == "Highlight my message"; // TODO: check if this is correct
        IsSkippingSubMode = msg.Reward.Title == "Skip Sub Mode"; // TODO: check if this is correct
        IsCustomReward = true;
        CustomReward = IsCustomReward ? msg.Reward.Id : null;
        IsMe = msg.BroadcasterUserId == msg.UserId;
        IsBroadcaster = msg.BroadcasterUserId == msg.UserId;
        IsSubscriber = false; // TODO: lazy resolve
        IsModerator = false; // TODO: lazy resolve
        IsTurbo = false; // TODO: lazy resolve
        IsStaff = false; // TODO: lazy resolve
        IsReply = false; // TODO: lazy resolve
        IsVip = false; // TODO: lazy resolve
        IsPartner = false; // TODO: lazy resolve
        IsFirstMessage = false; // TODO
        RepliedTo = null;
        IsAnonymous = false;
    }

    public TwitchChatEventArgs(ChannelPointsAutomaticRewardRedemption msg, DateTime time)
    {
        Time = time;
        UserName = msg.UserLogin;
        DisplayName = msg.UserName;
        Message = msg.UserInput;
        MessageId = msg.Id;
        Channel = msg.BroadcasterUserLogin;
        UserId = msg.UserId;
        Color = "white";
        Bits = 0;
        IsHighlighted = false; // TODO: check if this is correct
        IsSkippingSubMode = false; // TODO: check if this is correct
        IsCustomReward = true;
        CustomReward = IsCustomReward ? msg.Reward.Type : null;
        IsMe = msg.BroadcasterUserId == msg.UserId;
        IsBroadcaster = msg.BroadcasterUserId == msg.UserId;
        IsSubscriber = false; // TODO: lazy resolve
        IsModerator = false; // TODO: lazy resolve
        IsTurbo = false; // TODO: lazy resolve
        IsStaff = false; // TODO: lazy resolve
        IsReply = false; // TODO: lazy resolve
        IsVip = false; // TODO: lazy resolve
        IsPartner = false; // TODO: lazy resolve
        IsFirstMessage = false; // TODO
        RepliedTo = null;
        IsAnonymous = false;
    }

    public TwitchChatEventArgs(ChannelCheer msg, DateTime time)
    {
        Time = time;
        UserName = msg.UserLogin;
        DisplayName = msg.UserName;
        Message = msg.Message;
        MessageId = null;
        Channel = msg.BroadcasterUserId;
        UserId = msg.UserId;
        Color = "white";
        Bits = msg.Bits;
        IsHighlighted = false; // TODO: check if this is correct
        IsSkippingSubMode = false; // TODO: check if this is correct
        IsCustomReward = false; // TODO: check if this is correct
        CustomReward = null;
        IsMe = msg.BroadcasterUserId == msg.UserId;
        IsBroadcaster = msg.BroadcasterUserId == msg.UserId;
        IsSubscriber = false; // TODO: lazy resolve
        IsModerator = false; // TODO: lazy resolve
        IsTurbo = false; // TODO: lazy resolve
        IsStaff = false; // TODO: lazy resolve
        IsReply = false; // TODO: lazy resolve
        IsVip = false; // TODO: lazy resolve
        IsPartner = false; // TODO: lazy resolve
        IsFirstMessage = false; // TODO
        RepliedTo = null;
        IsAnonymous = msg.IsAnonymous;
    }

    public string? UserName { get; } // => Data.ChatterUserLogin;
    public string? DisplayName { get; } // => Data.ChatterUserName;
    public string? Message { get; } // => Data.Message.Text;
    public string? MessageId { get; } // => Data.MessageId;
    public string? Channel { get; } // => Data.SourceBroadcasterUserLogin;
    public string? UserId { get; } // => Data.ChatterUserId;
    public string? Color { get; } // => Data.Color;

    // paid messages
    public int Bits { get; } // => Data.Cheer?.Bits ?? 0;
    public bool IsBitsReward => Bits > 0;

    // channel points
    public bool IsChannelPointsReward => IsHighlighted
                                         || IsSkippingSubMode
                                         || IsCustomReward;

    public bool IsHighlighted { get; }
    
    public bool IsSkippingSubMode { get; }
    public bool IsCustomReward { get; }
    public string? CustomReward { get; }

    public bool IsMe { get; }
    public bool IsBroadcaster { get; }
    public bool IsSubscriber { get; }
    public bool IsModerator { get; }
    public bool IsAnonymous { get; }
    public bool IsTurbo { get; }
    public bool IsStaff { get; }
    public bool IsReply { get; }
    public bool IsVip { get; }
    public bool IsPartner { get; }
    public bool IsFirstMessage { get; }

    private ChatReply? RepliedTo { get; }
}