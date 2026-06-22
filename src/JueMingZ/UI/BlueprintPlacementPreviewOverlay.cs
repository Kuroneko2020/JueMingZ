using System;
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
        private static bool _wasLeftDown;
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
                LeftDown = active && leftDown,
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
            _wasLeftDown = false;
            _wasActive = false;
        }

        private static void UpdateInputGuard(bool afterPlayerInput)
        {
            var snapshot = BlueprintPlacementPreviewState.GetSnapshot();
            if (!snapshot.Active)
            {
                _wasActive = false;
                _wasLeftDown = false;
                return;
            }

            var raw = DiagnosticMouseStateReader.Read();
            var leftDown = UiPointerOwnershipService.ResolveWorldLeftDown(raw);
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
                var shouldConsumeAfter = ShouldConsumeAfterPlayerInputForTesting(snapshot.Active, legacyUiOwned, vanillaUiOwned, pointerBlocksPlacement, leftDown);
                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "placement",
                    "after-player-input",
                    snapshot.Active,
                    raw,
                    legacyUiOwned,
                    vanillaUiOwned,
                    pointerUiOwned,
                    leftDown,
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
                leftDown,
                false,
                worldTileHit,
                tileX,
                tileY,
                uiOwned);

            var input = BuildPointerInputForTesting(
                snapshot.Active,
                legacyUiOwned,
                vanillaUiOwned,
                pointerBlocksPlacement,
                leftDown,
                !justActivated && leftDown && !_wasLeftDown,
                !leftDown && _wasLeftDown,
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

            _wasLeftDown = leftDown;
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
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, width, height, 0, 0, clipWidth, clipHeight, 78, 180, 124, 34);
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
            for (var index = 0; index < maxCells; index++)
            {
                var cell = template.Cells[index];
                if (cell == null)
                {
                    continue;
                }

                var x = (int)Math.Round((snapshot.OriginTileX + cell.X) * TileSize - screenPosition.X);
                var y = (int)Math.Round((snapshot.OriginTileY + cell.Y) * TileSize - screenPosition.Y);
                if (x >= clipWidth || y >= clipHeight || x + TileSize <= 0 || y + TileSize <= 0)
                {
                    continue;
                }

                var color = ResolvePreviewColor(cell);
                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, TileSize, TileSize, 0, 0, clipWidth, clipHeight, color[0], color[1], color[2], 92);
                UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, TileSize, TileSize, 1, 0, 0, clipWidth, clipHeight, color[0], color[1], color[2], 155);
            }
        }

        private static void DrawAnchor(object spriteBatch, BlueprintPlacementPreviewSnapshot snapshot, Vector2 screenPosition, int clipWidth, int clipHeight)
        {
            var x = (int)Math.Round(snapshot.HoverTileX * TileSize - screenPosition.X);
            var y = (int)Math.Round(snapshot.HoverTileY * TileSize - screenPosition.Y);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x + 2, y + 2, TileSize - 4, TileSize - 4, 2, 0, 0, clipWidth, clipHeight, 255, 242, 136, 225);
        }

        private static int[] ResolvePreviewColor(BlueprintCellRecord cell)
        {
            var layer = cell == null || cell.Layers == null || cell.Layers.Count <= 0 ? null : cell.Layers[0];
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
