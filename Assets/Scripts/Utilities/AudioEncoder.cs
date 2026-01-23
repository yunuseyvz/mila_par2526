using System;
using System.IO;
using UnityEngine;

namespace LanguageTutor.Utilities
{
    /// <summary>
    /// Utility class to encode Unity AudioClip to various audio formats for API upload.
    /// Supports WAV encoding which is widely accepted by STT APIs.
    /// </summary>
    public static class AudioEncoder
    {
        /// <summary>
        /// Convert an AudioClip to WAV format byte array.
        /// This is the most compatible format for STT APIs.
        /// </summary>
        /// <param name="clip">The AudioClip to convert</param>
        /// <returns>Byte array containing WAV file data</returns>
        public static byte[] EncodeToWav(AudioClip clip)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));

            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                // WAV file header
                int sampleRate = clip.frequency;
                int channels = clip.channels;
                int bitsPerSample = 16;
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                int blockAlign = channels * bitsPerSample / 8;
                int subchunk2Size = samples.Length * bitsPerSample / 8;
                int chunkSize = 36 + subchunk2Size;

                // RIFF header
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(chunkSize);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });

                // fmt subchunk
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // Subchunk1Size (16 for PCM)
                writer.Write((short)1); // AudioFormat (1 for PCM)
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write((short)bitsPerSample);

                // data subchunk
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(subchunk2Size);

                // Write audio samples (convert float to 16-bit PCM)
                foreach (float sample in samples)
                {
                    // Clamp and convert float [-1, 1] to short [-32768, 32767]
                    float clampedSample = Mathf.Clamp(sample, -1f, 1f);
                    short intSample = (short)(clampedSample * 32767f);
                    writer.Write(intSample);
                }

                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Get the duration of an AudioClip in seconds.
        /// </summary>
        public static float GetDuration(AudioClip clip)
        {
            if (clip == null) return 0f;
            return clip.length;
        }

        /// <summary>
        /// Get audio clip info as a formatted string (for debugging).
        /// </summary>
        public static string GetAudioInfo(AudioClip clip)
        {
            if (clip == null) return "null";
            return $"[{clip.name}] Duration: {clip.length:F2}s, Channels: {clip.channels}, Frequency: {clip.frequency}Hz, Samples: {clip.samples}";
        }

        /// <summary>
        /// Resample AudioClip to a target sample rate.
        /// Many STT APIs work best with 16kHz audio.
        /// </summary>
        /// <param name="clip">Source AudioClip</param>
        /// <param name="targetSampleRate">Target sample rate (e.g., 16000 for 16kHz)</param>
        /// <returns>New AudioClip with target sample rate</returns>
        public static AudioClip ResampleTo(AudioClip clip, int targetSampleRate)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));

            if (clip.frequency == targetSampleRate)
                return clip;

            float[] originalSamples = new float[clip.samples * clip.channels];
            clip.GetData(originalSamples, 0);

            // Calculate resampling ratio
            float ratio = (float)clip.frequency / targetSampleRate;
            int newSampleCount = (int)(clip.samples / ratio);

            float[] resampledSamples = new float[newSampleCount * clip.channels];

            // Simple linear interpolation resampling
            for (int i = 0; i < newSampleCount; i++)
            {
                float sourceIndex = i * ratio;
                int sourceIndexInt = (int)sourceIndex;
                float fraction = sourceIndex - sourceIndexInt;

                for (int channel = 0; channel < clip.channels; channel++)
                {
                    int idx1 = sourceIndexInt * clip.channels + channel;
                    int idx2 = Math.Min((sourceIndexInt + 1) * clip.channels + channel, originalSamples.Length - 1);

                    resampledSamples[i * clip.channels + channel] = 
                        Mathf.Lerp(originalSamples[idx1], originalSamples[idx2], fraction);
                }
            }

            AudioClip resampledClip = AudioClip.Create(
                $"{clip.name}_resampled",
                newSampleCount,
                clip.channels,
                targetSampleRate,
                false
            );

            resampledClip.SetData(resampledSamples, 0);
            return resampledClip;
        }

        /// <summary>
        /// Convert stereo AudioClip to mono (averages channels).
        /// Many STT APIs work better with mono audio.
        /// </summary>
        public static AudioClip ConvertToMono(AudioClip clip)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));

            if (clip.channels == 1)
                return clip;

            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            float[] monoSamples = new float[clip.samples];

            for (int i = 0; i < clip.samples; i++)
            {
                float sum = 0f;
                for (int channel = 0; channel < clip.channels; channel++)
                {
                    sum += samples[i * clip.channels + channel];
                }
                monoSamples[i] = sum / clip.channels;
            }

            AudioClip monoClip = AudioClip.Create(
                $"{clip.name}_mono",
                clip.samples,
                1,
                clip.frequency,
                false
            );

            monoClip.SetData(monoSamples, 0);
            return monoClip;
        }

        /// <summary>
        /// Prepare audio for STT API upload (convert to mono, resample to 16kHz, encode to WAV).
        /// This is the recommended pre-processing for most STT APIs.
        /// </summary>
        /// <param name="clip">Source AudioClip</param>
        /// <param name="targetSampleRate">Target sample rate (default 16000 for optimal STT)</param>
        /// <returns>WAV bytes ready for API upload</returns>
        public static byte[] PrepareForSTT(AudioClip clip, int targetSampleRate = 16000)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));

            // Convert to mono if stereo
            AudioClip processedClip = ConvertToMono(clip);

            // Resample to target sample rate
            processedClip = ResampleTo(processedClip, targetSampleRate);

            // Encode to WAV
            return EncodeToWav(processedClip);
        }
    }
}
