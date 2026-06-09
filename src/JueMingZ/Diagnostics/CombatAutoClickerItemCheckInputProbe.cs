using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;

namespace JueMingZ.Diagnostics
{
    internal static class CombatAutoClickerItemCheckInputProbe
    {
        private const int LifeCrystalItemType = 29;
        private const int RainbowRodItemType = 495;
        private const int RodOfHarmonyItemType = 5335;
        private const int MinRecordIntervalMs = 250;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, ProbeThrottleState> LastRecords =
            new Dictionary<string, ProbeThrottleState>(StringComparer.Ordinal);

        public static ItemCheckInputProbeFrame CapturePrefix(object player, bool bridgePendingAtStart)
        {
            return Capture(player, "prefix", bridgePendingAtStart, false);
        }

        public static void RecordPostfix(
            object player,
            ItemCheckInputProbeFrame before,
            bool bridgePendingAtStart,
            bool bridgeApplied,
            Guid bridgeRequestId,
            string bridgeSourceFeatureId,
            bool pulseApplied,
            bool pulsePressed,
            Guid pulseRequestId,
            bool autoMiningApplied,
            Guid autoMiningRequestId,
            bool autoHarvestApplied,
            Guid autoHarvestRequestId,
            bool autoCaptureApplied,
            Guid autoCaptureRequestId,
            bool autoClickerApplied,
            bool perfectRevolverApplied,
            bool flailApplied,
            bool travelMenuGuardApplied,
            bool aimApplied)
        {
            var anyScopedWriter = bridgeApplied ||
                                   pulseApplied ||
                                   autoMiningApplied ||
                                   autoHarvestApplied ||
                                   autoCaptureApplied ||
                                   autoClickerApplied ||
                                   perfectRevolverApplied ||
                                   flailApplied ||
                                  travelMenuGuardApplied;
            var after = Capture(player, "postfix-before-restore", bridgePendingAtStart, before != null && before.Captured);
            if (!ShouldRecord(before, after, anyScopedWriter))
            {
                return;
            }

            var verification = BuildVerificationJson(
                before,
                after,
                bridgePendingAtStart,
                bridgeApplied,
                bridgeRequestId,
                bridgeSourceFeatureId,
                pulseApplied,
                pulsePressed,
                pulseRequestId,
                autoMiningApplied,
                autoMiningRequestId,
                autoHarvestApplied,
                autoHarvestRequestId,
                autoCaptureApplied,
                autoCaptureRequestId,
                autoClickerApplied,
                perfectRevolverApplied,
                flailApplied,
                travelMenuGuardApplied,
                aimApplied,
                anyScopedWriter);
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                ScenarioNames.CombatAutoClickerItemCheckInputProbe,
                "ItemCheckInputProbe",
                string.Empty,
                "Observed",
                DiagnosticResultCode.NotApplicable.ToString(),
                BuildMessage(before, after, anyScopedWriter),
                0,
                BuildFrameJson(before),
                BuildFrameJson(after),
                verification,
                "Hook",
                "Player.ItemCheck",
                string.Empty,
                string.Empty);
        }

        internal static bool IsProbeItemForTesting(int itemType)
        {
            return IsProbeItem(itemType);
        }

        internal static string BuildAnimationBucketForTesting(int value)
        {
            return BuildAnimationBucket(value);
        }

