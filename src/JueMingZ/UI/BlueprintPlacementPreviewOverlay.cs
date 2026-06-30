using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;
using Microsoft.Xna.Framework;

namespace JueMingZ.UI
{
    public static class BlueprintPlacementPreviewOverlay
    {
        private const int TileSize = 16;
        private const int RangeFillAlpha = 10;
        private const int CellFillAlpha = 92;
        private const int CellBorderAlpha = 155;
        private const string VisualContract = "blueprint-placement-preview-late-overlay+ui-range-border-anchor-hints+foreground-cell-hints+skip-wall-content+mixed-cell-layer-aware+wall-content-owned-by-world-layer+wall-template-disables-late-range-fill";
        private static bool _wasPhysicalLeftDown;
        private static bool _wasActive;

        public static bool DrawInterfaceLayer()
        {
            try
            {
                var snapshot = BlueprintPlacementPreviewState.GetSnapshot();
                if (!ShouldDraw(snapshot))
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("BlueprintPlacementPreviewOverlay", false, out spriteBatch))
                {
                    return true;
                }

                DrawPreview(spriteBatch, snapshot);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("BlueprintPlacementPreviewOverlay", error);
                LogThrottle.ErrorThrottled(
                    "blueprint-placement-preview-overlay-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintPlacementPreviewOverlay",
                    "Blueprint placement preview draw failed; exception swallowed.", error);
            }

            return true;
        }

