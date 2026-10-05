// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Game.Audio;

namespace osu.Desktop.Rendering
{
    /// <summary>
    /// A single gameplay sample playback, captured while stepping the replay.
    /// </summary>
    public readonly struct GameplaySampleEvent
    {
        public readonly double Time;
        public readonly ISampleInfo[] Samples;

        public GameplaySampleEvent(double time, ISampleInfo[] samples)
        {
            Time = time;
            Samples = samples;
        }
    }

    /// <summary>
    /// Offline mixer which combines the beatmap's music track with gameplay hitsounds
    /// (and any other gameplay sample triggered during playback) at their exact gameplay times.
    /// </summary>
    /// <remarks>
    /// The renderer captures frames by seeking, so the game's own audio output cannot be recorded directly
    /// (sample playback is suppressed while the gameplay clock is paused, and there is no capture device).
    /// Instead sample events are collected while seeking and mixed here.
    /// </remarks>
    public static class GameplayAudioMixer
    {
        private const int sample_rate = 44100;
        private const int channels = 2;

        private const float music_gain = 0.85f;

        private static readonly string[] sample_extensions = { ".wav", ".mp3", ".ogg", ".m4a", ".flac" };

        public static string? Mix(string? musicPath, IReadOnlyList<GameplaySampleEvent> events, IResourceStore<byte[]> resources, string ffmpegPath, bool musicOnly,
                                double segmentStartMs, double segmentEndMs, out int mixedSampleCount)
        {
            mixedSampleCount = 0;

            if (musicOnly && musicPath == null && events.Count == 0)
                return null;

            // decode music first (also gives us the target duration)
            float[]? music = musicPath == null ? null : decodeToPcm(musicPath, ffmpegPath);
            // music must be shifted into the exported segment as well.
            int musicSkip = (int)(Math.Max(0, segmentStartMs) * sample_rate / 1000.0) * channels;
            long musicLength = music == null ? 0 : Math.Max(0, music.Length - musicSkip);

            // group events by resolved sample file so each file is only decoded once
            var decodedCache = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            var timeline = new List<(int offset, float[] pcm, float gain)>();
            long hitsoundEnd = 0;

            foreach (var e in events)
            {
                // events are captured in gameplay time; shift them into the exported segment's timeline.
                double localTime = e.Time - segmentStartMs;

                if (localTime < -20 || localTime > segmentEndMs + 2000)
                    continue;

                foreach (var info in e.Samples)
                {
                    foreach (string lookup in info.LookupNames)
                    {
                        if (!decodedCache.TryGetValue(lookup, out float[]? pcm))
                        {
                            pcm = loadSample(lookup, resources, ffmpegPath);
                            decodedCache[lookup] = pcm ?? Array.Empty<float>();
                        }

                        if (pcm == null || pcm.Length == 0)
                            continue;

                        float gain = Math.Clamp(info.Volume / 100f, 0f, 1f);
                        int offset = (int)(Math.Max(0, localTime) * sample_rate / 1000.0) * channels;

                        timeline.Add((offset, pcm, gain));
                        hitsoundEnd = Math.Max(hitsoundEnd, offset + pcm.Length);
                        break;
                    }
                }
            }

            long totalLength = Math.Max(musicLength, hitsoundEnd);

            if (totalLength == 0)
                return null;

            float[] mix = new float[totalLength];

            if (music != null)
            {
                float gain = music_gain;
                int count = (int)Math.Min(musicLength, mix.Length);

                for (int i = 0; i < count; i++)
                    mix[i] += music[musicSkip + i] * gain;
            }

            foreach (var (offset, pcm, gain) in timeline)
            {
                int count = Math.Min(pcm.Length, mix.Length - offset);

                for (int i = 0; i < count; i++)
                    mix[offset + i] += pcm[i] * gain;
            }

            string outputPath = Path.Combine(Path.GetTempPath(), $"osu-render-audio-{Guid.NewGuid():N}.wav");
            writeWav(outputPath, mix);

            mixedSampleCount = timeline.Count;
            Logger.Log($"Mixed gameplay audio: {timeline.Count} sample events, {(hitsoundEnd / (double)sample_rate / channels):0.0}s of hitsounds.", LoggingTarget.Runtime, LogLevel.Debug);

            return outputPath;
        }

        private static float[]? loadSample(string lookup, IResourceStore<byte[]> resources, string ffmpegPath)
        {
            // lookup names look like "Gameplay/normal-hitnormal"
            string relativePath = lookup.StartsWith("Gameplay/", StringComparison.OrdinalIgnoreCase) ? lookup : $"Gameplay/{lookup}";

            foreach (string ext in sample_extensions)
            {
                string streamPath = $"Samples/{relativePath}{ext}";

                try
                {
                    using Stream? stream = resources.GetStream(streamPath);

                    if (stream == null)
                        continue;

                    string tempPath = Path.Combine(Path.GetTempPath(), $"osu-render-sample-{Guid.NewGuid():N}{ext}");
                    using (var file = File.Create(tempPath))
                        stream.CopyTo(file);

                    try
                    {
                        return decodeToPcm(tempPath, ffmpegPath);
                    }
                    finally
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to load gameplay sample {streamPath}: {e.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                }
            }

            return null;
        }

        private static float[]? decodeToPcm(string inputPath, string ffmpegPath)
        {
            string rawPath = Path.Combine(Path.GetTempPath(), $"osu-render-pcm-{Guid.NewGuid():N}.raw");

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-y -v error -i \"{inputPath}\" -f f32le -acodec pcm_f32le -ac {channels} -ar {sample_rate} \"{rawPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                    }
                };

                process.Start();
                process.WaitForExit(30000);

                if (process.ExitCode != 0 || !File.Exists(rawPath))
                    return null;

                byte[] bytes = File.ReadAllBytes(rawPath);

                int count = bytes.Length / 4;
                float[] result = new float[count];

                for (int i = 0; i < count; i++)
                    result[i] = BitConverter.ToSingle(bytes, i * 4);

                return result;
            }
            catch (Exception e)
            {
                Logger.Log($"Failed to decode audio {inputPath}: {e.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }
            finally
            {
                try { File.Delete(rawPath); } catch { }
            }
        }

        private static void writeWav(string path, float[] samples)
        {
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream, Encoding.ASCII);

            int dataSize = samples.Length * 2;

            writer.Write("RIFF"u8);
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)channels);
            writer.Write(sample_rate);
            writer.Write(sample_rate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataSize);

            foreach (float sample in samples)
                writer.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        }
    }
}