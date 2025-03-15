using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using ExitGames.Client.Photon;
using REPOLib.Modules;


namespace Talkward;

[PublicAPI]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID)]
public class Plugin : BaseUnityPlugin
{
    internal new static ManualLogSource? Logger { get; private set; }

    public static NetworkedEvent ExternalChatMessage { get; private set; } = null!;

    public AtomicBoolean _enabled;
    public AtomicBoolean _broadcast;
    public AtomicBoolean _hearBroadcast;

    [MemberNotNull]
    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} is loaded!");

        ExternalChatMessage = new NetworkedEvent("Enable or Disable Talkward", HandleExternalChatMessage);
    }

    private void HandleExternalChatMessage(EventData e)
    {
        var data = (byte[]) e.CustomData;
        var dataLength = data.Length;
        if (dataLength < Unsafe.SizeOf<MessageHeader>())
        {
            Logger.LogError($"Data length is less than 4 bytes: {dataLength}");
            return;
        }

        if (!MemoryMarshal.TryRead<MessageHeader>(data, out var header))
        {
        }
    }
}

internal readonly struct MessageHeader
{
    public readonly int Voice;
    public readonly int SenderLength;
    public readonly int MessageLength;

    public MessageHeader(int voice, int senderLength, int messageLength)
    {
        Voice = voice;
        SenderLength = senderLength;
        MessageLength = messageLength;
    }

    public ReadOnlySpan<byte> Sender
    {
        get
        {
            ref var self = ref Unsafe.AsRef(this);
            ref var after = ref Unsafe.As<MessageHeader, byte>(ref Unsafe.Add(ref self, 1));
            return MemoryMarshal.CreateReadOnlySpan(ref after, SenderLength);
        }
    }

    public ReadOnlySpan<byte> Message
    {
        get
        {
            ref var self = ref Unsafe.AsRef(this);
            ref var after = ref Unsafe.As<MessageHeader, byte>(ref Unsafe.Add(ref self, 1));
            ref var message = ref Unsafe.Add(ref after, SenderLength);
            return MemoryMarshal.CreateReadOnlySpan(ref message, MessageLength);
        }
    }

    private MessageContent Parse()
    {
        var utf8 = Encoding.UTF8;
        var message = new MessageContent
        {
            Voice = Voice,
            Sender = utf8.GetString(Sender),
            Message = utf8.GetString(Message)
        };
        return message;
    }

    public static implicit operator MessageContent(MessageHeader header)
        => header.Parse();
}

public struct MessageContent
{
    public int Voice;
    public string Sender;
    public string Message;
}