        public static void UpdatePrefixGuard()
        {
            try
            {
                UpdateInputGuard(false);
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "blueprint-placement-preview-update-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintPlacementPreviewOverlay",
                    "Blueprint placement preview input guard failed; exception swallowed.", error);
            }
        }

        public static void UpdateAfterPlayerInputGuard()
        {
            try
            {
                UpdateInputGuard(true);
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "blueprint-placement-preview-after-input-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintPlacementPreviewOverlay",
                    "Blueprint placement preview after-input guard failed; exception swallowed.", error);
            }
        }

        internal static BlueprintPlacementPointerInput BuildPointerInputForTesting(
            bool active,
            bool legacyUiOwned,
            bool vanillaUiOwned,
            bool leftDown,
            bool leftPressed,
            bool leftReleased,
            bool worldTileHit,
            int tileX,
            int tileY)
        {
            return BuildPointerInputForTesting(
                active,
                legacyUiOwned,
                vanillaUiOwned,
                false,
                leftDown,
                leftPressed,
                leftReleased,
                worldTileHit,
                tileX,
                tileY);
        }

        internal static BlueprintPlacementPointerInput BuildPointerInputForTesting(
            bool active,
            bool legacyUiOwned,
            bool vanillaUiOwned,
            bool pointerUiOwned,
            bool leftDown,
            bool leftPressed,
            bool leftReleased,
            bool worldTileHit,
            int tileX,
            int tileY)
        {
            return new BlueprintPlacementPointerInput
            {
                UiOwned = legacyUiOwned || vanillaUiOwned || pointerUiOwned,
                PhysicalLeftDown = active && leftDown,
                LeftDown = active && leftDown,
                LeftPressed = active && leftPressed,
                LeftReleased = active && leftReleased,
                WorldTileHit = worldTileHit,
                TileX = tileX,
                TileY = tileY
            };
        }

        internal static BlueprintPlacementPointerInput BuildPointerInputFromPhysicalEdgesForTesting(
            bool active,
            bool legacyUiOwned,
            bool vanillaUiOwned,
            bool pointerUiOwned,
            bool worldLeftDown,
            bool physicalLeftDown,
            bool wasPhysicalLeftDown,
            bool justActivated,
            bool worldTileHit,
            int tileX,
            int tileY)
        {
            var uiOwned = legacyUiOwned || vanillaUiOwned || pointerUiOwned;
            var leftPressed = !justActivated && physicalLeftDown && !wasPhysicalLeftDown && worldLeftDown && !uiOwned;
            var leftReleased = !physicalLeftDown && wasPhysicalLeftDown && !uiOwned;
            return new BlueprintPlacementPointerInput
            {
                UiOwned = uiOwned,
                PhysicalLeftDown = active && physicalLeftDown,
                LeftDown = active && worldLeftDown,
                LeftPressed = active && leftPressed,
                LeftReleased = active && leftReleased,
                WorldTileHit = worldTileHit,
                TileX = tileX,
                TileY = tileY
            };
        }

        internal static bool ShouldRegisterWorldOverlayForTesting()
        {
            return true;
        }

        internal static string GetVisualContractForTesting()
        {
            return VisualContract;
        }

        internal static int ResolveRangeFillAlphaForTesting()
        {
            return RangeFillAlpha;
        }

        internal static int ResolveCellFillAlphaForTesting()
        {
            return CellFillAlpha;
        }

        internal static bool ShouldDrawRangeFillForTesting(BlueprintPlacementPreviewSnapshot snapshot)
        {
            return ShouldDrawRangeFill(snapshot);
        }

        internal static bool ShouldDrawTemplateLayerInLateOverlayForTesting(string layerKind)
        {
            return ShouldDrawTemplateLayerInLateOverlay(layerKind);
        }

        internal static int ResolveTemplateLayerDrawPassForTesting(string layerKind)
        {
            return BlueprintProjectionGhostRenderer.ResolveLayerDrawPassForTesting(layerKind);
        }

        internal static IReadOnlyList<string> BuildLatePreviewDrawOrderForTesting(BlueprintPlacementPreviewSnapshot snapshot)
        {
            var order = new List<string>();
            AppendLatePreviewDrawOrder(order, snapshot);
            return order;
        }

        internal static bool ShouldConsumeAfterPlayerInputForTesting(bool active, bool legacyUiOwned, bool leftDown)
        {
            return ShouldConsumeAfterPlayerInputForTesting(active, legacyUiOwned, false, false, leftDown);
        }

        internal static bool ShouldConsumeAfterPlayerInputForTesting(
            bool active,
            bool legacyUiOwned,
            bool vanillaUiOwned,
            bool pointerUiOwned,
            bool leftDown)
        {
            return active && leftDown && !legacyUiOwned && !vanillaUiOwned && !pointerUiOwned;
        }

        internal static bool ShouldBlockPlacementForPointerOwnershipForTesting(UiPointerOwnershipDetails ownership)
        {
            return ShouldBlockPlacementForPointerOwnership(ownership);
        }

        internal static void ResetInputForTesting()
        {
            _wasPhysicalLeftDown = false;
            _wasActive = false;
        }

        private static void UpdateInputGuard(bool afterPlayerInput)
        {
            var snapshot = BlueprintPlacementPreviewState.GetSnapshot();
            if (!snapshot.Active)
            {
                _wasActive = false;
                _wasPhysicalLeftDown = false;
                return;
            }

            var raw = DiagnosticMouseStateReader.Read();
            var worldLeftDown = UiPointerOwnershipService.ResolveWorldLeftDown(raw);
            var physicalLeftDown = ResolvePhysicalLeftDown(raw);
            var justActivated = !_wasActive;
            _wasActive = true;
            var legacyUiOwned = IsLegacyWindowHit(raw);
            var vanillaUiOwned = IsVanillaUiBlockingWorldSelection();
            var pointerOwnership = UiPointerOwnershipService.ResolveWorldPointerOwnership(raw);
            var pointerUiOwned = pointerOwnership.PointerOwned;
            var pointerBlocksPlacement = ShouldBlockPlacementForPointerOwnership(pointerOwnership);
            var uiOwned = legacyUiOwned || vanillaUiOwned || pointerBlocksPlacement;

            if (afterPlayerInput)
            {
                var shouldConsumeAfter = ShouldConsumeAfterPlayerInputForTesting(snapshot.Active, legacyUiOwned, vanillaUiOwned, pointerBlocksPlacement, worldLeftDown);
                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "placement",
                    "after-player-input",
                    snapshot.Active,
                    raw,
                    legacyUiOwned,
                    vanillaUiOwned,
                    pointerUiOwned,
                    worldLeftDown,
                    shouldConsumeAfter,
                    false,
                    0,
                    0,
                    uiOwned);
                if (shouldConsumeAfter)
                {
                    UiMouseCaptureService.ConsumeMouseTriggerForOperationWindow("MouseLeft", out _);
                }

                return;
            }

            bool worldTileHit;
            int tileX;
            int tileY;
            ResolveWorldTile(raw, out worldTileHit, out tileX, out tileY);
            BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                "placement",
                "prefix",
                snapshot.Active,
                raw,
                legacyUiOwned,
                vanillaUiOwned,
                pointerUiOwned,
                worldLeftDown,
                false,
                worldTileHit,
                tileX,
                tileY,
                uiOwned);

            // Activation may be caused by a UI click that is still physically held.
            // Edges follow the physical button; resolved world-left only decides
            // whether that edge is allowed to reach placement confirmation.
            var input = BuildPointerInputFromPhysicalEdgesForTesting(
                snapshot.Active,
                legacyUiOwned,
                vanillaUiOwned,
                pointerBlocksPlacement,
                worldLeftDown,
                physicalLeftDown,
                _wasPhysicalLeftDown,
                justActivated,
                worldTileHit,
                tileX,
                tileY);
            var result = BlueprintPlacementPreviewState.HandlePointer(input);
            if (result != null && result.PlacedInstance)
            {
                BlueprintEntryState.MarkPlacementConfirmed(result);
                BlueprintPlacedInstanceUiState.NotifyInstanceCreated(result.Instance);
            }

            if (result != null && result.ShouldConsumeLeftInput && !legacyUiOwned)
            {
                UiMouseCaptureService.CaptureForOperationWindowPreserveMouseButtons();
                UiMouseCaptureService.ConsumeMouseTriggerForOperationWindow("MouseLeft", out _);
            }

            _wasPhysicalLeftDown = physicalLeftDown;
        }

        internal static bool ResolvePhysicalLeftDownForTesting(DiagnosticMouseState raw)
        {
            return ResolvePhysicalLeftDown(raw);
        }

        private static bool ResolvePhysicalLeftDown(DiagnosticMouseState raw)
        {
            if (raw == null || !raw.GameInputAvailable)
            {
                return false;
            }

            return raw.TerrariaLeftDown || raw.OsLeftDown;
        }

        private static bool ShouldBlockPlacementForPointerOwnership(UiPointerOwnershipDetails ownership)
        {
            if (ownership == null || !ownership.PointerOwned)
            {
                return false;
            }

            // Placement keeps the stricter click-blocking contract in this
            // phase: UI-consumed left blocks world placement, and bounds hit
            // blocks hover/drag in the same coordinate domain.
            return ownership.PointerBlocksWorldLeft || ownership.PointerBlocksHoverOrDrag;
        }

        private static bool IsLegacyWindowHit(DiagnosticMouseState raw)
        {
            if (!LegacyMainUiState.Visible)
            {
                return false;
            }

            var scale = LegacyMainUiScale.Resolve(raw);
            var mouse = LegacyUiInput.ReadMouseForOverlay(raw, scale);
            return mouse.WindowHit || LegacyMainUiState.WindowRect.Contains(mouse.X, mouse.Y);
        }

        private static bool IsVanillaUiBlockingWorldSelection()
        {
            try
            {
                return TerrariaMainCompat.IsInMainMenu ||
                       TerrariaMainCompat.IsMapFullscreenOpen ||
                       TerrariaMainCompat.IsPlayerInventoryOpen ||
                       TerrariaMainCompat.IsDrawingPlayerChat ||
                       !string.IsNullOrWhiteSpace(TerrariaMainCompat.NpcChatText) ||
                       !TerrariaMainCompat.IsWorldReady;
            }
            catch
            {
                return true;
            }
        }

        private static void ResolveWorldTile(DiagnosticMouseState raw, out bool hit, out int tileX, out int tileY)
        {
            hit = false;
            tileX = 0;
            tileY = 0;
            if (raw == null || !raw.GameInputAvailable)
            {
                return;
            }

            var screenX = raw.TerrariaReadAvailable && raw.TerrariaMouseX >= 0
                ? raw.TerrariaMouseX
                : raw.OsClientMouseX;
            var screenY = raw.TerrariaReadAvailable && raw.TerrariaMouseY >= 0
                ? raw.TerrariaMouseY
                : raw.OsClientMouseY;
            if (screenX < 0 ||
                screenY < 0 ||
                screenX >= Math.Max(1, TerrariaMainCompat.ScreenWidth) ||
                screenY >= Math.Max(1, TerrariaMainCompat.ScreenHeight))
            {
                return;
            }

            var screenPosition = TerrariaMainCompat.ScreenPosition;
            tileX = (int)Math.Floor((screenPosition.X + screenX) / TileSize);
            tileY = (int)Math.Floor((screenPosition.Y + screenY) / TileSize);
            hit = TerrariaMainCompat.IsTileCoordinateInWorld(tileX, tileY);
        }

        private static bool ShouldDraw(BlueprintPlacementPreviewSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.Active &&
                   snapshot.HoverTileHit &&
                   snapshot.TemplateSnapshot != null;
        }

        private static void DrawPreview(object spriteBatch, BlueprintPlacementPreviewSnapshot snapshot)
        {
            var clipWidth = Math.Max(1, TerrariaMainCompat.ScreenWidth);
            var clipHeight = Math.Max(1, TerrariaMainCompat.ScreenHeight);
            var screenPosition = TerrariaMainCompat.ScreenPosition;
            var template = snapshot.TemplateSnapshot;
            if (template == null)
            {
                return;
            }

            var x = (int)Math.Round(snapshot.OriginTileX * TileSize - screenPosition.X);
            var y = (int)Math.Round(snapshot.OriginTileY * TileSize - screenPosition.Y);
            var width = Math.Max(TileSize, Math.Max(1, snapshot.Width) * TileSize);
            var height = Math.Max(TileSize, Math.Max(1, snapshot.Height) * TileSize);
            if (ShouldDrawRangeFill(snapshot))
            {
                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, width, height, 0, 0, clipWidth, clipHeight, 78, 180, 124, RangeFillAlpha);
            }

            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, width, height, 2, 0, 0, clipWidth, clipHeight, 122, 238, 154, 210);
            DrawTemplateCells(spriteBatch, snapshot, screenPosition, clipWidth, clipHeight);
            DrawAnchor(spriteBatch, snapshot, screenPosition, clipWidth, clipHeight);
        }

        private static void DrawTemplateCells(object spriteBatch, BlueprintPlacementPreviewSnapshot snapshot, Vector2 screenPosition, int clipWidth, int clipHeight)
        {
            var template = snapshot.TemplateSnapshot;
            if (template == null || template.Cells == null)
            {
                return;
            }

            var maxCells = Math.Min(template.Cells.Count, 512);
            for (var pass = 1; pass <= 2; pass++)
            {
                for (var index = 0; index < maxCells; index++)
                {
                    var cell = template.Cells[index];
                    if (cell == null || cell.Layers == null)
                    {
                        continue;
                    }

                    var x = (int)Math.Round((snapshot.OriginTileX + cell.X) * TileSize - screenPosition.X);
                    var y = (int)Math.Round((snapshot.OriginTileY + cell.Y) * TileSize - screenPosition.Y);
                    if (x >= clipWidth || y >= clipHeight || x + TileSize <= 0 || y + TileSize <= 0)
                    {
                        continue;
                    }

                    for (var layerIndex = 0; layerIndex < cell.Layers.Count; layerIndex++)
                    {
                        var layer = cell.Layers[layerIndex];
                        if (layer == null ||
                            !ShouldDrawTemplateLayerInLateOverlay(layer.LayerKind) ||
                            BlueprintProjectionGhostRenderer.ResolveLayerDrawPass(layer.LayerKind) != pass)
                        {
                            continue;
                        }

                        var color = ResolvePreviewColor(layer);
                        UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, TileSize, TileSize, 0, 0, clipWidth, clipHeight, color[0], color[1], color[2], CellFillAlpha);
                        UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, TileSize, TileSize, 1, 0, 0, clipWidth, clipHeight, color[0], color[1], color[2], CellBorderAlpha);
                    }
                }
            }
        }

        private static void DrawAnchor(object spriteBatch, BlueprintPlacementPreviewSnapshot snapshot, Vector2 screenPosition, int clipWidth, int clipHeight)
        {
            var x = (int)Math.Round(snapshot.HoverTileX * TileSize - screenPosition.X);
            var y = (int)Math.Round(snapshot.HoverTileY * TileSize - screenPosition.Y);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x + 2, y + 2, TileSize - 4, TileSize - 4, 2, 0, 0, clipWidth, clipHeight, 255, 242, 136, 225);
        }

        private static void AppendLatePreviewDrawOrder(List<string> order, BlueprintPlacementPreviewSnapshot snapshot)
        {
            if (order == null || !ShouldDraw(snapshot))
            {
                return;
            }

            if (ShouldDrawRangeFill(snapshot))
            {
                order.Add("late-preview:range-fill");
            }

            order.Add("late-preview:range-border");
            var template = snapshot.TemplateSnapshot;
            var maxCells = Math.Min(template.Cells.Count, 512);
            for (var pass = 1; pass <= 2; pass++)
            {
                for (var cellIndex = 0; cellIndex < maxCells; cellIndex++)
                {
                    var cell = template.Cells[cellIndex];
                    if (cell == null || cell.Layers == null)
                    {
                        continue;
                    }

                    for (var layerIndex = 0; layerIndex < cell.Layers.Count; layerIndex++)
                    {
                        var layer = cell.Layers[layerIndex];
                        if (layer == null ||
                            !ShouldDrawTemplateLayerInLateOverlay(layer.LayerKind) ||
                            BlueprintProjectionGhostRenderer.ResolveLayerDrawPass(layer.LayerKind) != pass)
                        {
                            continue;
                        }

                        order.Add("late-preview:" + (layer.LayerKind ?? string.Empty) + ":" + (snapshot.OriginTileX + cell.X) + "," + (snapshot.OriginTileY + cell.Y));
                    }
                }
            }

            order.Add("late-preview:anchor");
        }

        private static bool ShouldDrawTemplateLayerInLateOverlay(string layerKind)
        {
            return !string.Equals(layerKind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldDrawRangeFill(BlueprintPlacementPreviewSnapshot snapshot)
        {
            if (!ShouldDraw(snapshot))
            {
                return false;
            }

            // Wall content is owned by the world layer. A filled late UI surface
            // over a wall template reads as topmost background wall in screenshots.
            return !TemplateHasWallLayer(snapshot.TemplateSnapshot);
        }

        private static bool TemplateHasWallLayer(BlueprintTemplateRecord template)
        {
            if (template == null || template.Cells == null)
            {
                return false;
            }

            var maxCells = Math.Min(template.Cells.Count, 512);
            for (var cellIndex = 0; cellIndex < maxCells; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                if (cell == null || cell.Layers == null)
                {
                    continue;
                }

                for (var layerIndex = 0; layerIndex < cell.Layers.Count; layerIndex++)
                {
                    var layer = cell.Layers[layerIndex];
                    if (layer != null &&
                        string.Equals(layer.LayerKind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int[] ResolvePreviewColor(BlueprintCellLayerRecord layer)
        {
            var kind = layer == null ? string.Empty : layer.LayerKind ?? string.Empty;
            if (string.Equals(kind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 92, 144, 214 };
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 226, 96, 92 };
            }

            if (string.Equals(kind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 218, 178, 84 };
            }

            if (string.Equals(kind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 126, 206, 132 };
            }

            return new[] { 178, 204, 148 };
        }
    }
}