        private static ItemCheckInputProbeFrame Capture(object player, string stage, bool bridgePendingAtStart, bool force)
        {
            var frame = new ItemCheckInputProbeFrame
            {
                Stage = stage ?? string.Empty,
                BridgePendingAtStart = bridgePendingAtStart
            };

            if (player == null)
            {
                frame.CaptureError = "playerUnavailable";
                return frame;
            }

            try
            {
                int selectedSlot;
                if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
                {
                    frame.CaptureError = "selectedSlotUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                    return frame;
                }

                frame.SelectedSlot = selectedSlot;
                ItemUseVerificationState itemState;
                if (TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out itemState))
                {
                    ApplyItemState(frame, itemState);
                }
                else
                {
                    frame.CaptureError = "itemStateUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                }

                if (!force && !IsProbeItem(frame.ItemType))
                {
                    return frame;
                }

                UseItemInputState input;
                if (TerrariaInputCompat.TryCaptureUseItemInputState(player, out input))
                {
                    ApplyInputState(frame, input);
                }
                else
                {
                    frame.InputCaptureError = TerrariaInputCompat.LastInputCompatError;
                }

                bool delayUseItem;
                if (TerrariaInputCompat.TryReadDelayUseItem(player, out delayUseItem))
                {
                    frame.DelayUseItemCaptured = true;
                    frame.DelayUseItem = delayUseItem;
                }

                bool physicalMouseLeftHeld;
                if (TerrariaInputCompat.TryReadPhysicalMouseLeftHeld(out physicalMouseLeftHeld))
                {
                    frame.PhysicalMouseLeftHeldCaptured = true;
                    frame.PhysicalMouseLeftHeld = physicalMouseLeftHeld;
                }

                long gameUpdateCount;
                if (TerrariaInputCompat.TryReadGameUpdateCount(out gameUpdateCount))
                {
                    frame.GameUpdateCountCaptured = true;
                    frame.GameUpdateCount = gameUpdateCount;
                }

                frame.Captured = frame.ItemCaptured || frame.InputCaptured;
                return frame;
            }
            catch (Exception error)
            {
                frame.CaptureError = "exception:" + error.GetType().Name;
                RuntimeDiagnostics.RecordError("CombatAutoClickerItemCheckInputProbe.Capture", error);
                return frame;
            }
        }

        private static void ApplyItemState(ItemCheckInputProbeFrame frame, ItemUseVerificationState state)
        {
            if (frame == null || state == null)
            {
                return;
            }

            frame.ItemCaptured = true;
            frame.SelectedSlot = state.SelectedSlot;
            frame.ItemType = state.ItemType;
            frame.ItemName = state.ItemName ?? string.Empty;
            frame.ItemStack = state.ItemStack;
            frame.ItemAnimation = state.ItemAnimation;
            frame.ItemTime = state.ItemTime;
            frame.ReuseDelay = state.ReuseDelay;
            frame.UseStyle = state.UseStyle;
            frame.Consumable = state.Consumable;
            frame.Mana = state.Mana;
            frame.ManaMax = state.ManaMax;
        }

        private static void ApplyInputState(ItemCheckInputProbeFrame frame, UseItemInputState state)
        {
            if (frame == null || state == null || !state.Captured)
            {
                return;
            }

            frame.InputCaptured = true;
            frame.UseItemHeld = state.UseItemHeld;
            frame.UseItemReleased = state.UseItemReleased;
            frame.MainMouseLeftCaptured = state.MainMouseLeftCaptured;
            frame.MainMouseLeft = state.MainMouseLeft;
            frame.MainMouseLeftReleaseCaptured = state.MainMouseLeftReleaseCaptured;
            frame.MainMouseLeftRelease = state.MainMouseLeftRelease;
            frame.MainMouseRightCaptured = state.MainMouseRightCaptured;
            frame.MainMouseRight = state.MainMouseRight;
            frame.MainMouseRightReleaseCaptured = state.MainMouseRightReleaseCaptured;
            frame.MainMouseRightRelease = state.MainMouseRightRelease;
        }

        private static bool ShouldRecord(ItemCheckInputProbeFrame before, ItemCheckInputProbeFrame after, bool anyScopedWriter)
        {
            // Probe events are throttled observations; ItemCheck sampling must not become a per-frame action log.
            var itemType = ResolveItemType(before, after);
            var interesting = anyScopedWriter || IsProbeItem(itemType) || IsSuspiciousFreshClick(before) || IsSuspiciousFreshClick(after);
            if (!interesting)
            {
                return false;
            }

            var key = itemType.ToString(CultureInfo.InvariantCulture) + ":" + (anyScopedWriter ? "writer" : "observe");
            var signature = BuildSignature(before, after, anyScopedWriter);
            var now = DateTime.UtcNow;
            lock (SyncRoot)
            {
                ProbeThrottleState previous;
                if (LastRecords.TryGetValue(key, out previous) &&
                    string.Equals(previous.Signature, signature, StringComparison.Ordinal) &&
                    (now - previous.RecordedUtc).TotalMilliseconds < MinRecordIntervalMs)
                {
                    return false;
                }

                LastRecords[key] = new ProbeThrottleState(signature, now);
                return true;
            }
        }

