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
        private const string VisualContract = "instance-erase-region+selected-priority+top-layer-fallback+store-mask-only+stage07-continuous-region-edit+cursor-red-follow-mask+cancel-only-exit";
        private static bool _wasPhysicalLeftDown;
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

        internal static BlueprintErasePointerInput BuildPointerInputFromPhysicalEdgesForTesting(
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
            return BuildPointerInputForTesting(
                active,
                legacyUiOwned,
                vanillaUiOwned,
                pointerUiOwned,
                worldLeftDown,
                leftPressed,
                leftReleased,
                worldTileHit,
                tileX,
                tileY);
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

        internal static bool ShouldBlockEraseForPointerOwnershipForTesting(UiPointerOwnershipDetails ownership)
        {
            return ShouldBlockEraseForPointerOwnership(ownership);
        }

        internal static void ResetInputForTesting()
        {
            _wasPhysicalLeftDown = false;
            _wasActive = false;
        }

        private static void UpdateInputGuard(bool afterPlayerInput)
        {
            var snapshot = BlueprintEraseRegionState.GetSnapshot();
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
            var pointerBlocksErase = ShouldBlockEraseForPointerOwnership(pointerOwnership);
            var uiOwned = legacyUiOwned || vanillaUiOwned || pointerBlocksErase;

            if (afterPlayerInput)
            {
                var shouldConsumeAfter = ShouldConsumeAfterPlayerInputForTesting(snapshot.Active, legacyUiOwned, vanillaUiOwned, pointerBlocksErase, worldLeftDown);
                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "erase",
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
                "erase",
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

            // Resolved world-left can go false when a UI owner consumes the click.
            // Press/release edges must follow the physical button so a consumed
            // frame cannot fake a release and interrupt region-modify dragging.
            var input = BuildPointerInputFromPhysicalEdgesForTesting(
                snapshot.Active,
                legacyUiOwned,
                vanillaUiOwned,
                pointerBlocksErase,
                worldLeftDown,
                physicalLeftDown,
                _wasPhysicalLeftDown,
                justActivated,
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

        private static bool ShouldBlockEraseForPointerOwnership(UiPointerOwnershipDetails ownership)
        {
            if (ownership == null || !ownership.PointerOwned)
            {
                return false;
            }

            // LeftConsumed is already reflected in ResolveWorldLeftDown. Erase
            // hover/drag only becomes UI-owned when the pointer still hits the
            // owner bounds, otherwise a consumed frame can stop region dragging.
            return ownership.PointerBlocksHoverOrDrag;
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
            return snapshot != null && snapshot.Active && (snapshot.Dragging || snapshot.HasHoverTile);
        }

        private static void DrawEraseRegion(object spriteBatch, BlueprintEraseRegionSnapshot snapshot)
        {
            var clipWidth = Math.Max(1, TerrariaMainCompat.ScreenWidth);
            var clipHeight = Math.Max(1, TerrariaMainCompat.ScreenHeight);
            var screenPosition = TerrariaMainCompat.ScreenPosition;
            if (!snapshot.Dragging && snapshot.HasHoverTile)
            {
                DrawEraseRect(spriteBatch, snapshot.HoverTileX, snapshot.HoverTileX, snapshot.HoverTileY, snapshot.HoverTileY, screenPosition, clipWidth, clipHeight);
                return;
            }

            var minX = Math.Min(snapshot.DragStartX, snapshot.DragCurrentX);
            var maxX = Math.Max(snapshot.DragStartX, snapshot.DragCurrentX);
            var minY = Math.Min(snapshot.DragStartY, snapshot.DragCurrentY);
            var maxY = Math.Max(snapshot.DragStartY, snapshot.DragCurrentY);
            DrawEraseRect(spriteBatch, minX, maxX, minY, maxY, screenPosition, clipWidth, clipHeight);
        }

        private static void DrawEraseRect(object spriteBatch, int minX, int maxX, int minY, int maxY, Vector2 screenPosition, int clipWidth, int clipHeight)
        {
            var x = (int)Math.Round(minX * TileSize - screenPosition.X);
            var y = (int)Math.Round(minY * TileSize - screenPosition.Y);
            var width = Math.Max(TileSize, (maxX - minX + 1) * TileSize);
            var height = Math.Max(TileSize, (maxY - minY + 1) * TileSize);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, width, height, 0, 0, clipWidth, clipHeight, 238, 92, 116, 42);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, width, height, 2, 0, 0, clipWidth, clipHeight, 255, 128, 152, 220);
        }
    }
}
