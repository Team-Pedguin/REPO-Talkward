using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx;
using ExitGames.Client.Photon;
using Photon.Voice.Unity;
using REPOLib;
using REPOLib.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Talkward;

[PublicAPI]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID)]
public class Plugin : BaseUnityPlugin
{
    internal new static ManualLogSource? Logger { get; private set; }
    public static NetworkedEvent? ExternalChatMessage { get; private set; }
    public static Plugin Instance { get; private set; } = null!;

    public static PlayerVoiceChat? PlayerVoiceChat
        => PlayerVoiceChat.instance;


    private AtomicBoolean _talkwardEnabled;

    public bool TalkwardEnabled
    {
        get => _talkwardEnabled;
        set => _talkwardEnabled.Set(value);
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
    private GameObject? _gameObject;
    private TalkwardBehavior? _behavior;

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

        CreateGameObject();
    }

    private void CreateGameObject()
    {
        _gameObject = new GameObject("Talkward Local Game Object")
        {
            hideFlags = HideFlags.HideAndDontSave,
            transform =
            {
                localPosition = Vector3.zero,
                localRotation = Quaternion.identity,
                parent = null
            },
            isStatic = true
        };
        DontDestroyOnLoad(_gameObject);
        
        _behavior = _gameObject.AddComponent<TalkwardBehavior>();
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

    public static StringBuilder Sanitize(string sb)
        => Sanitize(new StringBuilder(sb));

    public static StringBuilder Sanitize(StringBuilder sb)
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

    public void Speak(MessageContent msg)
    {
        var sanitized = Sanitize(msg.Message).ToString();
        var whisper = msg.Voice == 1;
        if (_alertsMobs)
            PlayerVoiceChat?.ttsVoice?.TTSSpeakNow(sanitized, whisper);
        else
            _behavior!.Speak(sanitized, whisper);
    }
}