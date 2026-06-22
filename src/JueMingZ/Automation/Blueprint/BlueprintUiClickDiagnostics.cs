using System;
using System.Globalization;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Blueprint
{
    internal sealed class BlueprintUiClickDiagnosticsSnapshot
    {
        public BlueprintUiClickDiagnosticsSnapshot()
        {
            HandheldInputTrace = string.Empty;
            HandheldOwnershipTrace = string.Empty;
            WorldOverlayInputTrace = string.Empty;
            CreationPrefixWorldOverlayInputTrace = string.Empty;
            CreationAfterPlayerInputWorldOverlayInputTrace = string.Empty;
            CreationLastClearReasonTrace = string.Empty;
        }

        public string HandheldInputTrace { get; set; }
        public string HandheldOwnershipTrace { get; set; }
        public string WorldOverlayInputTrace { get; set; }
        public string CreationPrefixWorldOverlayInputTrace { get; set; }
        public string CreationAfterPlayerInputWorldOverlayInputTrace { get; set; }
        public string CreationLastClearReasonTrace { get; set; }
    }

    internal static class BlueprintUiClickDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static string _handheldInputTrace = string.Empty;
        private static string _handheldOwnershipTrace = string.Empty;
        private static string _worldOverlayInputTrace = string.Empty;
        private static string _creationPrefixWorldOverlayInputTrace = string.Empty;
        private static string _creationAfterPlayerInputWorldOverlayInputTrace = string.Empty;
        private static string _creationLastClearReasonTrace = string.Empty;

        public static void RecordHandheldInput(
            string source,
            BlueprintHandheldActionBarFrame frame,
            BlueprintHandheldActionBarPointerInput input,
            BlueprintHandheldActionBarInteraction interaction)
        {
            input = input ?? new BlueprintHandheldActionBarPointerInput();
            var visible = frame != null && frame.Visible;
            var hiddenReason = frame == null ? "no-frame" : frame.HiddenReason ?? string.Empty;
            var overBar = visible && frame.Bounds.Contains(input.MouseX, input.MouseY);
            var hovered = interaction == null ? string.Empty : interaction.HoveredButtonId ?? string.Empty;
            var pressed = interaction == null ? string.Empty : interaction.PressedButtonId ?? string.Empty;
            var shouldCapture = interaction != null && interaction.ShouldCaptureMouse;
            var shouldConsumeLeft = interaction != null && interaction.ShouldConsumeLeftInput;
            var shouldConsumeScroll = interaction != null && interaction.ShouldConsumeScroll;
            var clicked = interaction != null && interaction.Clicked;
            var registered = visible && (shouldCapture || shouldConsumeLeft || shouldConsumeScroll);
            var ownerId = registered
                ? BlueprintHandheldActionBarState.BuildPointerOwnerId(hovered)
                : string.Empty;
            var ownershipReason = registered
                ? ResolveOwnershipReason(shouldConsumeLeft, shouldConsumeScroll, shouldCapture)
                : string.Empty;
            var bounds = visible ? FormatRect(frame.Bounds) : "0,0,0,0";

            var inputTrace =
                "source=" + Sanitize(source) +
                ";afterPlayerInput=" + Bool(input.AfterPlayerInput) +
                ";allowCommand=" + Bool(input.AllowCommand) +
                ";frameVisible=" + Bool(visible) +
                ";hiddenReason=" + Sanitize(hiddenReason) +
                ";toolItem=" + Int(frame == null ? 0 : frame.ToolItemId) +
                ";selectedItemType=" + Int(frame == null ? 0 : frame.SelectedItemType) +
                ";readAvailable=" + Bool(input.ReadAvailable) +
                ";mouseReadMode=" + Sanitize(input.MouseReadMode) +
                ";mouse=" + Int(input.MouseX) + "," + Int(input.MouseY) +
                ";leftDown=" + Bool(input.LeftDown) +
                ";scroll=" + Int(input.ScrollDelta) +
                ";overBar=" + Bool(overBar) +
                ";hovered=" + Sanitize(hovered) +
                ";pressed=" + Sanitize(pressed) +
                ";clicked=" + Bool(clicked) +
                ";capture=" + Bool(shouldCapture) +
                ";consumeLeft=" + Bool(shouldConsumeLeft) +
                ";consumeScroll=" + Bool(shouldConsumeScroll);

            var ownershipTrace =
                "registered=" + Bool(registered) +
                ";ownerId=" + Sanitize(ownerId) +
                ";ownerKind=BlueprintHandheldActionBar" +
                ";reason=" + Sanitize(ownershipReason) +
                ";leftOwned=" + Bool(registered && (shouldConsumeLeft || input.LeftDown)) +
                ";leftConsumed=" + Bool(registered && shouldConsumeLeft) +
                ";scrollOwned=" + Bool(registered && shouldConsumeScroll) +
                ";bounds=" + bounds;

            lock (SyncRoot)
            {
                _handheldInputTrace = inputTrace;
                _handheldOwnershipTrace = ownershipTrace;
            }
        }

        public static void RecordWorldOverlayInput(
            string overlay,
            string phase,
            bool active,
            DiagnosticMouseState raw,
            bool legacyUiOwned,
            bool vanillaUiOwned,
            bool pointerUiOwned,
            bool resolvedLeftDown,
            bool shouldConsumeAfterPlayerInput,
            bool worldTileHit,
            int tileX,
            int tileY,
            bool? resolvedUiOwned = null)
        {
            raw = raw ?? new DiagnosticMouseState();
            var pointerOwnership = UiPointerOwnershipService.ResolveWorldPointerOwnership(raw);
            var uiOwned = resolvedUiOwned.HasValue
                ? resolvedUiOwned.Value
                : legacyUiOwned || vanillaUiOwned || pointerOwnership.PointerBlocksHoverOrDrag;
            var physicalLeftDown = ResolvePhysicalLeftDown(raw);
            var trace =
                "overlay=" + Sanitize(overlay) +
                ";phase=" + Sanitize(phase) +
                ";active=" + Bool(active) +
                ";readMode=" + Sanitize(raw.ReadMode) +
                ";gameInput=" + Bool(raw.GameInputAvailable) +
                ";terrariaLeft=" + Bool(raw.TerrariaLeftDown) +
                ";osLeft=" + Bool(raw.OsLeftDown) +
                ";resolvedLeft=" + Bool(resolvedLeftDown) +
                ";physicalLeft=" + Bool(physicalLeftDown) +
                ";legacyUiOwned=" + Bool(legacyUiOwned) +
                ";vanillaUiOwned=" + Bool(vanillaUiOwned) +
                ";pointerUiOwned=" + Bool(pointerUiOwned) +
                ";uiOwned=" + Bool(uiOwned) +
                ";pointerBlocksWorldLeft=" + Bool(pointerOwnership.PointerBlocksWorldLeft) +
                ";pointerBlocksHoverOrDrag=" + Bool(pointerOwnership.PointerBlocksHoverOrDrag) +
                ";pointerOwnerId=" + Sanitize(pointerOwnership.OwnerId) +
                ";pointerOwnerKind=" + Sanitize(pointerOwnership.OwnerKind) +
                ";pointerOwnerReason=" + Sanitize(pointerOwnership.Reason) +
                ";pointerOwnerHasBounds=" + Bool(pointerOwnership.HasBounds) +
                ";pointerOwnerBounds=" + FormatRect(pointerOwnership.Bounds) +
                ";pointerOwnerMouse=" + FormatMouse(pointerOwnership) +
                ";pointerOwnerMouseSource=" + Sanitize(pointerOwnership.MouseSource) +
                ";pointerOwnerBoundsHit=" + Bool(pointerOwnership.BoundsHit) +
                ";pointerLeftOwned=" + Bool(pointerOwnership.LeftOwned) +
                ";pointerLeftConsumed=" + Bool(pointerOwnership.LeftConsumed) +
                ";pointerScrollOwned=" + Bool(pointerOwnership.ScrollOwned) +
                ";consumeAfter=" + Bool(shouldConsumeAfterPlayerInput) +
                ";worldTileHit=" + Bool(worldTileHit) +
                ";tile=" + Int(tileX) + "," + Int(tileY);

            lock (SyncRoot)
            {
                // The legacy field remains the last world-overlay summary; creation keeps fixed phase slots so
                // after-player-input replay cannot hide the prefix facts needed for flicker diagnosis.
                _worldOverlayInputTrace = trace;
                if (IsCreationPrefix(overlay, phase))
                {
                    _creationPrefixWorldOverlayInputTrace = trace;
                }
                else if (IsCreationAfterPlayerInput(overlay, phase))
                {
                    _creationAfterPlayerInputWorldOverlayInputTrace = trace;
                }
            }
        }

        public static void RecordCreationStateTransition(
            string phase,
            DiagnosticMouseState raw,
            bool legacyUiOwned,
            bool vanillaUiOwned,
            bool pointerUiOwned,
            bool pointerBlocksCreation,
            bool resolvedLeftDown,
            bool worldTileHit,
            int tileX,
            int tileY,
            BlueprintCreationPointerInput input,
            BlueprintCreationMaskSnapshot before,
            BlueprintCreationInteractionResult result)
        {
            input = input ?? new BlueprintCreationPointerInput();
            raw = raw ?? new DiagnosticMouseState();
            var after = result == null ? null : result.Snapshot;
            if (!ShouldRecordCreationStateTrace(input, before, after, result))
            {
                return;
            }

            var pointerOwnership = UiPointerOwnershipService.ResolveWorldPointerOwnership(raw);
            var physicalLeftDown = ResolvePhysicalLeftDown(raw);
            var resultCode = result == null ? string.Empty : result.ResultCode ?? string.Empty;
            var trace =
                "phase=" + Sanitize(phase) +
                ";reason=" + Sanitize(resultCode) +
                ";changed=" + Bool(result != null && result.Changed) +
                ";consumeLeft=" + Bool(result != null && result.ShouldConsumeLeftInput) +
                ";inputActive=" + Bool(result != null && result.InputActive) +
                ";beforeDragging=" + Bool(before != null && before.Dragging) +
                ";afterDragging=" + Bool(after != null && after.Dragging) +
                ";beforeHover=" + FormatHover(before) +
                ";afterHover=" + FormatHover(after) +
                ";beforeSelected=" + Int(before == null ? 0 : before.SelectedCount) +
                ";afterSelected=" + Int(after == null ? 0 : after.SelectedCount) +
                ";beforeMaskCount=" + Int(before == null ? 0 : before.SelectedCount) +
                ";afterMaskCount=" + Int(after == null ? 0 : after.SelectedCount) +
                ";beforeLastResult=" + Sanitize(before == null ? string.Empty : before.LastResultCode) +
                ";afterLastResult=" + Sanitize(after == null ? string.Empty : after.LastResultCode) +
                ";leftDown=" + Bool(input.LeftDown) +
                ";leftPressed=" + Bool(input.LeftPressed) +
                ";leftReleased=" + Bool(input.LeftReleased) +
                ";resolvedLeft=" + Bool(resolvedLeftDown) +
                ";physicalLeft=" + Bool(physicalLeftDown) +
                ";legacyUiOwned=" + Bool(legacyUiOwned) +
                ";vanillaUiOwned=" + Bool(vanillaUiOwned) +
                ";pointerUiOwned=" + Bool(pointerUiOwned) +
                ";pointerBlocksCreation=" + Bool(pointerBlocksCreation) +
                ";pointerBlocksWorldLeft=" + Bool(pointerOwnership.PointerBlocksWorldLeft) +
                ";pointerBlocksHoverOrDrag=" + Bool(pointerOwnership.PointerBlocksHoverOrDrag) +
                ";uiOwned=" + Bool(input.UiOwned) +
                ";worldTileHit=" + Bool(worldTileHit) +
                ";tile=" + Int(tileX) + "," + Int(tileY) +
                ";mouseReadMode=" + Sanitize(raw.ReadMode) +
                ";gameInput=" + Bool(raw.GameInputAvailable) +
                ";terrariaMouse=" + FormatRawMouse(raw.TerrariaReadAvailable, raw.TerrariaMouseX, raw.TerrariaMouseY) +
                ";osMouse=" + FormatRawMouse(raw.OsReadAvailable, raw.OsClientMouseX, raw.OsClientMouseY) +
                ";worldMouseSource=" + Sanitize(ResolveWorldMouseSource(raw)) +
                ";pointerOwnerId=" + Sanitize(pointerOwnership.OwnerId) +
                ";pointerOwnerKind=" + Sanitize(pointerOwnership.OwnerKind) +
                ";pointerOwnerReason=" + Sanitize(pointerOwnership.Reason) +
                ";pointerOwnerBounds=" + FormatRect(pointerOwnership.Bounds) +
                ";pointerOwnerMouse=" + FormatMouse(pointerOwnership) +
                ";pointerOwnerMouseSource=" + Sanitize(pointerOwnership.MouseSource) +
                ";pointerOwnerBoundsHit=" + Bool(pointerOwnership.BoundsHit) +
                ";pointerLeftOwned=" + Bool(pointerOwnership.LeftOwned) +
                ";pointerLeftConsumed=" + Bool(pointerOwnership.LeftConsumed) +
                ";pointerScrollOwned=" + Bool(pointerOwnership.ScrollOwned);

            lock (SyncRoot)
            {
                // This is diagnostics only: record why creation hover/drag/mask state changed without
                // feeding the summary back into pointer decisions or altering HandlePointer results.
                _creationLastClearReasonTrace = trace;
            }
        }

        public static BlueprintUiClickDiagnosticsSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return new BlueprintUiClickDiagnosticsSnapshot
                {
                    HandheldInputTrace = _handheldInputTrace ?? string.Empty,
                    HandheldOwnershipTrace = _handheldOwnershipTrace ?? string.Empty,
                    WorldOverlayInputTrace = _worldOverlayInputTrace ?? string.Empty,
                    CreationPrefixWorldOverlayInputTrace = _creationPrefixWorldOverlayInputTrace ?? string.Empty,
                    CreationAfterPlayerInputWorldOverlayInputTrace = _creationAfterPlayerInputWorldOverlayInputTrace ?? string.Empty,
                    CreationLastClearReasonTrace = _creationLastClearReasonTrace ?? string.Empty
                };
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _handheldInputTrace = string.Empty;
                _handheldOwnershipTrace = string.Empty;
                _worldOverlayInputTrace = string.Empty;
                _creationPrefixWorldOverlayInputTrace = string.Empty;
                _creationAfterPlayerInputWorldOverlayInputTrace = string.Empty;
                _creationLastClearReasonTrace = string.Empty;
            }
        }

        private static bool IsCreationPrefix(string overlay, string phase)
        {
            return string.Equals(overlay, "creation", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(phase, "prefix", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCreationAfterPlayerInput(string overlay, string phase)
        {
            return string.Equals(overlay, "creation", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(phase, "after-player-input", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveOwnershipReason(bool shouldConsumeLeft, bool shouldConsumeScroll, bool shouldCaptureMouse)
        {
            if (shouldConsumeLeft)
            {
                return BlueprintHandheldActionBarState.PointerOwnershipReasonLeft;
            }

            if (shouldConsumeScroll)
            {
                return BlueprintHandheldActionBarState.PointerOwnershipReasonScroll;
            }

            return shouldCaptureMouse ? BlueprintHandheldActionBarState.PointerOwnershipReasonHover : string.Empty;
        }

        private static string FormatRect(LegacyUiRect rect)
        {
            return Int(rect.X) + "," + Int(rect.Y) + "," + Int(rect.Width) + "," + Int(rect.Height);
        }

        private static string FormatMouse(UiPointerOwnershipDetails details)
        {
            if (details == null || !details.MouseAvailable)
            {
                return "unavailable";
            }

            return Int(details.MouseX) + "," + Int(details.MouseY);
        }

        private static bool ShouldRecordCreationStateTrace(
            BlueprintCreationPointerInput input,
            BlueprintCreationMaskSnapshot before,
            BlueprintCreationMaskSnapshot after,
            BlueprintCreationInteractionResult result)
        {
            if (result == null)
            {
                return false;
            }

            if (IsCreationStateTraceCode(result.ResultCode))
            {
                return true;
            }

            if (before != null && after != null)
            {
                if (before.Dragging != after.Dragging ||
                    before.HoverTileHit != after.HoverTileHit ||
                    before.HoverTileX != after.HoverTileX ||
                    before.HoverTileY != after.HoverTileY ||
                    before.SelectedCount != after.SelectedCount)
                {
                    return true;
                }
            }

            return input != null && (input.UiOwned || input.LeftPressed || input.LeftReleased);
        }

        private static bool IsCreationStateTraceCode(string resultCode)
        {
            return string.Equals(resultCode, "uiOwned", StringComparison.Ordinal) ||
                   string.Equals(resultCode, "worldMiss", StringComparison.Ordinal) ||
                   string.Equals(resultCode, "dragCancelled", StringComparison.Ordinal) ||
                   string.Equals(resultCode, "heldIgnored", StringComparison.Ordinal) ||
                   string.Equals(resultCode, "tileUnavailable", StringComparison.Ordinal) ||
                   string.Equals(resultCode, "selectionToggled", StringComparison.Ordinal) ||
                   string.Equals(resultCode, "selectionUnchanged", StringComparison.Ordinal) ||
                   string.Equals(resultCode, "selectionTooLarge", StringComparison.Ordinal);
        }

        private static string FormatHover(BlueprintCreationMaskSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.HoverTileHit)
            {
                return "none";
            }

            return Int(snapshot.HoverTileX) + "," + Int(snapshot.HoverTileY);
        }

        private static string FormatRawMouse(bool available, int x, int y)
        {
            return available && x >= 0 && y >= 0
                ? Int(x) + "," + Int(y)
                : "unavailable";
        }

        private static string ResolveWorldMouseSource(DiagnosticMouseState raw)
        {
            if (raw == null)
            {
                return "none";
            }

            if (raw.TerrariaReadAvailable && raw.TerrariaMouseX >= 0 && raw.TerrariaMouseY >= 0)
            {
                return "Terraria";
            }

            if (raw.OsReadAvailable && raw.OsClientMouseX >= 0 && raw.OsClientMouseY >= 0)
            {
                return "OsClient";
            }

            return "none";
        }

        private static bool ResolvePhysicalLeftDown(DiagnosticMouseState raw)
        {
            return raw != null &&
                   raw.GameInputAvailable &&
                   (raw.TerrariaLeftDown || raw.OsLeftDown);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Int(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace(";", ",").Replace("|", ",").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
