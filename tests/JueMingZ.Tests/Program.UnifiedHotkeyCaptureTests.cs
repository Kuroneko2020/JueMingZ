using System;
using System.Collections.Generic;
using JueMingZ.Config;
using JueMingZ.Input.Hotkeys;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void UnifiedHotkeyCaptureEvaluatesClearCancelAndFailureReasons()
        {
            AssertCaptureResult(
                HotkeyCaptureService.EvaluateTokens(new[] { "LCtrl", "Num1" }),
                HotkeyCaptureResultKind.Captured,
                "captured",
                "LCtrl+Num1",
                "LCtrl + Num1");
            AssertCaptureResult(
                HotkeyCaptureService.EvaluateTokens(new[] { "Backspace" }),
                HotkeyCaptureResultKind.Cleared,
                "cleared",
                string.Empty,
                string.Empty);
            AssertCaptureResult(
                HotkeyCaptureService.EvaluateTokens(new[] { "Esc" }),
                HotkeyCaptureResultKind.Cancelled,
                "cancelled",
                string.Empty,
                string.Empty);
            AssertCaptureResult(
                HotkeyCaptureService.EvaluateTokens(new[] { "LCtrl", "F5" }),
                HotkeyCaptureResultKind.Failed,
                "reservedKey",
                string.Empty,
                string.Empty);
            AssertCaptureResult(
                HotkeyCaptureService.EvaluateTokens(new[] { "RAlt", "Esc" }),
                HotkeyCaptureResultKind.Failed,
                "reservedKey",
                string.Empty,
                string.Empty);
            AssertCaptureResult(
                HotkeyCaptureService.EvaluateTokens(new[] { "WheelUp" }),
                HotkeyCaptureResultKind.Failed,
                "unsupportedToken",
                string.Empty,
                string.Empty);
        }

        private static void UnifiedHotkeyCaptureReadsLeftRightModifiersNumpadAndMouse()
        {
            var down = new HashSet<int>();
            var session = new HotkeyCaptureSession();
            HotkeyCaptureService.Seed(session, down.Contains);

            down.Add(0xA3);
            down.Add(0x61);
            var result = HotkeyCaptureService.Update(session, down.Contains);
            AssertCaptureResult(result, HotkeyCaptureResultKind.Captured, "captured", "RCtrl+Num1", "RCtrl + Num1");

            down.Clear();
            HotkeyCaptureService.Update(session, down.Contains);
            down.Add(0xA4);
            down.Add(0x05);
            result = HotkeyCaptureService.Update(session, down.Contains);
            AssertCaptureResult(result, HotkeyCaptureResultKind.Captured, "captured", "LAlt+MouseX1", "LAlt + MouseX1");
        }

        private static void UnifiedHotkeyCaptureSeedPreventsStarterMouseClick()
        {
            var down = new HashSet<int> { 0x01 };
            var session = new HotkeyCaptureSession();
            HotkeyCaptureService.Seed(session, down.Contains);

            var result = HotkeyCaptureService.Update(session, down.Contains);
            AssertCaptureResult(result, HotkeyCaptureResultKind.None, "none", string.Empty, string.Empty);

            down.Clear();
            HotkeyCaptureService.Update(session, down.Contains);
            down.Add(0x01);
            result = HotkeyCaptureService.Update(session, down.Contains);
            AssertCaptureResult(result, HotkeyCaptureResultKind.Captured, "captured", "MouseLeft", "MouseLeft");
        }

        private static void UnifiedHotkeyUiHelpersReadNewBindingsInPlace()
        {
            var restore = PushTemporaryConfigDirectory("unified-hotkeys-ui");
            try
            {
                ConfigService.Initialize();
                UnifiedHotkeyBindingUpdateResult result;
                if (!ConfigService.TrySaveUnifiedHotkeyBinding(
                        UnifiedHotkeyBindingIds.ForFeatureToggleTarget("buff.auto_heal"),
                        "RCtrl+NumPlus",
                        out result))
                {
                    throw new InvalidOperationException("Expected feature toggle unified binding to save.");
                }

                if (!ConfigService.TrySaveUnifiedHotkeyBinding(
                        UnifiedHotkeyBindingIds.ForQuickItemSlot(0),
                        "LAlt+MouseX1",
                        out result))
                {
                    throw new InvalidOperationException("Expected quick item unified binding to save.");
                }

                AssertStringEquals(
                    LegacyMainWindow.GetUnifiedHotkeyDisplayForTesting(UnifiedHotkeyBindingIds.ForFeatureToggleTarget("buff.auto_heal")),
                    "RCtrl + Num+",
                    "feature toggle unified display");
                AssertStringEquals(
                    LegacyMainWindow.GetUnifiedHotkeyDisplayForTesting(UnifiedHotkeyBindingIds.ForQuickItemSlot(0)),
                    "LAlt + MouseX1",
                    "quick item unified display");
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void UnifiedHotkeyUiReasonMessagesUsePlayerReadableCopy()
        {
            AssertStringEquals(
                LegacyMainWindow.BuildUnifiedHotkeyCancelledMessageForTesting(),
                "已取消录入",
                "cancelled visible message");
            AssertStringEquals(
                LegacyMainWindow.BuildUnifiedHotkeyFailureMessageForTesting("reservedKey"),
                "这个键保留给系统功能",
                "reserved key visible message");
            AssertStringEquals(
                LegacyMainWindow.BuildUnifiedHotkeyFailureMessageForTesting("unsupportedToken"),
                "暂不支持这个按键",
                "unsupported token visible message");
            AssertStringEquals(
                LegacyMainWindow.BuildUnifiedHotkeyFailureMessageForTesting("missingPrimaryKey"),
                "需要一个主键",
                "missing primary visible message");
            AssertStringEquals(
                LegacyMainWindow.BuildUnifiedHotkeyFailureMessageForTesting("tooManyPrimaryKeys"),
                "主键过多",
                "too many primary visible message");
            var conflictMessage = LegacyMainWindow.BuildUnifiedHotkeyFailureMessageForTesting("conflictWith:快捷物品 1");
            AssertStringEquals(conflictMessage, "与 快捷物品 1 冲突", "conflict visible message");
            AssertDoesNotContain(conflictMessage, "conflictWith:");

            var settings = CreateEmptyUnifiedHotkeySettings();
            UnifiedHotkeyBindingUpdateResult update;
            if (settings.TrySetBinding(UnifiedHotkeyBindingIds.ForQuickItemSlot(0), string.Empty, out update))
            {
                AssertStringEquals(
                    LegacyMainWindow.BuildUnifiedHotkeyUpdateMessageForTesting(update),
                    "当前未绑定",
                    "empty binding visible message");
            }
            else
            {
                throw new InvalidOperationException("Expected empty binding clear to be accepted.");
            }

            var saveFailed = LegacyMainWindow.BuildUnifiedHotkeyUpdateMessageForTesting(
                UnifiedHotkeyBindingUpdateResult.SaveFailed("test.binding", "disk busy"));
            AssertStringEquals(
                saveFailed,
                "保存失败：disk busy",
                "save failed visible message");
            AssertDoesNotContain(
                saveFailed,
                "saveFailed");
            AssertStringEquals(
                LegacyMainWindow.BuildUnifiedHotkeyUpdateMessageForTesting(null),
                "保存失败",
                "null update failure visible message");
            AssertDoesNotContain(
                LegacyMainWindow.BuildUnifiedHotkeyUpdateMessageForTesting(
                    UnifiedHotkeyBindingUpdateResult.Cleared("test.binding", false)),
                "emptyBinding");
        }

        private static void AssertCaptureResult(
            HotkeyCaptureResult result,
            HotkeyCaptureResultKind expectedKind,
            string expectedResultCode,
            string expectedNormalized,
            string expectedDisplay)
        {
            if (result == null)
            {
                throw new InvalidOperationException("Expected hotkey capture result.");
            }

            if (result.Kind != expectedKind)
            {
                throw new InvalidOperationException("Expected capture kind " + expectedKind + ", got " + result.Kind + ".");
            }

            AssertStringEquals(result.ResultCode, expectedResultCode, "capture result code");
            AssertStringEquals(result.Normalized, expectedNormalized, "capture normalized");
            AssertStringEquals(result.Display, expectedDisplay, "capture display");
        }
    }
}
