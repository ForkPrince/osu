# Rendering replays from the command line

Render a replay to video from the terminal. Provide a replay (`.osr`),
the beatmap package it was played on (`.osz`), optionally a skin
(`.osk` file or folder), and an output path:

```
./osu! --render --replay play.osr --beatmap map.osz --skin skin.osk --output render.mp4
```

Or with danser-go-style flags:

```
./osu! --render -replay=replay.osr -osu=map.osz -skinpath=./skins/myskin -record -out=/tmp/out
```

Run with `--help` to see all options:

```
./osu! --help
```

## Options

| Flag | Description | Default |
| ---- | ----------- | ------- |
| `--replay`, `-replay`, `-r` | Replay file (`.osr`) to render. Required. | – |
| `--beatmap`, `-osu` | Beatmap package (`.osz`) matching the replay. Required (imported first, since replay lookup needs its MD5 hash). | – |
| `--output`, `-out` | Output video file. Required. Extension defaults to `.mp4`. | – |
| `--skin`, `-skinpath` | Skin `.osk` file or legacy skin folder. | Current user skin |
| `--width` / `--height` | Frame size (320–7680 / 240–4320). If the window can't honour it, the real framebuffer size is used so footage never crops. Prefer 16:9 sizes (e.g. 1920x1080, 1280x720). | 1920x1080 |
| `--fps` | Output framerate (1–240). | 60 |
| `--bitrate`, `--video-bitrate` | ffmpeg video bitrate (e.g. `8000k`). | `8000k` |
| `--encoder` | ffmpeg video encoder (`libx264`, `libx265`, `h264_nvenc`, …). | `libx264` |
| `--no-audio` | Don't mux the beatmap's audio track. | Audio included |
| `--no-video` | Hide beatmap background video. | Shown |
| `--no-storyboard` | Disable storyboard. | Shown |
| `--no-hud` | Hide HUD for clean footage. | Shown |
| `--no-progress-bar` | Hide the song progress bar. | Shown |
| `--no-combo-meter` | Hide the combo counter. | Shown |
| `--no-leaderboard` | Hide the gameplay leaderboard. | Shown |
| `--no-key-overlay` | Hide the key overlay. | Shown |
| `--end-on-fail` | If the replay failed the beatmap, stop at the fail point instead of rendering to the end of the song. | Off |
| `--start-at <sec>` / `--end-at <sec>` | Export only a window of the replay. | Full |
| `--max-duration <sec>` | Cap the exported length. | Unlimited |
| `--output-format <fmt>` | `mp4`, `mkv`, `webm` or `gif`. | `mp4` |
| `--quality <0-51>` | Constant-quality encode (overrides `--bitrate`), lower is better. | Bitrate |
| `--no-hitsounds` | Don't mix gameplay hitsounds into the audio. | Mixed in |
| `--hitsounds-only` | Only export gameplay hitsounds (no music). | Off |
| `--cursor-size <float>` | Gameplay cursor size multiplier. | `1` |
| `--cursor-trail <ms>` | Cursor trail length (`0` disables the trail). | Skin default |
| `--hide-cursor` | Don't render the cursor. | Shown |
| `--bg-dim <0-1>` | Background dim level. | `0.7` |
| `--bg-blur <0-20>` | Background blur level. | `0` |
| `--output-dir <dir>` | Directory to write the output into. | – |
| `--auto-name` | Generate the filename from the replay and beatmap names. | Off |
| `--no-overwrite` | Fail instead of overwriting an existing output file. | Overwrite |
| `--ffmpeg-path <path>` | Path to the ffmpeg binary. | `ffmpeg` |
| `--ffmpeg-extra-args "<args>"` | Extra arguments appended to the ffmpeg command. | – |
| `--storage-path <dir>` | Directory used for the isolated render data. | `osu-render` |
| `--lead-in` / `--lead-out` | Padding (ms) captured around first/last replay frame. | 2000 / 2000 |

## Notes

- Requires `ffmpeg` on `PATH`. Video is piped as raw RGBA to ffmpeg.
- The audio track is the beatmap's music with gameplay samples (hitsounds, ticks, etc.) mixed in at
  their exact gameplay times. Use `--hitsounds-only` or `--no-hitsounds` to change that.
- Renders use an isolated `osu-render` data directory, so your main game library is untouched.
- Input files are never modified or deleted (imports run on temp copies).
- Exports contain no notification UI: import prompts, toasts and first-run overlays are suppressed during rendering.
- Exit code is `0` on success, `2` on bad arguments, `1` on render failure.
- Rendering needs a display with OpenGL. On headless Linux servers, run under a virtual display, e.g.
  `xvfb-run -a -s "-screen 0 1920x1080x24" dotnet osu.dll --render ...`.
