using System;
using System.Diagnostics;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.UI;
using JueMingZ.UI.Information;

namespace JueMingZ.Automation.Information
{
    public static partial class InformationOverlayService
    {
        private static readonly InformationWorldLabelRenderer LabelRenderer = new InformationWorldLabelRenderer();

        public static InformationOverlayDiagnostics GetDiagnostics()
        {
            return InformationOverlayDiagnosticsWriter.GetSnapshot();
        }

        public static double GetLastDrawElapsedMs()
        {
            return InformationOverlayDiagnosticsWriter.GetLastDrawElapsedMs();
        }

        public static bool ShouldDrawWorldOverlay()
        {
            return HasWorldOverlayEnabled(ConfigService.AppSettings ?? AppSettings.CreateDefault());
        }

        public static bool ShouldDrawStatusPanel()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            return HasStatusPanelEnabled(settings) || InformationStatusPanelService.IsAdjusting;
        }

        public static void DrawWorldOverlay(object spriteBatch)
        {
            var stopwatch = Stopwatch.StartNew();
            var npcLabels = 0;
            var chestLabels = 0;
            var signTextLabels = 0;
            var tombstoneTextLabels = 0;
            var tileHighlights = 0;
            var skip = string.Empty;

            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!HasWorldOverlayEnabled(settings))
                {
                    skip = "worldOverlayDisabled";
                    return;
                }

                InformationWorldContext context;
                if (!TryBuildContext(BuildWorldOverlayContextProfile(settings), out context, out skip))
                {
                    return;
                }

