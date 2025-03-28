using FluentAssertions;
using Talkward.Sam;

namespace Talkward.Test;

public class SamPhonemeTests
{
    [SetUp]
    public void Setup() => License.Accepted = true;

    [Test]
    public void CopyStress_AppliesStressToVoicedPhonemes()
    {
        // Arrange
        var sam = new SamContext();
        Span<byte> phonemeIndex = [36, 10, 255]; // First is voiced, second is vowel
        Span<byte> stress = [0, 3, 0]; // Stress on the vowel

        // Act
        sam.CopyStress(phonemeIndex, stress);

        // Assert
        stress[0].Should().Be(4); // Stress copied to voiced phoneme + 1
    }

    [Test]
    public void SetPhonemeLength_AssignsLengthsBasedOnStress()
    {
        // Arrange
        var sam = new SamContext();
        Span<byte> phonemeIndex = [10, 20, 255];
        Span<byte> phonemeLength = [0, 0, 0];
        Span<byte> stress = [0, 3, 0]; // No stress, stress, end

        // Act
        sam.SetPhonemeLength(phonemeIndex, phonemeLength, stress);

        // Assert
        phonemeLength[0].Should().Be(Resources.PhonemeLengthTable[10]); // Regular length
        phonemeLength[1].Should().Be(Resources.PhonemeStressedLengthTable[20]); // Stressed length
    }

    [Test]
    public void AdjustLengths_ModifiesPhonemeLength()
    {
        // Arrange
        var sam = new SamContext();
        // Setup phoneme sequence with vowel + consonant
        Span<byte> phonemeIndex =
        [
            10, // Vowel
            69, // 'T' (plosive)
            255 // End
        ];
        Span<byte> phonemeLength = [8, 6, 0];
        Span<byte> stress = [0, 0, 0];

        // Act
        sam.AdjustLengths(phonemeIndex, phonemeLength, stress);

        // Assert
        // Length should be adjusted based on context
        phonemeLength[0].Should().NotBe(8);
    }

    [Test]
    public void InsertBreath_AddsBreakMarkers()
    {
        // Arrange
        var sam = new SamContext();
        // Create a long sequence of phonemes to trigger breath insertion
        Span<byte> phonemeIndex = stackalloc byte[256];
        Span<byte> phonemeLength = stackalloc byte[256];
        Span<byte> stress = stackalloc byte[256];

        // Fill with dummy phonemes
        for (var i = 0; i < 30; i++)
        {
            phonemeIndex[i] = 10; // Vowel phoneme
            phonemeLength[i] = 10; // Long enough to trigger a break
        }

        phonemeIndex[30] = 255; // End marker

        // Act
        sam.InsertBreath(phonemeIndex, phonemeLength, stress);

        // Assert
        // Should find a break marker (254) in the sequence
        var foundBreak = false;
        for (var i = 0; i < 40; i++)
        {
            if (phonemeIndex[i] == 254)
                foundBreak = true;
            if (phonemeIndex[i] == 255) break;
        }

        foundBreak.Should().BeTrue();
    }

    [Test]
    public void ProcessStopConsonants_InsertsReleasePhonemes()
    {
        // Arrange
        var sam = new SamContext();
        Span<byte> phonemeIndex =
        [
            69, // 'T' (stop consonant)
            10, // Vowel
            255 // End
        ];
        Span<byte> phonemeLength = [6, 8, 0];
        Span<byte> stress = [0, 0, 0];

        // Act
        sam.ProcessStopConsonants(phonemeIndex, phonemeLength, stress);

        // Assert
        // Should insert two phonemes after the stop consonant
        phonemeIndex[1].Should().Be(70); // T + 1
        phonemeIndex[2].Should().Be(71); // T + 2
    }

    [Test]
    public void PrepareOutput_ProcessesPhonemeSequence()
    {
        // Arrange
        var sam = new SamContext();
        Span<byte> phonemeIndex = [0, 10, 0, 20, 255]; // With silence
        Span<byte> phonemeLength = [0, 8, 0, 6, 0];
        Span<byte> stress = [0, 0, 0, 0, 0];

        Span<byte> outputIndex = stackalloc byte[60];
        Span<byte> outputLength = stackalloc byte[60];
        Span<byte> outputStress = stackalloc byte[60];

        // Act
        sam.PrepareOutput(phonemeIndex, phonemeLength, stress,
            outputIndex, outputLength, outputStress);

        // Assert
        // Silent phonemes should be skipped
        outputIndex[0].Should().Be(10);
        outputIndex[1].Should().Be(20);
        outputIndex[2].Should().Be(255); // End marker
    }

    [Test]
    public void TextToPhonemes_FindsCorrectRuleForH()
    {
        // Arrange
        var sam = new SamContext();

        // Print rule information for 'H'
        var charIndex = (byte) ('H' - 'A');
        var ruleStart = (ushort) (Resources.PhonemeRuleLowBytes[charIndex] |
                                  (Resources.PhonemeRuleHighBytes[charIndex] << 8));

        var ruleIndex = ruleStart;

        // Try to find first rule with opening paren
        // This mimics the rule finding logic
        while ((sam.GetRuleByte(ruleIndex, 0) & 0x80) == 0) ruleIndex++;

        byte offset = 1;
        while (sam.GetRuleByte(ruleIndex, offset) != '(') offset++;

        // Assert
        offset.Should().BeLessThan(255, "Should find opening parenthesis within reasonable range");
    }
}