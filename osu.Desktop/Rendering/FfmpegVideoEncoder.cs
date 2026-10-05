// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using osu.Framework.Logging;

namespace osu.Desktop.Rendering
{
    /// <summary>
    /// Pipes raw RGBA frames to a system ffmpeg for H.264/H.265 encoding.
    /// Input is rawvideo/rgba; audio (beatmap track) is muxed separately when provided.
    /// </summary>
    public sealed class FfmpegVideoEncoder : IDisposable
    {
        private readonly ReplayRenderOptions options;
        private Process? ffmpeg;
        private Stream? stdin;
        private bool disposed;

        public string? AudioInputPath { get; set; }

        public string ExecutablePath => options.FFmpegPath ?? "ffmpeg";

        public FfmpegVideoEncoder(ReplayRenderOptions options)
        {
            this.options = options;
        }

        public static bool IsAvailable(ReplayRenderOptions? options = null)
        {
            string executable = options?.FFmpegPath ?? "ffmpeg";

            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                if (p == null) return false;
                p.WaitForExit(5000);
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public void Start()
        {
            string? outDir = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            bool hasAudio = options.IncludeAudio && !string.IsNullOrEmpty(AudioInputPath) && File.Exists(AudioInputPath);

            // raw RGBA stdin -> video encode, optional audio input -> mux with -shortest.
            string fps = options.Fps.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string videoInput = $"-f rawvideo -vcodec rawvideo -pix_fmt rgba -s {options.Width}x{options.Height} -r {fps} -i -";
            string audioInput = hasAudio ? $" -i \"{AudioInputPath}\"" : string.Empty;
            string maps = hasAudio ? "-map 0:v -map 1:a" : "-map 0:v";
            string audioCodec = hasAudio ? $"-c:a aac -b:a {options.AudioBitrate} -shortest" : "-an";

            // --quality switches from a fixed bitrate to a constant-quality encode.
            string videoCodec;

            if (options.Quality.HasValue && !options.Encoder.EndsWith("gif", StringComparison.OrdinalIgnoreCase))
                videoCodec = options.Encoder.StartsWith("libvpx", StringComparison.OrdinalIgnoreCase)
                    ? $"-c:v {options.Encoder} -crf {options.Quality.Value} -b:v 0"
                    : $"-c:v {options.Encoder} -crf {options.Quality.Value} -preset medium";
            else
                videoCodec = $"-c:v {options.Encoder} -b:v {options.VideoBitrate}";

            string pixelFormat = options.Encoder.EndsWith("gif", StringComparison.OrdinalIgnoreCase) ? string.Empty : $" -pix_fmt {options.PixelFormat}";
            string extra = string.IsNullOrWhiteSpace(options.FFmpegExtraArgs) ? string.Empty : $" {options.FFmpegExtraArgs}";

            string args =
                $"-y {videoInput}{audioInput} {maps} {videoCodec}{pixelFormat} -r {fps} {audioCodec}{extra} \"{options.OutputPath}\"";

            Logger.Log($"Starting ffmpeg: {ExecutablePath} {args}", LoggingTarget.Runtime, LogLevel.Debug);

            ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ExecutablePath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            ffmpeg.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Logger.Log($"[ffmpeg] {e.Data}", LoggingTarget.Runtime, LogLevel.Debug);
            };

            if (!ffmpeg.Start())
                throw new InvalidOperationException("Failed to start ffmpeg process.");

            stdin = ffmpeg.StandardInput.BaseStream;
            ffmpeg.BeginErrorReadLine();
        }

        public void WriteFrame(ReadOnlySpan<byte> rgba)
        {
            if (stdin == null)
                throw new InvalidOperationException("Encoder not started.");

            // Synchronous write keeps A/V order deterministic; frames are large (~8MB @1080p) but pipe buffers handle it.
            stdin.Write(rgba);
        }

        public int Finish()
        {
            try
            {
                try
                {
                    stdin?.Flush();
                    stdin?.Close();
                }
                catch (Exception ex)
                {
                    // ffmpeg likely exited early (e.g. finished before the video did, or crashed).
                    // Continue so we can still surface its exit code/stderr below.
                    Logger.Log($"Video pipe closed unexpectedly: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                }

                try { stdin?.Dispose(); } catch { }
                stdin = null;

                if (ffmpeg == null) return 1;

                if (!ffmpeg.WaitForExit(120000))
                {
                    try { ffmpeg.Kill(true); } catch { }
                    Logger.Log("ffmpeg timed out waiting for exit.", LoggingTarget.Runtime, LogLevel.Error);
                    return 1;
                }

                if (ffmpeg.ExitCode != 0)
                    Logger.Log($"ffmpeg exited with code {ffmpeg.ExitCode}.", LoggingTarget.Runtime, LogLevel.Error);

                return ffmpeg.ExitCode;
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            try { stdin?.Dispose(); } catch { }
            try { ffmpeg?.Dispose(); } catch { }

            stdin = null;
            ffmpeg = null;
        }
    }
}
