// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;

namespace osu.Desktop.Rendering
{
    /// <summary>
    /// Options for headless replay rendering, danser-go style.
    /// Supports both lazer-style (--replay, --beatmap, --skin, --output)
    /// and danser-go-style (-replay/-r, -osu, -skinpath, -out, -record) flags.
    /// </summary>
    public class ReplayRenderOptions
    {
        public string ReplayPath { get; set; } = string.Empty;
        public string BeatmapPath { get; set; } = string.Empty;
        public string? SkinPath { get; set; }
        public string OutputPath { get; set; } = string.Empty;

        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public double Fps { get; set; } = 60;

        public string VideoBitrate { get; set; } = "8000k";
        public string? AudioBitrate { get; set; } = "192k";
        public string Encoder { get; set; } = "libx264";
        public string PixelFormat { get; set; } = "yuv420p";
        public string OutputFormat { get; set; } = "mp4";

        public bool IncludeAudio { get; set; } = true;
        public bool ShowStoryboard { get; set; } = true;
        public bool ShowHUD { get; set; } = true;
        public bool EndOnFail { get; set; }

        /// <summary>
        /// Whether to wait for a seek to be fully applied (visually) before capturing a frame.
        /// Disable for faster renders on maps where the transition isn't visible.
        /// </summary>
        public bool SettleSeeks { get; set; } = true;

        public int? Quality { get; set; }
        public double? MaxDurationSeconds { get; set; }
        public double? StartAtSeconds { get; set; }
        public double? EndAtSeconds { get; set; }

        public string? OutputDir { get; set; }
        public bool AutoName { get; set; }
        public bool Overwrite { get; set; } = true;

        public string? FFmpegPath { get; set; }
        public string? FFmpegExtraArgs { get; set; }

        public bool NoHitsounds { get; set; }
        public bool HitsoundsOnly { get; set; }

        public bool ShowProgressBar { get; set; } = true;
        public bool ShowComboMeter { get; set; } = true;
        public bool ShowLeaderboard { get; set; } = true;
        public bool ShowKeyOverlay { get; set; } = true;

        public float? CursorSize { get; set; }
        public float? CursorTrailMs { get; set; }
        public bool HideCursor { get; set; }

        public float? BackgroundDim { get; set; }
        public float? BackgroundBlur { get; set; }

        public string? StoragePath { get; set; }

        /// <summary>
        /// Extra lead-in/out in ms captured around first/last replay frame.
        /// </summary>
        public double LeadInMs { get; set; } = 2000;
        public double LeadOutMs { get; set; } = 2000;

        public static bool IsRenderIntent(string[] args) => hasAnyFlag(args, "--render", "-record", "--record");

        public static bool IsHelpRequest(string[] args) => hasAnyFlag(args, "--help", "-h", "-help", "--render-help");

