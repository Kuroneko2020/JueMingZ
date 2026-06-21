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
        }

        public string HandheldInputTrace { get; set; }
        public string HandheldOwnershipTrace { get; set; }
        public string WorldOverlayInputTrace { get; set; }
    }

    internal static class BlueprintUiClickDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static string _handheldInputTrace = string.Empty;
        private static string _handheldOwnershipTrace = string.Empty;
        private static string _worldOverlayInputTrace = string.Empty;

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
            int tileY)
        {
            raw = raw ?? new DiagnosticMouseState();
            var uiOwned = legacyUiOwned || vanillaUiOwned || pointerUiOwned;
            var trace =
                "overlay=" + Sanitize(overlay) +
                ";phase=" + Sanitize(phase) +
                ";active=" + Bool(active) +
                ";readMode=" + Sanitize(raw.ReadMode) +
                ";gameInput=" + Bool(raw.GameInputAvailable) +
                ";terrariaLeft=" + Bool(raw.TerrariaLeftDown) +
                ";osLeft=" + Bool(raw.OsLeftDown) +
                ";resolvedLeft=" + Bool(resolvedLeftDown) +
                ";legacyUiOwned=" + Bool(legacyUiOwned) +
                ";vanillaUiOwned=" + Bool(vanillaUiOwned) +
                ";pointerUiOwned=" + Bool(pointerUiOwned) +
                ";uiOwned=" + Bool(uiOwned) +
                ";consumeAfter=" + Bool(shouldConsumeAfterPlayerInput) +
                ";worldTileHit=" + Bool(worldTileHit) +
                ";tile=" + Int(tileX) + "," + Int(tileY);

            lock (SyncRoot)
            {
                _worldOverlayInputTrace = trace;
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
                    WorldOverlayInputTrace = _worldOverlayInputTrace ?? string.Empty
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
            }
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
