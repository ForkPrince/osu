// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Size = System.Drawing.Size;

namespace osu.Desktop.Rendering
{
    /// <summary>
    /// Headless-ish render game, danser-go style:
    /// imports beatmap + skin + replay, plays via <see cref="ReplayPlayer"/>,
    /// seeks frame-by-frame and pipes raw RGBA to ffmpeg.
    /// </summary>
    internal partial class ReplayRenderGame : OsuGameDesktop
    {
        private readonly ReplayRenderOptions options;

        public ReplayRenderGame(ReplayRenderOptions options)
            : base(Array.Empty<string>())
        {
            this.options = options;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Task.Run(async () =>
            {
                try
                {
                    await RunRenderAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Render failed: {ex}", LoggingTarget.Runtime, LogLevel.Error);
                    Console.Error.WriteLine($"Render failed: {ex.Message}");
                    finish(1);
                }
            });
        }

        private async Task RunRenderAsync()
        {
            Logger.Log($"Render: replay={options.ReplayPath} beatmap={options.BeatmapPath} output={options.OutputPath} {options.Width}x{options.Height}@{options.Fps}",
                LoggingTarget.Runtime, LogLevel.Debug);

            if (!FfmpegVideoEncoder.IsAvailable())
                throw new InvalidOperationException("ffmpeg was not found on PATH. Install ffmpeg to render videos.");

            applyRenderConfig();

            await runOnUpdateThreadAsync(() =>
            {
                // Never show import/result prompts or toasts in exports:
                // rewire all archive manager notifications to no-ops and hide any
                // notifications posted during base game load.
                BeatmapManager.PostNotification = _ => { };
                ScoreManager.PostNotification = _ => { };
                SkinManager.PostNotification = _ => { };
                BeatmapDownloader.PostNotification = _ => { };
                ScoreDownloader.PostNotification = _ => { };
                BeatmapManager.PresentImport = _ => { };
                ScoreManager.PresentImport = _ => { };
                SkinManager.PresentImport = _ => { };
                Notifications.Hide();
                return true;
            }).ConfigureAwait(false);

            // Give config + window a moment to settle.
            await Task.Delay(1000).ConfigureAwait(false);

            // Copy file inputs to temp: lazer importers delete source archives on success,
            // and a render CLI must not consume the user's files (unlike in-game import).
            string beatmapImportPath = copyToTemp(options.BeatmapPath);
            string replayImportPath = copyToTemp(options.ReplayPath);
            string? skinImportPath = !string.IsNullOrEmpty(options.SkinPath) && File.Exists(options.SkinPath)
                ? copyToTemp(options.SkinPath!)
                : options.SkinPath;

            // 1. Beatmap first: replay import needs its MD5 hash.
            Logger.Log($"Importing beatmap {options.BeatmapPath} ...", LoggingTarget.Runtime, LogLevel.Debug);
            var beatmapSet = await BeatmapManager.Import(new ImportTask(beatmapImportPath)).ConfigureAwait(false);

            if (beatmapSet == null)
                throw new InvalidOperationException("Beatmap import failed (no result). Check the .osz file.");

            // 2. Skin (.osk file or legacy folder).
            Live<SkinInfo>? importedSkin = null;

            if (!string.IsNullOrEmpty(skinImportPath))
            {
                Logger.Log($"Importing skin {options.SkinPath} ...", LoggingTarget.Runtime, LogLevel.Debug);
                importedSkin = await SkinManager.Import(new ImportTask(skinImportPath!)).ConfigureAwait(false);

                if (importedSkin != null)
                {
                    var skinToApply = importedSkin;
                    await runOnUpdateThreadAsync(() =>
                    {
                        SkinManager.CurrentSkinInfo.Value = skinToApply;
                        return true;
                    }).ConfigureAwait(false);
                }
                else
                    Logger.Log("Skin import returned no result; continuing with current skin.", LoggingTarget.Runtime, LogLevel.Debug);
            }

            // 3. Replay.
            Logger.Log($"Importing replay {options.ReplayPath} ...", LoggingTarget.Runtime, LogLevel.Debug);
            var importedScores = await ScoreManager.Import(new ProgressNotification(), new[] { new ImportTask(replayImportPath) }).ConfigureAwait(false);
            var scoreInfoLive = importedScores.FirstOrDefault();

            if (scoreInfoLive == null)
                throw new InvalidOperationException("Replay import failed. Most likely the beatmap (.osz) does not match the replay's MD5 hash.");

            // RealmLive<T>.Value requires the update thread for managed objects.
            var detachedScoreInfo = await runOnUpdateThreadAsync(() => scoreInfoLive.Value.Detach()).ConfigureAwait(false);

            var score = ScoreManager.GetScore(detachedScoreInfo);

            if (score?.Replay == null || score.Replay.Frames.Count == 0)
                throw new InvalidOperationException("Imported score has no replay frames.");

            string? audioPath = null;

            if (options.IncludeAudio)
            {
                audioPath = tryResolveAudioPath(beatmapSet);

                if (audioPath == null)
                    Logger.Log("No audio track found in beatmap; rendering video-only.", LoggingTarget.Runtime, LogLevel.Debug);
            }

            var beatmapInfo = score.ScoreInfo.BeatmapInfo ?? throw new InvalidOperationException("Replay has no beatmap info.");
            var working = BeatmapManager.GetWorkingBeatmap(beatmapInfo);

            var pushTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Set beatmap/ruleset and push the player via the game's menu flow,
            // mirroring OsuGame.PresentScore: the global beatmap/ruleset bindables
            // are leased to screens and can only be changed from a valid screen.
            Schedule(() => PerformFromScreen(screen =>
            {
                try
                {
                    Ruleset.Value = score.ScoreInfo.Ruleset;
                    Beatmap.Value = working;

                    screen.Push(new ReplayPlayerLoader(score, new PlayerConfiguration
                    {
                        ShowResults = false,
                        AllowPause = true,
                        AllowRestart = false,
                        AllowUserInteraction = false,
                    }));

                    pushTcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    pushTcs.TrySetException(ex);
                }
            }));

            await pushTcs.Task.ConfigureAwait(false);

            ReplayPlayer? player = await waitForPlayerAsync(TimeSpan.FromSeconds(90)).ConfigureAwait(false);

            if (player == null)
                throw new InvalidOperationException("Timed out waiting for ReplayPlayer to load.");

            bool loaded = await runOnUpdateThreadAsync(() => player.LoadedBeatmapSuccessfully).ConfigureAwait(false);

            if (!loaded)
                throw new InvalidOperationException("Beatmap failed to load inside player (no hitobjects).");

            Schedule(player.PauseGameplay);
            await Task.Delay(500).ConfigureAwait(false);

            await runOnUpdateThreadAsync(() =>
            {
                // Match real gameplay state (overlays disabled) and flush any toasts
                // posted before capture (import notices, log forwards, first-run notices).
                // Exports must not contain notification UI.
                player.OverlayActivationMode.Value = OverlayActivation.Disabled;
                return true;
            }).ConfigureAwait(false);

            double firstFrame = score.Replay.Frames.First().Time;
            double lastFrame = score.Replay.Frames.Last().Time;

            double lastObject;
            try
            {
                lastObject = await runOnUpdateThreadAsync(() => player.GameplayState.Beatmap.GetLastObjectTime()).ConfigureAwait(false);
            }
            catch
            {
                lastObject = lastFrame;
            }

            double startTime = Math.Max(0, firstFrame - options.LeadInMs);
            double endTime = Math.Max(lastFrame + options.LeadOutMs, lastObject + options.LeadOutMs);
            double stepMs = 1000.0 / options.Fps;
            int totalFrames = Math.Max(1, (int)Math.Ceiling((endTime - startTime) / stepMs));

            Logger.Log($"Rendering {totalFrames} frames ({startTime:0}ms -> {endTime:0}ms @ {options.Fps}fps) ...", LoggingTarget.Runtime, LogLevel.Debug);

            // Probe the real framebuffer size. The window doesn't always honour the
            // requested size (e.g. clamped to desktop), and encoding at a different
            // size than the screenshots would crop the footage. Always match reality.
            using (var probe = await Host.TakeScreenshotAsync().ConfigureAwait(false))
            {
                if (probe == null || probe.Width < 64 || probe.Height < 64)
                    throw new InvalidOperationException("Failed to capture frame (screenshot returned null).");

                if (probe.Width != options.Width || probe.Height != options.Height)
                {
                    Logger.Log($"Requested {options.Width}x{options.Height} but framebuffer is {probe.Width}x{probe.Height}; "
                               + "using framebuffer size to avoid cropping. Run with a matching display (or adjust --width/--height) for exact sizing.",
                        LoggingTarget.Runtime, LogLevel.Debug);
                    options.Width = probe.Width;
                    options.Height = probe.Height;
                }
            }

            var encoder = new FfmpegVideoEncoder(options) { AudioInputPath = audioPath };
            encoder.Start();

            byte[] pixelBuffer = new byte[options.Width * options.Height * 4];

            try
            {
                for (int i = 0; i < totalFrames; i++)
                {
                    double time = startTime + i * stepMs;

                    Schedule(() => player.Seek(time));
                    await waitForDrawFramesAsync(3).ConfigureAwait(false);

                    using (var image = await Host.TakeScreenshotAsync().ConfigureAwait(false))
                    {
                        if (image == null)
                            throw new InvalidOperationException("Failed to capture frame (screenshot returned null).");

                        copyToBuffer(image, pixelBuffer, options.Width, options.Height);
                        encoder.WriteFrame(pixelBuffer);
                    }

                    if ((i + 1) % (int)Math.Max(1, options.Fps) == 0 || i + 1 == totalFrames)
                    {
                        Logger.Log($"Render progress: {i + 1}/{totalFrames} ({(i + 1) * 100.0 / totalFrames:0.0}%)", LoggingTarget.Runtime, LogLevel.Debug);
                        Console.WriteLine($"Render progress: {i + 1}/{totalFrames} ({(i + 1) * 100.0 / totalFrames:0.0}%)");
                    }
                }
            }
            finally
            {
                int ffmpegExit = encoder.Finish();
                if (ffmpegExit != 0)
                    throw new InvalidOperationException($"ffmpeg exited with code {ffmpegExit}. Output may be incomplete.");
            }

            Logger.Log($"Render complete: {options.OutputPath}", LoggingTarget.Runtime, LogLevel.Debug);
            Console.WriteLine($"Render complete: {options.OutputPath}");

            if (!string.IsNullOrEmpty(audioPath) && audioPath.StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
            {
                try { File.Delete(audioPath); } catch { }
            }

            finish(0);
        }

        private void applyRenderConfig()
        {
            try
            {
                var frameworkConfig = Dependencies.Get<FrameworkConfigManager>();
                frameworkConfig.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed);
                frameworkConfig.SetValue(FrameworkSetting.WindowedSize, new Size(options.Width, options.Height));
                frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Unlimited);
                frameworkConfig.SetValue(FrameworkSetting.ShowLogOverlay, false);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to apply window config: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }

