using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx;
using ExitGames.Client.Photon;
using Photon.Voice.Unity;
using REPOLib;
using REPOLib.Commands;
using REPOLib.Modules;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Talkward;

[PublicAPI]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID)]
public class Plugin : BaseUnityPlugin
{
    internal new static ManualLogSource? Logger { get; private set; }

    //public static NetworkedEvent? ExternalChatMessage { get; private set; }
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

    [NonSerialized]
    private SpeechBehavior? _speechBehavior;

    [NonSerialized]
    private ConsoleUI? _console;

    [NonSerialized]
    public GameObject? staticGameObject;

    [NonSerialized]
    private TwitchConfig _twitchConfig = TwitchConfig.Default;

    private int _twitchMinBits = 100;
    private int _twitchMinBitsForSubs = 0;
    private string _twitchSpeakCommand = "!tw";
    private string _twitchWhisperCommand = "!tww";
    private string _twitchCustomRewardName = "tw";
    private string _twitchCustomRewardNameWhisper = "tww";
    private bool _twitchCommandsNeedBits = true;
    private bool _twitchCommandsNeedBitsForSubs = false;
    private bool _twitchSpeakHighlighted;
    private bool _twitchSpeakSkipSubMode;
    private UnityTwitchIntegration? _twitchInt;

    public bool HearBroadcast
    {
        get => _hearBroadcast;
        set => _hearBroadcast.Set(value);
    }

