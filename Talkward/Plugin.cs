using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using ExitGames.Client.Photon;
using Photon.Voice.Unity;
using REPOLib.Modules;

namespace Talkward;

[PublicAPI]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID)]
public class Plugin : BaseUnityPlugin
{
    internal new static ManualLogSource? Logger { get; private set; }
    public static NetworkedEvent? ExternalChatMessage { get; private set; }
    public static Plugin Instance { get; private set; }

    public static PlayerVoiceChat PlayerVoiceChat => PlayerVoiceChat.instance;


    private AtomicBoolean _enabled;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled.Set(value);
    }

    private AtomicBoolean _alertsMobs;

    public bool AlertsMobs
    {
        get => _alertsMobs;
        set => _alertsMobs.Set(value);
    }

    private AtomicBoolean _broadcast;

    public bool Broadcast
    {
        get => _broadcast;
        set => _broadcast.Set(value);
    }

    private AtomicBoolean _hearBroadcast;

    public bool HearBroadcast
    {
        get => _hearBroadcast;
        set => _hearBroadcast.Set(value);
    }

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} is loaded!");

        ExternalChatMessage = new NetworkedEvent("Enable or Disable Talkward", HandleExternalChatMessage);

        Instance = this;
    }

    private void HandleExternalChatMessage(EventData e)
    {
        if (!HearBroadcast)
            return;

        var data = (byte[]) e.CustomData;
        var dataLength = data.Length;
        if (dataLength < Unsafe.SizeOf<MessageHeader>())
        {
            Logger!.LogError($"Data length is less than 4 bytes: {dataLength}");
            return;
        }

        if (!MemoryMarshal.TryRead<MessageHeader>(data, out var header))
            return;

        Speak(header);
    }

    private static StringBuilder Sanitize(StringBuilder sb)
        => sb
            .Replace("ö", "oe")
            .Replace("Ö", "OE")
            .Replace("ä", "ae")
            .Replace("Ä", "AE")
            .Replace("å", "oa")
            .Replace("Å", "OA")
            .Replace("ü", "ue")
            .Replace("Ü", "UE")
            .Replace("ß", "ss")
            .Replace("æ", "ae")
            .Replace("Æ", "AE")
            .Replace("ø", "oe")
            .Replace("Ø", "OE");

    private void Speak(MessageContent msg)
    {
        var sanitized = Sanitize(new StringBuilder(msg.Message));
        var whisper = msg.Voice == 1;
        PlayerVoiceChat.ttsVoice.TTSSpeakNow(sanitized.ToString(), whisper);
    }
}