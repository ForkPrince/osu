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
        public string Container { get; set; } = "mp4";

        public bool IncludeAudio { get; set; } = true;
        public bool IncludeVideo { get; set; } = true;
        public bool ShowStoryboard { get; set; } = true;
        public bool ShowHUD { get; set; } = true;
        public bool EndOnFail { get; set; }

        public string OutputFormat { get; set; } = "mp4";
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

        public static bool IsRenderIntent(string[] args)
        {
            foreach (var a in args)
            {
                string key = a.Split('=', 2)[0].Trim().ToLowerInvariant();
                if (key is "--render" or "-record" or "--record")
                    return true;
            }

            return false;
        }

        public static bool IsHelpRequest(string[] args)
        {
            foreach (var a in args)
            {
                string key = a.Split('=', 2)[0].Trim().ToLowerInvariant();
                if (key is "--help" or "-h" or "-help" or "--render-help")
                    return true;
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
  --no-video                            Don't show beatmap background video.
  --no-storyboard                       Disable storyboard.
  --no-hud                              Hide HUD (score, combo, leaderboard) for clean footage.
  --no-progress-bar                     Hide the song progress bar.
  --no-combo-meter                      Hide the combo counter.
  --no-leaderboard                      Hide the gameplay leaderboard.
  --no-key-overlay                      Hide the key overlay.
  --end-on-fail                         If the replay failed the beatmap, stop at the fail instead of rendering to the end of the song.
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

        public static bool TryParse(string[] args, string cwd, out ReplayRenderOptions? options, out string? error)
        {
            options = new ReplayRenderOptions();
            error = null;

            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in args)
            {
                if (!raw.StartsWith('-'))
                    continue;

                string trimmed = raw.TrimStart('-');
                string key;
                string? val;

                int eq = trimmed.IndexOf('=');
                if (eq >= 0)
                {
                    key = trimmed.Substring(0, eq).Trim();
                    val = trimmed.Substring(eq + 1).Trim().Trim('"');
                }
                else
                {
                    key = trimmed.Trim();
                    val = null;
                }

                // --flag value (space separated) support for known value flags.
                // Handled in second pass below; store null for now.
                map[key] = val;
            }

            // space-separated values: walk original args
            var valueFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "replay", "r", "beatmap", "osu", "skin", "skinpath", "output", "out",
                "width", "height", "fps", "bitrate", "video-bitrate", "encoder",
                "pixel-format", "container", "lead-in", "lead-out", "audio-bitrate",
                "output-format", "quality", "max-duration", "start-at", "end-at",
                "output-dir", "ffmpeg-path", "ffmpeg-extra-args", "cursor-size", "cursor-trail",
                "bg-dim", "bg-blur", "storage-path"
            };

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (!a.StartsWith('-')) continue;
                string k = a.TrimStart('-').Split('=', 2)[0];
                if (!valueFlags.Contains(k)) continue;
                if (map.TryGetValue(k, out string? existing) && !string.IsNullOrEmpty(existing)) continue;

                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                    map[k] = args[i + 1].Trim().Trim('"');
            }

            string? get(params string[] keys)
            {
                foreach (var k in keys)
                {
                    if (map.TryGetValue(k, out string? v) && !string.IsNullOrEmpty(v))
                        return v;
                }

                return null;
            }

            bool has(params string[] keys)
            {
                foreach (var k in keys)
                {
                    if (map.ContainsKey(k))
                        return true;
                }

                return false;
            }

            string? replay = get("replay", "r");
            string? beatmap = get("beatmap", "osu");
            string? skin = get("skin", "skinpath");
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
                // danser-go -record without -out writes to videos/<auto>.mp4; require explicit output for determinism.
                // Fall back to auto name next to replay if -record was passed.
                if (has("record", "render"))
                {
                    string autoBase = Path.GetFileNameWithoutExtension(replay);
                    output = Path.Combine(cwd, $"{autoBase}.mp4");
                }
                else
                {
                    error = "Missing output location. Specify --output <out.mp4> (danser-go: -out=<path>).";
                    return false;
                }
            }

            options.ReplayPath = Path.GetFullPath(replay!, cwd);
            options.BeatmapPath = Path.GetFullPath(beatmap!, cwd);
            options.OutputPath = Path.GetFullPath(output!, cwd);

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

            string? w = get("width");
            if (w != null && (!int.TryParse(w, out int wi) || wi < 320 || wi > 7680))
            {
                error = $"Invalid --width value: {w} (expected 320..7680).";
                return false;
            }

            if (w != null) options.Width = int.Parse(w);

            string? h = get("height");
            if (h != null && (!int.TryParse(h, out int hi) || hi < 240 || hi > 4320))
            {
                error = $"Invalid --height value: {h} (expected 240..4320).";
                return false;
            }

            if (h != null) options.Height = int.Parse(h);

            string? fps = get("fps");
            if (fps != null && (!double.TryParse(fps, out double fpsi) || fpsi < 1 || fpsi > 240))
            {
                error = $"Invalid --fps value: {fps} (expected 1..240).";
                return false;
            }

            if (fps != null) options.Fps = double.Parse(fps);

            string? bitrate = get("video-bitrate", "bitrate");
            if (!string.IsNullOrEmpty(bitrate)) options.VideoBitrate = bitrate!;

            string? encoder = get("encoder");
            if (!string.IsNullOrEmpty(encoder)) options.Encoder = encoder!;

            string? pixFmt = get("pixel-format");
            if (!string.IsNullOrEmpty(pixFmt)) options.PixelFormat = pixFmt!;

            string? format = get("output-format", "container");
            if (!string.IsNullOrEmpty(format))
            {
                options.OutputFormat = format.ToLowerInvariant().TrimStart('.');

                switch (options.OutputFormat)
                {
                    case "webm":
                        options.Container = "webm";
                        options.PixelFormat = "yuv420p";
                        break;
                    case "mkv":
                        options.Container = "mkv";
                        break;
                    case "gif":
                        options.Container = "gif";
                        options.PixelFormat = "rgb24";
                        options.Encoder = "gif";
                        break;
                    default:
                        options.Container = options.OutputFormat;
                        break;
                }
            }

            string? quality = get("quality");
            if (quality != null && !int.TryParse(quality, out int qv))
            {
                error = $"Invalid --quality value: {quality} (expected 0..51).";
                return false;
            }
            if (quality != null)
                options.Quality = Math.Clamp(int.Parse(quality), 0, 51);

            string? maxDuration = get("max-duration");
            if (maxDuration != null && (!double.TryParse(maxDuration, out double md) || md <= 0))
            {
                error = $"Invalid --max-duration value: {maxDuration} (expected seconds > 0).";
                return false;
            }
            if (maxDuration != null) options.MaxDurationSeconds = double.Parse(maxDuration);

            string? startAt = get("start-at");
            if (startAt != null && (!double.TryParse(startAt, out double s) || s < 0))
            {
                error = $"Invalid --start-at value: {startAt} (expected seconds >= 0).";
                return false;
            }
            if (startAt != null) options.StartAtSeconds = double.Parse(startAt);

            string? endAt = get("end-at");
            if (endAt != null && (!double.TryParse(endAt, out double e) || e <= 0))
            {
                error = $"Invalid --end-at value: {endAt} (expected seconds > 0).";
                return false;
            }
            if (endAt != null) options.EndAtSeconds = double.Parse(endAt);

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

            string? cursorSize = get("cursor-size");
            if (cursorSize != null && float.TryParse(cursorSize, out float cs) && cs > 0)
                options.CursorSize = cs;

            string? cursorTrail = get("cursor-trail");
            if (cursorTrail != null && float.TryParse(cursorTrail, out float ct) && ct >= 0)
                options.CursorTrailMs = ct;

            options.HideCursor = has("hide-cursor", "no-cursor");

            string? bgDim = get("bg-dim");
            if (bgDim != null && float.TryParse(bgDim, out float bd) && bd >= 0 && bd <= 1)
                options.BackgroundDim = bd;

            string? bgBlur = get("bg-blur");
            if (bgBlur != null && float.TryParse(bgBlur, out float bb) && bb >= 0 && bb <= 20)
                options.BackgroundBlur = bb;

            options.StoragePath = get("storage-path");

            string? leadIn = get("lead-in");
            if (leadIn != null && double.TryParse(leadIn, out double li)) options.LeadInMs = li;

            string? leadOut = get("lead-out");
            if (leadOut != null && double.TryParse(leadOut, out double lo)) options.LeadOutMs = lo;

            options.IncludeAudio = !has("no-audio", "mute");
            options.IncludeVideo = !has("no-video");
            options.ShowStoryboard = !has("no-storyboard", "no-storyboards");
            options.ShowHUD = !has("no-hud", "hide-hud");
            options.EndOnFail = has("end-on-fail", "stop-on-fail");

            // Ensure output has an extension; default to container.
            if (string.IsNullOrEmpty(Path.GetExtension(options.OutputPath)))
                options.OutputPath += $".{options.Container.TrimStart('.')}";

            if (options.AutoName)
            {
                string dir = !string.IsNullOrEmpty(options.OutputDir) ? Path.GetFullPath(options.OutputDir!, cwd) : cwd;

                string replayName = Path.GetFileNameWithoutExtension(options.ReplayPath);
                string beatmapName = Path.GetFileNameWithoutExtension(options.BeatmapPath);

                options.OutputPath = Path.Combine(dir, $"{replayName} - {beatmapName}.{options.Container.TrimStart('.')}");
            }
            else if (!string.IsNullOrEmpty(options.OutputDir) && string.IsNullOrEmpty(Path.GetDirectoryName(Path.GetFileName(options.OutputPath))))
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
    }
}
