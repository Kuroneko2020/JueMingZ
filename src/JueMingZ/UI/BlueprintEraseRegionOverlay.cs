using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;
using Microsoft.Xna.Framework;

namespace JueMingZ.UI
{
    public static class BlueprintEraseRegionOverlay
    {
        private const int TileSize = 16;
        private const string VisualContract = "instance-erase-region+selected-priority+top-layer-fallback+store-mask-only";
        private static bool _wasLeftDown;
        private static bool _wasActive;

        public static bool DrawInterfaceLayer()
        {
            try
            {
                var snapshot = BlueprintEraseRegionState.GetSnapshot();
                if (!ShouldDraw(snapshot))
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("BlueprintEraseRegionOverlay", false, out spriteBatch))
                {
                    return true;
                }

                DrawEraseRegion(spriteBatch, snapshot);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("BlueprintEraseRegionOverlay", error);
                LogThrottle.ErrorThrottled(
                    "blueprint-erase-region-overlay-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintEraseRegionOverlay",
                    "Blueprint erase region overlay draw failed; exception swallowed.", error);
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
                    "blueprint-erase-region-update-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintEraseRegionOverlay",
                    "Blueprint erase region input guard failed; exception swallowed.", error);
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
                    "blueprint-erase-region-after-input-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintEraseRegionOverlay",
                    "Blueprint erase region after-input guard failed; exception swallowed.", error);
            }
        }

        internal static BlueprintErasePointerInput BuildPointerInputForTesting(
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

        internal static BlueprintErasePointerInput BuildPointerInputForTesting(
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
            return new BlueprintErasePointerInput
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

        internal static string GetVisualContractForTesting()
        {
            return VisualContract;
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

        internal static void ResetInputForTesting()
        {
            _wasLeftDown = false;
            _wasActive = false;
        }

        private static void UpdateInputGuard(bool afterPlayerInput)
        {
            var snapshot = BlueprintEraseRegionState.GetSnapshot();
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
            var pointerUiOwned = UiPointerOwnershipService.IsPointerOwnedThisFrame();

            if (afterPlayerInput)
            {
                var shouldConsumeAfter = ShouldConsumeAfterPlayerInputForTesting(snapshot.Active, legacyUiOwned, vanillaUiOwned, pointerUiOwned, leftDown);
                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "erase",
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
                    0);
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
                "erase",
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
                tileY);

            var input = BuildPointerInputForTesting(
                snapshot.Active,
                legacyUiOwned,
                vanillaUiOwned,
                pointerUiOwned,
                leftDown,
                !justActivated && leftDown && !_wasLeftDown,
                !leftDown && _wasLeftDown,
                worldTileHit,
                tileX,
                tileY);
            var result = BlueprintEraseRegionState.HandlePointer(input);
            if (result != null && result.ErasedRegion)
            {
                BlueprintPlacedInstanceUiState.NotifyInstancesChanged(result.TargetInstanceId);
                BlueprintEntryState.MarkEraseApplied(result);
            }

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

        private static bool ShouldDraw(BlueprintEraseRegionSnapshot snapshot)
        {
            return snapshot != null && snapshot.Active && snapshot.Dragging;
        }

        private static void DrawEraseRegion(object spriteBatch, BlueprintEraseRegionSnapshot snapshot)
        {
            var clipWidth = Math.Max(1, TerrariaMainCompat.ScreenWidth);
            var clipHeight = Math.Max(1, TerrariaMainCompat.ScreenHeight);
            var screenPosition = TerrariaMainCompat.ScreenPosition;
            var minX = Math.Min(snapshot.DragStartX, snapshot.DragCurrentX);
            var maxX = Math.Max(snapshot.DragStartX, snapshot.DragCurrentX);
            var minY = Math.Min(snapshot.DragStartY, snapshot.DragCurrentY);
            var maxY = Math.Max(snapshot.DragStartY, snapshot.DragCurrentY);
            var x = (int)Math.Round(minX * TileSize - screenPosition.X);
            var y = (int)Math.Round(minY * TileSize - screenPosition.Y);
            var width = Math.Max(TileSize, (maxX - minX + 1) * TileSize);
            var height = Math.Max(TileSize, (maxY - minY + 1) * TileSize);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, width, height, 0, 0, clipWidth, clipHeight, 238, 92, 116, 42);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, width, height, 2, 0, 0, clipWidth, clipHeight, 255, 128, 152, 220);
        }
    }
}
