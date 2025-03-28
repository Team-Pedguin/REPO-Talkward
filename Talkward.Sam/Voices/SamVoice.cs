namespace Talkward.Sam.Voices;

public sealed class SamVoice : IVoice
{
    public float Speed => 72.0f;
    public float Pitch => 64.0f;
    public float Throat => 128.0f;
    public float Mouth => 128.0f;
    public bool Whisper => false;
}