            try
            {
                LocalConfig.SetValue(OsuSetting.ShowStoryboard, options.ShowStoryboard);
                LocalConfig.SetValue(OsuSetting.BeatmapSkins, true);
                LocalConfig.SetValue(OsuSetting.BeatmapColours, true);
                LocalConfig.SetValue(OsuSetting.BeatmapHitsounds, true);
                LocalConfig.SetValue(OsuSetting.ShowFpsDisplay, false);
                LocalConfig.SetValue(OsuSetting.HUDVisibilityMode, options.ShowHUD
                    ? HUDVisibilityMode.Always
                    : HUDVisibilityMode.Never);
                LocalConfig.SetValue(OsuSetting.ShowFirstRunSetup, false);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to apply gameplay config: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }
        }

        private async Task<ReplayPlayer?> waitForPlayerAsync(TimeSpan timeout)
        {
            var start = DateTime.UtcNow;

            while (DateTime.UtcNow - start < timeout)
            {
                try
                {
                    var found = await runOnUpdateThreadAsync(() =>
                        ScreenStack.CurrentScreen is ReplayPlayer rp && rp.IsLoaded ? rp : null).ConfigureAwait(false);

                    if (found != null)
                        return found;
                }
                catch
                {
                    // ScreenStack may not be ready yet.
                }

                await Task.Delay(500).ConfigureAwait(false);
            }

            return null;
        }

