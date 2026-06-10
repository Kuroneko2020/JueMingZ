using System;
using System.Collections;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using JueMingZ.Automation.WorldAutomation;

namespace JueMingZ.Automation.Combat
{
    internal static class CombatPhasebladeQuickSwitchRuntimeService
    {
        private const string FeatureId = FeatureIds.CombatPhasebladeQuickSwitch;
        private const int QueueTimeoutMilliseconds = 150;
        private static readonly object DiagnosticsSyncRoot = new object();
        private static CombatPhasebladeQuickSwitchDiagnostics _diagnostics = new CombatPhasebladeQuickSwitchDiagnostics();

        public static CombatPhasebladeQuickSwitchDiagnostics GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                return _diagnostics == null ? new CombatPhasebladeQuickSwitchDiagnostics() : _diagnostics.Clone();
            }
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState, RuntimeSettingsSnapshot settings)
        {
            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            settings = settings ?? RuntimeSettingsSnapshotProvider.GetCurrent();

            try
            {
                if (settings == null || !settings.CombatPhasebladeQuickSwitchEnabled)
                {
                    CancelOwnAction(queue, "disabled");
                    RecordDecision("disabled", "disabled", tick, false, null, false);
                    return;
                }

                if (queue == null)
                {
                    RecordDecision("skipped", "queueUnavailable", tick, false, null, true);
                    return;
                }

                if (snapshot == null || !snapshot.IsInWorld)
                {
                    CancelOwnAction(queue, "notInWorld");
                    RecordDecision("skipped", "notInWorld", tick, false, null, true);
                    return;
                }

                if (CombatAutomationDecisionDiagnostics.IsBlockingSnapshotUiForCombat(snapshot.Ui))
                {
                    var snapshotUiReason = CombatAutomationDecisionDiagnostics.BuildSnapshotUiSkipReason(snapshot.Ui);
                    CancelOwnAction(queue, snapshotUiReason);
                    RecordDecision("skipped", snapshotUiReason, tick, false, null, true);
                    return;
                }

                var fast = queue.GetFastState();
                var ownRunning = fast != null &&
                                 fast.HasRunningAction &&
                                 fast.RunningActionKindValue == InputActionKind.RawInput &&
                                 string.Equals(fast.RunningActionSource, FeatureId, StringComparison.OrdinalIgnoreCase);

                PhasebladeQuickSwitchRuntimeProfile profile;
                string reason;
                if (!TryReadProfile(settings, out profile, out reason))
                {
                    if (ownRunning)
                    {
                        CancelOwnAction(queue, reason);
                    }

                    RecordDecision(ownRunning ? "cancelled" : "skipped", reason, tick, false, profile, true);
                    return;
                }

                if (ownRunning)
                {
                    RecordDecision("running", "alreadyRunning", tick, false, profile, true);
                    return;
                }

                if (fast != null &&
                    (fast.PendingCount > 0 || fast.HasRunningAction) &&
                    !PhasebladeQuickSwitchBridge.HasActiveUse)
                {
                    RecordDecision("skipped", "queueBusy", tick, false, profile, true);
                    return;
                }

                var request = BuildRequest(profile);
                InputActionAdmissionResult admission;
                if (!queue.TryEnqueue(request, out admission))
                {
                    RecordDecision("skipped", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, false, profile, true);
                    return;
                }

                RecordDecision("submitted", string.Empty, tick, true, profile, true);
            }
            catch (Exception error)
            {
                RecordDecision("exception", "exception:" + error.GetType().Name, tick, false, null, true);
                RuntimeDiagnostics.RecordError("CombatPhasebladeQuickSwitchRuntimeService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "combat-phaseblade-quick-switch-runtime-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatPhasebladeQuickSwitchRuntimeService",
                    "Combat phaseblade quick switch runtime tick failed; exception swallowed.", error);
            }
        }

        internal static bool TryReadProfileForTesting(RuntimeSettingsSnapshot settings, out PhasebladeQuickSwitchRuntimeProfile profile, out string reason)
        {
            return TryReadProfile(settings, out profile, out reason);
        }

        internal static InputActionRequest BuildRequestForTesting(PhasebladeQuickSwitchRuntimeProfile profile)
        {
            return BuildRequest(profile);
        }

        internal static void ResetDiagnosticsForTesting()
        {
            lock (DiagnosticsSyncRoot)
            {
                _diagnostics = new CombatPhasebladeQuickSwitchDiagnostics();
            }
        }

        private static bool TryReadProfile(RuntimeSettingsSnapshot settings, out PhasebladeQuickSwitchRuntimeProfile profile, out string reason)
        {
            profile = new PhasebladeQuickSwitchRuntimeProfile();
            reason = string.Empty;
            if (settings == null || !settings.CombatPhasebladeQuickSwitchEnabled)
            {
                reason = "disabled";
                return false;
            }

            profile.IntervalTicks = CombatPhasebladeQuickSwitchSettings.NormalizeIntervalTicks(settings.CombatPhasebladeQuickSwitchIntervalTicks);

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                reason = "localPlayerUnavailable";
                return false;
            }

            if (!TerrariaInputCompat.TryIsLocalPlayer(player))
            {
                reason = "notLocalPlayer";
                return false;
            }

            string playerStateReason;
            if (IsPlayerBlocked(player, out playerStateReason))
            {
                reason = playerStateReason;
                return false;
            }

            bool rightHeld;
            if (!TerrariaInputCompat.TryReadPhysicalMouseRightHeld(out rightHeld))
            {
                reason = "rightInputUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            profile.RightHeld = rightHeld;
            if (!rightHeld)
            {
                reason = "rightNotHeld";
                return false;
            }

            bool leftHeld;
            if (!TerrariaInputCompat.TryReadPhysicalMouseLeftHeld(out leftHeld))
            {
                reason = "leftInputUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            if (leftHeld)
            {
                reason = "physicalLeftHeld";
                return false;
            }

            string uiReason;
            if (IsUiBlocked(player, out uiReason))
            {
                reason = uiReason;
                return false;
            }

            string worldRightClickReason;
            if (TerrariaInputCompat.IsWorldRightClickInteractionActive(player, out worldRightClickReason))
            {
                reason = worldRightClickReason;
                return false;
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                reason = "selectedSlotUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            profile.SelectedSlot = selectedSlot;
            if (selectedSlot < 0 || selectedSlot >= CombatPhasebladeQuickSwitchService.HotbarSlotCount)
            {
                reason = "selectedSlotNotHotbar";
                return false;
            }

            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null || selectedSlot >= inventory.Count)
            {
                reason = "inventoryUnavailable";
                return false;
            }

            int selectedItemType;
            int selectedItemStack;
            string selectedItemName;
            if (!TryReadHotbarItem(inventory, selectedSlot, out selectedItemType, out selectedItemStack, out selectedItemName))
            {
                reason = "selectedItemUnavailable";
                return false;
            }

            profile.ItemType = selectedItemType;
            profile.ItemStack = selectedItemStack;
            profile.ItemName = selectedItemName;

            string vanillaRightClickReason;
            if (CombatFlailRuntime.HasVanillaRightClickSemantics(profile.ItemType, player, out vanillaRightClickReason))
            {
                reason = vanillaRightClickReason;
                return false;
            }

            var eligibleSlots = new int[CombatPhasebladeQuickSwitchService.HotbarSlotCount];
            profile.EligibleSlotCount = FindEligibleHotbarSlots(inventory, eligibleSlots);
            if (profile.EligibleSlotCount < CombatPhasebladeQuickSwitchService.RequiredEligibleHotbarCount)
            {
                reason = "notEnoughPhaseblades";
                return false;
            }

            if (!CombatPhasebladeQuickSwitchService.IsSelectedSlotEligible(selectedSlot, eligibleSlots, profile.EligibleSlotCount))
            {
                reason = "currentNotPhaseblade";
                return false;
            }

            profile.NextSlot = CombatPhasebladeQuickSwitchService.FindNextEligibleSlot(selectedSlot, eligibleSlots, profile.EligibleSlotCount);
            if (profile.NextSlot < 0)
            {
                reason = "nextSlotUnavailable";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static InputActionRequest BuildRequest(PhasebladeQuickSwitchRuntimeProfile profile)
        {
            profile = profile ?? new PhasebladeQuickSwitchRuntimeProfile();
            var request = new InputActionRequest
            {
                Kind = InputActionKind.RawInput,
                Priority = InputActionPriority.Normal,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureId,
                Description = "Combat phaseblade quick switch loops selected hotbar phaseblades while right click is held",
                QueueTimeout = TimeSpan.FromMilliseconds(QueueTimeoutMilliseconds),
                AdmissionKey = FeatureId + "|rightHeld",
                Timeout = TimeSpan.FromMinutes(15),
                IsExclusive = true
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.CombatPhasebladeQuickSwitch;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.RawInputMode] = "PhasebladeQuickSwitch";
            request.Metadata["AllowCombatAim"] = "true";
            request.Metadata["PhasebladeQuickSwitchIntervalTicks"] = profile.IntervalTicks.ToString(CultureInfo.InvariantCulture);
            request.Metadata["PhasebladeQuickSwitchSelectedSlot"] = profile.SelectedSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["PhasebladeQuickSwitchItemType"] = profile.ItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["PhasebladeQuickSwitchItemName"] = profile.ItemName ?? string.Empty;
            request.Metadata["PhasebladeQuickSwitchEligibleSlotCount"] = profile.EligibleSlotCount.ToString(CultureInfo.InvariantCulture);
            request.Metadata["PhasebladeQuickSwitchNextSlot"] = profile.NextSlot.ToString(CultureInfo.InvariantCulture);
            return request;
        }

        private static bool IsPlayerBlocked(object player, out string reason)
        {
            bool active;
            bool dead;
            bool ghost;
            GameStateReflection.TryGetBool(player, "active", out active);
            GameStateReflection.TryGetBool(player, "dead", out dead);
            GameStateReflection.TryGetBool(player, "ghost", out ghost);
            if (!active)
            {
                reason = "playerInactive";
                return true;
            }

            if (dead)
            {
                reason = "playerDead";
                return true;
            }

            if (ghost)
            {
                reason = "playerGhost";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private static bool IsUiBlocked(object player, out string reason)
        {
            var ui = TerrariaInputCompat.ReadUiInputContext(player);
            if (ui.MainTypeUnavailable)
            {
                reason = "mainTypeUnavailable";
                return true;
            }

            if (ui.GameMenu)
            {
                reason = "gameMenu";
                return true;
            }

            if (ui.ChatOpen)
            {
                reason = "chatOpen";
                return true;
            }

            if (ui.NpcChatOpen)
            {
                reason = "npcChatOpen";
                return true;
            }

            if (ui.ChestOpen)
            {
                reason = "chestOpen";
                return true;
            }

            if (ui.MouseCapturedByUi)
            {
                reason = "uiBlocked:" + (ui.MouseCaptureReason ?? string.Empty);
                return true;
            }

            if (TravelMenuService.ShouldPauseAutomationForTravelMenu())
            {
                reason = "travelMenu";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private static int FindEligibleHotbarSlots(IList inventory, int[] destination)
        {
            if (inventory == null || destination == null)
            {
                return 0;
            }

            var count = 0;
            var limit = Math.Min(CombatPhasebladeQuickSwitchService.HotbarSlotCount, inventory.Count);
            for (var slot = 0; slot < limit && count < destination.Length; slot++)
            {
                int itemType;
                int stack;
                string itemName;
                if (TryReadHotbarItem(inventory, slot, out itemType, out stack, out itemName) &&
                    CombatPhasebladeQuickSwitchService.IsEligibleHotbarItem(itemType, stack))
                {
                    destination[count++] = slot;
                }
            }

            return count;
        }

        private static bool TryReadHotbarItem(IList inventory, int slot, out int itemType, out int stack, out string itemName)
        {
            itemType = 0;
            stack = 0;
            itemName = string.Empty;
            if (inventory == null || slot < 0 || slot >= inventory.Count)
            {
                return false;
            }

            int buffType;
            int buffTime;
            bool summon;
            return InventoryMutationCompat.TryReadItemFields(inventory[slot], out itemType, out itemName, out stack, out buffType, out buffTime, out summon);
        }

        private static void CancelOwnAction(InputActionQueue queue, string reason)
        {
            if (queue == null)
            {
                return;
            }

            queue.CancelBySource(FeatureId);
        }

        private static void RecordDecision(
            string decision,
            string reason,
            long tick,
            bool submitted,
            PhasebladeQuickSwitchRuntimeProfile profile,
            bool enabled)
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new CombatPhasebladeQuickSwitchDiagnostics() : _diagnostics.Clone();
                current.Enabled = enabled;
                current.RightHeld = enabled && profile != null && profile.RightHeld;
                current.Eligible = enabled &&
                                   profile != null &&
                                   profile.RightHeld &&
                                   profile.SelectedSlot >= 0 &&
                                   profile.NextSlot >= 0 &&
                                   profile.EligibleSlotCount >= CombatPhasebladeQuickSwitchService.RequiredEligibleHotbarCount;
                current.LastDecision = decision ?? string.Empty;
                current.LastReason = submitted ? string.Empty : reason ?? string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.LastTick = tick;
                current.CurrentSlot = profile == null ? -1 : profile.SelectedSlot;
                current.NextSlot = profile == null ? -1 : profile.NextSlot;
                current.EligibleSlotCount = profile == null ? 0 : profile.EligibleSlotCount;
                current.IntervalTicks = profile == null
                    ? CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks
                    : CombatPhasebladeQuickSwitchSettings.NormalizeIntervalTicks(profile.IntervalTicks);
                current.ItemType = profile == null ? 0 : profile.ItemType;
                if (submitted)
                {
                    current.SubmittedCount++;
                }
                else if (!string.Equals(decision, "running", StringComparison.OrdinalIgnoreCase))
                {
                    current.SkippedCount++;
                }

                _diagnostics = current;
            }
        }
    }

    internal sealed class PhasebladeQuickSwitchRuntimeProfile
    {
        public bool RightHeld { get; set; }
        public int SelectedSlot { get; set; }
        public int ItemType { get; set; }
        public int ItemStack { get; set; }
        public string ItemName { get; set; }
        public int EligibleSlotCount { get; set; }
        public int NextSlot { get; set; }
        public int IntervalTicks { get; set; }

        public PhasebladeQuickSwitchRuntimeProfile()
        {
            SelectedSlot = -1;
            NextSlot = -1;
            ItemName = string.Empty;
            IntervalTicks = CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks;
        }
    }

    public sealed class CombatPhasebladeQuickSwitchDiagnostics
    {
        public bool Enabled { get; set; }
        public bool RightHeld { get; set; }
        public bool Eligible { get; set; }
        public string LastDecision { get; set; }
        public string LastReason { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public long LastTick { get; set; }
        public int CurrentSlot { get; set; }
        public int NextSlot { get; set; }
        public int EligibleSlotCount { get; set; }
        public int IntervalTicks { get; set; }
        public int ItemType { get; set; }
        public long SubmittedCount { get; set; }
        public long SkippedCount { get; set; }

        public CombatPhasebladeQuickSwitchDiagnostics()
        {
            LastDecision = string.Empty;
            LastReason = string.Empty;
            CurrentSlot = -1;
            NextSlot = -1;
            IntervalTicks = CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks;
        }

        public CombatPhasebladeQuickSwitchDiagnostics Clone()
        {
            return (CombatPhasebladeQuickSwitchDiagnostics)MemberwiseClone();
        }
    }
}
