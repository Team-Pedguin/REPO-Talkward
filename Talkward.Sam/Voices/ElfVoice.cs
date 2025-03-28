namespace Talkward.Sam.Voices;

public sealed class ElfVoice : IVoice
{
    public float Speed => 72.0f;
    public float Pitch => 64.0f;
    public float Throat => 110.0f;
    public float Mouth => 160.0f;
    public bool Whisper => false;
}