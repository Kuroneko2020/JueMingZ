using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using JueMingZ.Automation.Information;
using JueMingZ.Diagnostics;
using JueMingZ.UI;

namespace JueMingZ.Automation.Search.ChestLocator
{
    internal static class ChestItemLocatorOverlayService
    {
        internal const ulong SnapshotMaxAgeTicksForTesting = 3600;
        private const int MaxOverlayHitsPerFrame = 32;
        private const int ScreenCullPadding = 48;
        private static readonly object SyncRoot = new object();
        private static ChestItemLocatorOverlayDiagnostics _diagnostics = new ChestItemLocatorOverlayDiagnostics();

        internal static bool NeedsWorldContext(ChestItemLocatorSnapshot snapshot)
        {
            return snapshot != null &&
                   !object.ReferenceEquals(snapshot, ChestItemLocatorSnapshot.Empty) &&
                   string.Equals(snapshot.Status, ChestItemLocatorSnapshot.StatusOk, StringComparison.Ordinal) &&
                   snapshot.HitCount > 0;
        }

        internal static void RecordSnapshotSkip(ChestItemLocatorSnapshot snapshot)
        {
            UpdateDiagnostics(BuildSnapshotSkipView(snapshot, ResolveSnapshotSkipReason(snapshot)), 0d, 0);
        }

        internal static void RecordContextSkip(ChestItemLocatorSnapshot snapshot, string skipReason)
        {
            UpdateDiagnostics(BuildSnapshotSkipView(snapshot, NormalizeSkip("contextUnavailable", skipReason)), 0d, 0);
        }

        internal static void RecordDrawGuardSkip(ChestItemLocatorSnapshot snapshot)
        {
            UpdateDiagnostics(BuildSnapshotSkipView(snapshot, "drawGuardUnavailable"), 0d, 0);
        }

        internal static int DrawWorldOverlay(object spriteBatch, InformationWorldContext context, ChestItemLocatorSnapshot snapshot)
        {
            var stopwatch = Stopwatch.StartNew();
            var drawn = 0;
            ChestItemLocatorOverlayView view = null;
            try
            {
                // Draw is intentionally a snapshot consumer only. Do not call
                // ChestItemLocatorService.GetSnapshot or perform tile/item scans here.
                view = BuildView(snapshot, context);
                if (!view.Enabled)
                {
                    return 0;
                }

                for (var index = 0; index < view.Hits.Count; index++)
                {
                    if (DrawHit(spriteBatch, context, view.Hits[index]))
                    {
                        drawn++;
                    }
                }

                return drawn;
            }
            catch (Exception error)
            {
                view = BuildSnapshotSkipView(snapshot, "overlayException");
                LogThrottle.ErrorThrottled(
                    "search-chest-locator-overlay-error",
                    TimeSpan.FromSeconds(10),
                    "ChestItemLocatorOverlayService",
                    "Search chest locator overlay draw failed; exception swallowed.", error);
                return 0;
            }
            finally
            {
                stopwatch.Stop();
                UpdateDiagnostics(view ?? BuildSnapshotSkipView(snapshot, "overlayUnavailable"), stopwatch.Elapsed.TotalMilliseconds, drawn);
            }
        }

        internal static ChestItemLocatorOverlayDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        internal static void ResetDiagnosticsForTesting()
        {
            lock (SyncRoot)
            {
                _diagnostics = new ChestItemLocatorOverlayDiagnostics();
            }
        }

        internal static ChestItemLocatorOverlayView BuildViewForTesting(ChestItemLocatorSnapshot snapshot, InformationWorldContext context)
        {
            return BuildView(snapshot, context);
        }

