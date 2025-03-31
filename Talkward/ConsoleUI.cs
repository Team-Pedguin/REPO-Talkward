using System.Collections;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Talkward;

public class ConsoleUI : SemiUI
{
    private const float LogLineFadeDuration = 10f;
    private const int InitialHistoryLimit = 20;
    private static readonly ForwardingLogListener LogListener = new();

    static ConsoleUI() => BepInEx.Logging.Logger.Listeners.Add(LogListener);

    private static ConsoleUI? _instance;

    private const string ConsoleFont = "Teko-VariableFont_wght SDF 1";

    // LiberationSans SDF
    // LiberationSans SDF - Fallback
    // Teko-VariableFont_wght SDF 1
    // RobotoMono-Thin SDF
    // Dirt Finder Complete

    private static TMP_FontAsset? _consoleFontAsset;

    private static TMP_FontAsset? ConsoleFontAsset
        => _consoleFontAsset ??= Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
            .FirstOrDefault(asset => asset.name == ConsoleFont);


    [NonSerialized]
    public Canvas _canvas;

    [NonSerialized]
    private int _historyIndex;

    [NonSerialized]
    private readonly CircularQueue<string> _history = new(InitialHistoryLimit);

    [NonSerialized]
    private readonly StringBuilder _message = new();

    [NonSerialized]
    private ConsoleState _state;

    [NonSerialized]
    private ConsoleState _prevState;

    [NonSerialized]
    private GameObject _container = null!;

    [NonSerialized]
    private GameObject _textObj = null!;

    [NonSerialized]
    private RectTransform _containerRect = null!;

    [NonSerialized]
    private RectTransform _textRect = null!;

    // ReSharper disable once InconsistentNaming
    private new RectTransform textRectTransform
    {
        get => (RectTransform) base.textRectTransform;
        set => base.textRectTransform = value;
    }

    public static ConsoleUI Instance => _instance!;

    public string Message => _message.ToString();

    public KeyCode ConsoleKey { get; set; }
        = KeyCode.BackQuote;

    [NonSerialized]
    private readonly CircularQueue<GameObject> _logLines = new(20);

    [NonSerialized]
    private GameObject _logContainer;

    [NonSerialized]
    private RectTransform _logContainerRect;

    // Default height of each log line

    // Maximum number of visible log lines
    [NonSerialized]
    private int _maxVisibleLogLines = 20;

    [NonSerialized]
    private Color _defaultLogColor = Color.white;


    [NonSerialized]
    private bool hiding;

    public bool Hiding => hiding;

    [NonSerialized]
    private float hidingProgress;

    public bool Hidden => hidingProgress >= 1f;

    [NonSerialized]
    private float hideTransitionScaler = 0.2f;

    [NonSerialized]
    private float showTransitionScaler = -0.8f; // should be negative

    [NonSerialized]
    private float _consoleLineHeight;

    [NonSerialized]
    private CanvasGroup _logContainerCanvasGroup;