        private static bool hasAnyFlag(string[] args, params string[] flags)
        {
            foreach (string arg in args)
            {
                foreach (string flag in flags)
                {
                    if (arg.Equals(flag, StringComparison.OrdinalIgnoreCase) || arg.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public static string GetHelpText()
        {
            return
@"osu!lazer replay renderer (danser-go style)

Usage:
  osu! --render --replay <replay.osr> --beatmap <map.osz> [--skin <skin.osk|folder>] --output <out.mp4> [options]
  osu! --render -replay=""replay.osr"" -osu=""map.osz"" -skinpath=""path/to/skin"" -record -out=""videos/output""

Required:
  --replay, -replay, -r <path.osr>      Replay file to render.
  --beatmap, -osu, --osu <path.osz>     Beatmap package containing the replay's map.
  --output, -out <path.mp4>             Output video file (extension defaults to .mp4).

Optional:
  --skin, -skinpath <path.osk|dir>      Skin to use (.osk file or legacy skin folder). Default: current user skin.
  --width <int>                         Frame width. Default: 1920.
  --height <int>                        Frame height. Default: 1080.
  --fps <double>                        Output framerate. Default: 60.
  --bitrate, --video-bitrate <str>      ffmpeg video bitrate (e.g. 8000k). Default: 8000k.
  --encoder <str>                       ffmpeg video encoder (libx264, libx265, h264_nvenc, ...). Default: libx264.
  --no-audio                            Don't mux beatmap audio track.
  --no-storyboard                       Disable storyboard.
  --no-hud                              Hide HUD (score, combo, leaderboard) for clean footage.
  --no-progress-bar                     Hide the song progress bar.
  --no-combo-meter                      Hide the combo counter.
  --no-leaderboard                      Hide the gameplay leaderboard.
  --no-key-overlay                      Hide the key overlay.
  --end-on-fail                         If the replay failed the beatmap, stop at the fail instead of rendering to the end of the song.
  --no-seek-settle                     Don't wait for seeks to be fully applied before capturing (faster, may show a fast-forward at the start).
  --start-at <sec>                      Start of the exported segment, in seconds.
  --end-at <sec>                        End of the exported segment, in seconds.
  --max-duration <sec>                  Maximum exported length, in seconds.
  --output-format <fmt>                 mp4 (default), mkv, webm or gif.
  --quality <0-51>                      Constant-quality encode (overrides --bitrate). Lower is better quality.
  --no-hitsounds                        Don't mix gameplay hitsounds into the exported audio.
  --hitsounds-only                      Only export gameplay hitsounds (no music track).
  --cursor-size <float>                 Gameplay cursor size multiplier.
  --cursor-trail <ms>                   Cursor trail length (0 disables the trail).
  --hide-cursor                         Don't render the cursor.
  --bg-dim <0-1>                        Background dim level.
  --bg-blur <0-20>                      Background blur level.
  --output-dir <dir>                    Directory to write the output into.
  --auto-name                           Generate the output filename from the replay and beatmap names.
  --no-overwrite                        Fail instead of overwriting an existing output file.
  --ffmpeg-path <path>                  Path to the ffmpeg binary.
  --ffmpeg-extra-args ""<args>""        Extra arguments appended to the ffmpeg command.
  --storage-path <dir>                  Directory used for the isolated render data (database, logs, imports).
  --lead-in <ms>                        Capture padding before first frame. Default: 2000.
  --lead-out <ms>                       Capture padding after last frame. Default: 2000.
  --help, -h                            Show this help.

Examples:
  osu! --render --replay play.osr --beatmap map.osz --skin skin.osk --output render.mp4
  osu! --render --replay play.osr --beatmap map.osz --skin ./my-skin-folder --output render.mp4 --width 1280 --height 720 --fps 60
  ./osu! --render -replay=replay.osr -osu=map.osz -skinpath=./skins/myskin -record -out=/tmp/out

Notes:
  - Requires ffmpeg on PATH. Video is piped as raw RGBA to ffmpeg (libx264/yuv420p by default).
  - Beatmap is imported first (replay lookup needs its MD5 hash), then skin, then replay.
  - Input files are never modified or deleted; imports run on temp copies.
  - Audio is the beatmap's track with gameplay samples (hitsounds, ticks) mixed in at their exact gameplay times.
  - Use --hitsounds-only or --no-hitsounds to change what ends up in the audio track.
";
        }

        private static readonly HashSet<string> value_flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "replay", "r", "beatmap", "osu", "skin", "skinpath", "output", "out",
            "width", "height", "fps", "bitrate", "video-bitrate", "encoder",
            "pixel-format", "container", "lead-in", "lead-out", "audio-bitrate",
            "output-format", "quality", "max-duration", "start-at", "end-at",
            "output-dir", "ffmpeg-path", "ffmpeg-extra-args", "cursor-size", "cursor-trail",
            "bg-dim", "bg-blur", "storage-path"
        };

        public static bool TryParse(string[] args, string cwd, out ReplayRenderOptions? options, out string? error)
        {
            options = new ReplayRenderOptions();
            error = null;

            var map = parseFlags(args);

            string? get(params string[] keys)
            {
                foreach (string key in keys)
                {
                    if (map.TryGetValue(key, out string? value) && !string.IsNullOrEmpty(value))
                        return value;
                }

                return null;
            }

            bool has(params string[] keys)
            {
                foreach (string key in keys)
                {
                    if (map.ContainsKey(key))
                        return true;
                }

                return false;
            }

            string? replay = get("replay", "r");
            string? beatmap = get("beatmap", "osu");
            string? output = get("output", "out");

            if (string.IsNullOrEmpty(replay))
            {
                error = "Missing replay file. Specify --replay <replay.osr> (danser-go: -replay=<replay.osr>).";
                return false;
            }

            if (string.IsNullOrEmpty(beatmap))
            {
                error = "Missing beatmap file. Specify --beatmap <map.osz> (danser-go: -osu=<map.osz>).";
                return false;
            }

            if (string.IsNullOrEmpty(output))
            {
                // danser-go -record without -out writes to videos/<auto>.mp4; require explicit output for determinism,
                // but allow an auto-named file next to the replay when -record was passed.
                if (has("record", "render"))
                    output = Path.Combine(cwd, $"{Path.GetFileNameWithoutExtension(replay)}.mp4");
                else
                {
                    error = "Missing output location. Specify --output <out.mp4> (danser-go: -out=<path>).";
                    return false;
                }
            }

            options.ReplayPath = Path.GetFullPath(replay!, cwd);
            options.BeatmapPath = Path.GetFullPath(beatmap!, cwd);
            options.OutputPath = Path.GetFullPath(output!, cwd);

            string? skin = get("skin", "skinpath");
            if (!string.IsNullOrEmpty(skin))
                options.SkinPath = Path.GetFullPath(skin!, cwd);

            if (!File.Exists(options.ReplayPath))
            {
                error = $"Replay file not found: {options.ReplayPath}";
                return false;
            }

            if (!File.Exists(options.BeatmapPath))
            {
                error = $"Beatmap file not found: {options.BeatmapPath}";
                return false;
            }

            if (!string.IsNullOrEmpty(options.SkinPath) && !File.Exists(options.SkinPath) && !Directory.Exists(options.SkinPath))
            {
                error = $"Skin path not found (expected .osk file or skin folder): {options.SkinPath}";
                return false;
            }

            if (!tryParseInt("--width", get("width"), 320, 7680, out int? width, out error)) return false;
            if (width.HasValue) options.Width = width.Value;

            if (!tryParseInt("--height", get("height"), 240, 4320, out int? height, out error)) return false;
            if (height.HasValue) options.Height = height.Value;

            if (!tryParseDouble("--fps", get("fps"), 1, 240, "1..240", out double? fps, out error)) return false;
            if (fps.HasValue) options.Fps = fps.Value;

            string? bitrate = get("video-bitrate", "bitrate");
            if (!string.IsNullOrEmpty(bitrate)) options.VideoBitrate = bitrate!;

            string? encoder = get("encoder");
            if (!string.IsNullOrEmpty(encoder)) options.Encoder = encoder!;

            string? pixelFormat = get("pixel-format");
            if (!string.IsNullOrEmpty(pixelFormat)) options.PixelFormat = pixelFormat!;

            string? format = get("output-format", "container");
            if (!string.IsNullOrEmpty(format))
            {
                options.OutputFormat = format.ToLowerInvariant().TrimStart('.');

                switch (options.OutputFormat)
                {
                    case "webm":
                        options.PixelFormat = "yuv420p";
                        break;

                    case "gif":
                        options.PixelFormat = "rgb24";
                        options.Encoder = "gif";
                        break;
                }
            }

            if (!tryParseInt("--quality", get("quality"), 0, 51, out int? quality, out error)) return false;
            options.Quality = quality;

            if (!tryParseDouble("--max-duration", get("max-duration"), double.Epsilon, double.MaxValue, "seconds > 0", out double? maxDuration, out error)) return false;
            options.MaxDurationSeconds = maxDuration;

            if (!tryParseDouble("--start-at", get("start-at"), 0, double.MaxValue, "seconds >= 0", out double? startAt, out error)) return false;
            options.StartAtSeconds = startAt;

            if (!tryParseDouble("--end-at", get("end-at"), double.Epsilon, double.MaxValue, "seconds > 0", out double? endAt, out error)) return false;
            options.EndAtSeconds = endAt;

            options.OutputDir = get("output-dir");
            options.AutoName = has("auto-name", "autoname");
            options.Overwrite = !has("no-overwrite");
            options.FFmpegPath = get("ffmpeg-path");
            options.FFmpegExtraArgs = get("ffmpeg-extra-args");

            options.NoHitsounds = has("no-hitsounds", "hitsounds-off");
            options.HitsoundsOnly = has("hitsounds-only", "hitsounds-only-mode");

            options.ShowProgressBar = !has("no-progress-bar");
            options.ShowComboMeter = !has("no-combo-meter");
            options.ShowLeaderboard = !has("no-leaderboard");
            options.ShowKeyOverlay = !has("no-key-overlay", "hide-key-overlay");

            if (float.TryParse(get("cursor-size"), out float cursorSize) && cursorSize > 0)
                options.CursorSize = cursorSize;

            if (float.TryParse(get("cursor-trail"), out float cursorTrail) && cursorTrail >= 0)
                options.CursorTrailMs = cursorTrail;

            options.HideCursor = has("hide-cursor", "no-cursor");

            if (float.TryParse(get("bg-dim"), out float bgDim) && bgDim >= 0 && bgDim <= 1)
                options.BackgroundDim = bgDim;

            if (float.TryParse(get("bg-blur"), out float bgBlur) && bgBlur >= 0 && bgBlur <= 20)
                options.BackgroundBlur = bgBlur;

            options.StoragePath = get("storage-path");

            if (double.TryParse(get("lead-in"), out double leadIn)) options.LeadInMs = leadIn;
            if (double.TryParse(get("lead-out"), out double leadOut)) options.LeadOutMs = leadOut;

            options.IncludeAudio = !has("no-audio", "mute");
            options.ShowStoryboard = !has("no-storyboard", "no-storyboards");
            options.ShowHUD = !has("no-hud", "hide-hud");
            options.EndOnFail = has("end-on-fail", "stop-on-fail");
            options.SettleSeeks = !has("no-seek-settle", "no-settle-seeks");

            // Ensure output has an extension; default to the container format.
            if (string.IsNullOrEmpty(Path.GetExtension(options.OutputPath)))
                options.OutputPath += $".{options.OutputFormat}";

            if (options.AutoName)
            {
                string dir = !string.IsNullOrEmpty(options.OutputDir) ? Path.GetFullPath(options.OutputDir!, cwd) : cwd;
                options.OutputPath = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(options.ReplayPath)} - {Path.GetFileNameWithoutExtension(options.BeatmapPath)}.{options.OutputFormat}");
            }
            else if (!string.IsNullOrEmpty(options.OutputDir))
            {
                options.OutputPath = Path.Combine(Path.GetFullPath(options.OutputDir!, cwd), Path.GetFileName(options.OutputPath));
            }

            if (!options.Overwrite && File.Exists(options.OutputPath))
            {
                error = $"Output file already exists (and --no-overwrite was passed): {options.OutputPath}";
                return false;
            }

            return true;
        }

        private static Dictionary<string, string?> parseFlags(string[] args)
        {
            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (!arg.StartsWith('-'))
                    continue;

                string trimmed = arg.TrimStart('-');
                int eq = trimmed.IndexOf('=');
                string key;
                string? value;

                if (eq >= 0)
                {
                    key = trimmed.Substring(0, eq).Trim();
                    value = trimmed.Substring(eq + 1).Trim().Trim('"');
                }
                else
                {
                    key = trimmed.Trim();
                    value = null;

                    // support space-separated values for known value-taking flags.
                    if (value_flags.Contains(key) && i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        value = args[++i].Trim().Trim('"');
                }

                map[key] = value;
            }

            return map;
        }

        private static bool tryParseInt(string flag, string? value, int min, int max, out int? result, out string? error)
        {
            result = null;
            error = null;

            if (value == null)
                return true;

            if (!int.TryParse(value, out int parsed) || parsed < min || parsed > max)
            {
                error = $"Invalid {flag} value: {value} (expected {min}..{max}).";
                return false;
            }

            result = parsed;
            return true;
        }

        private static bool tryParseDouble(string flag, string? value, double min, double max, string range, out double? result, out string? error)
        {
            result = null;
            error = null;

            if (value == null)
                return true;

            if (!double.TryParse(value, out double parsed) || parsed < min || parsed > max)
            {
                error = $"Invalid {flag} value: {value} (expected {range}).";
                return false;
            }

            result = parsed;
            return true;
        }
    }
}
