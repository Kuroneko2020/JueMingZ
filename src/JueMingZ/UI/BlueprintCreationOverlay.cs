using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;
using Microsoft.Xna.Framework;

namespace JueMingZ.UI
{
    public static class BlueprintCreationOverlay
    {
        private const int TileSize = 16;
        private static bool _wasLeftDown;
        private static bool _wasActive;

        public static bool DrawInterfaceLayer()
        {
            try
            {
                var snapshot = BlueprintCreationMaskState.GetSnapshot();
                if (!ShouldDraw(snapshot))
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("BlueprintCreationOverlay", false, out spriteBatch))
                {
                    return true;
                }

                DrawMask(spriteBatch, snapshot);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("BlueprintCreationOverlay", error);
                LogThrottle.ErrorThrottled(
                    "blueprint-creation-overlay-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintCreationOverlay",
                    "Blueprint creation overlay draw failed; exception swallowed.", error);
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
                    "blueprint-creation-overlay-update-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintCreationOverlay",
                    "Blueprint creation input guard failed; exception swallowed.", error);
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
                    "blueprint-creation-overlay-after-input-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintCreationOverlay",
                    "Blueprint creation after-input guard failed; exception swallowed.", error);
            }
        }

        internal static BlueprintCreationPointerInput BuildPointerInputForTesting(
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
            return new BlueprintCreationPointerInput
            {
                UiOwned = legacyUiOwned || vanillaUiOwned,
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
            return active && leftDown && !legacyUiOwned;
        }

        internal static void ResetInputForTesting()
        {
            _wasLeftDown = false;
            _wasActive = false;
        }

        private static void UpdateInputGuard(bool afterPlayerInput)
        {
            var snapshot = BlueprintCreationMaskState.GetSnapshot();
            if (!snapshot.Active)
            {
                _wasActive = false;
                _wasLeftDown = false;
                return;
            }

            var raw = DiagnosticMouseStateReader.Read();
            var leftDown = raw != null && raw.GameInputAvailable && (raw.TerrariaLeftDown || raw.OsLeftDown);
            var justActivated = !_wasActive;
            _wasActive = true;

            if (afterPlayerInput)
            {
                if (ShouldConsumeAfterPlayerInputForTesting(snapshot.Active, IsLegacyWindowHit(raw), leftDown))
                {
                    UiMouseCaptureService.ConsumeMouseTriggerForOperationWindow("MouseLeft", out _);
                }

                return;
            }

            var legacyUiOwned = IsLegacyWindowHit(raw);
            var vanillaUiOwned = IsVanillaUiBlockingWorldSelection();
            bool worldTileHit;
            int tileX;
            int tileY;
            ResolveWorldTile(raw, out worldTileHit, out tileX, out tileY);

            var input = BuildPointerInputForTesting(
                snapshot.Active,
                legacyUiOwned,
                vanillaUiOwned,
                leftDown,
                !justActivated && leftDown && !_wasLeftDown,
                !leftDown && _wasLeftDown,
                worldTileHit,
                tileX,
                tileY);
            var result = BlueprintCreationMaskState.HandlePointer(input);
            if (result != null && result.ShouldConsumeLeftInput && !legacyUiOwned)
            {
                UiMouseCaptureService.CaptureForOperationWindowPreserveMouseButtons();
                UiMouseCaptureService.ConsumeMouseTriggerForOperationWindow("MouseLeft", out _);
            }

            _wasLeftDown = leftDown;
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

        private static bool ShouldDraw(BlueprintCreationMaskSnapshot snapshot)
        {
            return snapshot != null &&
                   (snapshot.Active || snapshot.CompletedPendingCapture || snapshot.Dragging || snapshot.SelectedCount > 0);
        }

        private static void DrawMask(object spriteBatch, BlueprintCreationMaskSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var clipWidth = Math.Max(1, TerrariaMainCompat.ScreenWidth);
            var clipHeight = Math.Max(1, TerrariaMainCompat.ScreenHeight);
            var screenPosition = TerrariaMainCompat.ScreenPosition;
            if (snapshot.SelectedCells != null)
            {
                for (var index = 0; index < snapshot.SelectedCells.Count; index++)
                {
                    DrawTileCell(spriteBatch, snapshot.SelectedCells[index], screenPosition, clipWidth, clipHeight);
                }
            }

            if (snapshot.Dragging)
            {
                DrawDragRect(spriteBatch, snapshot, screenPosition, clipWidth, clipHeight);
            }
        }

        private static void DrawTileCell(object spriteBatch, BlueprintCreationMaskCell cell, Vector2 screenPosition, int clipWidth, int clipHeight)
        {
            if (cell == null)
            {
                return;
            }

            var x = (int)Math.Round(cell.X * TileSize - screenPosition.X);
            var y = (int)Math.Round(cell.Y * TileSize - screenPosition.Y);
            if (x >= clipWidth || y >= clipHeight || x + TileSize <= 0 || y + TileSize <= 0)
            {
                return;
            }

            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, TileSize, TileSize, 0, 0, clipWidth, clipHeight, 70, 150, 255, 72);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, TileSize, TileSize, 1, 0, 0, clipWidth, clipHeight, 92, 210, 255, 165);
        }

        private static void DrawDragRect(object spriteBatch, BlueprintCreationMaskSnapshot snapshot, Vector2 screenPosition, int clipWidth, int clipHeight)
        {
            var minX = Math.Min(snapshot.DragStartX, snapshot.DragCurrentX);
            var maxX = Math.Max(snapshot.DragStartX, snapshot.DragCurrentX);
            var minY = Math.Min(snapshot.DragStartY, snapshot.DragCurrentY);
            var maxY = Math.Max(snapshot.DragStartY, snapshot.DragCurrentY);
            var x = (int)Math.Round(minX * TileSize - screenPosition.X);
            var y = (int)Math.Round(minY * TileSize - screenPosition.Y);
            var width = Math.Max(TileSize, (maxX - minX + 1) * TileSize);
            var height = Math.Max(TileSize, (maxY - minY + 1) * TileSize);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, width, height, 0, 0, clipWidth, clipHeight, 92, 180, 255, 42);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, width, height, 2, 0, 0, clipWidth, clipHeight, 132, 230, 255, 210);
        }
    }
}