    public void Awake()
    {
        Plugin.Logger?.LogDebug("ConsoleUI Awake");

        if (_instance)
        {
            if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public override void Start()
    {
        Plugin.Logger?.LogDebug("ConsoleUI Start");
        // Setup Canvas on root GameObject first
        _canvas = gameObject.GetOrAddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        var scaler = gameObject.GetOrAddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // Create a container that overlays the entire screen
        _container = new GameObject("ConsoleContainer", typeof(RectTransform));
        _container.transform.SetParent(gameObject.transform, false);
        _containerRect = _container.GetComponent<RectTransform>();
        _containerRect.anchorMin = new Vector2(0, 0);
        _containerRect.anchorMax = new Vector2(1, 1);
        _containerRect.offsetMin = Vector2.zero;
        _containerRect.offsetMax = Vector2.zero;
        //containerRect.anchoredPosition = Vector2.zero;
        //containerRect.localPosition = Vector3.zero;

        // Create text object inside the container, bottom left
        _textObj = new GameObject("ConsoleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        _textRect = _textObj.GetComponent<RectTransform>();
        _textRect.anchorMin = new Vector2(0, 0);
        _textRect.anchorMax = new Vector2(0, 0);
        _textRect.pivot = new Vector2(0, 0);
        const float edgeOffset = 18;
        _textRect.offsetMin = new Vector2(edgeOffset, edgeOffset);
        _textRect.offsetMax = new Vector2(-edgeOffset, -edgeOffset);


        // Add TextMeshProUGUI component
        var consoleText = _textObj.GetComponent<TextMeshProUGUI>();
        consoleText.color = Color.white;
        consoleText.fontSize = 12;
        consoleText.richText = true;
        consoleText.alignment = TextAlignmentOptions.BottomLeft;
        consoleText.enableWordWrapping = false;
        consoleText.raycastTarget = false;
        consoleText.text = "Console Ready";

        // Set font if available
        if (ConsoleFontAsset)
            consoleText.font = ConsoleFontAsset;

        // Set SemiUI properties
        uiText = consoleText;
        textRectTransform = _containerRect;

        //animateTheEntireObject = true;
        animateTheEntireObject = false;


        /*var consoleFont = consoleText.font;
        if (consoleFont)
        {
            var faceInfo = consoleFont!.faceInfo;
            _consoleLineHeight = faceInfo.lineHeight;
        }
        else
        {
            _consoleLineHeight = 20f;
        }*/
        _consoleLineHeight = consoleText.preferredHeight;
        var ascent = _consoleLineHeight + edgeOffset;

        showPosition = Vector2.zero;
        hidePosition = new Vector2(0, -ascent);

        // Initialize SemiUI
        base.Start();

        showPosition = Vector2.zero;
        hidePosition = new Vector2(0, -ascent);

        // Create container for log lines just above uiText
        _logContainer = new GameObject("LogContainer", typeof(RectTransform));
        _logContainerCanvasGroup = _logContainer.GetOrAddComponent<CanvasGroup>();
        _logContainerCanvasGroup.alpha = 1f;
        _logContainer.transform.SetParent(_container.transform, false);
        _logContainerRect = _logContainer.GetComponent<RectTransform>();
        _logContainerRect.anchorMin = new Vector2(0, 0);
        _logContainerRect.anchorMax = new Vector2(1, 1);
        _logContainerRect.pivot = new Vector2(0, 0);
        _logContainerRect.offsetMin = new Vector2(edgeOffset, edgeOffset + uiText.preferredHeight + 5);
        _logContainerRect.offsetMax = new Vector2(-edgeOffset, -edgeOffset);

        gameObject.SetActive(true);
        AllChildrenSetActive(true);

        // Set the parent of the text object to the container AFTER SemiUI.Start as it re-parents all children
        _textObj.transform.SetParent(_container.transform, false);

        _state = ConsoleState.Inactive;
        Hide();
    }

    public override void Update()
    {
        base.Update();

        var scaler = hiding ? hideTransitionScaler : showTransitionScaler;
        var delta = Time.fixedUnscaledDeltaTime;
        hidingProgress = Mathf.Clamp01
            (hidingProgress + delta * scaler);

        _containerRect.localPosition = Vector2.LerpUnclamped
            (showPosition, hidePosition, hidingProgress);

        _logContainerCanvasGroup.alpha = 1f - hidingProgress;

        // prevent console from being active during initial load
        // does not prevent it from being active on main menu
        var levelGen = LevelGenerator.Instance;
        if (!levelGen || !levelGen.Generated)
            return;

        var state = _state;

        switch (state)
        {
            case ConsoleState.Inactive:
                HandleInactiveState();
                break;
            case ConsoleState.Active:
                HandleActiveState();
                break;
            case ConsoleState.Send:
                HandleSendState();
                break;
            default:
                _state = ConsoleState.Inactive;
                goto case ConsoleState.Inactive;
        }

        _prevState = state;
    }

    public void SetInputText(StringBuilder text)
    {
        if (!uiText) return;
        text.Insert(0, SemiFunc.IsMasterClient() ? "$ " : "# ");
        uiText.text = text.ToString();
    }

    public void ClearInputText()
    {
        if (!uiText) return;
        uiText.text = SemiFunc.IsMasterClient() ? "$ " : "# ";
    }


    public void SetHistoryLimit(int limit)
        => _history.ResizeTo(limit);

    private static void CharRemoveEffect()
    {
        var console = Instance;
        console.SemiUITextFlashColor(Color.red, 0.2f);
        console.SemiUISpringShakeX(5f, 5f, 0.2f);
        MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Dud, null, 2f, 1f, true);
    }

    private static void ErrorEffect()
    {
        var console = Instance;
        console.SemiUITextFlashColor(Color.red, 0.2f);
        console.SemiUISpringShakeX(10f, 10f, 0.3f);
        console.SemiUISpringScale(0.05f, 5f, 0.2f);
        MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Deny, null, 1f, 1f, true);
    }

    private static void TypeEffect(Color color)
    {
        var console = Instance;
        console.SemiUITextFlashColor(color, 0.2f);
        console.SemiUISpringShakeY(2f, 5f, 0.2f);
        MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Tick, null, 2f, 0.2f, true);
    }

