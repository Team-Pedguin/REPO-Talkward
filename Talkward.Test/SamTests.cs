using FluentAssertions;
using Talkward.Sam;

namespace Talkward.Test;

public class SamContextTests
{
    [SetUp]
    public void Setup() => License.Accepted = true;

    [Test]
    public void Constructor_WithDefaultParameters_InitializesCorrectly()
    {
        // Act
        var sam = new SamContext();

        // Assert
        sam.Pitch.Should().Be(64);
        sam.Speed.Should().Be(72);
        sam.Mouth.Should().Be(128);
        sam.Throat.Should().Be(128);
        sam.SingMode.Should().BeFalse();
    }

    [Test]
    public void Constructor_WithCustomParameters_InitializesCorrectly()
    {
        // Act
        var sam = new SamContext(100, 80, 140, 110, true);

        // Assert
        sam.Pitch.Should().Be(100);
        sam.Speed.Should().Be(80);
        sam.Mouth.Should().Be(140);
        sam.Throat.Should().Be(110);
        sam.SingMode.Should().BeTrue();
    }

    [Test]
    public void TextToPhonemes_WithValidInput_ReturnsTrue()
    {
        // Arrange
        var sam = new SamContext();
        var input = "hello"u8;
        Span<byte> output = stackalloc byte[256];

        // Act
        var result = sam.TextToPhonemes(input, output);

        // Assert
        result.Should().BeTrue();
        output[0].Should().NotBe(0); // Should have some phoneme data
    }

    [Test]
    public void TextToPhonemes_WithEmptyInput_ReturnsEndMarker()
    {
        // Arrange
        var sam = new SamContext();
        var input = ""u8;
        Span<byte> output = stackalloc byte[256];

        // Act
        var result = sam.TextToPhonemes(input, output);

        // Assert
        result.Should().BeTrue();
        output[0].Should().Be(155); // End marker
    }

    [Test]
    public void Parser1_WithValidInput_ReturnsTrue()
    {
        // Arrange
        var sam = new SamContext();
        var input = "AH"u8; // Simple phoneme
        Span<byte> phonemeIndex = stackalloc byte[256];
        Span<byte> stress = stackalloc byte[256];

        // Act
        var result = sam.Parser1(input, phonemeIndex, stress);

        // Assert
        result.Should().BeTrue();
        phonemeIndex[0].Should().NotBe(255); // Not end marker
        phonemeIndex[1].Should().Be(255); // End marker
    }

    /*
    [Test]
    public void Generate_WithShortText_ProducesSamples()
    {
        // Arrange
        var sam = new SamContext();
        var text = "Hello";

        // Act
        var samples = sam.Generate<short>(text);

        // Assert
        samples.Length.Should().BeGreaterThan(0);
        samples.WrittenLength.Should().BeGreaterThan(0);
    }
    */

    /*
    [Test]
    public void Generate_WithEmptyText_ReturnsEmptyBuffer()
    {
        // Arrange
        var sam = new SamContext();
        var text = "";

        // Act
        var samples = sam.Generate<short>(text);

        // Assert
        samples.WrittenLength.Should().Be(0);
    }
    */

    /*
    [Test]
    public void Generate_WithExistingBuffer_AppendsToBuffer()
    {
        // Arrange
        var sam = new SamContext();
        var buffer = new SampleBuffer<short>(1024, true);
        var text = "Test";

        // Act
        sam.Generate(text, ref buffer);

        // Assert
        buffer.WrittenLength.Should().BeGreaterThan(0);
    }
    */
    [Test]
    public void SetMouthThroat_ChangesFormantFrequencies()
    {
        // Arrange
        var sam = new SamContext();

        var originalFreq1 = sam.Freq1Data.ToArray(); // Clone the entire array
        var originalFreq2 = sam.Freq2Data.ToArray();

        // Act
        sam.SetMouthThroat(150, 200); // Different from default 128, 128

        // Assert

        sam.Freq1Data.SequenceEqual(originalFreq1)
            .Should().BeFalse("Freq1Data should change when SetMouthThroat is called");
        sam.Freq2Data.SequenceEqual(originalFreq2).Should()
            .BeFalse("Freq2Data should change when SetMouthThroat is called");
    }
    /*
    [Test]
    public void OutputSample_WritesDifferentValuesForDifferentTypes()
    {
        // Arrange
        var sam = new SamContext();
        var buffer1 = new SampleBuffer<byte>(10);
        var buffer2 = new SampleBuffer<short>(10);
        var buffer3 = new SampleBuffer<float>(10);

        // Act - output same value to different type buffers
        sam.OutputSample(5, 15, ref buffer1);
        sam.OutputSample(5, 15, ref buffer2);
        sam.OutputSample(5, 15, ref buffer3);

        // Assert - each buffer should have appropriately scaled values
        buffer1.WrittenLength.Should().Be(1);
        buffer2.WrittenLength.Should().Be(1);
        buffer3.WrittenLength.Should().Be(1);

        // Different buffer types should contain differently scaled values
        // (not testing exact values as scaling is complex)
    }
    */

    [Test]
    public void CreateFrames_PopulatesFrameData()
    {
        // Arrange
        var sam = new SamContext();
        ReadOnlySpan<byte> phonemeIndex = [1, 10, 255]; // Some phonemes + end marker
        ReadOnlySpan<byte> stress = [0, 2, 0];
        ReadOnlySpan<byte> phonemeLength = [5, 8, 0];

        // Act
        sam.CreateFrames(phonemeIndex, stress, phonemeLength, 64);

        // Assert
        // Verify some frame data was populated
        sam.Frequency1.Should().NotBeNull();
        sam.Frequency2.Should().NotBeNull();
        sam.Frequency3.Should().NotBeNull();
        sam.Amplitude1.Should().NotBeNull();
    }

    [Test]
    public void AddInflection_ModifiesPitchValues()
    {
        // Arrange
        var sam = new SamContext();

        // Initialize some pitch values
        for (var i = 0; i < 100; i++)
        {
            sam.Pitches[i] = 64;
        }

        // Act
        sam.AddInflection(InflectionType.Rising, 50);

        // Assert
        // Pitch values should be modified before position 50
        sam.Pitches[20].Should().NotBe(64);
    }
}