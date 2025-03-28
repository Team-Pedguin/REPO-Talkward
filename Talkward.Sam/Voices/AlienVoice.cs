namespace Talkward.Sam.Voices;

public sealed class AlienVoice : IVoice
{
    public float Speed => 100.0f;
    public float Pitch => 64.0f;
    public float Throat => 150.0f;
    public float Mouth => 200.0f;
    public bool Whisper => false;
}