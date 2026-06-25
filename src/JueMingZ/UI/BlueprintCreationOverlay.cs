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
        private const string VisualContract = "world-hover+air-select+lower-saturation-lower-alpha-no-border+continuous-row-runs+world-left-consume";
        private const int TileSize = 16;
        private const int SelectedMaskAlpha = 30;
        private const int HoverMaskAlpha = 24;
        private const int DragMaskAlpha = 20;
        private static bool _wasPhysicalLeftDown;
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
            int tileY,
            bool contentKnown = false,
            bool hasSelectableContent = false,
            Func<int, int, bool> isSelectableTile = null)
        {
            return BuildPointerInputForTesting(
                active,
                legacyUiOwned,
                vanillaUiOwned,
                false,
                leftDown,
                leftDown,
                leftPressed,
                leftReleased,
                worldTileHit,
                tileX,
                tileY,
                contentKnown,
                hasSelectableContent,
                isSelectableTile);
        }

        internal static BlueprintCreationPointerInput BuildPointerInputForTesting(
            bool active,
            bool legacyUiOwned,
            bool vanillaUiOwned,
            bool pointerUiOwned,
            bool leftDown,
            bool leftPressed,
            bool leftReleased,
            bool worldTileHit,
            int tileX,
            int tileY,
            bool contentKnown = false,
            bool hasSelectableContent = false,
            Func<int, int, bool> isSelectableTile = null)
        {
            return BuildPointerInputForTesting(
                active,
                legacyUiOwned,
                vanillaUiOwned,
                pointerUiOwned,
                leftDown,
                leftDown,
                leftPressed,
                leftReleased,
                worldTileHit,
                tileX,
                tileY,
                contentKnown,
                hasSelectableContent,
                isSelectableTile);
        }

        internal static BlueprintCreationPointerInput BuildPointerInputForTesting(
            bool active,
            bool legacyUiOwned,
            bool vanillaUiOwned,
            bool pointerUiOwned,
            bool worldLeftDown,
            bool physicalLeftDown,
            bool leftPressed,
            bool leftReleased,
            bool worldTileHit,
            int tileX,
            int tileY,
            bool contentKnown = false,
            bool hasSelectableContent = false,
            Func<int, int, bool> isSelectableTile = null)
        {
            var inputActive = active;
            return new BlueprintCreationPointerInput
            {
                UiOwned = legacyUiOwned || vanillaUiOwned || pointerUiOwned,
                WorldLeftDown = inputActive && worldLeftDown,
                PhysicalLeftDown = inputActive && physicalLeftDown,
                LeftDown = inputActive && worldLeftDown,
                LeftPressed = inputActive && leftPressed,
                LeftReleased = inputActive && leftReleased,
                WorldTileHit = worldTileHit,
                TileX = tileX,
                TileY = tileY,
                ContentKnown = contentKnown,
                HasSelectableContent = hasSelectableContent,
                IsSelectableTile = isSelectableTile
            };
        }

        internal static BlueprintCreationPointerInput BuildPointerInputFromPhysicalEdgesForTesting(
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
            int tileY,
            bool contentKnown = false,
            bool hasSelectableContent = false,
            Func<int, int, bool> isSelectableTile = null)
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
                physicalLeftDown,
                leftPressed,
                leftReleased,
                worldTileHit,
                tileX,
                tileY,
                contentKnown,
                hasSelectableContent,
                isSelectableTile);
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

        internal static bool ShouldBlockCreationForPointerOwnershipForTesting(UiPointerOwnershipDetails ownership)
        {
            return ShouldBlockCreationForPointerOwnership(ownership);
        }

        internal static string GetVisualContractForTesting()
        {
            return VisualContract;
        }

        internal static int GetSelectedMaskAlphaForTesting()
        {
            return SelectedMaskAlpha;
        }

        internal static int GetHoverMaskAlphaForTesting()
        {
            return HoverMaskAlpha;
        }

        internal static int GetDragMaskAlphaForTesting()
        {
            return DragMaskAlpha;
        }

        internal static void ResetInputForTesting()
        {
            _wasPhysicalLeftDown = false;
            _wasActive = false;
        }

        private static void UpdateInputGuard(bool afterPlayerInput)
        {
            var snapshot = BlueprintCreationMaskState.GetSnapshot();
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
            var pointerBlocksHoverOrDrag = ShouldBlockCreationForPointerOwnership(pointerOwnership);
            var pointerBlocksCreation = pointerBlocksHoverOrDrag;
            var uiOwned = legacyUiOwned || vanillaUiOwned || pointerBlocksHoverOrDrag;

            if (afterPlayerInput)
            {
                var shouldConsumeAfter = ShouldConsumeAfterPlayerInputForTesting(snapshot.Active, legacyUiOwned, vanillaUiOwned, pointerBlocksCreation, worldLeftDown);
                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "creation",
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
                "creation",
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
            var contentKnown = false;
            var hasSelectableContent = false;
            Func<int, int, bool> isSelectableTile = null;
            if (!uiOwned && worldTileHit)
            {
                var tileReader = BlueprintCaptureService.CreateWorldTileReader();
                contentKnown = BlueprintCaptureService.TryHasSelectableContent(tileReader, tileX, tileY, out hasSelectableContent);
                isSelectableTile = (x, y) =>
                {
                    bool selectable;
                    return BlueprintCaptureService.TryHasSelectableContent(tileReader, x, y, out selectable);
                };
            }

            // Press/release edges must follow the physical button. The resolved
            // world-left value is allowed to go false when UI consumes the click,
            // so deriving LeftReleased from it would turn a consume into a fake
            // mouse release and break creation drag state.
            var input = BuildPointerInputFromPhysicalEdgesForTesting(
                snapshot.Active,
                legacyUiOwned,
                vanillaUiOwned,
                pointerBlocksCreation,
                worldLeftDown,
                physicalLeftDown,
                _wasPhysicalLeftDown,
                justActivated,
                worldTileHit,
                tileX,
                tileY,
                contentKnown,
                hasSelectableContent,
                isSelectableTile);
            var result = BlueprintCreationMaskState.HandlePointer(input);
            if (result != null && result.Changed)
            {
                BlueprintCreationPromptService.NotifyMaskChanged(result.Snapshot != null ? result.Snapshot.SelectedCount : 0);
            }

            BlueprintUiClickDiagnostics.RecordCreationStateTransition(
                "prefix",
                raw,
                legacyUiOwned,
                vanillaUiOwned,
                pointerUiOwned,
                pointerBlocksCreation,
                worldLeftDown,
                worldTileHit,
                tileX,
                tileY,
                input,
                snapshot,
                result);
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

        private static bool ShouldBlockCreationForPointerOwnership(UiPointerOwnershipDetails ownership)
        {
            if (ownership == null || !ownership.PointerOwned)
            {
                return false;
            }

            // LeftConsumed is handled by ResolveWorldLeftDown so OS left cannot
            // revive. Creation hover/drag only stops when the mouse still hits
            // the owner's bounds.
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
                DrawSelectedRuns(spriteBatch, snapshot, screenPosition, clipWidth, clipHeight);
            }

            if (snapshot.Active && snapshot.HoverTileHit)
            {
                DrawTileFill(spriteBatch, snapshot.HoverTileX, snapshot.HoverTileY, 1, screenPosition, clipWidth, clipHeight, HoverMaskAlpha);
            }

            if (snapshot.Dragging)
            {
                DrawDragRect(spriteBatch, snapshot, screenPosition, clipWidth, clipHeight);
            }
        }

        private static void DrawSelectedRuns(object spriteBatch, BlueprintCreationMaskSnapshot snapshot, Vector2 screenPosition, int clipWidth, int clipHeight)
        {
            var cells = snapshot.SelectedCells;
            if (cells == null || cells.Count <= 0)
            {
                return;
            }

            var runY = cells[0].Y;
            var runStartX = cells[0].X;
            var runEndX = cells[0].X;
            for (var index = 1; index < cells.Count; index++)
            {
                var cell = cells[index];
                if (cell != null && cell.Y == runY && cell.X == runEndX + 1)
                {
                    runEndX = cell.X;
                    continue;
                }

                DrawTileFill(spriteBatch, runStartX, runY, runEndX - runStartX + 1, screenPosition, clipWidth, clipHeight, SelectedMaskAlpha);
                if (cell == null)
                {
                    continue;
                }

                runY = cell.Y;
                runStartX = cell.X;
                runEndX = cell.X;
            }

            DrawTileFill(spriteBatch, runStartX, runY, runEndX - runStartX + 1, screenPosition, clipWidth, clipHeight, SelectedMaskAlpha);
        }

        private static void DrawTileFill(
            object spriteBatch,
            int tileX,
            int tileY,
            int tileWidth,
            Vector2 screenPosition,
            int clipWidth,
            int clipHeight,
            int alpha)
        {
            var width = Math.Max(1, tileWidth) * TileSize;
            var x = (int)Math.Round(tileX * TileSize - screenPosition.X);
            var y = (int)Math.Round(tileY * TileSize - screenPosition.Y);
            if (x >= clipWidth || y >= clipHeight || x + width <= 0 || y + TileSize <= 0)
            {
                return;
            }

            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, width, TileSize, 0, 0, clipWidth, clipHeight, 88, 142, 220, alpha);
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
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, width, height, 0, 0, clipWidth, clipHeight, 96, 150, 220, DragMaskAlpha);
        }
    }
}
