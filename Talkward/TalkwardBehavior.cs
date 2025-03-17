using BepInEx.Logging;
using UnityEngine;

namespace Talkward;

public class TalkwardBehavior : MonoBehaviour
{
    private AtomicBoolean _init;
    private AudioSource? _privateAudioSource;
    private TTSVoice? _privateTts;
    private static ManualLogSource? Logger => Plugin.Logger;
    private void Awake()
    {
        Logger?.LogDebug("TalkwardBehavior Awake");
        gameObject.SetActive(true);
        enabled = true;
    }

    private void LateUpdate()
    {
        Initialize();
        Logger?.LogDebug("TalkwardBehavior LateUpdate");
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
        _privateTts!.TTSSpeakNow(speech, whisper);
    }

    private void Initialize()
    {
        if (_init.TrySet())
        {
            _privateAudioSource = gameObject.AddComponent<AudioSource>();
            _privateTts = gameObject.AddComponent<TTSVoice>();
        }
    }
}