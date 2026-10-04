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
  --end-on-fail                         If the replay failed the beatmap, stop at the fail instead of rendering to the end of the song.
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
  - Audio is the beatmap's audio track muxed with -shortest; hitsounds come through gameplay capture only if audible at render time.
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
                "pixel-format", "container", "lead-in", "lead-out", "audio-bitrate"
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

            string? container = get("container");
            if (!string.IsNullOrEmpty(container)) options.Container = container!;

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

            return true;
        }
    }
}
