namespace Talkward.Sam;

/// <summary>
/// Flags representing phoneme properties used in the speech synthesis rules
/// </summary>
[Flags]
public enum PhonemeFlags : byte
{
    None = 0,

    // Basic phoneme types and properties
    Plosive = 1, // Stop consonants like P, T, K
    Fricative = 2, // Fricatives like F, S
    Liquid = 4, // Liquid consonants like L, R
    Nasal = 8, // Nasal consonants like M, N
    Alveolar = 16, // Formed with tongue against alveolar ridge
    Punct = 32, // Punctuation
    Voiced = 64, // Sounds with vocal cord vibration
    StopCons = 128, // General stop consonant flag
}