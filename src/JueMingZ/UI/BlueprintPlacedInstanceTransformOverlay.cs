using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.UI
{
    public static class BlueprintPlacedInstanceTransformOverlay
    {
        private const int TileSize = 16;
        private static bool _wasLeftDown;
        private static bool _wasActive;

        public static void UpdatePrefixGuard()
        {
            try
            {
                UpdateInputGuard(false);
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "blueprint-transform-update-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintPlacedInstanceTransformOverlay",
                    "Blueprint placed-instance transform input guard failed; exception swallowed.", error);
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
                    "blueprint-transform-after-input-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintPlacedInstanceTransformOverlay",
                    "Blueprint placed-instance transform after-input guard failed; exception swallowed.", error);
            }
        }

        internal static BlueprintPlacedInstanceTransformPointerInput BuildPointerInputForTesting(
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
            return new BlueprintPlacedInstanceTransformPointerInput
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

        internal static bool ShouldConsumeAfterPlayerInputForTesting(
            bool active,
            bool legacyUiOwned,
            bool vanillaUiOwned,
            bool pointerUiOwned,
            bool leftDown)
        {
            return active && leftDown && !legacyUiOwned && !vanillaUiOwned && !pointerUiOwned;
        }

        internal static bool ShouldBlockTransformForPointerOwnershipForTesting(UiPointerOwnershipDetails ownership)
        {
            return ShouldBlockTransformForPointerOwnership(ownership);
        }

        internal static void ResetInputForTesting()
        {
            _wasLeftDown = false;
            _wasActive = false;
        }

        private static void UpdateInputGuard(bool afterPlayerInput)
        {
            var snapshot = BlueprintPlacedInstanceTransformState.GetSnapshot();
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
            var pointerBlocksTransform = ShouldBlockTransformForPointerOwnership(pointerOwnership);
            var uiOwned = legacyUiOwned || vanillaUiOwned || pointerBlocksTransform;

            if (afterPlayerInput)
            {
                var shouldConsumeAfter = ShouldConsumeAfterPlayerInputForTesting(snapshot.Active, legacyUiOwned, vanillaUiOwned, pointerBlocksTransform, leftDown);
                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "placed-transform",
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
                "placed-transform",
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
                pointerBlocksTransform,
                leftDown,
                !justActivated && leftDown && !_wasLeftDown,
                !leftDown && _wasLeftDown,
                worldTileHit,
                tileX,
                tileY);
            var result = BlueprintPlacedInstanceTransformState.HandlePointer(input);
            if (result != null && result.Completed)
            {
                BlueprintPlacedInstanceUiState.NotifyInstancesChanged(result.TargetInstanceId);
            }

            if (result != null && result.ShouldConsumeLeftInput && !legacyUiOwned)
            {
                UiMouseCaptureService.CaptureForOperationWindowPreserveMouseButtons();
                UiMouseCaptureService.ConsumeMouseTriggerForOperationWindow("MouseLeft", out _);
            }

            _wasLeftDown = leftDown;
        }

        private static bool ShouldBlockTransformForPointerOwnership(UiPointerOwnershipDetails ownership)
        {
            if (ownership == null || !ownership.PointerOwned)
            {
                return false;
            }

            // Move/mirror uses the same world-click ownership contract as erase:
            // UI-owned left or hover bounds block target selection and placement.
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
    }
}
