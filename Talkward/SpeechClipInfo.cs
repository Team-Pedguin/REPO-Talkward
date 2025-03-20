using System.Text;
using Strobotnik.Klattersynth;
using Unity.VisualScripting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Talkward;

public sealed class SpeechClipInfo : IEquatable<SpeechClipInfo>
{
    public StringBuilder Speech { get; }
    public int Frequency { get; }
    public SpeechSynth.VoicingSource VoicingSource { get; }

    public SpeechClipInfo(string speech, int frequency, SpeechSynth.VoicingSource voicingSource)
    {
        Speech = Normalize(speech);
        Frequency = frequency;
        VoicingSource = voicingSource;
    }

    private static StringBuilder Normalize(string speech)
    {
        var sb = new StringBuilder(speech);
        var sbLenPrior = sb.Length;
        for(;;)
        {
            sb.Replace("  ", " ");
            if (sb.Length == sbLenPrior)
                break;
            sbLenPrior = sb.Length;
        }
        // convert all characters to lowercase
        for (var i = 0; i < sb.Length; i++)
            sb[i] = char.ToLowerInvariant(sb[i]);

        return sb;
    }
    
    public AudioClip Generate(SpeechSynth synth)
    {
        lock (synth)
        {
            Debug.Log(ToString());
            var clip = synth.pregenerate(
                Speech,
                Frequency,
                VoicingSource,
                false
            );
            var audio = clip.pregenAudio;
            var copy = AudioClip.Create(
                ToString(),
                audio.samples,
                1,
                audio.frequency,
                false);
            var data = new float[audio.samples];
            audio.GetData(data, 0);
            copy.SetData(data, 0);
            return copy;
        }
    }


    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj)
           || obj is SpeechClipInfo other
           && Equals(other);

    public bool Equals(SpeechClipInfo? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Frequency == other.Frequency
               && VoicingSource == other.VoicingSource
               && Speech.Equals(other.Speech);
    }

    public override int GetHashCode()
        => HashCode.Combine(Speech, Frequency, (int) VoicingSource);

    public class EqualityComparer : IEqualityComparer<SpeechClipInfo>
    {
        public bool Equals(SpeechClipInfo? x, SpeechClipInfo? y)
            => x is not null && y is not null && x.Equals(y);

        public int GetHashCode(SpeechClipInfo? obj)
            => obj is null ? 0 : obj.GetHashCode();

        public static EqualityComparer Default { get; } = new();
    }
    
    public override string ToString()
        => $"{Frequency}Hz {(VoicingSource == SpeechSynth.VoicingSource.natural ? 'N' : 'W')}: {Speech}";
}