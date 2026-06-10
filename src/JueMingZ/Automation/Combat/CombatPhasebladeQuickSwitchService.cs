using System;
using System.Collections.Generic;
using JueMingZ.Config;

namespace JueMingZ.Automation.Combat
{
    internal static class CombatPhasebladeQuickSwitchService
    {
        public const int HotbarSlotCount = 10;
        public const int RequiredEligibleHotbarCount = 2;

        private static readonly int[] EligibleItemTypes =
        {
            198, 199, 200, 201, 202, 203,
            3764, 3765, 3766, 3767, 3768, 3769,
            4258, 4259,
            5535, 5536,
            5670, 5671
        };

        public static int EligibleItemTypeCount
        {
            get { return EligibleItemTypes.Length; }
        }

        public static bool IsEligibleItemType(int itemType)
        {
            // Terraria 1.4.5.6 ShootsOnUseRelease also contains Keybrand,
            // Antlion Claw, and Stylist scissors; quick switch must stay on
            // the audited 18 Phaseblade / Phasesaber item IDs only.
            switch (itemType)
            {
                case 198:
                case 199:
                case 200:
                case 201:
                case 202:
                case 203:
                case 3764:
                case 3765:
                case 3766:
                case 3767:
                case 3768:
                case 3769:
                case 4258:
                case 4259:
                case 5535:
                case 5536:
                case 5670:
                case 5671:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsEligibleHotbarItem(int itemType, int stack)
        {
            return stack > 0 && IsEligibleItemType(itemType);
        }

        public static int CopyEligibleItemTypesForTesting(int[] destination)
        {
            if (destination == null)
            {
                return 0;
            }

            var count = Math.Min(destination.Length, EligibleItemTypes.Length);
            Array.Copy(EligibleItemTypes, destination, count);
            return count;
        }

        public static int FindEligibleHotbarSlots(IList<int> hotbarItemTypes, int[] destination)
        {
            if (hotbarItemTypes == null || destination == null || destination.Length == 0)
            {
                return 0;
            }

            var count = 0;
            var limit = Math.Min(HotbarSlotCount, hotbarItemTypes.Count);
            for (var slot = 0; slot < limit && count < destination.Length; slot++)
            {
                if (IsEligibleItemType(hotbarItemTypes[slot]))
                {
                    destination[count++] = slot;
                }
            }

            return count;
        }

        public static bool IsSelectedSlotEligible(int selectedSlot, int[] eligibleSlots, int eligibleSlotCount)
        {
            if (!IsHotbarSlot(selectedSlot))
            {
                return false;
            }

            for (var i = 0; i < eligibleSlotCount && eligibleSlots != null && i < eligibleSlots.Length; i++)
            {
                if (eligibleSlots[i] == selectedSlot)
                {
                    return true;
                }
            }

            return false;
        }

        public static int FindNextEligibleSlot(int selectedSlot, int[] eligibleSlots, int eligibleSlotCount)
        {
            if (!IsHotbarSlot(selectedSlot) || CountValidEligibleSlots(eligibleSlots, eligibleSlotCount) < RequiredEligibleHotbarCount)
            {
                return -1;
            }

            for (var offset = 1; offset < HotbarSlotCount; offset++)
            {
                var candidate = (selectedSlot + offset) % HotbarSlotCount;
                if (IsSelectedSlotEligible(candidate, eligibleSlots, eligibleSlotCount))
                {
                    return candidate;
                }
            }

            return -1;
        }

        public static CombatPhasebladeQuickSwitchDecision Decide(
            CombatPhasebladeQuickSwitchState current,
            CombatPhasebladeQuickSwitchFrame frame)
        {
            current = current ?? CombatPhasebladeQuickSwitchState.Idle();
            frame = frame ?? CombatPhasebladeQuickSwitchFrame.Empty();

            string resetReason;
            if (!TryValidateActiveFrame(frame, out resetReason))
            {
                return CombatPhasebladeQuickSwitchDecision.Reset(resetReason);
            }

            var state = string.IsNullOrWhiteSpace(current.State)
                ? PhasebladeQuickSwitchStates.Idle
                : current.State;

            if (string.Equals(state, PhasebladeQuickSwitchStates.PressCurrent, StringComparison.Ordinal))
            {
                return CombatPhasebladeQuickSwitchDecision.CreateReleaseCurrent("releaseCurrent");
            }

            if (string.Equals(state, PhasebladeQuickSwitchStates.ReleaseCurrent, StringComparison.Ordinal))
            {
                var nextSlot = FindNextEligibleSlot(frame.SelectedSlot, frame.EligibleSlots, frame.EligibleSlotCount);
                return nextSlot >= 0
                    ? CombatPhasebladeQuickSwitchDecision.CreateSwitchNext(nextSlot, "switchNext")
                    : CombatPhasebladeQuickSwitchDecision.Reset("nextSlotUnavailable");
            }

            if (string.Equals(state, PhasebladeQuickSwitchStates.SwitchNext, StringComparison.Ordinal))
            {
                if (IsHotbarSlot(current.ExpectedSelectedSlot) && frame.SelectedSlot != current.ExpectedSelectedSlot)
                {
                    return CombatPhasebladeQuickSwitchDecision.SwitchPending(current.ExpectedSelectedSlot, "switchPending");
                }

                var waitUntilTick = frame.Tick + CombatPhasebladeQuickSwitchSettings.NormalizeIntervalTicks(frame.IntervalTicks);
                return CombatPhasebladeQuickSwitchDecision.WaitInterval(waitUntilTick, "switchConfirmed");
            }

            if (string.Equals(state, PhasebladeQuickSwitchStates.WaitInterval, StringComparison.Ordinal))
            {
                var waitUntilTick = current.WaitUntilTick > 0 ? current.WaitUntilTick : frame.Tick;
                if (frame.Tick < waitUntilTick)
                {
                    return CombatPhasebladeQuickSwitchDecision.WaitInterval(waitUntilTick, "intervalWait");
                }

                return frame.ItemReady
                    ? CombatPhasebladeQuickSwitchDecision.CreatePressCurrent("pressCurrent")
                    : CombatPhasebladeQuickSwitchDecision.WaitInterval(waitUntilTick, "itemNotReady");
            }

            return frame.ItemReady
                ? CombatPhasebladeQuickSwitchDecision.CreatePressCurrent("pressCurrent")
                : CombatPhasebladeQuickSwitchDecision.NoOp(PhasebladeQuickSwitchStates.Idle, "itemNotReady", CombatPhasebladeQuickSwitchState.Idle());
        }

        private static bool TryValidateActiveFrame(CombatPhasebladeQuickSwitchFrame frame, out string reason)
        {
            if (!frame.Enabled)
            {
                reason = "disabled";
                return false;
            }

            if (!frame.RightHeld)
            {
                reason = "rightNotHeld";
                return false;
            }

            if (!frame.SafeContext)
            {
                reason = "unsafeContext";
                return false;
            }

            if (!IsHotbarSlot(frame.SelectedSlot))
            {
                reason = "invalidSelectedSlot";
                return false;
            }

            if (CountValidEligibleSlots(frame.EligibleSlots, frame.EligibleSlotCount) < RequiredEligibleHotbarCount)
            {
                reason = "notEnoughPhaseblades";
                return false;
            }

            if (!IsSelectedSlotEligible(frame.SelectedSlot, frame.EligibleSlots, frame.EligibleSlotCount))
            {
                reason = "currentNotPhaseblade";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static int CountValidEligibleSlots(int[] eligibleSlots, int eligibleSlotCount)
        {
            if (eligibleSlots == null || eligibleSlotCount <= 0)
            {
                return 0;
            }

            var count = 0;
            var limit = Math.Min(eligibleSlotCount, eligibleSlots.Length);
            for (var i = 0; i < limit; i++)
            {
                if (IsHotbarSlot(eligibleSlots[i]) && !ContainsSlotBefore(eligibleSlots, i, eligibleSlots[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ContainsSlotBefore(int[] slots, int exclusiveEnd, int slot)
        {
            for (var i = 0; i < exclusiveEnd && i < slots.Length; i++)
            {
                if (slots[i] == slot)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHotbarSlot(int slot)
        {
            return slot >= 0 && slot < HotbarSlotCount;
        }
    }

    internal static class PhasebladeQuickSwitchStates
    {
        public const string Idle = "Idle";
        public const string PressCurrent = "PressCurrent";
        public const string ReleaseCurrent = "ReleaseCurrent";
        public const string SwitchNext = "SwitchNext";
        public const string WaitInterval = "WaitInterval";
    }

    internal sealed class CombatPhasebladeQuickSwitchFrame
    {
        public bool Enabled { get; set; }
        public bool RightHeld { get; set; }
        public bool SafeContext { get; set; }
        public int SelectedSlot { get; set; }
        public int[] EligibleSlots { get; set; }
        public int EligibleSlotCount { get; set; }
        public bool ItemReady { get; set; }
        public long Tick { get; set; }
        public int IntervalTicks { get; set; }

        public CombatPhasebladeQuickSwitchFrame()
        {
            SelectedSlot = -1;
            EligibleSlots = new int[0];
            IntervalTicks = CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks;
        }

        public static CombatPhasebladeQuickSwitchFrame Empty()
        {
            return new CombatPhasebladeQuickSwitchFrame();
        }
    }

    internal sealed class CombatPhasebladeQuickSwitchState
    {
        private CombatPhasebladeQuickSwitchState(string state, long waitUntilTick, int expectedSelectedSlot)
        {
            State = string.IsNullOrWhiteSpace(state) ? PhasebladeQuickSwitchStates.Idle : state;
            WaitUntilTick = waitUntilTick;
            ExpectedSelectedSlot = expectedSelectedSlot;
        }

        public string State { get; private set; }
        public long WaitUntilTick { get; private set; }
        public int ExpectedSelectedSlot { get; private set; }

        public static CombatPhasebladeQuickSwitchState Idle()
        {
            return new CombatPhasebladeQuickSwitchState(PhasebladeQuickSwitchStates.Idle, 0, -1);
        }

        public static CombatPhasebladeQuickSwitchState ForState(string state)
        {
            return new CombatPhasebladeQuickSwitchState(state, 0, -1);
        }

        public static CombatPhasebladeQuickSwitchState Waiting(long waitUntilTick)
        {
            return new CombatPhasebladeQuickSwitchState(PhasebladeQuickSwitchStates.WaitInterval, waitUntilTick, -1);
        }

        public static CombatPhasebladeQuickSwitchState SwitchPending(int expectedSelectedSlot)
        {
            return new CombatPhasebladeQuickSwitchState(PhasebladeQuickSwitchStates.SwitchNext, 0, expectedSelectedSlot);
        }
    }

    internal sealed class CombatPhasebladeQuickSwitchDecision
    {
        private CombatPhasebladeQuickSwitchDecision(
            string state,
            string reason,
            bool pressCurrent,
            bool releaseCurrent,
            bool switchNext,
            bool resetState,
            int targetSlot,
            long waitUntilTick,
            CombatPhasebladeQuickSwitchState nextState)
        {
            State = state ?? string.Empty;
            Reason = reason ?? string.Empty;
            PressCurrent = pressCurrent;
            ReleaseCurrent = releaseCurrent;
            SwitchNext = switchNext;
            ResetState = resetState;
            TargetSlot = targetSlot;
            WaitUntilTick = waitUntilTick;
            NextState = nextState ?? CombatPhasebladeQuickSwitchState.Idle();
        }

        public string State { get; private set; }
        public string Reason { get; private set; }
        public bool PressCurrent { get; private set; }
        public bool ReleaseCurrent { get; private set; }
        public bool SwitchNext { get; private set; }
        public bool ResetState { get; private set; }
        public int TargetSlot { get; private set; }
        public long WaitUntilTick { get; private set; }
        public CombatPhasebladeQuickSwitchState NextState { get; private set; }

        public static CombatPhasebladeQuickSwitchDecision NoOp(
            string state,
            string reason,
            CombatPhasebladeQuickSwitchState nextState)
        {
            return new CombatPhasebladeQuickSwitchDecision(state, reason, false, false, false, false, -1, 0, nextState);
        }

        public static CombatPhasebladeQuickSwitchDecision Reset(string reason)
        {
            return new CombatPhasebladeQuickSwitchDecision(
                PhasebladeQuickSwitchStates.Idle,
                reason,
                false,
                false,
                false,
                true,
                -1,
                0,
                CombatPhasebladeQuickSwitchState.Idle());
        }

        public static CombatPhasebladeQuickSwitchDecision CreatePressCurrent(string reason)
        {
            return new CombatPhasebladeQuickSwitchDecision(
                PhasebladeQuickSwitchStates.PressCurrent,
                reason,
                true,
                false,
                false,
                false,
                -1,
                0,
                CombatPhasebladeQuickSwitchState.ForState(PhasebladeQuickSwitchStates.PressCurrent));
        }

        public static CombatPhasebladeQuickSwitchDecision CreateReleaseCurrent(string reason)
        {
            return new CombatPhasebladeQuickSwitchDecision(
                PhasebladeQuickSwitchStates.ReleaseCurrent,
                reason,
                false,
                true,
                false,
                false,
                -1,
                0,
                CombatPhasebladeQuickSwitchState.ForState(PhasebladeQuickSwitchStates.ReleaseCurrent));
        }

        public static CombatPhasebladeQuickSwitchDecision CreateSwitchNext(int targetSlot, string reason)
        {
            return new CombatPhasebladeQuickSwitchDecision(
                PhasebladeQuickSwitchStates.SwitchNext,
                reason,
                false,
                false,
                true,
                false,
                targetSlot,
                0,
                CombatPhasebladeQuickSwitchState.SwitchPending(targetSlot));
        }

        public static CombatPhasebladeQuickSwitchDecision SwitchPending(int targetSlot, string reason)
        {
            return new CombatPhasebladeQuickSwitchDecision(
                PhasebladeQuickSwitchStates.SwitchNext,
                reason,
                false,
                false,
                false,
                false,
                targetSlot,
                0,
                CombatPhasebladeQuickSwitchState.SwitchPending(targetSlot));
        }

        public static CombatPhasebladeQuickSwitchDecision WaitInterval(long waitUntilTick, string reason)
        {
            return new CombatPhasebladeQuickSwitchDecision(
                PhasebladeQuickSwitchStates.WaitInterval,
                reason,
                false,
                false,
                false,
                false,
                -1,
                waitUntilTick,
                CombatPhasebladeQuickSwitchState.Waiting(waitUntilTick));
        }
    }
}