        private static int ResolveItemType(ItemCheckInputProbeFrame before, ItemCheckInputProbeFrame after)
        {
            if (before != null && before.ItemType > 0)
            {
                return before.ItemType;
            }

            return after == null ? 0 : after.ItemType;
        }

        private static bool IsProbeItem(int itemType)
        {
            return itemType == LifeCrystalItemType ||
                   itemType == RainbowRodItemType ||
                   itemType == RodOfHarmonyItemType;
        }

        private static bool IsSuspiciousFreshClick(ItemCheckInputProbeFrame frame)
        {
            if (frame == null || !frame.InputCaptured)
            {
                return false;
            }

            return (frame.UseItemHeld && frame.UseItemReleased) ||
                   (frame.MainMouseLeftCaptured &&
                    frame.MainMouseLeft &&
                    frame.MainMouseLeftReleaseCaptured &&
                    frame.MainMouseLeftRelease);
        }

        private static string BuildSignature(ItemCheckInputProbeFrame before, ItemCheckInputProbeFrame after, bool anyScopedWriter)
        {
            var builder = new StringBuilder();
            AppendFrameSignature(builder, before);
            builder.Append("|");
            AppendFrameSignature(builder, after);
            builder.Append("|writer=").Append(anyScopedWriter ? "1" : "0");
            return builder.ToString();
        }

        private static void AppendFrameSignature(StringBuilder builder, ItemCheckInputProbeFrame frame)
        {
            if (frame == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append(frame.Stage).Append(":")
                .Append(frame.ItemType.ToString(CultureInfo.InvariantCulture)).Append(":")
                .Append(frame.SelectedSlot.ToString(CultureInfo.InvariantCulture)).Append(":")
                .Append(frame.UseItemHeld ? "H" : "h").Append(frame.UseItemReleased ? "R" : "r").Append(":")
                .Append(frame.MainMouseLeft ? "L" : "l").Append(frame.MainMouseLeftRelease ? "F" : "f").Append(":")
                .Append(BuildAnimationBucket(frame.ItemAnimation)).Append(":")
                .Append(BuildAnimationBucket(frame.ItemTime)).Append(":")
                .Append(frame.DelayUseItem ? "D" : "d");
        }

        private static string BuildAnimationBucket(int value)
        {
            if (value <= 0)
            {
                return "0";
            }

            if (value <= 2)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }

            if (value <= 5)
            {
                return "3-5";
            }

            if (value <= 10)
            {
                return "6-10";
            }

            if (value <= 20)
            {
                return "11-20";
            }

            return "21+";
        }

        private static string BuildMessage(ItemCheckInputProbeFrame before, ItemCheckInputProbeFrame after, bool anyScopedWriter)
        {
            var itemType = ResolveItemType(before, after);
            return "ItemCheck input probe captured item " +
                   itemType.ToString(CultureInfo.InvariantCulture) +
                   (anyScopedWriter ? " with a JueMing scoped writer applied." : " with no JueMing scoped writer applied.");
        }

