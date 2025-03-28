namespace Talkward.Sam;

/// <summary>
/// Flags representing additional phoneme properties used in the speech synthesis rules
/// </summary>
[Flags]
public enum PhonemeFlags2 : byte
{
    Vowel = 1, // Vowel sounds
    Dipthong = 2, // Diphthong sounds (two vowels together)
    DipYX = 4, // Diphthong ending with IY sound
    Consonant = 8 // General consonant flag
}