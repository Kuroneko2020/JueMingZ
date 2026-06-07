using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Combat
{
    // Magic-string clicks stay scoped to eligible ItemCheck frames and must not revive broad auto-click input policy.
    public static class CombatMagicStringClickerService
    {
        private const string FeatureId = FeatureIds.CombatMagicStringClicker;
        private const int PulseIntervalTicks = 2;
        private static readonly object DiagnosticsSyncRoot = new object();
        private static CombatAutomationDecisionDiagnosticInfo _diagnostics = new CombatAutomationDecisionDiagnosticInfo();

        public static CombatAutomationDecisionDiagnosticInfo GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        public static bool IsYoyoItemType(int itemType)
        {
            switch (itemType)
            {
                case 3262: // Code 1
                case 3278: // Wooden Yoyo
                case 3279: // Malaise
                case 3280: // Artery
                case 3281: // Amazon
                case 3282: // Cascade
                case 3283: // Chik
                case 3284: // Code 2
                case 3285: // Rally
                case 3286: // Yelets
                case 3287: // Red's Throw
                case 3288: // Valkyrie Yoyo
                case 3289: // Amarok
                case 3290: // Hel-Fire
                case 3291: // Kraken
                case 3292: // The Eye of Cthulhu
                case 3315: // Format:C
                case 3316: // Gradient
                case 3317: // Valor
                case 3389: // Terrarian
                case 5294: // Hive-Five
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsYoyoProjectileType(int projectileType)
        {
            return projectileType >= 541 && projectileType <= 555 ||
                   projectileType >= 562 && projectileType <= 564 ||
                   projectileType == 603 ||
                   projectileType == 999;
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.CombatMagicStringClickerEnabled)
                {
                    if (queue != null)
                    {
                        queue.CancelBySource(FeatureId);
                    }

                    RecordDecision("disabled", "disabled", tick, false);
                    return;
                }

                if (queue == null)
                {
                    RecordDecision("skipped", "queueUnavailable", tick, false);
                    return;
                }

                if (snapshot == null || !snapshot.IsInWorld)
                {
                    RecordDecision("skipped", "notInWorld", tick, false);
                    return;
                }

                if (CombatAutomationDecisionDiagnostics.IsBlockingSnapshotUiForCombat(snapshot.Ui))
                {
                    RecordDecision("skipped", CombatAutomationDecisionDiagnostics.BuildSnapshotUiSkipReason(snapshot.Ui), tick, false);
                    return;
                }

                var fast = queue.GetFastState();
                if (string.Equals(fast.RunningActionSource, FeatureId, StringComparison.OrdinalIgnoreCase) &&
                    fast.RunningActionKindValue == InputActionKind.RawInput)
                {
                    RecordDecision("running", "alreadyRunning", tick, false);
                    return;
                }

                if (fast.PendingCount > 0 ||
                    fast.HasRunningAction ||
                    ItemUseBridge.PendingRequestId != Guid.Empty)
                {
                    RecordDecision("skipped", "queueBusy", tick, false);
                    return;
                }

                object player;
                if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
                {
                    RecordDecision("skipped", "localPlayerUnavailable", tick, false);
                    return;
                }

                var ui = TerrariaInputCompat.ReadUiInputContext(player);
                if (ui.MainTypeUnavailable ||
                    ui.GameMenu ||
                    ui.ChatOpen ||
                    ui.NpcChatOpen ||
                    ui.MouseCapturedByUi)
                {
                    RecordDecision("skipped", CombatAutomationDecisionDiagnostics.BuildUiSkipReason(ui), tick, false);
                    return;
                }

                MagicStringItemProfile profile;
                string reason;
                if (!TryReadProfile(player, out profile, out reason))
                {
                    RecordDecision("skipped", "profile:" + reason, tick, false);
                    return;
                }

                if (!IsEligible(profile, out reason))
                {
                    RecordDecision("skipped", "ineligible:" + reason, tick, false);
                    return;
                }

                var request = new InputActionRequest
                {
                    Kind = InputActionKind.RawInput,
                    Priority = InputActionPriority.Normal,
                    DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                    SourceFeatureId = FeatureId,
                    Description = "Combat magic string clicker pulses selected yoyo use input",
                    QueueTimeout = TimeSpan.FromMilliseconds(250),
                    AdmissionKey = FeatureId + "|selected",
                    Timeout = TimeSpan.FromMinutes(15),
                    IsExclusive = true
                };
                request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.CombatMagicStringClicker;
                request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
                request.Metadata[ActionMetadataKeys.RawInputMode] = "MagicStringClicker";
                request.Metadata["RequireUseItemHeld"] = "true";
                request.Metadata["AllowCombatAim"] = "true";
                request.Metadata["MagicStringItemType"] = profile.ItemType.ToString(CultureInfo.InvariantCulture);
                request.Metadata["MagicStringItemName"] = profile.ItemName ?? string.Empty;
                request.Metadata["MagicStringSelectedSlot"] = profile.SelectedSlot.ToString(CultureInfo.InvariantCulture);
                request.Metadata["MagicStringProjectileType"] = profile.Shoot.ToString(CultureInfo.InvariantCulture);
                request.Metadata["MagicStringUseAnimation"] = profile.UseAnimation.ToString(CultureInfo.InvariantCulture);
                request.Metadata["MagicStringUseTime"] = profile.UseTime.ToString(CultureInfo.InvariantCulture);
                request.Metadata["MagicStringPulseIntervalTicks"] = PulseIntervalTicks.ToString(CultureInfo.InvariantCulture);

                InputActionAdmissionResult admission;
                if (!queue.TryEnqueue(request, out admission))
                {
                    RecordDecision("skipped", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, false);
                    return;
                }

                RecordDecision("submitted", string.Empty, tick, true);
            }
            catch (Exception error)
            {
                RecordDecision("exception", "exception:" + error.GetType().Name, tick, false);
                RuntimeDiagnostics.RecordError("CombatMagicStringClickerService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "combat-magic-string-clicker-tick-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatMagicStringClickerService",
                    "Combat magic string clicker tick failed; exception swallowed.", error);
            }
        }

        private static bool TryReadProfile(object player, out MagicStringItemProfile profile, out string reason)
        {
            profile = new MagicStringItemProfile();
            reason = string.Empty;

            if (player == null)
            {
                reason = "playerUnavailable";
                return false;
            }

            bool active;
            bool dead;
            bool ghost;
            GameStateReflection.TryGetBool(player, "active", out active);
            GameStateReflection.TryGetBool(player, "dead", out dead);
            GameStateReflection.TryGetBool(player, "ghost", out ghost);
            if (!active || dead || ghost)
            {
                reason = !active ? "playerInactive" : dead ? "playerDead" : "playerGhost";
                return false;
            }

            CombatAimUseInputSnapshot input;
            if (!TerrariaInputCompat.TryReadCombatAimUseInputSnapshot(player, out input) || input == null || !input.Available)
            {
                reason = string.IsNullOrWhiteSpace(TerrariaInputCompat.LastInputCompatError)
                    ? "inputSnapshotUnavailable"
                    : TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            profile.UseItemHeld = input.UseItemHeld;
            profile.UseItemReleased = input.UseItemReleased;
            profile.ItemAnimation = input.ItemAnimation;
            profile.ItemTime = input.ItemTime;
            profile.GameUpdateCount = input.GameUpdateCount;

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                reason = TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            profile.SelectedSlot = selectedSlot;
            if (selectedSlot < 0 || selectedSlot > 9)
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

            var item = inventory[selectedSlot];
            if (item == null)
            {
                reason = "selectedItemUnavailable";
                return false;
            }

            profile.ItemType = ReadInt(item, "type", 0);
            profile.ItemStack = ReadInt(item, "stack", 0);
            profile.ItemName = ReadItemName(item);
            profile.UseStyle = ReadInt(item, "useStyle", 0);
            profile.UseAnimation = ReadInt(item, "useAnimation", 0);
            profile.UseTime = ReadInt(item, "useTime", 0);
            profile.Shoot = ReadInt(item, "shoot", 0);
            profile.Damage = ReadInt(item, "damage", 0);
            profile.Channel = ReadBool(item, "channel", false);
            profile.AltFunctionUse = ReadInt(player, "altFunctionUse", 0);
            profile.MagicString = ReadBool(player, "magicString", false);
            return true;
        }

        private static bool IsEligible(MagicStringItemProfile profile, out string reason)
        {
            reason = string.Empty;
            if (profile == null || profile.ItemType <= 0 || profile.ItemStack <= 0)
            {
                reason = "selectedItemEmpty";
                return false;
            }

            if (!profile.UseItemHeld)
            {
                reason = "notHoldingUseItem";
                return false;
            }

            if (!profile.MagicString)
            {
                reason = "magicStringAccessoryInactive";
                return false;
            }

            if (profile.AltFunctionUse != 0)
            {
                reason = "altFunctionUse";
                return false;
            }

            if (profile.UseStyle <= 0 || profile.UseAnimation <= 0 || profile.UseTime <= 0)
            {
                reason = "notUsableItem";
                return false;
            }

            if (profile.Shoot <= 0 || profile.Damage <= 0)
            {
                reason = "notProjectileDamageItem";
                return false;
            }

            if (!IsYoyoItemType(profile.ItemType) && !IsYoyoProjectileType(profile.Shoot))
            {
                reason = "notYoyoWeapon";
                return false;
            }

            return true;
        }

        private static int ReadInt(object source, string name, int fallback)
        {
            int value;
            return GameStateReflection.TryGetInt(source, name, out value) ? value : fallback;
        }

        private static bool ReadBool(object source, string name, bool fallback)
        {
            bool value;
            return GameStateReflection.TryGetBool(source, name, out value) ? value : fallback;
        }

        private static string ReadItemName(object item)
        {
            var name = GameStateReflection.GetMember(item, "Name") ??
                       GameStateReflection.GetMember(item, "name");
            return name == null ? string.Empty : Convert.ToString(name, CultureInfo.InvariantCulture);
        }

        private static void RecordDecision(string decision, string reason, long tick, bool submitted)
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new CombatAutomationDecisionDiagnosticInfo() : _diagnostics.Clone();
                current.LastDecision = decision ?? string.Empty;
                current.LastSkipReason = submitted ? string.Empty : reason ?? string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.LastTick = tick;
                if (submitted)
                {
                    current.SubmittedCount++;
                }
                else
                {
                    current.SkippedCount++;
                }

                _diagnostics = current;
            }
        }

        private sealed class MagicStringItemProfile
        {
            public bool UseItemHeld;
            public bool UseItemReleased;
            public int ItemAnimation;
            public int ItemTime;
            public long GameUpdateCount;
            public int SelectedSlot = -1;
            public int ItemType;
            public int ItemStack;
            public string ItemName = string.Empty;
            public int UseStyle;
            public int UseAnimation;
            public int UseTime;
            public int Shoot;
            public int Damage;
            public bool Channel;
            public int AltFunctionUse;
            public bool MagicString;
        }
    }
}