    private void HandleInactiveState()
    {
        _message.Clear();
        ClearInputText();
        var menuManager = MenuManager.instance;
        if (menuManager.currentMenuPage is
            {menuPageIndex: MenuPageIndex.Escape or MenuPageIndex.Settings})
            return;

        // ReSharper disable once InvertIf
        if (Input.GetKeyDown(ConsoleKey))
        {
            var chatManager = ChatManager.instance;
            if (chatManager && chatManager.StateIsActive())
                return;

            menuManager.MenuEffectClick(MenuManager.MenuClickEffectType.Action, null, 1f, 1f, true);
            _state = ConsoleState.Active;
            _historyIndex = 0;

            // Show the console UI when moving to active state
            Show();
        }
    }


    public new void Show()
    {
        if (!hiding)
            return;

        hiding = false;
        base.Show();
    }

    public new void Hide()
    {
        if (hiding)
            return;

        hiding = true;
        base.Hide();
    }

    private void HandleActiveState()
    {
        var chatMgr = ChatManager.instance;
        if (chatMgr && chatMgr.StateIsActive())
            return;

        if (_prevState != ConsoleState.Active)
            Show();

        SemiFunc.InputDisableMovement();

        if (SemiFunc.InputDown(InputKey.Back))
        {
            _state = ConsoleState.Inactive;
            BackEffect();

            Input.imeCompositionMode = IMECompositionMode.Auto;
            chatMgr.enabled = true;
            ChatUI.instance.enabled = true;

            // Hide the console UI when moving to inactive state
            Hide();
            return;
        }

        if (chatMgr.chatState != ChatManager.ChatState.Inactive)
        {
            chatMgr.StateSet(ChatManager.ChatState.Inactive);
            chatMgr.enabled = false;
            ChatUI.instance.enabled = false;
            Input.imeCompositionMode = IMECompositionMode.On;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) && _history.Count > 0)
        {
            if (_historyIndex > 0)
                _historyIndex--;
            else
                _historyIndex = _history.Count - 1;

            _message.Clear();
            _message.Append(_history[_historyIndex]);
            SetInputText(Escape(_message));
            LoadHistoryEffect();
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) && _history.Count > 0)
        {
            if (_historyIndex < _history.Count - 1)
                _historyIndex++;
            else
                _historyIndex = 0;
            _message.Clear();
            _message.Append(_history[_historyIndex]);
            SetInputText(Escape(_message));
            LoadHistoryEffect();
            return;
        }

        if (SemiFunc.InputDown(InputKey.Confirm))
        {
            _state = _message.Length == 0
                ? ConsoleState.Inactive
                : ConsoleState.Send;

            Input.imeCompositionMode = IMECompositionMode.Auto;
            chatMgr.enabled = true;
            ChatUI.instance.enabled = true;

            // If state is becoming inactive, hide the console UI
            if (_state == ConsoleState.Inactive)
                Hide();

            return;
        }

        if (SemiFunc.InputDown(InputKey.ChatDelete))
        {
            if (_message.Length <= 0)
                return;

            _message.Length -= 1;
            SetInputText(Escape(_message));
            CharRemoveEffect();
            return;
        }

        if (_message.Length == 1 && _message[0] == '\b')
            _message.Clear();

        var msgBeforeAppend = _message.ToString();
        _message.Append(Input.inputString);
        _message.Replace("\n", "");

        if (!_message.Equals(msgBeforeAppend))
        {
            if (Input.inputString.EndsWith('\b'))
            {
                switch (_message.Length)
                {
                    case 1:
                        _message.Length = 0;
                        break;
                    default:
                        _message.Length -= 2;
                        break;
                }

                CharRemoveEffect();
                return;
            }

            _message.Replace("\r", "");
            SetInputText(Escape(_message));
            TypeEffect(Color.yellow);
            return;
        }

        SetInputText(Escape(_message));
    }

    private void LoadHistoryEffect()
    {
        SemiUITextFlashColor(Color.cyan, 0.2f);
        SemiUISpringShakeY(2f, 5f, 0.2f);
        MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Tick, null, 1f, 0.2f, true);
    }

    private void BackEffect()
    {
        SemiUISpringShakeX(10f, 10f, 0.3f);
        SemiUISpringScale(0.05f, 5f, 0.2f);
        MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Deny, null, 1f, 1f, true);
    }

    private void HandleSendState()
    {
        if (_message.Length == 0)
            return;

        var msg = _message.ToString();
        var handled = false;

        // capture logging as output
        switch (msg)
        {
            case "clear":
                ClearLogLines();
                handled = true;
                break;
            case "exit":
                Application.Quit();
                handled = true;
                break;
            default:
            {
                EventHandler<LogEventArgs> logEventHandler = LogLine;

                try
                {
                    LogListener.LogEvent += logEventHandler;

                    foreach (var tryExecHandlers in Console.GetTryExecuteHandlers())
                    {
                        try
                        {
                            handled = tryExecHandlers(msg);
                            if (handled) break;
                        }
                        catch (Exception e)
                        {
                            Plugin.Logger?.LogError($"Error in ConsoleUI.TryExecute handler: {e}");
                        }
                    }
                }
                finally
                {
                    LogListener.LogEvent -= logEventHandler;
                }

                break;
            }
        }

        if (handled != true)
        {
            ErrorEffect();
            _state = ConsoleState.Active;
            return;
        }

        if (_history.TryGetNewest(out var lastMessage))
        {
            if (!_message.Equals(lastMessage))
                _history.TryEnqueue(msg, true);
        }
        else
        {
            _history.TryEnqueue(msg, true);
        }

        _message.Clear();
        SetInputText(Escape(msg));
        SemiUITextFlashColor(Color.green, 0.2f);
        SemiUISpringShakeX(10f, 10f, 0.3f);
        SemiUISpringScale(0.05f, 5f, 0.2f);
        MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Confirm, null, 1f, 1f, true);

        // Return to inactive state and hide the console after successful command
        _state = ConsoleState.Inactive;
        Hide();
    }

    public void ClearLogLines()
    {
        foreach (var line in _logLines)
        {
            if (line)
                Destroy(line);
        }

        _logLines.Clear();
    }

    private void LogLine(object sender, LogEventArgs args)
        => LogLine(args.Level, args.ToStringLine());

    private void LogLine(LogLevel level, string msg)
    {
        if (string.IsNullOrWhiteSpace(msg))
            return;

        var lineObj = new GameObject($"LogLine_{_logLines.Count}", typeof(RectTransform), typeof(TextMeshProUGUI));
        lineObj.transform.SetParent(_logContainer.transform, false);

        var rectTransform = lineObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(1, 0);
        rectTransform.pivot = new Vector2(0, 0);
        rectTransform.sizeDelta = new Vector2(0, _consoleLineHeight);
        rectTransform.localPosition = new Vector2(0, 0);

        var text = lineObj.GetComponent<TextMeshProUGUI>();
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        if (ConsoleFontAsset)
            text.font = ConsoleFontAsset;

        // Set color based on log level
        text.color = GetColorForLogLevel(level);
        
        var time = DateTime.Now.ToString("HH:mm:ss");

        // Set font if available
        var font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
            .FirstOrDefault(asset => asset.name == ConsoleFont);
        if (font) text.font = font;

        // Set log message with level prefix
        text.text = $"{time} | {msg}";

        // Add to list and reposition all entries
        var oldestEntry = _logLines.EnqueueAndDequeue(lineObj);
        if (oldestEntry)
        {
            IEnumerator FadeOutAndDestroyLogLine()
            {
                var logLine = oldestEntry!.GetComponent<TextMeshProUGUI>();
                var entryRect = oldestEntry.GetComponent<RectTransform>();
                var elapsedTime = 0f;

                var localPos = entryRect.localPosition;
                localPos.y += _consoleLineHeight;
                
                logLine.CrossFadeAlpha(0f, LogLineFadeDuration, true);
                do
                {
                    elapsedTime += Time.fixedUnscaledDeltaTime;
                    var delta = elapsedTime / LogLineFadeDuration;
                    entryRect.localPosition = new Vector2(
                        delta * -Screen.width,
                        localPos.y);
                    yield return null;
                } while (elapsedTime < LogLineFadeDuration);

                Destroy(oldestEntry);
                
                yield break;
            }

            StartCoroutine(FadeOutAndDestroyLogLine());
        }

        float y = 0;

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = _logLines.Count - 1; i >= 0; --i)
        {
            var entry = _logLines[i];
            var entryRect = entry.GetComponent<RectTransform>();
            entryRect.localPosition = new Vector2(0, y);
            y += _consoleLineHeight;
        }
    }


    // Add this method to determine log color based on level
    private Color GetColorForLogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Fatal => new Color(0.7f, 0f, 0f),
            LogLevel.Error => Color.red,
            LogLevel.Warning => Color.yellow,
            LogLevel.Message => Color.white,
            LogLevel.Info => Color.cyan,
            LogLevel.Debug => new Color(0.5f, 1f, 0.5f),
            _ => _defaultLogColor
        };
    }

    private enum ConsoleState
    {
        Inactive,
        Active,
        Send
    }

    private static StringBuilder Escape(string message)
        => Escape(new StringBuilder(message), true);

    private static StringBuilder Escape(StringBuilder? message, bool inPlace = false)
    {
        if (message is null || message.Length == 0)
            return new StringBuilder(0);

        var escaped = inPlace
            ? message
            : new StringBuilder(message.Length)
                .Append(message);

        return escaped
            .Replace("</noparse>", "</\u200Bnoparse>")
            .Insert(0, "<noparse>")
            .Append("</noparse>");
    }
}