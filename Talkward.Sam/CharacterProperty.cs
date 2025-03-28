using System;

namespace Talkward.Sam;

/// <summary>
/// Character property flags used by the text-to-phoneme algorithm.
/// </summary>
[Flags]
public enum CharacterClass : byte
{
    None = 0,
    
    /// <summary>First bit used in numeric character detection</summary>
    Numeric = 1,
    
    /// <summary>Used with various symbols and punctuation</summary>
    Symbol = 2,
    
    /// <summary>Used for consonants that can form special phonetic combinations with T/C/S/R</summary>
    CombiningConsonant = 4,
    
    /// <summary>Used when checking for period character</summary>
    Period = 8,
    
    /// <summary>Used when checking for consonant digraphs with H (CH, SH)</summary>
    DigraphWithH = 16,
    
    /// <summary>Used when checking for vowels</summary>
    Vowel = 32,
    
    /// <summary>Used when checking for digits/numbers</summary>
    Digit = 64,
    
    /// <summary>Used when checking for whitespace</summary>
    Space = 128
}