                // Use one read-only context for this overlay pass. New scanners
                // must not submit actions or duplicate context construction here.
                InformationChestRecordService.ImportLegacyKnownChests(context, settings);
                InformationChestRecordService.RecordOpenChest(context);
                npcLabels = DrawNpcLabels(spriteBatch, context, settings);
                chestLabels = DrawChestLabels(spriteBatch, context, settings);
                signTextLabels = DrawSignTextLabels(spriteBatch, context, settings);
                tombstoneTextLabels = DrawTombstoneTextLabels(spriteBatch, context, settings);
                tileHighlights = DrawTileHighlights(spriteBatch, context, settings);
            }
            catch (Exception error)
            {
                skip = "worldOverlayException";
                LogThrottle.ErrorThrottled(
                    "information-world-overlay-service-error",
                    TimeSpan.FromSeconds(10),
                    "InformationOverlayService",
                    "Information world overlay failed; exception swallowed.", error);
            }
            finally
            {
                stopwatch.Stop();
                InformationOverlayDiagnosticsWriter.UpdateWorldOverlay(
                    npcLabels,
                    chestLabels,
                    signTextLabels,
                    tombstoneTextLabels,
                    tileHighlights,
                    stopwatch.Elapsed.TotalMilliseconds,
                    skip);
            }
        }

        public static void DrawStatusPanel(object spriteBatch)
        {
            var stopwatch = Stopwatch.StartNew();
            var statusLines = 0;
            var skip = string.Empty;

            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!HasStatusPanelEnabled(settings) && !InformationStatusPanelService.IsAdjusting)
                {
                    skip = "statusPanelDisabled";
                    return;
                }

                InformationWorldContext context;
                if (!TryBuildContext(BuildStatusContextProfile(settings), out context, out skip))
                {
                    return;
                }

                // Status lines are a read-only model bridge; fishing catch lines
                // must not enqueue filter, auto-fish, or inventory actions.
                var lines = InformationStatusLineService.GetLines(context, settings);
                statusLines = InformationStatusPanelService.DrawPanel(spriteBatch, context, lines);
            }
            catch (Exception error)
            {
                skip = "statusPanelException";
                LogThrottle.ErrorThrottled(
                    "information-status-panel-service-error",
                    TimeSpan.FromSeconds(10),
                    "InformationOverlayService",
                    "Information status panel failed; exception swallowed.", error);
            }
            finally
            {
                stopwatch.Stop();
                InformationOverlayDiagnosticsWriter.UpdateStatusPanel(statusLines, stopwatch.Elapsed.TotalMilliseconds, skip);
            }
        }

        private static bool TryBuildContext(out InformationWorldContext context, out string skipReason)
        {
            return InformationWorldContextProvider.TryBuild(out context, out skipReason);
        }

        private static bool TryBuildContext(InformationWorldContextProfile profile, out InformationWorldContext context, out string skipReason)
        {
            return InformationWorldContextProvider.TryBuild(profile, out context, out skipReason);
        }

        private static int DrawNpcLabels(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            if (!settings.InformationEnemyNameLabelsEnabled &&
                !settings.InformationCritterNameLabelsEnabled &&
                string.Equals(NormalizeNpcMode(settings.InformationNpcNameLabelsMode), "Off", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var labels = InformationNpcLabelService.GetLabels(context, settings);
            var drawn = 0;
            for (var index = 0; index < labels.Length && drawn < InformationNpcLabelService.MaxLabelsPerFrame; index++)
            {
                var label = labels[index];
                var labelDrawn = string.IsNullOrWhiteSpace(label.HealthText)
                    ? LabelRenderer.DrawWorldLabel(spriteBatch, context, label.WorldX, label.WorldY, label.Text, label.Color, label.MaxDistance, false, -1f, label.FontScale)
                    : LabelRenderer.DrawWorldLabelWithSubLabel(spriteBatch, context, label.WorldX, label.WorldY, label.Text, label.HealthText, label.Color, label.MaxDistance, false, -1f, label.FontScale, label.HealthFontScale);
                if (labelDrawn)
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static int DrawChestLabels(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            var mode = InformationChestLabelService.NormalizeMode(settings);
            if (string.Equals(mode, InformationChestLabelService.ModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(mode, InformationChestLabelService.ModeAlways, StringComparison.OrdinalIgnoreCase) &&
                !InformationPlayerDetectionService.HasMetalDetector(context.LocalPlayer))
            {
                return 0;
            }

            var labels = InformationChestLabelService.GetLabelsForDrawing(context, settings, mode);
            var drawn = 0;
            var color = InformationColorHelper.ChestName(settings);
            var fontScale = InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.ChestNameFeatureId);
            for (var index = 0; index < labels.Length && drawn < InformationChestLabelService.MaxLabelsPerFrame; index++)
            {
                var label = labels[index];
                if (LabelRenderer.DrawWorldLabel(spriteBatch, context, label.WorldX, label.WorldY, label.Name, color, InformationChestLabelService.LabelMaxDistance, false, 0f, (float)fontScale))
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static int DrawSignTextLabels(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            var mode = NormalizeSignTextMode(settings);
            if (string.Equals(mode, InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var labels = InformationSignTextLabelService.GetSignLabels(context);
            var drawn = 0;
            var color = InformationColorHelper.SignText(settings);
            var fontScale = (float)InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.SignTextFeatureId);
            for (var index = 0; index < labels.Length && drawn < InformationSignTextLabelService.MaxSignLabelsPerFrame; index++)
            {
                var label = labels[index];
                var layout = InformationSignTextLayoutCache.GetOrBuild(
                    label.Text,
                    label.TextHash,
                    mode,
                    settings.InformationSignTextMaxLines,
                    settings.InformationSignTextMaxCharacters,
                    fontScale);
                if (layout == null)
                {
                    continue;
                }

                if (DrawSignTextBlock(spriteBatch, context, label, layout, color))
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static int DrawTombstoneTextLabels(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            var mode = NormalizeTombstoneTextMode(settings);
            if (string.Equals(mode, InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var labels = InformationSignTextLabelService.GetTombstoneLabels(context);
            var drawn = 0;
            var color = InformationColorHelper.TombstoneText(settings);
            var fontScale = (float)InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.TombstoneTextFeatureId);
            for (var index = 0; index < labels.Length && drawn < InformationSignTextLabelService.MaxTombstoneLabelsPerFrame; index++)
            {
                var label = labels[index];
                var layout = InformationSignTextLayoutCache.GetOrBuild(
                    label.Text,
                    label.TextHash,
                    mode,
                    settings.InformationTombstoneTextMaxLines,
                    settings.InformationTombstoneTextMaxCharacters,
                    fontScale);
                if (layout == null)
                {
                    continue;
                }

                if (DrawSignTextBlock(spriteBatch, context, label, layout, color))
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static bool DrawSignTextBlock(object spriteBatch, InformationWorldContext context, SignTextLabel label, SignTextLayout layout, InformationColor color)
        {
            if (spriteBatch == null ||
                context == null ||
                label == null ||
                layout == null ||
                layout.DisplayLines.Length <= 0 ||
                !layout.HasVisibleText ||
                !LabelRenderer.CanDraw(context, label.WorldLeft, label.WorldTop, InformationSignTextLabelService.LabelMaxDistance, false))
            {
                return false;
            }

            var signCenterX = ((label.WorldLeft + label.WorldRight) * 0.5f) - context.ScreenX;
            var signTop = label.WorldTop - context.ScreenY;
            var ok = false;
            for (var index = 0; index < layout.DisplayLines.Length; index++)
            {
                var lineWidth = index < layout.LineWidths.Length ? layout.LineWidths[index] : 0;
                var drawX = InformationSignTextLayoutCache.CalculateLineX(signCenterX, lineWidth, context.ScreenWidth);
                ok |= UiTextRenderer.DrawText(spriteBatch, layout.DisplayLines[index], drawX, signTop + index * layout.LineHeight, color.R, color.G, color.B, color.A, layout.Scale);
            }

            return ok;
        }

        private static int DrawTileHighlights(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            return InformationTileHighlightService.Draw(spriteBatch, context, settings);
        }

        private static bool HasWorldOverlayEnabled(AppSettings settings)
        {
            return settings != null &&
                   (settings.InformationEnemyNameLabelsEnabled ||
                    settings.InformationCritterNameLabelsEnabled ||
                    !string.Equals(NormalizeNpcMode(settings.InformationNpcNameLabelsMode), "Off", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(InformationChestLabelService.NormalizeMode(settings), InformationChestLabelService.ModeOff, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(NormalizeSignTextMode(settings), InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(NormalizeTombstoneTextMode(settings), InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase) ||
                    settings.InformationHighlightLifeCrystalEnabled ||
                    settings.InformationHighlightManaCrystalEnabled ||
                    settings.InformationHighlightDigtoiseEnabled ||
                    settings.InformationHighlightLifeFruitEnabled ||
                    settings.InformationHighlightDragonEggEnabled);
        }

        private static bool HasStatusPanelEnabled(AppSettings settings)
        {
            return settings != null &&
                   (settings.InformationBiomeDisplayEnabled ||
                    settings.InformationWorldInfectionEnabled ||
                    settings.InformationLuckValueEnabled ||
                    settings.InformationFishingCatchesEnabled ||
                    settings.InformationFishingFilteredCatchesEnabled ||
                    settings.InformationAnglerQuestEnabled);
        }

        private static InformationWorldContextProfile BuildStatusContextProfile(AppSettings settings)
        {
            return InformationWorldContextProfile.Status;
        }

        private static InformationWorldContextProfile BuildWorldOverlayContextProfile(AppSettings settings)
        {
            return InformationWorldContextProfile.FullRecord;
        }

        private static string NormalizeSignTextMode(AppSettings settings)
        {
            return InformationSignTextModes.Normalize(settings == null ? null : settings.InformationSignTextLabelsMode);
        }

        private static string NormalizeTombstoneTextMode(AppSettings settings)
        {
            return InformationSignTextModes.Normalize(settings == null ? null : settings.InformationTombstoneTextLabelsMode);
        }

        private static string NormalizeNpcMode(string mode)
        {
            if (string.Equals(mode, "Name", StringComparison.OrdinalIgnoreCase))
            {
                return "Name";
            }

            return string.Equals(mode, "Type", StringComparison.OrdinalIgnoreCase) ? "Type" : "Off";
        }

    }
}