        private static string BuildFrameJson(ItemCheckInputProbeFrame frame)
        {
            if (frame == null)
            {
                return "{}";
            }

            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "stage", frame.Stage, true);
            AppendRaw(builder, "captured", BoolRaw(frame.Captured), true);
            AppendRaw(builder, "itemCaptured", BoolRaw(frame.ItemCaptured), true);
            AppendRaw(builder, "inputCaptured", BoolRaw(frame.InputCaptured), true);
            AppendRaw(builder, "gameUpdateCountCaptured", BoolRaw(frame.GameUpdateCountCaptured), true);
            AppendRaw(builder, "gameUpdateCount", frame.GameUpdateCount.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "selectedSlot", SlotRaw(frame.SelectedSlot), true);
            AppendRaw(builder, "selectedSlotDisplay", SlotDisplayRaw(frame.SelectedSlot), true);
            AppendRaw(builder, "itemType", frame.ItemType.ToString(CultureInfo.InvariantCulture), true);
            AppendString(builder, "itemName", frame.ItemName, true);
            AppendRaw(builder, "itemStack", frame.ItemStack.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "itemAnimation", frame.ItemAnimation.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "itemAnimationBucket", "\"" + EscapeJson(BuildAnimationBucket(frame.ItemAnimation)) + "\"", true);
            AppendRaw(builder, "itemTime", frame.ItemTime.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "itemTimeBucket", "\"" + EscapeJson(BuildAnimationBucket(frame.ItemTime)) + "\"", true);
            AppendRaw(builder, "reuseDelay", frame.ReuseDelay.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "useStyle", frame.UseStyle.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "consumable", BoolRaw(frame.Consumable), true);
            AppendRaw(builder, "mana", frame.Mana.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "manaMax", frame.ManaMax.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "itemUseHeld", BoolRaw(frame.UseItemHeld), true);
            AppendRaw(builder, "itemUseReleased", BoolRaw(frame.UseItemReleased), true);
            AppendRaw(builder, "mainMouseLeftCaptured", BoolRaw(frame.MainMouseLeftCaptured), true);
            AppendRaw(builder, "mainMouseLeft", BoolRaw(frame.MainMouseLeft), true);
            AppendRaw(builder, "mainMouseLeftReleaseCaptured", BoolRaw(frame.MainMouseLeftReleaseCaptured), true);
            AppendRaw(builder, "mainMouseLeftRelease", BoolRaw(frame.MainMouseLeftRelease), true);
            AppendRaw(builder, "mainMouseRightCaptured", BoolRaw(frame.MainMouseRightCaptured), true);
            AppendRaw(builder, "mainMouseRight", BoolRaw(frame.MainMouseRight), true);
            AppendRaw(builder, "mainMouseRightReleaseCaptured", BoolRaw(frame.MainMouseRightReleaseCaptured), true);
            AppendRaw(builder, "mainMouseRightRelease", BoolRaw(frame.MainMouseRightRelease), true);
            AppendRaw(builder, "delayUseItemCaptured", BoolRaw(frame.DelayUseItemCaptured), true);
            AppendRaw(builder, "delayUseItem", BoolRaw(frame.DelayUseItem), true);
            AppendRaw(builder, "physicalMouseLeftHeldCaptured", BoolRaw(frame.PhysicalMouseLeftHeldCaptured), true);
            AppendRaw(builder, "physicalMouseLeftHeld", BoolRaw(frame.PhysicalMouseLeftHeld), true);
            AppendRaw(builder, "bridgePendingAtStart", BoolRaw(frame.BridgePendingAtStart), true);
            AppendRaw(builder, "suspiciousFreshClick", BoolRaw(IsSuspiciousFreshClick(frame)), true);
            AppendString(builder, "captureError", frame.CaptureError, true);
            AppendString(builder, "inputCaptureError", frame.InputCaptureError, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildVerificationJson(
            ItemCheckInputProbeFrame before,
            ItemCheckInputProbeFrame after,
            bool bridgePendingAtStart,
            bool bridgeApplied,
            Guid bridgeRequestId,
            string bridgeSourceFeatureId,
            bool pulseApplied,
            bool pulsePressed,
            Guid pulseRequestId,
            bool autoMiningApplied,
            Guid autoMiningRequestId,
            bool autoHarvestApplied,
            Guid autoHarvestRequestId,
            bool autoCaptureApplied,
            Guid autoCaptureRequestId,
            bool autoClickerApplied,
            bool perfectRevolverApplied,
            bool flailApplied,
            bool travelMenuGuardApplied,
            bool aimApplied,
            bool anyScopedWriter)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "probeItem", BoolRaw(IsProbeItem(ResolveItemType(before, after))), true);
            AppendRaw(builder, "suspiciousFreshClickBefore", BoolRaw(IsSuspiciousFreshClick(before)), true);
            AppendRaw(builder, "suspiciousFreshClickAfter", BoolRaw(IsSuspiciousFreshClick(after)), true);
            AppendRaw(builder, "anyJueMingScopedWriterApplied", BoolRaw(anyScopedWriter), true);
            AppendRaw(builder, "bridgePendingAtStart", BoolRaw(bridgePendingAtStart), true);
            AppendRaw(builder, "bridgeApplied", BoolRaw(bridgeApplied), true);
            AppendString(builder, "bridgeRequestId", bridgeRequestId == Guid.Empty ? string.Empty : bridgeRequestId.ToString(), true);
            AppendString(builder, "bridgeSourceFeatureId", bridgeSourceFeatureId, true);
            AppendRaw(builder, "pulseApplied", BoolRaw(pulseApplied), true);
            AppendRaw(builder, "pulsePressed", BoolRaw(pulsePressed), true);
            AppendString(builder, "pulseRequestId", pulseRequestId == Guid.Empty ? string.Empty : pulseRequestId.ToString(), true);
            AppendRaw(builder, "autoMiningApplied", BoolRaw(autoMiningApplied), true);
            AppendString(builder, "autoMiningRequestId", autoMiningRequestId == Guid.Empty ? string.Empty : autoMiningRequestId.ToString(), true);
            AppendRaw(builder, "autoHarvestApplied", BoolRaw(autoHarvestApplied), true);
            AppendString(builder, "autoHarvestRequestId", autoHarvestRequestId == Guid.Empty ? string.Empty : autoHarvestRequestId.ToString(), true);
            AppendRaw(builder, "autoCaptureApplied", BoolRaw(autoCaptureApplied), true);
            AppendString(builder, "autoCaptureRequestId", autoCaptureRequestId == Guid.Empty ? string.Empty : autoCaptureRequestId.ToString(), true);
            AppendRaw(builder, "autoClickerApplied", BoolRaw(autoClickerApplied), true);
            AppendRaw(builder, "perfectRevolverApplied", BoolRaw(perfectRevolverApplied), true);
            AppendRaw(builder, "flailApplied", BoolRaw(flailApplied), true);
            AppendRaw(builder, "travelMenuGuardApplied", BoolRaw(travelMenuGuardApplied), true);
            AppendRaw(builder, "aimApplied", BoolRaw(aimApplied), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":\"").Append(EscapeJson(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string SlotRaw(int slot)
        {
            return TerrariaInputCompat.IsSupportedItemUseSlot(slot)
                ? slot.ToString(CultureInfo.InvariantCulture)
                : "null";
        }

        private static string SlotDisplayRaw(int slot)
        {
            return TerrariaInputCompat.IsSupportedItemUseSlot(slot)
                ? (slot + 1).ToString(CultureInfo.InvariantCulture)
                : "null";
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private sealed class ProbeThrottleState
        {
            public ProbeThrottleState(string signature, DateTime recordedUtc)
            {
                Signature = signature ?? string.Empty;
                RecordedUtc = recordedUtc;
            }

            public readonly string Signature;
            public readonly DateTime RecordedUtc;
        }
    }

    internal sealed class ItemCheckInputProbeFrame
    {
        public string Stage { get; set; }
        public bool Captured { get; set; }
        public bool ItemCaptured { get; set; }
        public bool InputCaptured { get; set; }
        public bool GameUpdateCountCaptured { get; set; }
        public long GameUpdateCount { get; set; }
        public int SelectedSlot { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int ItemStack { get; set; }
        public int ItemAnimation { get; set; }
        public int ItemTime { get; set; }
        public int ReuseDelay { get; set; }
        public int UseStyle { get; set; }
        public bool Consumable { get; set; }
        public int Mana { get; set; }
        public int ManaMax { get; set; }
        public bool UseItemHeld { get; set; }
        public bool UseItemReleased { get; set; }
        public bool MainMouseLeftCaptured { get; set; }
        public bool MainMouseLeft { get; set; }
        public bool MainMouseLeftReleaseCaptured { get; set; }
        public bool MainMouseLeftRelease { get; set; }
        public bool MainMouseRightCaptured { get; set; }
        public bool MainMouseRight { get; set; }
        public bool MainMouseRightReleaseCaptured { get; set; }
        public bool MainMouseRightRelease { get; set; }
        public bool DelayUseItemCaptured { get; set; }
        public bool DelayUseItem { get; set; }
        public bool PhysicalMouseLeftHeldCaptured { get; set; }
        public bool PhysicalMouseLeftHeld { get; set; }
        public bool BridgePendingAtStart { get; set; }
        public string CaptureError { get; set; }
        public string InputCaptureError { get; set; }

        public ItemCheckInputProbeFrame()
        {
            Stage = string.Empty;
            SelectedSlot = -1;
            ItemName = string.Empty;
            CaptureError = string.Empty;
            InputCaptureError = string.Empty;
        }
    }
}
