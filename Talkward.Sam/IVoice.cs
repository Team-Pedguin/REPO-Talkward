namespace Talkward.Sam;

public interface IVoice
{
    public float Speed { get; }
    public float Pitch { get; }
    public float Throat { get; }
    public float Mouth { get; }
    public bool Whisper { get; }
}