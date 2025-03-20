using BepInEx.Logging;
using BitFaster.Caching.Lru;
using Strobotnik.Klattersynth;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Talkward;

public class SpeechBehavior : MonoBehaviour
{
    private AtomicBoolean _init;
    private AudioSource? _audioSource;
    private readonly SpeechSynth _naturalSynth = new();
    private readonly SpeechSynth _whisperSynth = new();

    /*
    private int _freq = 220;
    private int _whisperFreqOffset = 180;
    */

    private int _freq = 320;
    private int _whisperFreqOffset = 0; // the frequency does nothing! it's broken!

    private static ManualLogSource? Logger => Plugin.Logger;

    private static SpeechBehavior? _instance;

    private void Awake()
    {
        Logger?.LogDebug("SpeechBehavior Awake");

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

        gameObject.SetActive(true);
        enabled = true;
    }

    private void LateUpdate()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Logger?.LogDebug("TalkwardBehavior OnEnable");
    }

    private void OnDisable()
    {
        Logger?.LogDebug("TalkwardBehavior OnDisable");
    }

    private void OnDestroy()
    {
        Logger?.LogDebug("TalkwardBehavior OnDestroy");
    }

    public void Speak(string speech, bool whisper)
    {
        Initialize();
        var clip = GetClip(speech, whisper);
        Schedule(clip);
    }

    private void Schedule(AudioClip clip)
    {
        if (!clip) return;
        _scheduled.Enqueue(clip);
    }


    private double _nextClipTime;

    private readonly Queue<AudioClip> _scheduled = new();

    private void SpeakScheduled()
    {
        if (!_scheduled.TryDequeue(out var clip))
            return;

        var clipFreq = clip.frequency;
        var clipSamples = clip.samples;
        var clipSeconds = (double) clipSamples / clipFreq + 0.1;
        if (_audioSource)
            _audioSource!.PlayOneShot(clip);

        _nextClipTime = Time.fixedTimeAsDouble + clipSeconds;
    }

    private void Update()
    {
        if (_scheduled.Count <= 0)
            return;

        var t = Time.fixedTimeAsDouble;
        if (t >= _nextClipTime)
            SpeakScheduled();
    }

    private static readonly FastConcurrentLru<SpeechClipInfo, AudioClip> Cache
        = new(1, 60, SpeechClipInfo.EqualityComparer.Default);

    private AudioClip GetClip(string speech, bool whisper)
    {
        var freq = _freq + (whisper ? _whisperFreqOffset : 0);
        var vs = whisper
            ? SpeechSynth.VoicingSource.whisper
            : SpeechSynth.VoicingSource.natural;
        var info = new SpeechClipInfo(speech, freq, vs);
        var clip = Cache.GetOrAdd(info,
            static (info, synth) => info.Generate(synth),
            whisper ? _whisperSynth : _naturalSynth);
        return clip;
    }

    public void Stop()
    {
        Initialize();
    }

    public void SetVolume(float volume)
    {
        Initialize();
        _audioSource.volume = volume;
    }

    public void SetBaseFrequency(int freq)
    {
        _freq = freq;
    }

    public void SetWhisperFrequencyOffset(int offset)
    {
        _whisperFreqOffset = offset;
    }

    [MemberNotNull(nameof(_audioSource))]
    private void Initialize()
    {
#pragma warning disable CS8774 // Member must have a non-null value when exiting.
        if (!_init.TrySet()) return;
#pragma warning restore CS8774
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.volume = 0.125f;

        var dummyNat = new GameObject("Natural Synth",
            typeof(AudioSource))
        {
            hideFlags = HideFlags.HideAndDontSave,
            transform = {parent = Plugin.Instance.staticGameObject!.transform}
        };
        var dummyNatSrc = dummyNat.GetComponent<AudioSource>();
        dummyNatSrc.enabled = false;
        dummyNat.SetActive(false);
        DontDestroyOnLoad(dummyNat);
        _naturalSynth.init(dummyNatSrc, false);

        var dummyWhisper = new GameObject("Whisper Synth",
            typeof(AudioSource))
        {
            hideFlags = HideFlags.HideAndDontSave,
            transform = {parent = Plugin.Instance.staticGameObject!.transform}
        };
        var dummyWhisperSrc = dummyWhisper.GetComponent<AudioSource>();
        dummyWhisperSrc.enabled = false;
        dummyWhisper.SetActive(false);
        DontDestroyOnLoad(dummyWhisper);
        _whisperSynth.init(dummyWhisperSrc, false);
    }

    private void OnLevelWasLoaded(int level)
    {
        Logger?.LogDebug($"TalkwardBehavior OnLevelWasLoaded {level}");
    }

    private void OnServerInitialized()
    {
        Logger?.LogDebug("TalkwardBehavior OnServerInitialized");
    }
}