    private void Awake()
    {
        Logger = base.Logger;

        Logger?.LogInfo($"Loading {MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION}...");
        try
        {
            typeof(TalkwardCommand).Assembly.GetTypes();
        }
        catch(Exception ex)
        {
            Logger?.LogError($"Failed to poke types: {ex}");
        }
        try
        {
            foreach (var mi in typeof(TalkwardCommand).GetMethods()
                .Where(method => method.GetCustomAttribute<CommandInitializerAttribute>() != null))
                Logger?.LogInfo($"Type-poking command initializer: {mi.Name}");
        }
        catch(Exception ex)
        {
            Logger?.LogError($"Failed to poke command types: {ex}");
        }
        UnityThreadHelper.Post(static _ => Logger?.LogInfo("UnityThreadHelper initialized"), null);
        UnityThreadHelper.Initialize();

        if (Instance)
        {
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        //ExternalChatMessage = new NetworkedEvent("Enable or Disable Talkward", HandleExternalChatMessage);

        var go = new GameObject("Talkward")
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
        DontDestroyOnLoad(go);

        _speechBehavior = go.AddComponent<SpeechBehavior>();

        _console = go.AddComponent<ConsoleUI>();

        staticGameObject = go;

        TalkwardEnabled = Config
            .Bind("Talkward", "Enabled", true)
            .Value;
        /*AlertsMobs = Config
            .Bind("Talkward", "AlertsMobs", true)
            .Value;
        Broadcast = Config
            .Bind("Talkward", "Broadcast", true)
            .Value;
        HearBroadcast = Config
            .Bind("Talkward", "HearBroadcast", true)
            .Value;*/

        Config.SettingChanged += (sender, args) =>
        {
            var setting = args.ChangedSetting;
            var settingDef = setting.Definition;
            var section = settingDef.Section;
            var key = settingDef.Key;
            if (section != "Talkward") return;
            switch (key)
            {
                case "Enabled":
                    TalkwardEnabled = setting.ConvertTo<bool>();
                    break;
                /*case "AlertsMobs":
                    AlertsMobs = setting.ConvertTo<bool>();
                    break;
                case "Broadcast":
                    Broadcast = setting.ConvertTo<bool>();
                    break;
                case "HearBroadcast":
                    HearBroadcast = setting.ConvertTo<bool>();
                    break;*/
            }
        };


        var twitchConfig = new TwitchConfig();
        twitchConfig.BroadcasterId = Config
            .Bind("Talkward.Twitch", "BroadcasterId", twitchConfig.BroadcasterId)
            .Value;
        twitchConfig.ModeratorId = Config
            .Bind("Talkward.Twitch", "ModeratorId", twitchConfig.ModeratorId)
            .Value;
        twitchConfig.ClientId = Config
            .Bind("Talkward.Twitch", "ClientId", twitchConfig.ClientId)
            .Value;
        twitchConfig.DrawAuthCode = Config
            .Bind("Talkward.Twitch", "DrawAuthCode", twitchConfig.DrawAuthCode)
            .Value;
        twitchConfig.OpenAuthInBrowser = Config
            .Bind("Talkward.Twitch", "OpenAuthInBrowser", twitchConfig.OpenAuthInBrowser)
            .Value;
        //twitchConfig.DisplayNameTransforms

        _twitchMinBits = Config
            .Bind("Talkward.Twitch", "TwitchMinimumBits", 100)
            .Value;
        _twitchMinBitsForSubs = Config
            .Bind("Talkward.Twitch", "TwitchMinimumBitsForSubs", 0)
            .Value;
        _twitchSpeakCommand = Config
            .Bind("Talkward.Twitch", "TwitchSpeakCommand", "!tw")
            .Value ?? "!tw";
        _twitchWhisperCommand = Config
            .Bind("Talkward.Twitch", "WhisperCommand", "!tww")
            .Value ?? "!tww";
        _twitchCustomRewardName = Config
            .Bind("Talkward.Twitch", "CustomRewardName", "tw")
            .Value ?? "tw";
        _twitchCustomRewardNameWhisper = Config
            .Bind("Talkward.Twitch", "CustomRewardNameWhisper", "tww")
            .Value ?? "tww";
        _twitchSpeakHighlighted = Config
            .Bind("Talkward.Twitch", "SpeakHighlighted", false)
            .Value;
        _twitchSpeakSkipSubMode = Config
            .Bind("Talkward.Twitch", "SpeakSkipSubMode", false)
            .Value;
        _twitchCommandsNeedBits = Config
            .Bind("Talkward.Twitch", "CommandsNeedBits", false)
            .Value;
        _twitchCommandsNeedBitsForSubs = Config
            .Bind("Talkward.Twitch", "CommandsNeedBitsForSubs", false)
            .Value;

        _twitchConfig = twitchConfig;

        Logger?.LogInfo($"{MyPluginInfo.PLUGIN_NAME} is loaded!");
        
        UnityThreadHelper.Schedule(Time.unscaledTimeAsDouble + 10, _ =>
        {
            _twitchInt = new UnityTwitchIntegration(_twitchConfig);
            _twitchInt.OnTwitchChatEvent += HandleTwitchChatEvent;
        }, null);
    }

    private void HandleTwitchChatEvent(object _, TwitchChatEventArgs args)
    {
        if (!TalkwardEnabled) return;

        var sender = args.DisplayName ?? args.UserName ?? "";
        var message = args.Message ?? "";
        if (_twitchMinBits > 0 && args.Bits >= _twitchMinBits
            || args.IsSubscriber && args.Bits >= _twitchMinBitsForSubs
            || args.IsBroadcaster)
        {
            var voice = message.StartsWith(_twitchSpeakCommand) && message[_twitchSpeakCommand.Length] == ' '
                ? 0
                : message.StartsWith(_twitchWhisperCommand) && message[_twitchWhisperCommand.Length] == ' '
                    ? 1
                    : -1;
            if (voice >= 0)
                Speak(new MessageContent {Voice = voice, Message = message, Sender = sender});
            return;
        }

        if (args.IsChannelPointsReward)
        {
            if (args.CustomReward == _twitchCustomRewardName)
            {
                Speak(new MessageContent {Voice = 0, Message = message, Sender = sender});
                return;
            }

            if (args.CustomReward == _twitchCustomRewardNameWhisper)
            {
                Speak(new MessageContent {Voice = 1, Message = message, Sender = sender});
                return;
            }

            if (args.IsHighlighted && _twitchSpeakHighlighted)
            {
                Speak(new MessageContent {Voice = 0, Message = message, Sender = sender});
                return;
            }

            if (args.IsSkippingSubMode && _twitchSpeakSkipSubMode)
            {
                Speak(new MessageContent {Voice = 0, Message = message, Sender = sender});
                return;
            }
        }

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
            _speechBehavior!.Speak(sanitized, whisper);
    }
    
}