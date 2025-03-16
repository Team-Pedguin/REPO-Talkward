using TwitchLib.Client.Models;

namespace Talkward;

public class TwitchChatEventArgs : EventArgs
{
    public ChatMessage Data { get; }

    public TwitchChatEventArgs(ChatMessage msg)
        => Data = msg;

    public string? UserName => Data.Username;
    public string? DisplayName => Data.DisplayName;
    public string? Message => Data.Message;
    public string? MessageId => Data.Id;
    public string? Channel => Data.Channel;
    public string? UserId => Data.UserId;
    public string? Color => Data.ColorHex;

    // paid messages
    public bool IsBitsReward => Data.Bits > 0;
    public int Bits => Data.Bits;

    // channel points
    public bool IsChannelPointsReward => IsHighlighted
                                         || IsSkippingSubMode
                                         || IsCustomReward;

    public bool IsHighlighted => Data.IsHighlighted;
    public bool IsSkippingSubMode => Data.IsSkippingSubMode;
    public bool IsCustomReward => Data.CustomRewardId != null;
    public string? CustomReward => Data.CustomRewardId;

    public bool IsMe => Data.IsMe;
    public bool IsBroadcaster => Data.IsBroadcaster;
    public bool IsSubscriber => Data.IsSubscriber;
    public bool IsModerator => Data.IsModerator;
    public bool IsTurbo => Data.IsTurbo;
    public bool IsStaff => Data.IsStaff;
    public bool IsReply => Data.ChatReply != null;
    public bool IsVip => Data.IsVip;
    public bool IsPartner => Data.IsPartner;
    public bool IsFirstMessage => Data.IsFirstMessage;

    private ChatReply? RepliedTo => Data.ChatReply;

    private string? _userType;
    public string? UserType => _userType ??= Data.UserType.ToString();

    private DateTimeOffset? _sent;

    public DateTimeOffset Sent
        => _sent ??= DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(Data.TmiSentTs));
}