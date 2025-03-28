using System.Buffers;
using UnityEngine;
using UnityEngine.Audio;

namespace Talkward.Sam;

/// <summary>
/// Software Automatic Mouth, a tiny text-to-speech synthesizer.
/// It is an adaption of the speech software SAM (Software Automatic Mouth)
/// for the Commodore C64 published in the year 1982 by Don't Ask Software
/// (now SoftVoice, Inc.).
/// It includes a Text-To-Phoneme converter called reciter and a
/// Phoneme-To-Speech routine for the final output.
/// </summary>
/// <remarks>
/// Based on Vidar Hokstad's revised implementation.
/// <see href="https://github.com/vidarh/SAM" />
/// </remarks>
internal sealed class Resources
{
    internal static readonly RawResource CharacterClasses
        = RawResources.Get("tab36376");

    internal static readonly RawResource Rules
        = RawResources.Get("rules");

    internal static readonly RawResource Rules2
        = RawResources.Get("rules2");

    internal static readonly RawResource PhonemeRuleLowBytes
        = RawResources.Get("tab37489");

    internal static readonly RawResource PhonemeRuleHighBytes
        = RawResources.Get("tab37515");

    internal static readonly RawResource UnvoicedConsonantAmplitudes
        = RawResources.Get("tab48426");

    internal static readonly RawResource StressToPitchTable
        = RawResources.Get("tab47492");

    internal static readonly RawResource AmplitudeRescale
        = RawResources.Get("amplitudeRescale");

    internal static readonly RawResource BlendRank
        = RawResources.Get("blendRank");

    internal static readonly RawResource OutBlendLength
        = RawResources.Get("outBlendLength");

    internal static readonly RawResource InBlendLength
        = RawResources.Get("inBlendLength");

    internal static readonly RawResource SampledConsonantFlags
        = RawResources.Get("sampledConsonantFlags");

    internal static readonly RawResource Freq1Data
        = RawResources.Get("freq1data");

    internal static readonly RawResource Freq2Data
        = RawResources.Get("freq2data");

    internal static readonly RawResource Freq3Data
        = RawResources.Get("freq3data");

    internal static readonly RawResource Ampl1Data
        = RawResources.Get("ampl1data");

    internal static readonly RawResource Ampl2Data
        = RawResources.Get("ampl2data");

    internal static readonly RawResource Ampl3Data
        = RawResources.Get("ampl3data");

    internal static readonly RawResource Sinus
        = RawResources.Get("sinus");

    internal static readonly RawResource Rectangle
        = RawResources.Get("rectangle");

    internal static readonly RawResource MultTable
        = RawResources.Get("multTable");

    internal static readonly RawResource SampleTable
        = RawResources.Get("sampleTable");

    internal static readonly RawResource StressInputTable
        = RawResources.Get("stressInputTable");

    internal static readonly RawResource SignInputTable1
        = RawResources.Get("signInputTable1");

    internal static readonly RawResource SignInputTable2
        = RawResources.Get("signInputTable2");

    internal static readonly RawResource Flags
        = RawResources.Get("flags");

    internal static readonly RawResource PhonemeStressedLengthTable
        = RawResources.Get("phonemeStressedLengthTable");

    internal static readonly RawResource PhonemeLengthTable
        = RawResources.Get("phonemeLengthTable");

    internal static readonly RawResource MouthFormantsPrimary
        = RawResources.Get("mouthFormants5_29");

    internal static readonly RawResource ThroatFormantsPrimary
        = RawResources.Get("throatFormants5_29");

    internal static readonly RawResource MouthFormantsSecondary
        = RawResources.Get("mouthFormants48_53");

    internal static readonly RawResource ThroatFormantsSecondary
        = RawResources.Get("throatFormants48_53");

}