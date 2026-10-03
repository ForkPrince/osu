# Rendering replays from the command line

*osu!* can render a replay file to video without opening the game window, danser-go style.
You provide a replay (`.osr`), the beatmap package it was played on (`.osz`),
optionally a skin (`.osk` file or legacy skin folder), and an output path:

```shell
./osu! --render --replay play.osr --beatmap map.osz --skin skin.osk --output render.mp4
```

Or with danser-go-style flags:

```shell
./osu! --render -replay=replay.osr -osu=map.osz -skinpath=./skins/myskin -record -out=/tmp/out
```

Run with `--help` to see all options:

```shell
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
| `--lead-in` / `--lead-out` | Padding (ms) captured around first/last replay frame. | 2000 / 2000 |

## Notes

- Requires `ffmpeg` on `PATH`. Video is piped as raw RGBA to ffmpeg (`libx264`/`yuv420p` by default);
  the beatmap's audio track is muxed with `-shortest`.
- Renders use an isolated `osu-render` data directory, so your main game library is untouched.
- Input files are never modified or deleted (imports run on temp copies).
- Exports contain no notification UI: import prompts, toasts and first-run overlays are suppressed during rendering.
- Exit code is `0` on success, `2` on bad arguments, `1` on render failure.
- Rendering needs a display with OpenGL. On headless Linux servers, run under a virtual display, e.g.
  `xvfb-run -a -s "-screen 0 1920x1080x24" dotnet osu.dll --render ...`.