        private static ChestItemLocatorOverlayView BuildView(ChestItemLocatorSnapshot snapshot, InformationWorldContext context)
        {
            if (!NeedsWorldContext(snapshot))
            {
                return BuildSnapshotSkipView(snapshot, ResolveSnapshotSkipReason(snapshot));
            }

            if (context == null)
            {
                return BuildSnapshotSkipView(snapshot, "contextUnavailable");
            }

            if (context.GameUpdateCount < snapshot.GeneratedTick)
            {
                return BuildSnapshotSkipView(snapshot, "tickReset");
            }

            var age = context.GameUpdateCount - snapshot.GeneratedTick;
            if (!WorldMatches(snapshot, context))
            {
                return BuildSnapshotSkipView(snapshot, "worldChanged", age);
            }

            if (age > SnapshotMaxAgeTicksForTesting)
            {
                return BuildSnapshotSkipView(snapshot, "snapshotExpired", age);
            }

            var hits = new List<ChestItemLocatorOverlayHitView>();
            var skippedInvalidContainer = false;
            for (var index = 0; index < snapshot.Hits.Count && hits.Count < MaxOverlayHitsPerFrame; index++)
            {
                var hit = snapshot.Hits[index];
                if (hit == null)
                {
                    continue;
                }

                int currentTileType;
                // Snapshot hits are search results, not proof that the container
                // still exists. Validate only the origin tile before drawing; the
                // overlay must not rescan chest contents here.
                if (!TryResolveCurrentHitTileType(context, hit, out currentTileType))
                {
                    skippedInvalidContainer = true;
                    continue;
                }

                var width = Math.Max(1, InformationChestTileScanner.GetFrameColumns(currentTileType)) * InformationChestTileScanner.TileSize;
                var height = Math.Max(1, InformationChestTileScanner.GetFrameRows(currentTileType)) * InformationChestTileScanner.TileSize;
                var screenX = (int)Math.Round((hit.ChestX * InformationChestTileScanner.TileSize) - context.ScreenX);
                var screenY = (int)Math.Round((hit.ChestY * InformationChestTileScanner.TileSize) - context.ScreenY);
                if (!IsVisible(context, screenX, screenY, width, height))
                {
                    continue;
                }

                hits.Add(new ChestItemLocatorOverlayHitView(
                    hit.ChestX,
                    hit.ChestY,
                    screenX,
                    screenY,
                    width,
                    height,
                    BuildLabel(hit),
                    hit.TotalStack));
            }

            var skipReason = hits.Count <= 0 ? (skippedInvalidContainer ? "invalidContainer" : "offscreen") : string.Empty;
            return new ChestItemLocatorOverlayView(
                hits.Count > 0,
                skipReason,
                snapshot.QueryVersion,
                snapshot.Status,
                snapshot.CandidateChestCount,
                snapshot.ScannedChestCount,
                snapshot.HitCount,
                hits.Count,
                age,
                hits);
        }

        private static bool TryResolveCurrentHitTileType(InformationWorldContext context, ChestItemLocatorHit hit, out int tileType)
        {
            tileType = InformationChestTileScanner.TileTypeContainers;
            return hit != null &&
                   InformationChestTileScanner.TryResolveTileInfoAt(context, hit.ChestX, hit.ChestY, out tileType, out _);
        }

        private static ChestItemLocatorOverlayView BuildSnapshotSkipView(ChestItemLocatorSnapshot snapshot, string skipReason)
        {
            return BuildSnapshotSkipView(snapshot, skipReason, 0);
        }

        private static ChestItemLocatorOverlayView BuildSnapshotSkipView(ChestItemLocatorSnapshot snapshot, string skipReason, ulong age)
        {
            return new ChestItemLocatorOverlayView(
                false,
                skipReason,
                snapshot == null ? 0 : snapshot.QueryVersion,
                snapshot == null ? string.Empty : snapshot.Status,
                snapshot == null ? 0 : snapshot.CandidateChestCount,
                snapshot == null ? 0 : snapshot.ScannedChestCount,
                snapshot == null ? 0 : snapshot.HitCount,
                0,
                age,
                new List<ChestItemLocatorOverlayHitView>());
        }

        private static void UpdateDiagnostics(ChestItemLocatorOverlayView view, double elapsedMs, int drawn)
        {
            if (view == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _diagnostics = new ChestItemLocatorOverlayDiagnostics
                {
                    Enabled = view.Enabled,
                    QueryVersion = view.QueryVersion,
                    SnapshotStatus = view.SnapshotStatus ?? string.Empty,
                    CandidateChestCount = view.CandidateChestCount,
                    ScannedChestCount = view.ScannedChestCount,
                    HitCount = view.HitCount,
                    DrawnHitCount = Math.Max(0, drawn),
                    SkipReason = view.SkipReason ?? string.Empty,
                    RecentElapsedBucket = BuildElapsedBucket(elapsedMs),
                    SnapshotAgeTicks = ToDiagnosticLong(view.SnapshotAgeTicks)
                };
            }
        }

