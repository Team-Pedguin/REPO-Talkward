using System.Text;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Talkward.Sam;

/// <summary>
/// Provides context for the Software Automatic Mouth (SAM) speech synthesizer,
/// managing sound parameters and rendering phonemes to audio output.
/// </summary>
[PublicAPI, StructLayout(LayoutKind.Sequential)]
public struct SamContext
{
    private const int RulesTableStart = 32000;
    private const ushort Rules2TableStart = 37541;

    public LazyRawResourceMutableCopy LazySampledConsonantFlags
        = Resources.SampledConsonantFlags;

    public Span<byte> SampledConsonantFlags
        => LazySampledConsonantFlags;

    public LazyRawResourceMutableCopy LazyFreq1Data
        = Resources.Freq1Data;

    public Span<byte> Freq1Data
        => LazyFreq1Data;

    public LazyRawResourceMutableCopy LazyFreq2Data
        = Resources.Freq2Data;

    public Span<byte> Freq2Data
        => LazyFreq2Data;


    // Sound parameters - all 256 bytes each
    public Bytes256 Pitches; // 168
    public Bytes256 Frequency1; // 169
    public Bytes256 Frequency2; // 170
    public Bytes256 Frequency3; // 171
    public Bytes256 Amplitude1; // 172
    public Bytes256 Amplitude2; // 173
    public Bytes256 Amplitude3; // 174

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte Ref(byte parameter, byte index)
    {
        switch (parameter)
        {
            //@formatter:off
            case 168: return ref Pitches[index];
            case 169: return ref Frequency1[index];
            case 170: return ref Frequency2[index];
            case 171: return ref Frequency3[index];
            case 172: return ref Amplitude1[index];
            case 173: return ref Amplitude2[index];
            case 174: return ref Amplitude3[index];
            //@formatter:on
            default:
                ThrowParameterOutOfRange(nameof(parameter));
                return ref Unsafe.NullRef<byte>();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(byte parameter, byte index)
        => Ref(parameter, index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte parameter, byte index, byte value)
        => Ref(parameter, index) = value;

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static byte ThrowParameterOutOfRange(string paramName) =>
        throw new ArgumentOutOfRangeException(paramName);

    internal void Interpolate(byte width, byte table, byte frame, sbyte delta)
    {
        var sign = delta < 0;
        var absDelta = Math.Abs(delta);
        var div = (byte) Math.DivRem(absDelta, width, out var remainderInt);
        var remainder = (byte) remainderInt;

        byte error = 0;
        var pos = width;
        var val = (byte) (Read(table, frame) + div);

        while (--pos != 0)
        {
            error += remainder;
            if (error >= width)
            {
                error -= width;
                if (sign)
                    _ = unchecked(--val);
                // if input is 0, leave it alone
                else if (val != 0)
                    _ = unchecked(++val);
            }

            Write(table, unchecked(++frame), val);
            val += div;
        }
    }

    /// <summary>
    /// Interpolates pitch values between phonemes.
    /// </summary>
    internal void InterpolatePitch(byte pos, byte framePos, byte phase3, ReadOnlySpan<byte> phonemeLengthOutput)
    {
        // Pitches interpolate from the middle of current phoneme to middle of next
        var curWidth = (byte) (phonemeLengthOutput[pos] / 2);
        var nextWidth = (byte) (phonemeLengthOutput[(byte) unchecked(pos + 1)] / 2);
        var width = (byte) (curWidth + nextWidth);
        var pitch = (sbyte) (Pitches[nextWidth + framePos] - Pitches[framePos - curWidth]);
        Interpolate(width, 168, phase3, pitch);
    }


    /// <summary>
    /// Creates smooth transitions between phonemes based on their ranks and blend lengths.
    /// </summary>
    /// <returns>Total length of the processed phonemes.</returns>
    public byte CreateTransitions(ReadOnlySpan<byte> phonemeLengthOutput)
    {
        byte framePos = 0;
        byte pos = 0;

        for (;;)
        {
            var phoneme = phonemeLengthOutput[pos];
            var nextPhoneme = phonemeLengthOutput[(byte) unchecked(pos + 1)];

            if (nextPhoneme == 255) break; // 255 == end_token

            // Get the ranking of each phoneme
            var nextRank = Resources.BlendRank[nextPhoneme];
            var rank = Resources.BlendRank[phoneme];

            // Determine blend lengths based on rank comparison
            byte phase1, phase2;

            if (rank == nextRank)
            {
                // Same rank, use out blend lengths from each phoneme
                phase1 = Resources.OutBlendLength[phoneme];
                phase2 = Resources.OutBlendLength[nextPhoneme];
            }
            else if (rank < nextRank)
            {
                // Next phoneme is stronger
                phase1 = Resources.InBlendLength[nextPhoneme];
                phase2 = Resources.OutBlendLength[nextPhoneme];
            }
            else
            {
                // Current phoneme is stronger
                phase1 = Resources.OutBlendLength[phoneme];
                phase2 = Resources.InBlendLength[phoneme];
            }

            framePos += phonemeLengthOutput[pos];

            var speedcounter = (byte) (framePos + phase2);
            var phase3 = (byte) (framePos - phase1);
            var transition = (byte) (phase1 + phase2);

            if (((transition - 2) & 128) == 0)
            {
                InterpolatePitch(pos, framePos, phase3, phonemeLengthOutput);

                // Interpolate all frequency and amplitude parameters
                for (byte table = 169; table < 175; _ = unchecked(++table))
                {
                    var value = (sbyte)
                        (Read(table, speedcounter)
                         - Read(table, phase3));
                    Interpolate(transition, table, phase3, value);
                }
            }

            _ = unchecked(++pos);
        }

        // Add the length of the final phoneme
        return (byte) (framePos + phonemeLengthOutput[pos]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AdvancePhases(byte index, ref byte phase1, ref byte phase2, ref byte phase3)
    {
        phase1 += Frequency1[index];
        phase2 += Frequency2[index];
        phase3 += Frequency3[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte CalculateGlottalWindow(byte index)
    {
        var glottalPulse = Pitches[index];
        return (byte) (glottalPulse - (glottalPulse >> 2)); // 75% of glottal pulse
    }

    private void CombineGlottalAndFormants<T>(byte phase1, byte phase2, byte phase3, byte y,
        ref SampleBuffer<T> outputBuffer)
        where T : unmanaged
    {
        uint tmp = Resources.MultTable[Resources.Sinus[phase1] | Amplitude1[y]];
        tmp += Resources.MultTable[Resources.Sinus[phase2] | Amplitude2[y]];

        tmp += tmp > 255 ? 1u : 0u; // If addition overflows, add one
        tmp += Resources.MultTable[Resources.Rectangle[phase3] | Amplitude3[y]];
        tmp += 136u;
        tmp >>= 4; // Scale down to 0..15 range

        OutputSample<T>((byte) (tmp & 0xf), ref outputBuffer);
    }

    /// <summary>
    /// Renders audio frames based on sound parameters
    /// </summary>
    /// <param name="frameCount">Number of frames to process</param>
    /// <param name="speed">Speed of processing</param>
    /// <param name="outputBuffer">Output buffer for audio samples</param>
    public void ProcessFrames<T>(byte frameCount, byte speed, ref SampleBuffer<T> outputBuffer)
        where T : unmanaged
    {
        var sampledConsonantFlags = SampledConsonantFlags;

        byte speedCounter = 72;
        byte phase1 = 0;
        byte phase2 = 0;
        byte phase3 = 0;
        byte mem66 = 0;

        byte y = 0;

        var glottalPulse = Pitches[0];
        var glottalWindow = CalculateGlottalWindow(0);

        while (frameCount > 0)
        {
            var flags = sampledConsonantFlags[y];

            // Unvoiced sampled phoneme?
            if ((flags & 248) != 0)
            {
                RenderSample(ref mem66, flags, y, ref outputBuffer);
                // Skip ahead two in the phoneme buffer
                y += 2;
                frameCount -= 2;
                speedCounter = speed;
            }
            else
            {
                CombineGlottalAndFormants(phase1, phase2, phase3, y, ref outputBuffer);

                _ = unchecked(--speedCounter);
                if (speedCounter == 0)
                {
                    _ = unchecked(++y); // Go to next amplitude
                    _ = unchecked(--frameCount);
                    if (frameCount == 0) return;
                    speedCounter = speed;
                }

                _ = unchecked(--glottalPulse);

                if (glottalPulse != 0)
                {
                    // Not finished with a glottal pulse
                    _ = unchecked(--glottalWindow);

                    // Within the first 75% of the glottal pulse?
                    if ((glottalWindow != 0) || (flags == 0))
                    {
                        AdvancePhases(y, ref phase1, ref phase2, ref phase3);
                        continue;
                    }

                    // Voiced sampled phonemes interleave the sample with the glottal pulse
                    RenderSample(ref mem66, flags, y, ref outputBuffer);
                }
            }

            glottalPulse = Pitches[y];
            glottalWindow = CalculateGlottalWindow(y);

            // Reset the formant wave generators to sync with the glottal pulse
            phase1 = 0;
            phase2 = 0;
            phase3 = 0;
        }
    }

    private void RenderSample<T>(ref byte position, byte consonantFlag, byte index, ref SampleBuffer<T> outputBuffer)
        where T : unmanaged
    {
        // Mask low three bits and subtract 1 to get value for converting 0 bits on unvoiced samples
        var hibyte = (byte) ((consonantFlag & 7) - 1);

        // Determine which offset to use (forms table offset into sample data)
        var sampleOffset = hibyte * 256;

        // Check if this is a voiced sample
        var pitchLevel = (byte) (consonantFlag & 248);
        if (pitchLevel == 0)
        {
            // Voiced phoneme: Z*, ZH, V*, DH
            pitchLevel = (byte) (Pitches[index] >> 4);
            position = RenderVoicedSample(sampleOffset, position, (byte) (pitchLevel ^ 255), ref outputBuffer);
        }
        else
        {
            // Unvoiced phoneme
            RenderUnvoicedSample(sampleOffset, (byte) (pitchLevel ^ 255), Resources.UnvoicedConsonantAmplitudes[hibyte],
                ref outputBuffer);
        }
    }

    private byte RenderVoicedSample<T>(int sampleOffset, byte position, byte phase, ref SampleBuffer<T> outputBuffer)
        where T : unmanaged
    {
        do
        {
            byte bit = 8;
            var sample = Resources.SampleTable[sampleOffset + position];
            do
            {
                // Output samples based on bit pattern
                if ((sample & 128) != 0)
                    OutputSample(26, ref outputBuffer);
                else
                    OutputSample(6, ref outputBuffer);
                sample <<= 1;
            } while (unchecked(--bit) != 0);

            _ = unchecked(++position);
        } while (unchecked(++phase) != 0);

        return position;
    }

    private void RenderUnvoicedSample<T>(int sampleOffset, byte position, byte amplitude,
        ref SampleBuffer<T> outputBuffer)
        where T : unmanaged
    {
        do
        {
            byte bit = 8;
            var sample = Resources.SampleTable[sampleOffset + position];
            do
            {
                // Output samples based on bit pattern
                if ((sample & 128) != 0)
                    OutputSample(5, ref outputBuffer);
                else
                    OutputSample(amplitude, ref outputBuffer);
                sample <<= 1;
            } while (unchecked(--bit) != 0);
        } while (unchecked(++position) != 0);
    }

    internal void OutputSample<T>(byte value, ref SampleBuffer<T> outputBuffer)
        where T : unmanaged
    {
        const byte sampleMax = (1 << 4) - 1; // 4-bit sample, 0 to 15
        value = Math.Min(value, sampleMax);

        if (SampleBuffer<T>.Versus<sbyte>.IsSameType)
            // Convert 4-bit value to 8-bit signed sample in -128 to 127 range
            outputBuffer.As<sbyte>()
                .WriteAndCommit((sbyte) ((sbyte) (value * (byte.MaxValue / sampleMax)) + sbyte.MinValue));
        else if (SampleBuffer<T>.Versus<byte>.IsSameType)
            // Convert 4-bit value to 8-bit unsigned sample in 0 to 255 range
            outputBuffer.As<byte>().WriteAndCommit((byte) (value * (byte.MaxValue / sampleMax)));
        else if (SampleBuffer<T>.Versus<short>.IsSameType)
            // Convert 4-bit value to signed 16-bit sample in -32768 to 32767 range
            outputBuffer.As<short>()
                .WriteAndCommit((short) ((short) (value * (ushort.MaxValue / sampleMax)) + short.MinValue));
        else if (SampleBuffer<T>.Versus<ushort>.IsSameType)
            // Convert 4-bit value to unsigned 16-bit sample in 0 to 65535 range
            outputBuffer.As<ushort>().WriteAndCommit((ushort) (value * (ushort.MaxValue / sampleMax)));
        else if (SampleBuffer<T>.Versus<int>.IsSameType)
            // Convert 4-bit value to signed 32-bit sample in -2147483648 to 2147483647 range
            outputBuffer.As<int>().WriteAndCommit((int) (value * (uint.MaxValue / sampleMax)) + int.MinValue);
        else if (SampleBuffer<T>.Versus<uint>.IsSameType)
            // Convert 4-bit value to unsigned 32-bit sample in 0 to 4294967295 range
            outputBuffer.As<uint>().WriteAndCommit(value * (uint.MaxValue / sampleMax));
        else if (SampleBuffer<T>.Versus<long>.IsSameType)
            // Convert 4-bit value to signed 64-bit sample in min to max range
            outputBuffer.As<long>().WriteAndCommit((long) (value * (ulong.MaxValue / sampleMax)) + long.MinValue);
        else if (SampleBuffer<T>.Versus<ulong>.IsSameType)
            // Convert 4-bit value to unsigned 64-bit sample in 0 to max range
            outputBuffer.As<ulong>().WriteAndCommit(value * (ulong.MaxValue / sampleMax));
        // TODO: support Half later
        else if (SampleBuffer<T>.Versus<float>.IsSameType)
            // Convert 4-bit value to floating-point sample in -1.0 to 1.0 range
            outputBuffer.As<float>().WriteAndCommit(value / (float) sampleMax * 2.0f - 1.0f);
        else if (SampleBuffer<T>.Versus<double>.IsSameType)
            // Convert 4-bit value to double-precision sample in -1.0 to 1.0 range
            outputBuffer.As<double>().WriteAndCommit(value / (double) sampleMax * 2.0 - 1.0);
        else
            throw new NotImplementedException($"OutputSample not implemented for {typeof(T).Name}");
    }

    /// <summary>
    /// Creates a rising or falling inflection 30 frames prior to the given position.
    /// </summary>
    /// <param name="inflection">Type of inflection (rising or falling)</param>
    /// <param name="pos">Position to start the inflection</param>
    public void AddInflection(InflectionType inflection, byte pos)
    {
        byte pitch;
        var end = pos;

        if (pos < 30) pos = 0;
        else pos -= 30;

        // Find first valid pitch
        while ((pitch = Pitches[pos]) == 127)
            _ = unchecked(++pos);

        while (pos != end)
        {
            // Add inflection direction
            pitch += (byte) inflection;
            Pitches[pos] = pitch;

            while (unchecked(++pos) != end && Pitches[pos] == 255)
            {
                // Skip
            }
        }
    }

    /// <summary>
    /// Creates frames from phoneme data, expanding each phoneme to its specified length.
    /// </summary>
    /// <param name="phonemeIndexOutput">Phoneme index data</param>
    /// <param name="stressOutput">Stress data</param>
    /// <param name="phonemeLengthOutput">Phoneme length data</param>
    /// <param name="basePitch">Base pitch for phonemes</param>
    public void CreateFrames(ReadOnlySpan<byte> phonemeIndexOutput,
        ReadOnlySpan<byte> stressOutput,
        ReadOnlySpan<byte> phonemeLengthOutput,
        byte basePitch)
    {
        var sampledConsonantFlags = SampledConsonantFlags;

        byte x = 0;
        var i = 0;

        while (i < 256)
        {
            var phoneme = phonemeIndexOutput[i];
            if (phoneme == 255) break; // Terminal phoneme

            switch (phoneme)
            {
                case (byte) Phoneme.Period:
                    AddInflection(InflectionType.Rising, x);
                    break;
                case (byte) Phoneme.Question:
                    AddInflection(InflectionType.Falling, x);
                    break;
            }

            // Get stress amount (more stress = higher pitch)
            var phase1 = Resources.StressToPitchTable[stressOutput[i] + 1];
            var phase2 = phonemeLengthOutput[i];

            // Copy from source to frames list
            do
            {
                Frequency1[x] = Resources.Freq1Data[phoneme];
                Frequency2[x] = Resources.Freq2Data[phoneme];
                Frequency3[x] = Resources.Freq3Data[phoneme];
                Amplitude1[x] = Resources.Ampl1Data[phoneme];
                Amplitude2[x] = Resources.Ampl2Data[phoneme];
                Amplitude3[x] = Resources.Ampl3Data[phoneme];
                sampledConsonantFlags[x] = sampledConsonantFlags[phoneme];
                Pitches[x] = (byte) (basePitch + phase1);
                _ = unchecked(++x);
            } while (unchecked(--phase2) != 0);

            ++i;
        }
    }

    /// <summary>
    /// Rescales amplitude values from linear scale to decibels.
    /// </summary>
    public void RescaleAmplitude()
    {
        for (var i = 255; i >= 0; i--)
        {
            Amplitude1[i] = Resources.AmplitudeRescale[Amplitude1[i]];
            Amplitude2[i] = Resources.AmplitudeRescale[Amplitude2[i]];
            Amplitude3[i] = Resources.AmplitudeRescale[Amplitude3[i]];
        }
    }

    /// <summary>
    /// Adjusts pitch contour by subtracting half of F1 frequency from pitch.
    /// </summary>
    public void AssignPitchContour()
    {
        for (var i = 0; i < 256; i++)
        {
            // Subtract half the frequency of formant 1 to add variety
            Pitches[i] -= (byte) (Frequency1[i] >> 1);
        }
    }

    /// <summary>
    /// Renders phonemes into sound samples
    /// </summary>
    /// <param name="phonemeIndexOutput">Phoneme index data</param>
    /// <param name="stressOutput">Stress data</param>
    /// <param name="phonemeLengthOutput">Phoneme length data</param>
    /// <param name="outputBuffer">Output buffer for audio samples</param>
    /// <param name="singMode">Flag for singing mode</param>
    /// <param name="speed">Speed of processing</param>
    /// <param name="basePitch">Base pitch for phonemes</param>
    public void Render<T>(ReadOnlySpan<byte> phonemeIndexOutput,
        ReadOnlySpan<byte> stressOutput,
        ReadOnlySpan<byte> phonemeLengthOutput,
        ref SampleBuffer<T> outputBuffer,
        bool singMode = false,
        byte speed = 72,
        byte basePitch = 64)
        where T : unmanaged
    {
        if (phonemeIndexOutput[0] == 255) return; // Exit if no data

        CreateFrames(phonemeIndexOutput, stressOutput, phonemeLengthOutput, basePitch);
        var frameCount = CreateTransitions(phonemeLengthOutput);

        if (!singMode) AssignPitchContour();
        RescaleAmplitude();

        ProcessFrames(frameCount, speed, ref outputBuffer);
    }

    /// <summary>
    /// Helper method for transforming formant values
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Trans(byte a, byte b) =>
        (byte) (((uint) a * b >> 8) << 1);

    /// <summary>
    /// Alters SAM's voice by changing frequencies of mouth and throat formants
    /// </summary>
    /// <param name="mouth">Value controlling mouth formant frequencies (F1)</param>
    /// <param name="throat">Value controlling throat formant frequencies (F2)</param>
    public void SetMouthThroat(byte mouth, byte throat)
    {
        var freq1Data = Freq1Data;
        var freq2Data = Freq2Data;

        // Formant tables are accessed through the Sam class

        byte newFrequency = 0;
        byte pos = 5;

        // Recalculate formant frequencies 5..29
        while (pos < 30)
        {
            // Mouth frequency (F1)
            var initialFrequency = Resources.MouthFormantsPrimary[pos];
            if (initialFrequency != 0)
                newFrequency = Trans(mouth, initialFrequency);
            freq1Data[pos] = newFrequency;

            // Throat frequency (F2)
            initialFrequency = Resources.ThroatFormantsPrimary[pos];
            if (initialFrequency != 0)
                newFrequency = Trans(throat, initialFrequency);
            freq2Data[pos] = newFrequency;
            _ = unchecked(++pos);
        }

        // Recalculate formant frequencies 48..53
        pos = 0;
        while (pos < 6)
        {
            // F1 (mouth formant)
            var initialFrequency = Resources.MouthFormantsSecondary[pos];
            freq1Data[pos + 48] = Trans(mouth, initialFrequency);

            // F2 (throat formant)
            initialFrequency = Resources.ThroatFormantsSecondary[pos];
            freq2Data[pos + 48] = Trans(throat, initialFrequency);
            _ = unchecked(++pos);
        }
    }

    /// <summary>
    /// Converts input text to phoneme codes using the reciter rules
    /// </summary>
    /// <param name="input">Input text to convert</param>
    /// <param name="output">Buffer to store the resulting phoneme codes</param>
    /// <returns><see langword="true"/> if conversion succeeded, <see langword="false"/> otherwise</returns>
    public bool TextToPhonemes(ReadOnlySpan<byte> input, Span<byte> output)
    {
        const byte EndOfRuleMask = 0x80;
        const byte RuleCharacterMask = 0x7F;
        const byte PhonemeEndMarker = 0x9B;
        const byte InputEndMarker = 0x1B;

        if (input.IsEmpty)
        {
            output[0] = PhonemeEndMarker; // End marker
            return true;
        }

        // Temporary buffer to hold a copy of the input text
        Span<byte> inputBuffer = stackalloc byte[256];
        inputBuffer[0] = (byte) ' ';

        // Variable declarations with meaningful names
        byte charValue;
        byte charFlags;
        byte outputIndex = 255; // Start at -1 (255 unsigned)
        byte inputIndex = 255; // Start at -1 (255 unsigned)
        byte matchEndPos;
        ushort ruleStart;
        int ruleResult;

        // Copy and normalize input text
        byte charIndex = 0;
        do
        {
            charValue = charIndex < input.Length
                ? (byte) (input[charIndex] & RuleCharacterMask)
                : (byte) ' ';
            switch (charValue)
            {
                // convert to uppercase
                case >= 0x70:
                    charValue = (byte) (charValue & 0x5F);
                    break;
                case >= 0x60:
                    charValue = (byte) (charValue & 0x4F);
                    break;
                case 0:
                    inputBuffer[unchecked(++charIndex)] = InputEndMarker;
                    break; // End of input
            }

            inputBuffer[unchecked(++charIndex)] = charValue;
        } while (charIndex < 255);

        inputBuffer[255] = InputEndMarker; // End marker

        // Main processing loop
        ProcessNextChar:
        for (;;)
        {
            for (;;)
            {
                charIndex = unchecked(++inputIndex);
                var currentChar = inputBuffer[charIndex];

                // Check for end of input marker
                if (currentChar is (byte) '[')
                {
                    charIndex = unchecked(++outputIndex);
                    output[charIndex] = PhonemeEndMarker; // End marker
                    return true;
                }

                if (currentChar is not (byte) '.') break;

                _ = unchecked(charIndex++);
                var value = inputBuffer[charIndex];
                charValue = (byte) (Resources.CharacterClasses[value] & 1);
                if (charValue is not 0) break;

                charIndex = unchecked(++outputIndex);
                output[charIndex] = (byte) '.';
            }

            // Check character flags
            var value1 = inputBuffer[charIndex];
            charFlags = Resources.CharacterClasses[value1];
            if ((charFlags & 2) != 0)
            {
                ruleStart = Rules2TableStart;
                // ?
                goto FindMatchingRule;
            }

            if (charFlags != 0) break;

            // Handle space
            inputBuffer[charIndex] = (byte) ' ';
            charIndex = unchecked(++outputIndex);
            if (charIndex > 120)
            {
                output[charIndex] = PhonemeEndMarker;
                return true;
            }

            output[charIndex] = (byte) ' ';
        }

        // Verify valid character
        if ((charFlags & EndOfRuleMask) == 0) return false;

        // Look up rules for this character
        charIndex = (byte) (inputBuffer[inputIndex] - (byte) 'A');
        ruleStart = (ushort) (Resources.PhonemeRuleLowBytes[charIndex] |
                              (Resources.PhonemeRuleHighBytes[charIndex] << 8));

        var endOfInput = inputBuffer.IndexOf(InputEndMarker);
        Trace.WriteLine($"Input: {Encoding.ASCII.GetString(inputBuffer.Slice(0, endOfInput))}");

        FindMatchingRule:
        ++ruleStart;

#if DEBUG
        if (ruleStart < RulesTableStart)
            throw new InvalidOperationException("Invalid rule start");
        Trace.WriteLine($"Seeking through rule @ {ruleStart} matching {(char) inputBuffer[inputIndex]}");
        Trace.WriteLine($"Rule group: {ExtractRuleGroup(ruleStart)}");

#endif
        // Find start of next rule
        while ((GetRuleByte(ruleStart, 0) & EndOfRuleMask) == 0)
        {
            ++ruleStart;
        }

        var ruleOffset = 0;
        // find open parenthesis
        while (GetRuleByte(ruleStart, ruleOffset) is not (byte) '(')
            ++ruleOffset;

        var openParenPos = ruleOffset;

        // find closing parenthesis
        while (GetRuleByte(ruleStart, ruleOffset) is not (byte) ')')
            ++ruleOffset;

        var closeParenPos = ruleOffset;

        // find equal sign
        while ((GetRuleByte(ruleStart, ruleOffset) & RuleCharacterMask) is not (byte) '=')
            ++ruleOffset;

        var equalSignPos = ruleOffset;

        var patternMatchPos = inputIndex;
        charIndex = inputIndex;

        // Compare the string within the parentheses with the input text
        ruleOffset = openParenPos + 1;

        // Check if the rule pattern matches the input text
        for (;;)
        {
            var ruleByte = GetRuleByte(ruleStart, ruleOffset);
            if (ruleByte != inputBuffer[charIndex])
            {
#if DEBUG
                Trace.WriteLine($"Rule did not match: {ruleByte} != {inputBuffer[charIndex]}");
#endif
                goto FindMatchingRule;
            }

            if (++ruleOffset == closeParenPos)
                break;
            patternMatchPos = ++charIndex;
        }

        // The string in the bracket matches, now check prefix conditions
        var scanBackwardPos = inputIndex;

        // Check prefix conditions
        for (;;)
        {
            for (;;)
            {
                --openParenPos;

                charFlags = GetRuleByte(ruleStart, openParenPos);
                if ((charFlags & EndOfRuleMask) != 0)
                {
                    matchEndPos = patternMatchPos;
                    goto ProcessSuffix;
                }

                charIndex = (byte) (charFlags & RuleCharacterMask);
                if ((Resources.CharacterClasses[charIndex] & EndOfRuleMask) == 0)
                    break;
                var inputByte = inputBuffer[(byte) unchecked(scanBackwardPos - 1)];
                if (inputByte != charFlags)
                {
#if DEBUG
                    Trace.WriteLine($"Rule did not match: {charFlags} != {inputByte}");
#endif
                    goto FindMatchingRule;
                }

                _ = unchecked(--scanBackwardPos);
            }

            var conditionChar = charFlags;
            ruleResult = CheckPrefixCondition(conditionChar, unchecked((byte) (scanBackwardPos - 1)), inputBuffer);

            if (ruleResult == -1)
            {
                // Special prefix conditions
                switch (conditionChar)
                {
                    case (byte) '&':
                        if (GetCharacterClass(unchecked((byte) (scanBackwardPos - 1)),
                                CharacterClass.DigraphWithH,
                                inputBuffer) == 0)
                        {
                            if (inputBuffer[charIndex] is not (byte) 'H')
                            {
#if DEBUG
                                Trace.WriteLine($"Rule '&' did not match: {inputBuffer[charIndex]} != H");
#endif
                                ruleResult = 1;
                                goto FindMatchingRule;
                            }
                            else
                            {
                                charValue = inputBuffer[unchecked(--charIndex)];
                                if (charValue is not ((byte) 'C' or (byte) 'S'))
                                {
#if DEBUG
                                    Trace.WriteLine($"Rule '&' did not match: {charValue} != C|S");
#endif
                                    ruleResult = 1;
                                    goto FindMatchingRule;
                                }
                            }
                        }

                        break;

                    case (byte) '@':
                        if (GetCharacterClass(unchecked((byte) (scanBackwardPos - 1)),
                                CharacterClass.CombiningConsonant, inputBuffer) == 0)
                        {
                            charValue = inputBuffer[charIndex];
                            if (charValue is not (byte) 'H')
                            {
#if DEBUG
                                Trace.WriteLine($"Rule '@' did not match: {charValue} != H");
#endif
                                ruleResult = 1;
                                goto FindMatchingRule;
                            }
                            if (charValue is not ((byte) 'T' or (byte) 'C' or (byte) 'S'))
                            {
#if DEBUG
                                Trace.WriteLine($"Rule '@' did not match: {charValue} != T|C|S");
#endif
                                ruleResult = 1;
                                goto FindMatchingRule;
                            }
                        }

                        break;

                    case (byte) '+':
                        charIndex = scanBackwardPos;
                        charValue = inputBuffer[unchecked(--charIndex)];
                        if (charValue is not ((byte) 'E' or (byte) 'I' or (byte) 'Y'))
                        {
#if DEBUG
                            Trace.WriteLine($"Rule '+' did not match: {charValue} != E|I|Y");
#endif
                            ruleResult = 1;
                            goto FindMatchingRule;
                        }

                        break;

                    case (byte) ':':
                        while (GetCharacterClass(unchecked((byte) (scanBackwardPos - 1)),
                                   CharacterClass.Vowel, inputBuffer) != 0)
                            _ = unchecked(--scanBackwardPos);
                        continue;

                    default:
                    {
                        #if DEBUG
                            Trace.WriteLine($"Unknown prefix condition: {conditionChar}");
                        #endif
                        return false;
                    }
                }
            }

            if (ruleResult == 1)
            {
#if DEBUG
                Trace.WriteLine($"Rule did not match (???)");
#endif
                goto FindMatchingRule;
            }

            scanBackwardPos = charIndex;
        }

        ProcessSuffix:
        do
        {
            // Check for common endings like "ING" or "E+consonant"
            charIndex = unchecked((byte) (matchEndPos + 1));
            if (inputBuffer[charIndex] is (byte) 'E')
            {
                var value = inputBuffer[(byte) unchecked(charIndex + 1)];
                if ((Resources.CharacterClasses[value] & EndOfRuleMask) != 0)
                {
                    charValue = inputBuffer[unchecked(++charIndex)];
                    if (charValue is (byte) 'L')
                    {
                        if (inputBuffer[unchecked(++charIndex)] is not (byte) 'Y')
                        {
#if DEBUG
                            Trace.WriteLine($"Rule did not match ELY");
#endif
                            goto FindMatchingRule;
                        }
                    }
                    else
                    {
                        if (charValue is not ((byte) 'R' or (byte) 'S' or (byte) 'D') &&
                            !inputBuffer.Slice(charIndex).StartsWith("FUL"u8))
                        {
#if DEBUG
                            Trace.WriteLine($"Rule did not match R|S|D|FUL");
#endif
                            goto FindMatchingRule;
                        }
                    }
                }
            }
            else
            {
                if (!inputBuffer.Slice(charIndex).StartsWith("ING"u8))
                {
#if DEBUG
                    Trace.WriteLine($"Rule did not match ING");
#endif
                    goto FindMatchingRule;
                }

                matchEndPos = charIndex;
            }

            // Process the rule action part (after =)
            ruleResult = 0;
            do
            {
                for (;;)
                {
                    ruleOffset = closeParenPos + 1;
                    if (ruleOffset == equalSignPos)
                    {
                        inputIndex = patternMatchPos;

                        // Apply the rule - output phoneme codes
                        for (;;)
                        {
                            charFlags = charValue = GetRuleByte(ruleStart, ruleOffset);
                            charValue = (byte) (charValue & RuleCharacterMask);
                            if (charValue != (byte) '=')
                                output[unchecked(++outputIndex)] = charValue;
                            if ((charFlags & EndOfRuleMask) != 0)
                                goto ProcessNextChar;
                            ruleOffset++;
                        }
                    }

                    closeParenPos = ruleOffset;
                    charFlags = GetRuleByte(ruleStart, ruleOffset);
                    if ((Resources.CharacterClasses[charFlags] & EndOfRuleMask) == 0) break;
                    if (inputBuffer[(byte) unchecked(matchEndPos + 1)] != charFlags)
                    {
                        ruleResult = 1;
                        break;
                    }

                    _ = unchecked(++matchEndPos);
                }

                // Handle suffix conditions
                if (ruleResult == 0)
                {
                    charValue = charFlags;
                    switch (charValue)
                    {
                        case (byte) '@':
                        {
                            if (GetCharacterClass(unchecked((byte) (matchEndPos + 1)),
                                    CharacterClass.CombiningConsonant,
                                    inputBuffer) == 0)
                            {
                                charValue = inputBuffer[charIndex];
                                if (charValue is not ((byte) 'R'
                                    or (byte) 'T'
                                    or (byte) 'C'
                                    or (byte) 'S'))
                                    ruleResult = 1;
                            }
                            else
                            {
                                ruleResult = -2;
                            }

                            break;
                        }
                        case (byte) ':':
                        {
                            while (GetCharacterClass(unchecked((byte) (matchEndPos + 1)), CharacterClass.Vowel,
                                       inputBuffer) != 0)
                                matchEndPos = charIndex;
                            ruleResult = -2;
                            break;
                        }
                        default:
                            ruleResult = CheckSuffixCondition(charValue, unchecked((byte) (matchEndPos + 1)),
                                inputBuffer);
                            break;
                    }
                }

                switch (ruleResult)
                {
                    case 1:
                    {
                        // ?
                        goto FindMatchingRule;
                    }
                    case -2:
                        ruleResult = 0;
                        continue;
                    case 0:
                        matchEndPos = charIndex;
                        break;
                }
            } while (ruleResult == 0);
        } while (charValue is (byte) '%');

        return false;
    }

    private string ExtractRuleGroup(ushort ruleStart)
    {
        // structure of rule groups is (start char) (rule*) ( ']' | eos )
        // where char is [A-Z],
        // rule is [^=] '=' [^\x80-\xFF],
        // eos is '\xEA' (aka 'j' bit or'd with 0x80)
        // so just seek until ']'
        var ruleGroup = new StringBuilder();
        var ruleEnd = true;
        bool groupEnd;
        var i = 0;
        do
        {
            byte v;
            try
            {
                v = GetRuleByte(ruleStart, i++);
            }
            catch
            {
                ruleGroup.Append("💥");
                break;
            }

            ruleGroup.Append((char) (v & 0x7F));
            var prevRuleEnd = ruleEnd;
            ruleEnd = (v & 0x80) != 0;
            if (ruleEnd)
                ruleGroup.Append("🔚\n");
            if (i >= 65536)
            {
                ruleGroup.Append("☠️");
                break;
            }

            groupEnd = v == (byte) ']'
                       || (ruleEnd && prevRuleEnd && i > 1);
        } while (!groupEnd);

        return ruleGroup.ToString();
    }

    private string ExtractRule(ushort ruleStart)
    {
        var rule = new StringBuilder();
        bool end;
        var i = 0;
        do
        {
            byte v;
            try
            {
                v = GetRuleByte(ruleStart, i++);
            }
            catch
            {
                rule.Append("💥");
                break;
            }

            rule.Append((char) (v & 0x7F));
            end = (v & 0x80) != 0;
            if (end) rule.Append("🔚");
        } while (!end);

        return rule.ToString();
    }

    /// <summary>
    /// Retrieves a byte from the rules table at the specified address and offset.
    /// </summary>
    /// <param name="baseAddress">Base address of the rule (32000-37540 uses Rules, 37541+ uses Rules2)</param>
    /// <param name="offset">Offset within the rule</param>
    /// <returns>The byte value at the specified position in the rule</returns>
    internal byte GetRuleByte(int baseAddress, int offset)
        => baseAddress < Rules2TableStart
            ? Resources.Rules[baseAddress - RulesTableStart + offset]
            : Resources.Rules2[baseAddress - Rules2TableStart + offset];

    /// <summary>
    /// Gets character attributes from a lookup table and applies a bitmask to check specific properties.
    /// </summary>
    /// <param name="inputPos">Position in the input buffer to check</param>
    /// <param name="mask">Bitmask for the specific property to check (128=space, 64=digit, 32=vowel, etc.)</param>
    /// <param name="inputBuffer">Input buffer containing text being processed</param>
    /// <returns>Non-zero if the character has the specified property, zero otherwise</returns>
    private byte GetCharacterClass(byte inputPos, CharacterClass mask, Span<byte> inputBuffer)
        => (byte) (Resources.CharacterClasses[inputBuffer[inputPos]] & (byte) mask);


    /// <summary>
    /// Checks the suffix condition of a specific character.
    /// </summary>
    /// <param name="ch">Character to check, representing the condition type</param>
    /// <param name="position"> Position in the input buffer to check</param>
    /// <param name="input">Input buffer containing text being processed</param>
    /// <returns>0 if the condition is met, 1 if not, -1 if the character is not valid</returns>
    private int CheckSuffixCondition(byte ch, byte position, Span<byte> input)
    {
        var cursor = position;
        var value = input[cursor];
        var chProp = Resources.CharacterClasses[value];

        switch (ch)
        {
            case (byte) ' ':
                if ((chProp & (byte) CharacterClass.Space) != 0) return 1;
                break;
            case (byte) '#':
                if ((chProp & (byte) CharacterClass.Digit) == 0)
                    return 1;
                break;
            case (byte) '.':
                if ((chProp & (byte) CharacterClass.Period) == 0)
                    return 1;
                break;
            case (byte) '&':
                if ((chProp & (byte) CharacterClass.DigraphWithH) == 0)
                {
                    if (input[cursor] != (byte) 'H')
                        return 1; // 'H'
                    _ = unchecked(++cursor);
                }

                break;
            case (byte) '^':
                if ((chProp & (byte) CharacterClass.Vowel) == 0) return 1;
                break;
            case (byte) '+':
                cursor = position;
                ch = input[cursor];
                if (ch is not ((byte) 'E' or (byte) 'I' or (byte) 'Y'))
                    return 1;
                break;
            default:
                return -1;
        }

        return 0;
    }

    /// <summary>
    /// Checks the prefix condition of a specific character.
    /// </summary>
    /// <param name="ch">Character to check, representing the condition type</param>
    /// <param name="position">Position in the input buffer to check</param>
    /// <param name="input">Input buffer containing text being processed</param>
    /// <returns>0 if the condition is met, 1 if not, -1 if the character is not valid</returns>
    private int CheckPrefixCondition(byte ch, byte position, Span<byte> input)
    {
        var value = input[position];
        var chProp = Resources.CharacterClasses[value];

        switch (ch)
        {
            case (byte) ' ':
                if ((chProp & (byte) CharacterClass.Space) != 0)
                    return 1;
                break;
            case (byte) '#':
                if ((chProp & (byte) CharacterClass.Digit) == 0)
                    return 1;
                break;
            case (byte) '.':
                if ((chProp & (byte) CharacterClass.Period) == 0)
                    return 1;
                break;
            case (byte) '^':
                if ((chProp & (byte) CharacterClass.Vowel) == 0)
                    return 1;
                break;
            default:
                return -1;
        }

        return 0;
    }

    private bool Match(ReadOnlySpan<byte> str, Span<byte> input)
        => str.SequenceEqual(input);

    /// <summary>
    /// Parses the initial phoneme string and converts to phoneme indices
    /// </summary>
    /// <param name="input">Input buffer containing phoneme text</param>
    /// <param name="phonemeIndex">Buffer to store the resulting phoneme indices</param>
    /// <param name="stress">Buffer to store the stress values for each phoneme</param>
    /// <returns><see langword="true"/> if parsing succeeded, <see langword="false"/> otherwise</returns>
    public bool Parser1(ReadOnlySpan<byte> input, Span<byte> phonemeIndex, Span<byte> stress)
    {
        const byte EndMarker = 155; // End of line marker (0x9B)
        const byte End = 255; // End of buffer marker

        byte position = 0; // Position in output buffer
        byte srcPos = 0; // Position in input buffer

        // Clear the stress table
        stress.Clear();

        // Process until we reach the end marker (155 or 0x9B)
        while (srcPos < input.Length
               && input[srcPos] != EndMarker)
        {
            int match;
            var sign1 = input[unchecked(srcPos++)];

            // Try to match as a two-character phoneme first
            if (srcPos < input.Length)
            {
                var sign2 = input[srcPos];
                match = FullMatch(sign1, sign2);

                if (match != -1)
                {
                    // Matched both characters (no wildcards)
                    phonemeIndex[unchecked(position++)] = (byte) match;
                    srcPos++; // Skip the second character as we've matched it
                    continue;
                }
            }

            // Try to match as a one-character phoneme with wildcard
            match = WildMatch(sign1);
            if (match != -1)
            {
                // Matched just the first character (with second character being wildcard '*')
                phonemeIndex[position++] = (byte) match;
                continue;
            }

            // Should be a stress character. Search through the stress table backwards.
            match = 8; // End of stress table
            while (match > 0 && sign1 != Resources.StressInputTable[match])
                match--;

            if (match == 0)
                return false; // Failed to match anything

            stress[(byte) unchecked(position - 1)] = (byte) match; // Set stress for the prior phoneme
        }

        // Mark end of phoneme data
        phonemeIndex[position] = End;
        return true;
    }

    /// <summary>
    /// Attempts to find a match for a two-character phoneme
    /// </summary>
    private int FullMatch(byte sign1, byte sign2)
    {
        // TODO: Assert Sam.SignInputTable1.Length == 81 and replace 81 with Sam.SignInputTable1.Length 
        for (byte y = 0; y < 81; y++) // 81 is the size of the phoneme tables
        {
            var firstChar = Resources.SignInputTable1[y];
            if (firstChar != sign1) continue;

            var secondChar = Resources.SignInputTable2[y];
            // If not a wildcard and matches second character
            if (secondChar != (byte) '*' && secondChar == sign2)
                return y;
        }

        return -1;
    }

    /// <summary>
    /// Attempts to find a match for a single character phoneme (with wildcard)
    /// </summary>
    private int WildMatch(byte sign1)
    {
        // TODO: Assert Sam.SignInputTable1.Length == 81 and replace 81 with Sam.SignInputTable1.Length 
        for (byte y = 0; y < 81; y++) // 81 is the size of the phoneme tables
            if (Resources.SignInputTable2[y] is (byte) '*'
                && Resources.SignInputTable1[y] == sign1)
                return y;

        return -1;
    }

    /// <summary>
    /// Applies phoneme transformation rules to improve pronunciation
    /// </summary>
    /// <param name="phonemeIndex">Buffer containing phoneme indices</param>
    /// <param name="phonemeLength">Buffer containing phoneme lengths</param>
    /// <param name="stress">Buffer containing stress values</param>
    public void Parser2(Span<byte> phonemeIndex, Span<byte> phonemeLength, Span<byte> stress)
    {
        const byte EndMarker = 255; // End marker
        const byte pR = 23; // 'R' phoneme
        const byte pT = 69; // 'T' phoneme
        const byte pD = 57; // 'D' phoneme

        byte pos = 0;
        byte p;

        while ((p = phonemeIndex[pos]) != EndMarker)
        {
            // Skip pause phonemes
            if (p == 0)
            {
                pos++;
                continue;
            }

            var currentFlags = Resources.Flags[p];
            var prior = pos > 0 ? phonemeIndex[pos - 1] : (byte) 0;

            if ((currentFlags & (byte) PhonemeFlags2.Dipthong) != 0)
            {
                // Handle dipthongs
                HandleDipthong(p, currentFlags, pos, phonemeIndex, stress);
            }
            else if (p == 78) // UL -> AX L
            {
                ChangeRule(pos, 24, phonemeIndex, stress);
            }
            else if (p == 79) // UM -> AX M
            {
                ChangeRule(pos, 27, phonemeIndex, stress);
            }
            else if (p == 80) // UN -> AX N
            {
                ChangeRule(pos, 28, phonemeIndex, stress);
            }
            else if ((currentFlags & (byte) PhonemeFlags2.Vowel) != 0 && stress[pos] != 0)
            {
                // Handle stressed vowels
                // <STRESSED VOWEL> <SILENCE> <STRESSED VOWEL> -> <STRESSED VOWEL> <SILENCE> Q <VOWEL>
                if (phonemeIndex[pos + 1] == 0) // If next is silence
                {
                    var nextPhoneme = phonemeIndex[pos + 2];
                    if (nextPhoneme != EndMarker &&
                        (Resources.Flags[nextPhoneme] & (byte) PhonemeFlags2.Vowel) != 0 &&
                        stress[pos + 2] != 0)
                    {
                        // Insert glottal stop between stressed vowels
                        Insert((byte) unchecked(pos + 2), 31, 0, 0, phonemeIndex, phonemeLength, stress); // 31 = 'Q'
                    }
                }
            }
            else
                switch (p)
                {
                    // Rules for phonemes before R
                    case pR when prior == pT:
                        phonemeIndex[(byte) unchecked(pos - 1)]
                            = (byte) '*'; // T R -> CH R
                        break;
                    case pR when prior == pD:
                        phonemeIndex[(byte) unchecked(pos - 1)]
                            = (byte) ','; // D R -> J R
                        break;
                    case pR:
                    {
                        if ((Resources.Flags[prior] & (byte) PhonemeFlags2.Vowel) != 0)
                            phonemeIndex[pos] = 18; // <VOWEL> R -> <VOWEL> RX
                        break;
                    }
                    case 24 when (Resources.Flags[prior] & (byte) PhonemeFlags2.Vowel) != 0:
                        phonemeIndex[pos] = 19; // <VOWEL> L -> <VOWEL> LX
                        break;
                    default:
                    {
                        if (prior == 60 && p == 32) // G S -> G Z
                            phonemeIndex[pos] = 38;
                        else if (p == 60) // G rules
                            HandleGRule(pos, phonemeIndex);
                        else
                            HandleRemainingRules(p, prior, pos, phonemeIndex, phonemeLength, stress);

                        break;
                    }
                }

            _ = unchecked(++pos);
        }
    }

    /// <summary>
    /// Handles dipthong phonemes by inserting appropriate following phonemes
    /// </summary>
    private void HandleDipthong(byte p, byte flags, byte pos, Span<byte> phonemeIndex, Span<byte> stress)
    {
        // If ends with IY, use YX, else use WX
        var followingPhoneme = (flags & (byte) PhonemeFlags2.DipYX) != 0 ? (byte) 21 : (byte) 20; // 'YX' or 'WX'

        // Insert WX or YX following the dipthong
        Insert((byte) unchecked(pos + 1), followingPhoneme, 0, stress[pos], phonemeIndex, default, stress);

        switch (p)
        {
            // Special handling for certain phonemes
            case 53:
                HandleAlveolarUw(pos, phonemeIndex);
                break;
            case 42:
                HandleCh(pos, phonemeIndex, stress);
                break;
            case 44:
                HandleJ(pos, phonemeIndex, stress);
                break;
        }
    }

    /// <summary>
    /// Handles special case for G followed by vowels
    /// </summary>
    private void HandleGRule(byte pos, Span<byte> phonemeIndex)
    {
        // G <VOWEL OR DIPTHONG NOT ENDING WITH IY> -> GX <VOWEL OR DIPTHONG NOT ENDING WITH IY>
        var nextIndex = phonemeIndex[pos + 1];

        if (nextIndex != 255 && (Resources.Flags[nextIndex] & (byte) PhonemeFlags2.DipYX) == 0)
            // Replace G with GX
            phonemeIndex[pos] = 63; // 'GX'
    }

    /// <summary>
    /// Handles various other phoneme transformation rules
    /// </summary>
    private void HandleRemainingRules(byte p, byte prior, byte pos,
        Span<byte> phonemeIndex, Span<byte> phonemeLength, Span<byte> stress)
    {
        if (p == 72) // 'K'
        {
            // K <VOWEL OR DIPTHONG NOT ENDING WITH IY> -> KX <VOWEL OR DIPTHONG NOT ENDING WITH IY>
            var nextPhoneme = phonemeIndex[(byte) unchecked(pos + 1)];
            if (nextPhoneme == 255 || (Resources.Flags[nextPhoneme] & (byte) PhonemeFlags2.DipYX) == 0)
                phonemeIndex[pos] = p = 75; // 'KX'
        }

        // Replace with softer version after 'S'
        var flag = Resources.Flags[p];
        if ((flag & (byte) PhonemeFlags.Plosive) != 0 && prior is (byte) ' ') // 'S' followed by plosive
            // S P -> S B, S T -> S D, S K -> S G, S KX -> S GX
            phonemeIndex[pos] = (byte) unchecked(p - 12);

        else if ((flag & (byte) PhonemeFlags.Plosive) == 0)
        {
            p = phonemeIndex[pos];
            switch (p)
            {
                // Special handling for UW
                case 53:
                    HandleAlveolarUw(pos, phonemeIndex);
                    break;
                // CH
                case 42:
                    HandleCh(pos, phonemeIndex, stress);
                    break;
                // J
                case 44:
                    HandleJ(pos, phonemeIndex, stress);
                    break;
            }
        }

        // Handle T and D softening
        if (p != 69 && p != 57) return;

        // T or D
        if ((Resources.Flags[phonemeIndex[(byte) unchecked(pos - 1)]] & (byte) PhonemeFlags2.Vowel) == 0)
            return;
        // Soften T or D following vowel and preceding an unstressed vowel
        var nextP = phonemeIndex[(byte) unchecked(pos + 1)];
        if (nextP == 0) // silence, check the next phoneme
            nextP = phonemeIndex[(byte) unchecked(pos + 2)];

        if ((Resources.Flags[nextP] & (byte) PhonemeFlags2.Vowel) != 0
            && stress[(byte) unchecked(pos + 1)] == 0)
            phonemeIndex[pos] = 30; // Replace with 'DX'
    }

    /// <summary>
    /// Handles the special case for alveolar consonants followed by UW
    /// </summary>
    private void HandleAlveolarUw(byte pos, Span<byte> phonemeIndex)
    {
        // If the preceding phoneme is alveolar, replace UW with UX
        if (pos > 0 && (Resources.Flags[phonemeIndex[pos - 1]] & (byte) PhonemeFlags.Alveolar) != 0)
            phonemeIndex[pos] = 16; // 'UX'
    }

    /// <summary>
    /// Handles CH phoneme by inserting a following CH+1 phoneme
    /// </summary>
    private void HandleCh(byte pos, Span<byte> phonemeIndex, Span<byte> stress)
        => Insert((byte) unchecked(pos + 1), 43, 0, stress[pos], phonemeIndex, default, stress);

    /// <summary>
    /// Handles J phoneme by inserting a following J+1 phoneme
    /// </summary>
    private void HandleJ(byte pos, Span<byte> phonemeIndex, Span<byte> stress)
        => Insert((byte) unchecked(pos + 1), 45, 0, stress[pos], phonemeIndex, default, stress);

    /// <summary>
    /// Changes a phoneme and inserts a new one for specific rules
    /// </summary>
    private void ChangeRule(byte position, byte phoneme, Span<byte> phonemeIndex, Span<byte> stress)
    {
        // Change current phoneme to 'AX' (13)
        phonemeIndex[position] = 13;

        // Insert the specified phoneme with the same stress
        Insert((byte) unchecked(position + 1), phoneme, 0, stress[position], phonemeIndex, default, stress);
    }

    /// <summary>
    /// Inserts a phoneme at the specified position
    /// </summary>
    private void Insert(byte position, byte phonemeValue, byte length, byte stressValue,
        Span<byte> phonemeIndex, Span<byte> phonemeLength, Span<byte> stress)
    {
        // Shift everything down to make room
        for (var i = 253; i >= position; i--)
        {
            phonemeIndex[i + 1] = phonemeIndex[i];

            if (phonemeLength != default)
                phonemeLength[i + 1] = phonemeLength[i];

            stress[i + 1] = stress[i];
        }

        phonemeIndex[position] = phonemeValue;

        if (phonemeLength != default)
            phonemeLength[position] = length;

        stress[position] = stressValue;
    }

    /// <summary>
    /// Iterates through phonemes, copying stress values from stressed vowels to preceding voiced phonemes
    /// </summary>
    /// <param name="phonemeIndex">Buffer containing phoneme indices</param>
    /// <param name="stress">Buffer containing stress values</param>
    public void CopyStress(Span<byte> phonemeIndex, Span<byte> stress)
    {
        // Loop through all phonemes until END marker
        for (byte position = 0, phoneme;
             (phoneme = phonemeIndex[position]) != 255;
             _ = unchecked(position++)) // 255 = END
        {
            // If this phoneme has the voiced flag set
            if ((Resources.Flags[phoneme] & (byte) PhonemeFlags.Voiced) == 0)
                continue;

            phoneme = phonemeIndex[(byte) unchecked(position + 1)];

            // If the following phoneme is not END and has the vowel flag set
            if (phoneme == 255 || (Resources.Flags[phoneme] & (byte) PhonemeFlags2.Vowel) == 0)
                continue;

            // Get the stress value at the next position
            phoneme = stress[(byte) unchecked(position + 1)];

            // If next phoneme is stressed (non-zero) and high bit not set
            if (phoneme == 0 || (phoneme & 128) != 0)
                continue;

            // Copy stress from next phoneme to this one, adding 1
            stress[position] = (byte) unchecked(phoneme + 1);
        }
    }


    /// <summary>
    /// Sets phoneme lengths based on stress values
    /// </summary>
    /// <param name="phonemeIndex">Buffer containing phoneme indices</param>
    /// <param name="phonemeLength">Buffer containing phoneme lengths</param>
    /// <param name="stress">Buffer containing stress values</param>
    public void SetPhonemeLength(Span<byte> phonemeIndex, Span<byte> phonemeLength, Span<byte> stress)
    {
        // Loop through all phonemes until END marker
        for (byte position = 0, phoneme;
             (phoneme = phonemeIndex[position]) != 255; // 255 = END
             _ = unchecked(position++))
        {
            // Get the stress value for this phoneme
            var stressValue = stress[position];

            // Select the appropriate length table based on stress
            // Use normal table if stress is 0 or high bit is set
            phonemeLength[position]
                = stressValue == 0
                  || (stressValue & 128) != 0
                    ? Resources.PhonemeLengthTable[phoneme]
                    : Resources.PhonemeStressedLengthTable[phoneme];
        }
    }

    /// <summary>
    /// Adjusts phoneme lengths based on surrounding phonemes
    /// </summary>
    /// <param name="phonemeIndex">Buffer containing phoneme indices</param>
    /// <param name="phonemeLength">Buffer containing phoneme lengths</param>
    /// <param name="stress">Buffer containing stress values</param>
    public void AdjustLengths(Span<byte> phonemeIndex, Span<byte> phonemeLength, Span<byte> stress)
    {
        const byte EndMarker = 255; // End marker

        // LENGTHEN VOWELS PRECEDING PUNCTUATION
        //while ((phoneme = phonemeIndex[pos]) != End)
        for (byte pos = 0, phoneme;
             (phoneme = phonemeIndex[pos]) != EndMarker;
             _ = unchecked(++pos))
        {
            // Not punctuation?
            if ((Resources.Flags[phoneme] & (byte) PhonemeFlags.Punct) == 0)
            {
                //_ = unchecked(++pos);
                continue;
            }

            var punctuationPos = pos;

            // Back up while not a vowel
            while (unchecked(--pos) > 0
                   && (Resources.Flags[phonemeIndex[pos]] & (byte) PhonemeFlags2.Vowel) == 0)
            {
                // back up
            }

            if (pos == 0)
                break;

            do
            {
                phoneme = phonemeIndex[pos];
                var flags = Resources.Flags[phoneme];
                // Test for fricative/unvoiced or not voiced
                if ((flags & (byte) PhonemeFlags.Fricative) != 0 &&
                    (flags & (byte) PhonemeFlags.Voiced) == 0)
                    continue;

                var length = phonemeLength[pos];
                // Change phoneme length to (length * 1.5) + 1
                phonemeLength[pos] = (byte) ((length >> 1) + length + 1);
            } while (unchecked(++pos) != punctuationPos);
        }

        // Loop through all phonemes for other rules
        for (byte pos = 0, phoneme;
             (phoneme = phonemeIndex[pos]) != EndMarker;
             _ = unchecked(++pos))
        {
            var flags = Resources.Flags[phoneme];
            if ((flags & (byte) PhonemeFlags2.Vowel) != 0)
            {
                phoneme = phonemeIndex[(byte) unchecked(pos + 1)];
                if ((Resources.Flags[phoneme] & (byte) PhonemeFlags2.Consonant) == 0)
                {
                    if (phoneme is not (18 or 19))
                        continue;

                    // 'RX', 'LX'
                    phoneme = phonemeIndex[(byte) unchecked(pos + 2)];
                    if ((Resources.Flags[phoneme] & (byte) PhonemeFlags2.Consonant) != 0)
                        _ = unchecked(--phonemeLength[pos]); // Decrease vowel length by 1
                }
                else
                {
                    // Next phoneme is a consonant
                    flags = phoneme == EndMarker
                        ? (byte) 65
                        : Resources.Flags[phoneme];

                    if ((flags & (byte) PhonemeFlags.Voiced) == 0) // Unvoiced
                    {
                        if ((flags & (byte) PhonemeFlags.Plosive) == 0)
                            continue;

                        // Unvoiced plosive
                        // Decrease vowel by 1/8th
                        phonemeLength[pos] -= (byte) (phonemeLength[pos] >> 3);
                    }
                    else
                    {
                        // Increase vowel by 1/2 + 1
                        var length = phonemeLength[pos];
                        phonemeLength[pos] = (byte) ((length >> 2) + length + 1); // 5/4*A + 1
                    }
                }
            }
            else if ((flags & (byte) PhonemeFlags.Nasal) != 0) // Nasal?
            {
                var nextPos = pos;

                // <NASAL> <STOP CONSONANT> rule
                phoneme = phonemeIndex[unchecked(++nextPos)];
                if (phoneme == EndMarker
                    || (Resources.Flags[phoneme] & (byte) PhonemeFlags.StopCons) == 0)
                    continue;

                phonemeLength[nextPos] = 6; // Set stop consonant length to 6
                phonemeLength[(byte) unchecked(nextPos - 1)] = 5; // Set nasal length to 5
            }
            else if ((flags & (byte) PhonemeFlags.StopCons) != 0) // Stop consonant?
            {
                var nextPos = pos;

                // Move past silence
                while ((phoneme = phonemeIndex[unchecked(++nextPos)]) == 0)
                {
                    // skip
                }

                if (phoneme == EndMarker || (Resources.Flags[phoneme] & (byte) PhonemeFlags.StopCons) == 0)
                    continue;

                // Shorten both to (length/2 + 1)
                phonemeLength[nextPos] = (byte) ((phonemeLength[nextPos] >> 1) + 1);
                phonemeLength[pos] = (byte) ((phonemeLength[pos] >> 1) + 1);
            }
            else if ((flags & (byte) PhonemeFlags.Liquid) != 0) // Liquid consonant?
            {
                // <LIQUID CONSONANT> <DIPTHONG> rule
                phoneme = phonemeIndex[(byte) unchecked(pos - 1)]; // Prior phoneme

                if ((Resources.Flags[phoneme] & (byte) PhonemeFlags.StopCons) != 0)
                    phonemeLength[pos] -= 2; // Decrease by 20ms
            }
        }
    }

    /// <summary>
    /// Inserts breath marks into the phoneme stream for natural pauses
    /// </summary>
    /// <param name="phonemeIndex">Buffer containing phoneme indices</param>
    /// <param name="phonemeLength">Buffer containing phoneme lengths</param>
    /// <param name="stress">Buffer containing stress values</param>
    public void InsertBreath(Span<byte> phonemeIndex, Span<byte> phonemeLength, Span<byte> stress)
    {
        const byte EndMarker = 255; // End marker
        const byte Break = 254; // Break marker
        const byte GlottalStop = 31; // 'Q*' glottal stop

        byte lastNonSilencePos = 255; // Track last silence position
        byte cumulativeLength = 0;

        for (byte pos = 0, index;
             (index = phonemeIndex[pos]) != EndMarker;
             _ = unchecked(++pos))
        {
            cumulativeLength += phonemeLength[pos];

            if (cumulativeLength < 232)
            {
                if (index == Break)
                {
                    // Do nothing special for break
                    continue;
                }

                if ((Resources.Flags[index] & (byte) PhonemeFlags.Punct) == 0) // Not punctuation
                {
                    if (index == 0) // If silence
                        lastNonSilencePos = pos;
                    continue;
                }

                // Is punctuation
                cumulativeLength = 0; // Reset length counter
                // Increment because we inserted a phoneme
                Insert(unchecked(++pos), Break, 0, 0, phonemeIndex, phonemeLength, stress);

                continue;
            }

            // Speech too long without a break
            pos = lastNonSilencePos;

            // Replace with glottal stop
            phonemeIndex[pos] = GlottalStop;
            phonemeLength[pos] = 4;
            stress[pos] = 0;

            cumulativeLength = 0;
            // Increment because we inserted a phoneme
            Insert(unchecked(++pos), Break, 0, 0, phonemeIndex, phonemeLength, stress);
        }
    }

    /// <summary>
    /// Inserts a phoneme at the specified position and shifts all subsequent phonemes down
    /// </summary>
    /// <param name="phonemeIndex">Buffer containing phoneme indices</param>
    /// <param name="phonemeLength">Buffer containing phoneme lengths</param>
    /// <param name="stress">Buffer containing stress values</param>
    /// <param name="position">Position to insert at</param>
    /// <param name="phoneme">Phoneme value to insert</param>
    /// <param name="length">Length value to insert</param>
    /// <param name="stressValue">Stress value to insert</param>
    private void Insert(Span<byte> phonemeIndex, Span<byte> phonemeLength, Span<byte> stress,
        byte position, byte phoneme, byte length, byte stressValue)
    {
        // Shift everything down to make room (from 253 down to position)
        // This preserves the 255 end marker at the very end
        for (var i = 253; i >= position; i--)
        {
            phonemeIndex[i + 1] = phonemeIndex[i];
            phonemeLength[i + 1] = phonemeLength[i];
            stress[i + 1] = stress[i];
        }

        // Insert the new values at the specified position
        phonemeIndex[position] = phoneme;
        phonemeLength[position] = length;
        stress[position] = stressValue;
    }

    /// <summary>
    /// Prepares phoneme output for rendering by processing phoneme sequences and handling breaks
    /// </summary>
    /// <param name="phonemeIndex">Source buffer containing phoneme indices</param>
    /// <param name="phonemeLength">Source buffer containing phoneme lengths</param>
    /// <param name="stress">Source buffer containing stress values</param>
    /// <param name="phonemeIndexOutput">Destination buffer for processed phoneme indices</param>
    /// <param name="phonemeLengthOutput">Destination buffer for processed phoneme lengths</param>
    /// <param name="stressOutput">Destination buffer for processed stress values</param>
    /// <remarks>
    /// Processes phonemes from source buffers to destination buffers, handling special phonemes
    /// like end markers (255), breaks (254), and silence (0).
    /// </remarks>
    public void PrepareOutput(Span<byte> phonemeIndex, Span<byte> phonemeLength, Span<byte> stress,
        Span<byte> phonemeIndexOutput, Span<byte> phonemeLengthOutput, Span<byte> stressOutput)
    {
        const byte EndMarker = 255; // End marker
        const byte Break = 254; // Break marker

        for (byte srcPos = 0, destPos = 0;
             /* condition depends on phoneme */;
             _ = unchecked(srcPos++))
        {
            var phoneme = phonemeIndex[srcPos];
            phonemeIndexOutput[destPos] = phoneme;

            switch (phoneme)
            {
                case EndMarker:
                    // End of phoneme data - return to caller for rendering
                    return;

                case Break:
                    // Insert end marker and signal a break
                    phonemeIndexOutput[destPos] = EndMarker;
                    return;

                case 0:
                    // Skip silent phonemes without incrementing destination
                    break;

                default:
                    // Copy length and stress values for regular phonemes
                    phonemeLengthOutput[destPos] = phonemeLength[srcPos];
                    stressOutput[destPos] = stress[srcPos];
                    _ = unchecked(destPos++);
                    break;
            }
        }
    }

    /// <summary>
    /// Processes stop consonants by inserting additional phonemes for proper pronunciation.
    /// </summary>
    /// <remarks>
    /// This corresponds to the Code41240() function in the original SAM implementation.
    /// It inserts additional phonemes after stop consonants to create the release phase.
    /// </remarks>
    public void ProcessStopConsonants(Span<byte> phonemeIndex, Span<byte> phonemeLength, Span<byte> stress)
    {
        const byte EndMarker = 255; // End marker

        for (byte pos = 0; phonemeIndex[pos] != EndMarker; _ = unchecked(++pos))
        {
            var index = phonemeIndex[pos];

            var flag = Resources.Flags[index];

            if ((flag & (byte) PhonemeFlags.StopCons) == 0)
                continue;

            if ((flag & (byte) PhonemeFlags.Plosive) != 0)
            {
                var x = pos;

                // Skip over any silent phonemes (0)
                while (phonemeIndex[++x] == 0)
                {
                    // skip
                }

                var nextPhoneme = phonemeIndex[x];
                if (nextPhoneme != EndMarker)
                {
                    // Skip if followed by /H, /X (flags & 8 or phoneme 36 or 37)
                    if ((Resources.Flags[nextPhoneme] & 8) != 0
                        || nextPhoneme is 36 or 37)
                        continue;
                }
            }

            // Insert the release phonemes
            var stressValue = stress[pos];
            Insert((byte) unchecked(pos + 1),
                (byte) unchecked(index + 1),
                Resources.PhonemeLengthTable[(byte) unchecked(index + 1)],
                stressValue, phonemeIndex, phonemeLength, stress);
            Insert((byte) unchecked(pos + 2),
                (byte) unchecked(index + 2),
                Resources.PhonemeLengthTable[(byte) unchecked(index + 2)],
                stressValue, phonemeIndex, phonemeLength, stress);
            pos += 2;
        }
    }

    /// <summary>
    /// Configuration settings for the speech synthesizer
    /// </summary>
    public byte Pitch { get; } = 64;

    /// <summary>
    /// Speed of the speech synthesis
    /// </summary>
    public byte Speed { get; } = 72;

    /// <summary>
    /// Mouth configuration value (controls mouth formant frequencies)
    /// </summary>
    public byte Mouth { get; } = 128;

    /// <summary>
    /// Throat configuration value (controls throat formant frequencies)
    /// </summary>
    public byte Throat { get; } = 128;

    /// <summary>
    /// Whether to use singing mode for synthesis
    /// </summary>
    public bool SingMode { get; } = false;

    /// <summary>
    /// Creates a new speech synthesizer with default settings
    /// </summary>
    public SamContext() => SetMouthThroat(Mouth, Throat);

    /// <summary>
    /// Creates a new speech synthesizer with the specified settings
    /// </summary>
    /// <param name="pitch">Base pitch for the voice (0-255)</param>
    /// <param name="speed">Speed of speech (0-255)</param>
    /// <param name="mouth">Mouth configuration value (0-255)</param>
    /// <param name="throat">Throat configuration value (0-255)</param>
    /// <param name="singMode">Whether to use singing mode</param>
    public SamContext(byte pitch, byte speed, byte mouth, byte throat, bool singMode = false)
    {
        Pitch = pitch;
        Speed = speed;
        Mouth = mouth;
        Throat = throat;
        SingMode = singMode;

        SetMouthThroat(mouth, throat);
    }

    /// <summary>
    /// Processes a single chunk of ASCII text through the speech synthesis pipeline
    /// </summary>
    private unsafe void ProcessTextChunk<T>(ReadOnlySpan<byte> textChunk, ref SampleBuffer<T> outputBuffer)
        where T : unmanaged
    {
        // Buffers for the phoneme processing pipeline
        Span<byte> phonemeBuffer = stackalloc byte[256];
        Span<byte> phonemeIndexBuffer = stackalloc byte[256];
        Span<byte> stressBuffer = stackalloc byte[256];
        Span<byte> phonemeLengthBuffer = stackalloc byte[256];

        Span<byte> phonemeIndexOutput = stackalloc byte[60];
        Span<byte> phonemeLengthOutput = stackalloc byte[60];
        Span<byte> stressOutput = stackalloc byte[60];

        // Convert text to phonemes (using the reciter)
        if (!TextToPhonemes(textChunk, phonemeBuffer))
            return; // Failed to convert text to phonemes

        // Parse the phonemes
        if (!Parser1(phonemeBuffer, phonemeIndexBuffer, stressBuffer))
            return; // Failed to parse phonemes

        // Copy stress from vowels to preceding consonants
        CopyStress(phonemeIndexBuffer, stressBuffer);

        // Apply phoneme transformation rules
        Parser2(phonemeIndexBuffer, phonemeLengthBuffer, stressBuffer);

        // Set phoneme lengths based on stress
        SetPhonemeLength(phonemeIndexBuffer, phonemeLengthBuffer, stressBuffer);

        // Adjust phoneme lengths based on surrounding phonemes
        AdjustLengths(phonemeIndexBuffer, phonemeLengthBuffer, stressBuffer);

        // Process stop consonants
        ProcessStopConsonants(phonemeIndexBuffer, phonemeLengthBuffer, stressBuffer);

        // Insert breath marks for natural pauses
        InsertBreath(phonemeIndexBuffer, phonemeLengthBuffer, stressBuffer);

        // Main loop for breaking up the input into smaller chunks for processing
        byte srcpos = 0;

        while (phonemeIndexBuffer[srcpos] != 255)
        {
            // Clear output buffers
            phonemeIndexOutput.Clear();
            phonemeLengthOutput.Clear();
            stressOutput.Clear();

            // Prepare output (process until break or end)
            PrepareOutput(
                phonemeIndexBuffer.Slice(srcpos),
                phonemeLengthBuffer.Slice(srcpos),
                stressBuffer.Slice(srcpos),
                phonemeIndexOutput,
                phonemeLengthOutput,
                stressOutput);

            // Count phonemes in this chunk
            byte destpos = 0;
            while (phonemeIndexOutput[destpos] != 255)
                destpos++;

            // Advance source position past the phonemes we just processed
            while (srcpos < 255 && phonemeIndexBuffer[srcpos] != 255 && phonemeIndexBuffer[srcpos] != 254)
                srcpos++;

            // Skip the break marker if present
            if (srcpos < 255 && phonemeIndexBuffer[srcpos] == 254)
                srcpos++;

            // If we have phonemes to process, render them
            if (destpos > 0)
            {
                Render(
                    phonemeIndexOutput,
                    stressOutput,
                    phonemeLengthOutput,
                    ref outputBuffer,
                    SingMode,
                    Speed,
                    Pitch);
            }
        }
    }

    /// <summary>
    /// Generates speech samples from the given text
    /// </summary>
    /// <typeparam name="T">The sample data type</typeparam>
    /// <param name="text">Text to convert to speech</param>
    /// <param name="outputBuffer">Buffer to store the generated samples</param>
    /// <returns>The provided buffer with samples appended</returns>
    public unsafe ref SampleBuffer<T> Generate<T>(ReadOnlySpan<char> text, ref SampleBuffer<T> outputBuffer)
        where T : unmanaged
    {
        // Convert text to ASCII bytes
        Span<byte> asciiText = stackalloc byte[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            asciiText[i] = ch <= 127 ? (byte) text[i] : (byte) ' ';
        }

        // Process the ASCII text chunk
        return ref Generate(asciiText, ref outputBuffer);
    }


    /// <summary>
    /// Generates speech samples from the given string
    /// </summary>
    /// <typeparam name="T">The sample data type</typeparam>
    /// <param name="text">Text to convert to speech</param>
    /// <returns>A new buffer containing the generated samples</returns>
    public SampleBuffer<T> Generate<T>(string text) where T : unmanaged
    {
        var buffer = new SampleBuffer<T>(1024, true); // Start with 1024 samples, rented from pool
        Generate(text.AsSpan(), ref buffer);
        return buffer;
    }

    /// <summary>
    /// Generates speech samples from the given string
    /// </summary>
    /// <typeparam name="T">The sample data type</typeparam>
    /// <param name="text">Text to convert to speech</param>
    /// <param name="outputBuffer">Buffer to store the generated samples</param>
    /// <returns>The provided buffer with samples appended</returns>
    public ref SampleBuffer<T> Generate<T>(string text, ref SampleBuffer<T> outputBuffer) where T : unmanaged
        => ref Generate(text.AsSpan(), ref outputBuffer);

    /// <summary>
    /// Generates speech samples from the given ASCII text
    /// </summary>
    /// <typeparam name="T">The sample data type</typeparam>
    /// <param name="asciiText">ASCII text to convert to speech</param>
    /// <returns>A new buffer containing the generated samples</returns>
    public SampleBuffer<T> Generate<T>(ReadOnlySpan<byte> asciiText) where T : unmanaged
    {
        var buffer = new SampleBuffer<T>(1024, true); // Start with 1024 samples, rented from pool
        Generate(asciiText, ref buffer);
        return buffer;
    }


    /// <summary>
    /// Generates speech samples from the given ASCII text
    /// </summary>
    /// <typeparam name="T">The sample data type</typeparam>
    /// <param name="asciiText">ASCII text to convert to speech</param>
    /// <param name="outputBuffer">Buffer to store the generated samples</param>
    /// <returns>The provided buffer with samples appended</returns>
    public unsafe ref SampleBuffer<T> Generate<T>(ReadOnlySpan<byte> asciiText, ref SampleBuffer<T> outputBuffer)
        where T : unmanaged
    {
        const byte EndMarker = 155;

        if (asciiText.IsEmpty) return ref outputBuffer;

        // Process each chunk directly with the byte data
        const int MaxChunkSize = 250;
        var remainingText = asciiText;
        Span<byte> processChunk = stackalloc byte[256];

        while (!remainingText.IsEmpty)
        {
            // Find the best byte chunk size
            var chunkSize = FindOptimalChunkSize(remainingText, MaxChunkSize);
            var textChunk = remainingText.Slice(0, chunkSize);
            remainingText = remainingText.Slice(chunkSize);

            // Copy to process chunk
            textChunk.CopyTo(processChunk);

            // Add end marker(s)
            processChunk[textChunk.Length] = EndMarker; // End of text marker
            processChunk[textChunk.Length + 1] = 0;

            // Process text chunk
            ProcessTextChunk(processChunk.Slice(0, textChunk.Length + 2), ref outputBuffer);
        }

        return ref outputBuffer;
    }

    /// <summary>
    /// Checks if a byte is a vowel in ASCII
    /// </summary>
    private bool IsVowel(byte b)
        => (byte) (b | 0x20)
            is (byte) 'a' or (byte) 'e' or (byte) 'i' or (byte) 'o' or (byte) 'u'
            or (byte) 'y' or (byte) 'w'; // Sometimes y and w act as vowels

    /// <summary>
    /// Finds the best position to split a long word on a vowel
    /// </summary>
    /// <param name="text">ASCII text to analyze</param>
    /// <returns>Position to split at, or -1 if no good split found</returns>
    private int FindBestVowelSplit(ReadOnlySpan<byte> text)
    {
        // Try to find consonant-vowel transitions (split before vowel)
        var halfPoint = text.Length / 2;

        // First check the second half of the chunk
        for (var i = text.Length - 1; i > halfPoint; i--)
        {
            if (!IsVowel(text[i - 1]) && IsVowel(text[i]))
                return i; // Split before vowel
        }

        // Then check the first half
        for (var i = halfPoint; i > 0; i--)
        {
            if (!IsVowel(text[i - 1]) && IsVowel(text[i]))
                return i; // Split before vowel
        }

        // Fallback: check for vowel-consonant transitions
        for (var i = text.Length - 2; i >= halfPoint; i--)
        {
            if (IsVowel(text[i]) && !IsVowel(text[i + 1]))
                return i + 1; // Split after vowel
        }

        for (var i = halfPoint - 2; i >= 0; i--)
        {
            if (IsVowel(text[i]) && !IsVowel(text[i + 1]))
                return i + 1; // Split after vowel
        }

        // No good split found
        return -1;
    }

    /// <summary>
    /// Finds the optimal size for the next ASCII text chunk
    /// </summary>
    /// <param name="text">ASCII text to analyze</param>
    /// <param name="maxSize">Maximum allowed chunk size</param>
    /// <returns>Optimal chunk size</returns>
    private int FindOptimalChunkSize(ReadOnlySpan<byte> text, int maxSize)
    {
        // If text is shorter than max size, return its length
        if (text.Length <= maxSize)
            return text.Length;

        // Try to find a space to split at
        var lastSpace = -1;
        for (var i = 0; i < Math.Min(text.Length, maxSize); i++)
            if (text[i] == (byte) ' ')
                lastSpace = i;

        // If we found a space within the limit, split there
        if (lastSpace > 0)
            return lastSpace + 1; // Include the space in the current chunk

        // No space found - we have a long word
        // Check if it's longer than the max size
        var wordEnd = text.IndexOf((byte) ' ');
        if (wordEnd == -1)
            wordEnd = text.Length; // No more spaces, treat rest as one word

        // If word is reasonable size (less than 2x max size), 
        // and we're at the start of it, take the whole word
        if (wordEnd <= maxSize)
            return wordEnd;

        // For unreasonably long words (>maxSize), just truncate
        return maxSize;
    }
}