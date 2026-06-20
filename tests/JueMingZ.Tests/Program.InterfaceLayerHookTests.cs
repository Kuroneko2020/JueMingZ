using System;
using System.Collections.Generic;
using JueMingZ.Hooks;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void InformationUnderVanillaUiAnchorPrefersMapMinimap()
        {
            var layerNames = new List<string>
            {
                "Vanilla: Background",
                "Vanilla: Inventory",
                "Vanilla: Map / Minimap",
                "Vanilla: Resource Bars",
                "Vanilla: Mouse Text"
            };

            var index = InterfaceLayerHookCallbacks.FindInformationUnderVanillaUiInsertIndexForTesting(layerNames);
            AssertIntEquals(index, 2, "information under UI anchor");
        }

        private static void InformationUnderVanillaUiAnchorFallsBackThroughVanillaUiLayers()
        {
            AssertIntEquals(
                InterfaceLayerHookCallbacks.FindInformationUnderVanillaUiInsertIndexForTesting(new List<string>
                {
                    "Vanilla: Background",
                    "Vanilla: Resource Bars",
                    "Vanilla: Inventory",
                    "Vanilla: Mouse Text"
                }),
                1,
                "resource bars fallback");

            AssertIntEquals(
                InterfaceLayerHookCallbacks.FindInformationUnderVanillaUiInsertIndexForTesting(new List<string>
                {
                    "Vanilla: Background",
                    "Vanilla: Inventory",
                    "Vanilla: Mouse Text"
                }),
                1,
                "inventory fallback");

            AssertIntEquals(
                InterfaceLayerHookCallbacks.FindInformationUnderVanillaUiInsertIndexForTesting(new List<string>
                {
                    "Vanilla: Background",
                    "Vanilla: Mouse Text"
                }),
                1,
                "mouse text fallback");
        }

        private static void InformationUnderVanillaUiInsertionKeepsDispatchersBeforeAnchor()
        {
            var insertIndices = InterfaceLayerHookCallbacks.SimulateInformationUnderVanillaUiInsertIndicesForTesting(new List<string>
            {
                "Vanilla: Background",
                "Vanilla: Map / Minimap",
                "Vanilla: Resource Bars",
                "Vanilla: Mouse Text"
            });

            if (insertIndices.Length != 2)
            {
                throw new InvalidOperationException("Expected two information under UI insert indices.");
            }

            AssertIntEquals(insertIndices[0], 1, "world dispatcher insert index");
            AssertIntEquals(insertIndices[1], 2, "status panel dispatcher insert index");
        }

        private static void InformationUnderVanillaUiAnchorMissingIsSafe()
        {
            var insertIndices = InterfaceLayerHookCallbacks.SimulateInformationUnderVanillaUiInsertIndicesForTesting(new List<string>
            {
                "Vanilla: Background",
                "Vanilla: Unknown Layer"
            });

            AssertIntEquals(insertIndices[0], -1, "missing world dispatcher insert index");
            AssertIntEquals(insertIndices[1], -1, "missing status panel dispatcher insert index");
        }

        private static void LegacyFinalMouseTextGuardRunsAfterMouseOver()
        {
            AssertIntEquals(
                InterfaceLayerHookCallbacks.FindFinalMouseTextGuardInsertIndexForTesting(new List<string>
                {
                    "Vanilla: Mouse Text",
                    "Vanilla: Player Chat",
                    "Vanilla: Mouse Item / NPC Head",
                    "Vanilla: Mouse Over",
                    "Vanilla: Interact Item Icon",
                    "Vanilla: Interface Logic 4"
                }),
                4,
                "final mouse text guard before interact item icon");

            AssertIntEquals(
                InterfaceLayerHookCallbacks.FindFinalMouseTextGuardInsertIndexForTesting(new List<string>
                {
                    "Vanilla: Mouse Text",
                    "Vanilla: Mouse Item / NPC Head",
                    "Vanilla: Mouse Over",
                    "Vanilla: Interface Logic 4"
                }),
                3,
                "final mouse text guard after mouse over fallback");
        }

        private static void InformationWorldOverlayRoutesInformationUnderVanillaUiAndAutoMiningAbove()
        {
            AssertStringArrayEquals(
                InterfaceLayerHookCallbacks.GetInformationWorldUnderVanillaUiDispatcherRouteNamesForTesting(),
                new[]
                {
                    "InformationWorldOverlay.DrawInformationInterfaceLayer",
                    "SearchChestLocatorWorldOverlay.DrawInterfaceLayer"
                },
                "information world under UI dispatcher routes");

            AssertStringArrayEquals(
                InterfaceLayerHookCallbacks.GetGameOverlayDispatcherRouteNamesForTesting(true),
                new[]
                {
                    "InformationWorldOverlay.DrawAutoMiningInterfaceLayer",
                    "FishingStatusPromptOverlay.DrawInterfaceLayer",
                    "FirstWorldLoadPromptOverlay.DrawInterfaceLayer",
                    "CombatEquipmentWarningPromptOverlay.DrawInterfaceLayer",
                    "CombatAimMarkerOverlay.DrawInterfaceLayer",
                    "MapDirectionHintOverlay.DrawInterfaceLayer",
                    "BlueprintProjectionOverlay.DrawInterfaceLayer",
                    "BlueprintCreationOverlay.DrawInterfaceLayer",
                    "BlueprintPlacementPreviewOverlay.DrawInterfaceLayer",
                    "BlueprintEraseRegionOverlay.DrawInterfaceLayer"
                },
                "game overlay dispatcher routes when low information layer is active");

            AssertStringArrayEquals(
                InterfaceLayerHookCallbacks.GetGameOverlayDispatcherRouteNamesForTesting(false),
                new[]
                {
                    "InformationWorldOverlay.DrawInformationInterfaceLayer",
                    "SearchChestLocatorWorldOverlay.DrawInterfaceLayer",
                    "InformationWorldOverlay.DrawAutoMiningInterfaceLayer",
                    "FishingStatusPromptOverlay.DrawInterfaceLayer",
                    "FirstWorldLoadPromptOverlay.DrawInterfaceLayer",
                    "CombatEquipmentWarningPromptOverlay.DrawInterfaceLayer",
                    "CombatAimMarkerOverlay.DrawInterfaceLayer",
                    "MapDirectionHintOverlay.DrawInterfaceLayer",
                    "BlueprintProjectionOverlay.DrawInterfaceLayer",
                    "BlueprintCreationOverlay.DrawInterfaceLayer",
                    "BlueprintPlacementPreviewOverlay.DrawInterfaceLayer",
                    "BlueprintEraseRegionOverlay.DrawInterfaceLayer"
                },
                "game overlay fallback dispatcher routes when low information layer is missing");
        }

        private static void InformationStatusPanelRoutesUnderVanillaUiAndPinnedOverlayStaysAboveLegacyMainWindow()
        {
            AssertStringArrayEquals(
                InterfaceLayerHookCallbacks.GetInformationStatusPanelUnderVanillaUiDispatcherRouteNamesForTesting(),
                new[]
                {
                    "InformationStatusPanelOverlay.DrawInterfaceLayer"
                },
                "information status panel under UI dispatcher routes");

            AssertStringArrayEquals(
                InterfaceLayerHookCallbacks.GetUiOverlayDispatcherRouteNamesForTesting(true),
                new[]
                {
                    "LegacyMainWindow.DrawInterfaceLayer",
                    "BlueprintMaterialWindowOverlay.DrawInterfaceLayer",
                    "BlueprintHandheldActionBarOverlay.DrawInterfaceLayer",
                    "MapCustomMarkerStylePickerOverlay.DrawInterfaceLayer"
                },
                "UI overlay dispatcher routes when low status panel layer is active");

            AssertStringArrayEquals(
                InterfaceLayerHookCallbacks.GetUiOverlayDispatcherRouteNamesForTesting(false),
                new[]
                {
                    "InformationStatusPanelOverlay.DrawInterfaceLayer",
                    "LegacyMainWindow.DrawInterfaceLayer",
                    "BlueprintMaterialWindowOverlay.DrawInterfaceLayer",
                    "BlueprintHandheldActionBarOverlay.DrawInterfaceLayer",
                    "MapCustomMarkerStylePickerOverlay.DrawInterfaceLayer"
                },
                "UI overlay fallback dispatcher routes when low status panel layer is missing");

            AssertStringArrayEquals(
                InterfaceLayerHookCallbacks.GetUserNotesPinnedOverlayDispatcherRouteNamesForTesting(),
                new[]
                {
                    "UserNotesPinnedOverlay.DrawInterfaceLayer"
                },
                "user notes pinned overlay high dispatcher routes");

            if (!string.Equals(InterfaceLayerHookCallbacks.GetUserNotesPinnedOverlayScaleTypeNameForTesting(), "None", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected pinned overlay dispatcher to use unscaled screen coordinates.");
            }
        }

        private static void AssertIntEquals(int actual, int expected, string label)
        {
            if (actual != expected)
            {
                throw new InvalidOperationException("Expected " + label + " to be " + expected + ", got " + actual + ".");
            }
        }

        private static void AssertStringArrayEquals(string[] actual, string[] expected, string label)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
            {
                throw new InvalidOperationException("Expected " + label + " length to be " + (expected == null ? -1 : expected.Length) + ", got " + (actual == null ? -1 : actual.Length) + ".");
            }

            for (var index = 0; index < actual.Length; index++)
            {
                if (!string.Equals(actual[index], expected[index], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected " + label + "[" + index + "] to be " + expected[index] + ", got " + actual[index] + ".");
                }
            }
        }
    }
}