        private static bool DrawHit(object spriteBatch, InformationWorldContext context, ChestItemLocatorOverlayHitView hit)
        {
            if (hit == null)
            {
                return false;
            }

            var pulse = 170 + (int)(Math.Abs(Math.Sin((context == null ? 0 : context.GameUpdateCount) / 10d)) * 65d);
            var x = hit.ScreenX;
            var y = hit.ScreenY;
            var width = hit.PixelWidth;
            var height = hit.PixelHeight;
            var clipWidth = context == null ? 0 : Math.Max(1, context.ScreenWidth);
            var clipHeight = context == null ? 0 : Math.Max(1, context.ScreenHeight);

            var ok = UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, width, height, 0, 0, clipWidth, clipHeight, 40, 220, 110, 38);
            ok |= UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x - 3, y - 3, width + 6, height + 6, 2, 0, 0, clipWidth, clipHeight, 72, 248, 128, pulse);
            ok |= UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x - 6, y - 6, width + 12, height + 12, 1, 0, 0, clipWidth, clipHeight, 255, 255, 255, 110);
            ok |= DrawLabel(spriteBatch, context, hit);
            return ok;
        }

        private static bool DrawLabel(object spriteBatch, InformationWorldContext context, ChestItemLocatorOverlayHitView hit)
        {
            if (context == null || hit == null || string.IsNullOrWhiteSpace(hit.Label))
            {
                return false;
            }

            var screenWidth = Math.Max(1, context.ScreenWidth);
            var screenHeight = Math.Max(1, context.ScreenHeight);
            var labelWidth = Math.Min(screenWidth, Math.Max(hit.PixelWidth + 28, 96));
            var labelHeight = 18;
            var labelX = hit.ScreenX + (hit.PixelWidth / 2) - (labelWidth / 2);
            labelX = Math.Max(0, Math.Min(labelX, Math.Max(0, screenWidth - labelWidth)));
            var labelY = hit.ScreenY >= labelHeight + 8
                ? hit.ScreenY - labelHeight - 6
                : hit.ScreenY + hit.PixelHeight + 6;
            labelY = Math.Max(0, Math.Min(labelY, Math.Max(0, screenHeight - labelHeight)));

            var ok = UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, labelX, labelY, labelWidth, labelHeight, 0, 0, screenWidth, screenHeight, 12, 20, 18, 170);
            ok |= UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, labelX, labelY, labelWidth, labelHeight, 1, 0, 0, screenWidth, screenHeight, 72, 248, 128, 160);
            ok |= UiTextRenderer.DrawAlignedTextClipped(spriteBatch, hit.Label, labelX + 3, labelY + 1, labelWidth - 6, labelHeight - 2, UiTextHorizontalAlignment.Center, 0, 0, screenWidth, screenHeight, 220, 255, 230, 235, 0.78f);
            return ok;
        }

        private static string BuildLabel(ChestItemLocatorHit hit)
        {
            if (hit == null || string.IsNullOrWhiteSpace(hit.ContainerName))
            {
                return string.Empty;
            }

            return hit.ContainerName.Trim() + " x" + Math.Max(0, hit.TotalStack).ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsVisible(InformationWorldContext context, int x, int y, int width, int height)
        {
            if (context == null)
            {
                return false;
            }

            return x + width >= -ScreenCullPadding &&
                   y + height >= -ScreenCullPadding &&
                   x <= context.ScreenWidth + ScreenCullPadding &&
                   y <= context.ScreenHeight + ScreenCullPadding;
        }

        private static bool WorldMatches(ChestItemLocatorSnapshot snapshot, InformationWorldContext context)
        {
            if (snapshot == null || context == null)
            {
                return false;
            }

            var snapshotRecord = snapshot.WorldRecordKey ?? string.Empty;
            var contextRecord = context.WorldRecordKey ?? string.Empty;
            if (!string.IsNullOrEmpty(snapshotRecord) || !string.IsNullOrEmpty(contextRecord))
            {
                return string.Equals(snapshotRecord, contextRecord, StringComparison.Ordinal);
            }

            return string.Equals(snapshot.WorldKey ?? string.Empty, context.WorldKey ?? string.Empty, StringComparison.Ordinal);
        }

        private static string ResolveSnapshotSkipReason(ChestItemLocatorSnapshot snapshot)
        {
            if (snapshot == null || object.ReferenceEquals(snapshot, ChestItemLocatorSnapshot.Empty))
            {
                return "noSnapshot";
            }

            if (!string.Equals(snapshot.Status, ChestItemLocatorSnapshot.StatusOk, StringComparison.Ordinal))
            {
                return "snapshotStatus:" + (snapshot.Status ?? string.Empty);
            }

            return snapshot.HitCount <= 0 ? "noHits" : string.Empty;
        }

        private static string NormalizeSkip(string prefix, string detail)
        {
            return string.IsNullOrWhiteSpace(detail) ? prefix : prefix + ":" + detail;
        }

        private static string BuildElapsedBucket(double elapsedMs)
        {
            if (elapsedMs <= 0d)
            {
                return "0ms";
            }

            if (elapsedMs < 1d)
            {
                return "<1ms";
            }

            if (elapsedMs < 2d)
            {
                return "<2ms";
            }

            if (elapsedMs < 5d)
            {
                return "<5ms";
            }

            return ">=5ms";
        }

        private static long ToDiagnosticLong(ulong value)
        {
            return value > long.MaxValue ? long.MaxValue : (long)value;
        }
    }
}