        private Task<T> runOnUpdateThreadAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Schedule(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        private static string copyToTemp(string sourcePath)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"osu-render-{Guid.NewGuid():N}-{Path.GetFileName(sourcePath)}");
            File.Copy(sourcePath, tempPath);
            return tempPath;
        }

        private Task waitForDrawFramesAsync(int frames)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int seen = 0;
            ScheduledDelegate? del = null;

            del = Host.DrawThread.Scheduler.AddDelayed(() =>
            {
                if (++seen >= frames)
                {
                    tcs.TrySetResult(true);
                    del?.Cancel();
                }
            }, 10, true);

            return tcs.Task;
        }

        private static void copyToBuffer(Image<Rgba32> image, byte[] buffer, int expectedWidth, int expectedHeight)
        {
            // Screenshots should already match the window size, but be defensive: centre-crop or fail clearly.
            if (image.Width != expectedWidth || image.Height != expectedHeight)
            {
                // Resize via simple check: if mismatch, we still copy row-by-row with clamping to avoid OOB.
                // Most setups will have exact match after WindowedSize applies.
            }

            int w = Math.Min(image.Width, expectedWidth);
            int h = Math.Min(image.Height, expectedHeight);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < h; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    int rowLen = Math.Min(row.Length, w);

                    for (int x = 0; x < rowLen; x++)
                    {
                        int dst = (y * expectedWidth + x) * 4;
                        var px = row[x];
                        buffer[dst] = px.R;
                        buffer[dst + 1] = px.G;
                        buffer[dst + 2] = px.B;
                        buffer[dst + 3] = px.A;
                    }

                    // Pad remainder of row with black if screenshot is narrower than expected.
                    for (int x = rowLen; x < expectedWidth; x++)
                    {
                        int dst = (y * expectedWidth + x) * 4;
                        buffer[dst] = 0;
                        buffer[dst + 1] = 0;
                        buffer[dst + 2] = 0;
                        buffer[dst + 3] = 255;
                    }
                }

                // Pad remaining rows with black.
                for (int y = h; y < expectedHeight; y++)
                {
                    for (int x = 0; x < expectedWidth; x++)
                    {
                        int dst = (y * expectedWidth + x) * 4;
                        buffer[dst] = 0;
                        buffer[dst + 1] = 0;
                        buffer[dst + 2] = 0;
                        buffer[dst + 3] = 255;
                    }
                }
            });
        }

        private string? tryResolveAudioPath(Live<BeatmapSetInfo> beatmapSet)
        {
            try
            {
                var realm = Dependencies.Get<RealmAccess>();
                var fileStore = new RealmFileStore(realm, Storage);

                string? audioFilename = null;
                string? storagePath = null;

                beatmapSet.PerformRead(s =>
                {
                    foreach (var f in s.Files)
                    {
                        string name = f.Filename.ToLowerInvariant();
                        if (name.EndsWith(".mp3", StringComparison.Ordinal) || name.EndsWith(".ogg", StringComparison.Ordinal)
                            || name.EndsWith(".wav", StringComparison.Ordinal) || name.EndsWith(".m4a", StringComparison.Ordinal)
                            || name.EndsWith(".flac", StringComparison.Ordinal))
                        {
                            audioFilename = f.Filename;
                            storagePath = f.File.GetStoragePath();
                            break;
                        }
                    }
                });

                if (audioFilename == null || storagePath == null)
                    return null;

                string tempPath = Path.Combine(Path.GetTempPath(), $"osu-render-{Guid.NewGuid():N}-{Path.GetFileName(audioFilename)}");

                using (var src = fileStore.Store.GetStream(storagePath))
                {
                    if (src == null) return null;
                    using (var dst = File.Create(tempPath))
                        src.CopyTo(dst);
                }

                return tempPath;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to resolve audio track: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }
        }

        private void finish(int code)
        {
            Environment.ExitCode = code;

            Schedule(() =>
            {
                try
                {
                    Host.Exit();
                }
                catch
                {
                    Environment.Exit(code);
                }

                // Failsafe: ensure process terminates even if host exit is delayed.
                Task.Delay(5000).ContinueWith(_ => Environment.Exit(code));
            });
        }
    }
}
