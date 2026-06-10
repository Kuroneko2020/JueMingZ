using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.Records;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void CombatEquipmentWarningMatchesRequestedEquipmentNames()
        {
            if (!CombatEquipmentWarningService.IsCombatHazardForTesting(true, false) ||
                !CombatEquipmentWarningService.IsCombatHazardForTesting(false, true) ||
                CombatEquipmentWarningService.IsCombatHazardForTesting(false, false))
            {
                throw new InvalidOperationException("Expected equipment warning hazard gate to accept bosses or non-blood-moon events only.");
            }

            if (!CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("建筑师发明背包") ||
                !CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("R.E.K. 3000") ||
                !CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("Hand of Creation") ||
                !CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("FPV飞行眼镜") ||
                !CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("Music Box (Overworld Day)") ||
                CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("Warrior Emblem"))
            {
                throw new InvalidOperationException("Expected requested equipment names to match and combat accessory names not to match.");
            }
        }

        private static void CombatEquipmentWarningPromptsOnlyOnHazardEntry()
        {
            if (Math.Abs(CombatEquipmentWarningService.PromptDurationSecondsForTesting - 2d) > 0.001d)
            {
                throw new InvalidOperationException("Expected combat equipment warning prompt to stay fully readable for 2 seconds.");
            }

            if (CombatEquipmentWarningService.PromptFadeDurationSecondsForTesting <= 0d ||
                CombatEquipmentWarningService.PromptTotalDurationSecondsForTesting <= 2d)
            {
                throw new InvalidOperationException("Expected combat equipment warning prompt to use a short fade after the 2 second readable window.");
            }

            if (Math.Abs(CombatEquipmentWarningService.CalculatePromptAlphaForTesting(0d) - 1d) > 0.001d ||
                Math.Abs(CombatEquipmentWarningService.CalculatePromptAlphaForTesting(1.99d) - 1d) > 0.001d ||
                CombatEquipmentWarningService.CalculatePromptAlphaForTesting(2.125d) >= 1d ||
                CombatEquipmentWarningService.CalculatePromptAlphaForTesting(2.3d) > 0.001d)
            {
                throw new InvalidOperationException("Expected combat equipment warning prompt alpha to remain full for 2 seconds, then fade out.");
            }

            var yAtStart = CombatEquipmentWarningPromptOverlay.CalculatePromptDrawYForTesting(100f, 20f, 0d);
            var yAtEnd = CombatEquipmentWarningPromptOverlay.CalculatePromptDrawYForTesting(100f, 20f, 1d);
            if (Math.Abs(yAtStart - yAtEnd) > 0.001f)
            {
                throw new InvalidOperationException("Expected combat equipment warning prompt to avoid vertical progress animation.");
            }

            if (CombatEquipmentWarningService.IsNonBloodMoonEventForTesting(0, false, false, false, false, false, true, false, false, false, false, false))
            {
                throw new InvalidOperationException("Expected DD2 ready-to-find-bartender state not to count as an active event.");
            }

            if (CombatEquipmentWarningService.IsNonBloodMoonEventForTesting(0, false, false, false, false, false, false, false, false, false, false, false))
            {
                throw new InvalidOperationException("Expected no active event when all event flags are false.");
            }

            if (!CombatEquipmentWarningService.IsNonBloodMoonEventForTesting(1, false, false, false, false, false, false, false, false, false, false, false) ||
                !CombatEquipmentWarningService.IsNonBloodMoonEventForTesting(0, true, false, false, false, false, false, false, false, false, false, false) ||
                !CombatEquipmentWarningService.IsNonBloodMoonEventForTesting(0, false, false, false, false, true, false, false, false, false, false, false))
            {
                throw new InvalidOperationException("Expected invasion, pumpkin moon, and ongoing DD2 to count as active events.");
            }

            if (!CombatEquipmentWarningService.ShouldPromptForHazardEntryForTesting(string.Empty, "boss:134", true))
            {
                throw new InvalidOperationException("Expected first boss hazard entry with non-combat equipment to prompt.");
            }

            if (CombatEquipmentWarningService.ShouldPromptForHazardEntryForTesting("boss:134", "boss:134", true))
            {
                throw new InvalidOperationException("Expected continuous same boss hazard not to prompt repeatedly.");
            }

            if (CombatEquipmentWarningService.ShouldPromptForHazardEntryForTesting(string.Empty, "event:eclipse", false))
            {
                throw new InvalidOperationException("Expected hazard entry without non-combat equipment not to prompt.");
            }
        }

        private static void CombatPerformanceCachesStableMetadataOnly()
        {
            if (TerrariaTypeCache.Find("System.String") != typeof(string) ||
                TerrariaTypeCache.Find("System.String, mscorlib") != typeof(string))
            {
                throw new InvalidOperationException("Expected shared Terraria type cache to resolve and reuse clean type names.");
            }

            if (CombatEquipmentWarningService.HazardScanIntervalTicksForTesting <= 0)
            {
                throw new InvalidOperationException("Expected combat equipment warning hazard scan to be throttled by a positive tick interval.");
            }

            if (!CombatEquipmentWarningService.ShouldRunHazardScanForTesting(120, 0) ||
                CombatEquipmentWarningService.ShouldRunHazardScanForTesting(125, 132) ||
                !CombatEquipmentWarningService.ShouldRunHazardScanForTesting(132, 132))
            {
                throw new InvalidOperationException("Expected hazard scan throttle to skip only intermediate ticks and run at the next due tick.");
            }
        }

        private static void RuntimePerformanceDiagnosticsRecordsSlowestOperation()
        {
            RuntimePerformanceDiagnostics.ResetForTesting();
            RuntimePerformanceDiagnostics.Record(8d, 9d, 1d, 2d, 3d, 6d, "stage-a", 4d, "dispatch.service-a", 5d);
            if (!string.Equals(RuntimePerformanceDiagnostics.LastSlowestOperationName, "dispatch.service-a", StringComparison.Ordinal) ||
                Math.Abs(RuntimePerformanceDiagnostics.LastSlowestOperationElapsedMs - 5d) > 0.001d ||
                Math.Abs(RuntimePerformanceDiagnostics.LastInformationDrawMs - 6d) > 0.001d)
            {
                throw new InvalidOperationException("Expected runtime performance diagnostics to keep the slowest sub-operation.");
            }

            var sample = new PerformanceHitchSample
            {
                UtcNow = DateTime.UtcNow,
                RuntimeUpdateMs = PerformanceHitchRecorder.RuntimeUpdateThresholdMs,
                SlowestStageName = "automation-request-dispatch",
                SlowestStageElapsedMs = 26d,
                SlowestOperationName = "dispatch.combat-equipment-warning",
                SlowestOperationElapsedMs = 12d
            };

            var reason = PerformanceHitchRecorder.BuildReason(sample);
            RuntimePerformanceDiagnostics.RecordHitch(sample, reason, string.Empty);
            if (!string.Equals(RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestOperationName, "dispatch.combat-equipment-warning", StringComparison.Ordinal) ||
                Math.Abs(RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestOperationMs - 12d) > 0.001d)
            {
                throw new InvalidOperationException("Expected hitch diagnostics to preserve the slowest sub-operation.");
            }

            RuntimePerformanceDiagnostics.ResetForTesting();
            var capacity = RuntimePerformanceDiagnostics.RecentWindowCapacitySamples;
            for (var index = 0; index < capacity; index++)
            {
                RuntimePerformanceDiagnostics.Record(1d, 0d, 1d, 1d, 1d, 1d, "stage-a", 1d, "dispatch.service-a", 1d);
            }

            RuntimePerformanceDiagnostics.Record(601d, 0d, 601d, 601d, 601d, 601d, "stage-b", 601d, "dispatch.service-b", 601d);
            if (RuntimePerformanceDiagnostics.RecentWindowSampleCount != capacity ||
                Math.Abs(RuntimePerformanceDiagnostics.RecentRuntimeUpdateAverageMs - 2d) > 0.001d ||
                Math.Abs(RuntimePerformanceDiagnostics.RecentGameStateReadAverageMs - 2d) > 0.001d ||
                Math.Abs(RuntimePerformanceDiagnostics.RecentActionQueueUpdateAverageMs - 2d) > 0.001d ||
                Math.Abs(RuntimePerformanceDiagnostics.RecentInputActionUpdateAverageMs - 2d) > 0.001d ||
                Math.Abs(RuntimePerformanceDiagnostics.RecentInformationDrawAverageMs - 2d) > 0.001d)
            {
                throw new InvalidOperationException("Expected runtime performance diagnostics to keep a fixed-size recent average window.");
            }
        }

        private static void InformationOverlayDiagnosticsWriterPreservesSectionCounts()
        {
            InformationOverlayDiagnosticsWriter.ResetForTesting();

            InformationOverlayDiagnosticsWriter.UpdateWorldOverlay(1, 2, 3, 4, 5, 6.5d, "worldOverlayProbe");
            var world = InformationOverlayService.GetDiagnostics();
            if (world.NpcLabelsDrawn != 1 ||
                world.ChestLabelsDrawn != 2 ||
                world.SignTextLabelsDrawn != 3 ||
                world.TombstoneTextLabelsDrawn != 4 ||
                world.TileHighlightsDrawn != 5 ||
                world.StatusLinesDrawn != 0 ||
                Math.Abs(world.LastDrawElapsedMs - 6.5d) > 0.001d ||
                !string.Equals(world.LastSkipReason, "worldOverlayProbe", StringComparison.Ordinal) ||
                Math.Abs(InformationOverlayService.GetLastDrawElapsedMs() - 6.5d) > 0.001d)
            {
                throw new InvalidOperationException("Expected information diagnostics writer to publish world overlay counts and last draw sample.");
            }

            InformationOverlayDiagnosticsWriter.UpdateStatusPanel(7, 8.25d, "statusPanelProbe");
            var status = InformationOverlayService.GetDiagnostics();
            if (status.NpcLabelsDrawn != 1 ||
                status.ChestLabelsDrawn != 2 ||
                status.SignTextLabelsDrawn != 3 ||
                status.TombstoneTextLabelsDrawn != 4 ||
                status.TileHighlightsDrawn != 5 ||
                status.StatusLinesDrawn != 7 ||
                Math.Abs(status.LastDrawElapsedMs - 8.25d) > 0.001d ||
                !string.Equals(status.LastSkipReason, "statusPanelProbe", StringComparison.Ordinal) ||
                Math.Abs(InformationOverlayService.GetLastDrawElapsedMs() - 8.25d) > 0.001d)
            {
                throw new InvalidOperationException("Expected status diagnostics update to preserve world counts and publish the latest draw sample.");
            }

            InformationOverlayDiagnosticsWriter.ResetForTesting();
        }

        private static void FishingFilterNestedScrollBubblesWhenListCannotMove()
        {
            FishingFilterUiState.Reset();
            var mouse = new LegacyMouseSnapshot
            {
                X = 20,
                Y = 20,
                ScrollDelta = -120
            };

            FishingFilterUiState.SetEntryViewport(new LegacyUiRect(10, 10, 100, 100), 80);
            if (FishingFilterUiState.TryConsumeNestedScroll(mouse, -120))
            {
                throw new InvalidOperationException("Expected a non-scrollable entry viewport to bubble to the main window.");
            }

            FishingFilterUiState.SetEntryViewport(new LegacyUiRect(10, 10, 100, 100), 240);
            if (!FishingFilterUiState.TryConsumeNestedScroll(mouse, -120))
            {
                throw new InvalidOperationException("Expected a scrollable entry viewport to consume downward wheel.");
            }

            if (FishingFilterUiState.EntryScrollOffset <= 0)
            {
                throw new InvalidOperationException("Expected entry scroll offset to increase.");
            }

            mouse.ScrollDelta = 120;
            if (!FishingFilterUiState.TryConsumeNestedScroll(mouse, 120))
            {
                throw new InvalidOperationException("Expected a scrollable entry viewport to consume upward wheel before reaching the top.");
            }

            if (FishingFilterUiState.EntryScrollOffset != 0)
            {
                throw new InvalidOperationException("Expected entry scroll offset to return to the top.");
            }

            if (FishingFilterUiState.TryConsumeNestedScroll(mouse, 120))
            {
                throw new InvalidOperationException("Expected wheel at the top edge to bubble to the main window.");
            }

            FishingFilterUiState.Reset();
        }

        private static void LegacyUiWindowCaptureAcceptsScaledScreenCoordinates()
        {
            LegacyMainUiState.EnsureLoaded();
            var window = LegacyMainUiState.WindowRect;
            Terraria.Main.screenWidth = 1536;
            Terraria.Main.screenHeight = 864;
            var effectiveScale = (864d - LegacyUiMetrics.VisualScreenMargin) / LegacyUiMetrics.DefaultHeight;
            var raw = new DiagnosticMouseState
            {
                UiScaleAvailable = true,
                UiScaleMatrixAvailable = true,
                UiScale = 1.25d,
                UiScaleX = 1.25d,
                UiScaleY = 1.25d,
                OsReadAvailable = true,
                OsClientMouseX = (int)Math.Round((window.X + 20) * effectiveScale),
                OsClientMouseY = (int)Math.Round((window.Y + 20) * effectiveScale)
            };

            if (!LegacyUiInput.IsMouseInWindowForDiagnostics(raw))
            {
                throw new InvalidOperationException("Expected screen-fit capped 125% OS coordinates over the visual F5 window to count as captured.");
            }

            raw.OsClientMouseX = (int)((window.X + 20) * 1.25d);
            raw.OsClientMouseY = (int)((window.Bottom - 10) * 1.25d);
            if (LegacyUiInput.IsMouseInWindowForDiagnostics(raw))
            {
                throw new InvalidOperationException("Expected old uncapped 125% bottom coordinates outside the capped visual F5 window not to count as captured.");
            }

            Terraria.Main.screenWidth = 2560;
            Terraria.Main.screenHeight = 1440;
            raw.UiScale = 1.3d;
            raw.UiScaleX = 1.3d;
            raw.UiScaleY = 1.3d;
            raw.OsClientMouseX = (int)Math.Round((window.X + 20) * 1.3d);
            raw.OsClientMouseY = (int)Math.Round((window.Y + 20) * 1.3d);
            if (!LegacyUiInput.IsMouseInWindowForDiagnostics(raw))
            {
                throw new InvalidOperationException("Expected 2560x1440 at 130% OS coordinates to keep following Terraria UI scale.");
            }

            raw.UiScale = 0.8d;
            raw.UiScaleX = 0.8d;
            raw.UiScaleY = 0.8d;
            raw.OsClientMouseX = (int)((window.X + 20) * 0.8d);
            raw.OsClientMouseY = (int)((window.Y + 20) * 0.8d);
            if (!LegacyUiInput.IsMouseInWindowForDiagnostics(raw))
            {
                throw new InvalidOperationException("Expected sub-100% OS coordinates to keep following Terraria UI scale.");
            }
        }

    }
}
