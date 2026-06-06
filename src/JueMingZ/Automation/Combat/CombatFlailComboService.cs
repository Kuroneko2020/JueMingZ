using System;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Automation.WorldAutomation;

namespace JueMingZ.Automation.Combat
{
    public static class CombatFlailComboService
    {
        private const string ScopeName = "CombatFlailComboItemCheck";
        private const int PulseCooldownTicks = 5;
        private const int StuckRecoveryTicks = 18;
        private static readonly object SyncRoot = new object();
        private static readonly CombatFlailRuntime.CombatFlailProjectileTracker ProjectileTracker = new CombatFlailRuntime.CombatFlailProjectileTracker();
        private static CombatFlailComboDiagnostics _diagnostics = CombatFlailComboDiagnostics.Empty();
        private static string _state = FlailComboStates.Idle;
        private static long _cooldownUntilTick;
        private static string _lastEventKey = string.Empty;
        private static DateTime _lastEventUtc = DateTime.MinValue;

        public static CombatFlailComboDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return _diagnostics == null ? CombatFlailComboDiagnostics.Empty() : _diagnostics.Clone();
            }
        }

        public static bool TryBeginItemCheckTakeover(object player, out TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            takeover = null;
            var diagnostics = CombatFlailComboDiagnostics.Empty();
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                diagnostics.Enabled = settings.CombatFlailComboEnabled;
                if (!settings.CombatFlailComboEnabled)
                {
                    ResetComboState();
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, "disabled", true), null, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
                {
                    ResetComboState();
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, player == null ? "playerUnavailable" : "notLocalPlayer", true), null, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                string playerStateReason;
                if (IsPlayerBlocked(player, out playerStateReason))
                {
                    ResetComboState();
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, playerStateReason, true), null, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                bool rightHeld;
                if (!TerrariaInputCompat.TryReadPhysicalMouseRightHeld(out rightHeld))
                {
                    ResetComboState();
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, "rightInputUnavailable:" + TerrariaInputCompat.LastInputCompatError, true), null, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                diagnostics.RightHeld = rightHeld;
                if (!rightHeld)
                {
                    ResetComboState();
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Idle, "rightNotHeld", true), null, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                bool leftHeld;
                if (TerrariaInputCompat.TryReadPhysicalMouseLeftHeld(out leftHeld) && leftHeld)
                {
                    ResetComboState();
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Idle, "physicalLeftHeld", true), null, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                string uiReason;
                // Inventory-open alone is not a blocker; real UI capture,
                // chest/chat/travel, and vanilla right-click semantics are.
                if (IsUiBlocked(player, out uiReason))
                {
                    ResetComboState();
                    diagnostics.UiBlocked = true;
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, uiReason, true), null, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                string worldRightClickReason;
                if (IsWorldRightClickBlocked(player, out worldRightClickReason))
                {
                    ResetComboState();
                    diagnostics.VanillaRightClickBlocked = true;
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, worldRightClickReason, true), null, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                CombatAimWeaponProfile weaponProfile;
                object selectedItem;
                string profileReason;
                if (!CombatFlailRuntime.TryReadSelectedWeaponProfile(player, out weaponProfile, out selectedItem, out profileReason))
                {
                    ResetComboState();
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, "profile:" + profileReason, true), null, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                var comboProfile = CombatFlailComboProfile.FromWeapon(weaponProfile);
                diagnostics.ItemType = comboProfile.ItemType;

                int projectileAiStyle;
                bool isYoyo;
                CombatAimFlailEligibility eligibility;
                CombatFlailRuntime.TryEvaluateEligibility(player, weaponProfile, out projectileAiStyle, out isYoyo, out eligibility);
                comboProfile.ProjectileAiStyle = projectileAiStyle;
                comboProfile.IsYoyo = isYoyo;
                comboProfile.Eligible = eligibility != null && eligibility.Eligible;
                comboProfile.Reason = eligibility == null ? "notFlail:eligibilityUnavailable" : eligibility.Reason;
                comboProfile.ItemReady = CombatFlailRuntime.CanUseSelectedItem(player);
                diagnostics.Eligible = comboProfile.Eligible;

                if (!comboProfile.Eligible)
                {
                    ResetComboState();
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, comboProfile.Reason, true), comboProfile, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                string vanillaRightClickReason;
                if (CombatFlailRuntime.HasVanillaRightClickSemantics(comboProfile.ItemType, player, out vanillaRightClickReason))
                {
                    ResetComboState();
                    comboProfile.VanillaRightClickBlocked = true;
                    comboProfile.VanillaRightClickReason = vanillaRightClickReason;
                    diagnostics.VanillaRightClickBlocked = true;
                    RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, vanillaRightClickReason, true), comboProfile, CombatFlailRuntimeFrame.None(), diagnostics);
                    return false;
                }

                long tick;
                TerrariaInputCompat.TryReadGameUpdateCount(out tick);
                var inCooldown = tick > 0 && tick < _cooldownUntilTick;

                CombatFlailRuntimeFrame frame;
                CombatFlailRuntime.TryReadActiveFrame(player, comboProfile.Shoot, ProjectileTracker, out frame);
                if (frame != null && frame.HasProjectile && frame.StuckTicks >= StuckRecoveryTicks)
                {
                    comboProfile.StuckRecovery = true;
                }

                string currentState;
                lock (SyncRoot)
                {
                    currentState = _state;
                }

                var decision = CreateDecision(comboProfile, frame, settings.CombatFlailComboEnabled, rightHeld, inCooldown, currentState);
                if (!decision.ApplyTakeover)
                {
                    RecordDecision(decision, comboProfile, frame, diagnostics);
                    ApplyStateTransition(decision, tick);
                    return false;
                }

                // Right-click combo creates one scoped press/release rhythm. It
                // must not layer another input takeover on top of ItemCheck.
                if (!TerrariaInputCompat.TryBeginScopedUseItemClickTakeoverSuppressingRightClick(player, decision.PressAttack, ScopeName, out takeover))
                {
                    var failed = CombatFlailComboDecision.NoOp(decision.State, "takeover:" + TerrariaInputCompat.LastInputCompatError, false);
                    RecordDecision(failed, comboProfile, frame, diagnostics);
                    return false;
                }

                RecordDecision(decision, comboProfile, frame, diagnostics);
                ApplyStateTransition(decision, tick);
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("CombatFlailComboService.TryBeginItemCheckTakeover", error);
                LogThrottle.ErrorThrottled(
                    "combat-flail-combo-itemcheck-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatFlailComboService",
                    "Combat flail combo ItemCheck takeover failed; exception swallowed.", error);
                ResetComboState();
                RecordDecision(CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, "exception:" + error.GetType().Name, true), null, CombatFlailRuntimeFrame.None(), diagnostics);
                return false;
            }
        }

        public static void RecordRestoreStatus(bool restored)
        {
            lock (SyncRoot)
            {
                var current = _diagnostics == null ? CombatFlailComboDiagnostics.Empty() : _diagnostics.Clone();
                current.RestoreOk = restored;
                _diagnostics = current;
            }
        }

        public static CombatFlailComboDecision CreateDecision(
            CombatFlailComboProfile profile,
            CombatFlailRuntimeFrame frame,
            bool featureEnabled,
            bool rightHeld,
            bool inCooldown,
            string currentState)
        {
            frame = frame ?? CombatFlailRuntimeFrame.None();
            currentState = string.IsNullOrWhiteSpace(currentState) ? FlailComboStates.Idle : currentState;

            if (!featureEnabled)
            {
                return CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, "disabled", true);
            }

            if (!rightHeld)
            {
                return CombatFlailComboDecision.NoOp(FlailComboStates.Idle, "rightNotHeld", true);
            }

            if (profile == null || !profile.Available)
            {
                return CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, profile == null ? "profileUnavailable" : profile.Reason, true);
            }

            if (!profile.Eligible)
            {
                return CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, profile.Reason, true);
            }

            if (profile.VanillaRightClickBlocked)
            {
                return CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, profile.VanillaRightClickReason, true);
            }

            if (string.Equals(currentState, FlailComboStates.LaunchPress, StringComparison.Ordinal))
            {
                return CombatFlailComboDecision.Release(FlailComboStates.LaunchRelease, "launchRelease");
            }

            if (string.Equals(currentState, FlailComboStates.RecallPress, StringComparison.Ordinal))
            {
                return CombatFlailComboDecision.Release(FlailComboStates.RecallRelease, "recallRelease");
            }

            if (inCooldown)
            {
                return CombatFlailComboDecision.NoOp(FlailComboStates.Cooldown, "cooldown", false);
            }

            if (!frame.HasProjectile)
            {
                return profile.ItemReady
                    ? CombatFlailComboDecision.Press(FlailComboStates.LaunchPress, "launchReady")
                    : CombatFlailComboDecision.NoOp(FlailComboStates.Idle, "itemNotReady", false);
            }

            if (CombatFlailRuntime.IsReturnOrEndingState(frame.ProjectileAi0))
            {
                return CombatFlailComboDecision.NoOp(FlailComboStates.InFlight, "returnState", false);
            }

            if (frame.HitDetected)
            {
                return CombatFlailComboDecision.Press(FlailComboStates.RecallPress, "hitDetected");
            }

            if (frame.CollisionDetected)
            {
                return CombatFlailComboDecision.Press(FlailComboStates.RecallPress, "collisionDetected");
            }

            if (profile.StuckRecovery)
            {
                return CombatFlailComboDecision.Press(FlailComboStates.RecallPress, "stuckRecovery");
            }

            return CombatFlailComboDecision.NoOp(FlailComboStates.InFlight, "inFlight", false);
        }

        internal static void ResetForTesting()
        {
            ResetComboState();
            CombatFlailRuntime.ResetItemSetCacheForTesting();
            lock (SyncRoot)
            {
                _diagnostics = CombatFlailComboDiagnostics.Empty();
                _lastEventKey = string.Empty;
                _lastEventUtc = DateTime.MinValue;
            }
        }

        internal static void SetStateForTesting(string state, long cooldownUntilTick)
        {
            lock (SyncRoot)
            {
                _state = string.IsNullOrWhiteSpace(state) ? FlailComboStates.Idle : state;
                _cooldownUntilTick = cooldownUntilTick;
            }
        }

        internal static void RecordDecisionForTesting(
            CombatFlailComboDecision decision,
            CombatFlailComboProfile profile,
            CombatFlailRuntimeFrame frame)
        {
            RecordDecision(decision, profile, frame, CombatFlailComboDiagnostics.Empty());
        }

        internal static bool IsUiBlockedForTesting(object player, out string reason)
        {
            return IsUiBlocked(player, out reason);
        }

        private static void ApplyStateTransition(CombatFlailComboDecision decision, long tick)
        {
            if (decision == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (decision.ResetState)
                {
                    _state = FlailComboStates.Idle;
                    _cooldownUntilTick = 0;
                    ProjectileTracker.Reset();
                    return;
                }

                _state = decision.State;
                if (decision.ApplyTakeover && !decision.PressAttack && tick > 0)
                {
                    _cooldownUntilTick = tick + PulseCooldownTicks;
                }
            }
        }

        private static void ResetComboState()
        {
            lock (SyncRoot)
            {
                _state = FlailComboStates.Idle;
                _cooldownUntilTick = 0;
                ProjectileTracker.Reset();
            }
        }

        private static bool IsPlayerBlocked(object player, out string reason)
        {
            reason = string.Empty;
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

            return false;
        }

        private static bool IsUiBlocked(object player, out string reason)
        {
            var ui = TerrariaInputCompat.ReadUiInputContext(player);
            // Do not add PlayerInventoryOpen here by itself; mouse UI capture
            // and concrete vanilla interaction states are the blockers.
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

        private static bool IsWorldRightClickBlocked(object player, out string reason)
        {
            return TerrariaInputCompat.IsWorldRightClickInteractionActive(player, out reason);
        }

        private static void RecordDecision(
            CombatFlailComboDecision decision,
            CombatFlailComboProfile profile,
            CombatFlailRuntimeFrame frame,
            CombatFlailComboDiagnostics diagnostics)
        {
            decision = decision ?? CombatFlailComboDecision.NoOp(FlailComboStates.Disabled, "decisionUnavailable", true);
            diagnostics = diagnostics == null ? CombatFlailComboDiagnostics.Empty() : diagnostics;
            frame = frame ?? CombatFlailRuntimeFrame.None();

            var apply = decision.ApplyTakeover;
            var press = apply && decision.PressAttack;
            var release = apply && !decision.PressAttack;
            lock (SyncRoot)
            {
                var current = _diagnostics == null ? CombatFlailComboDiagnostics.Empty() : _diagnostics.Clone();
                current.Enabled = diagnostics.Enabled;
                current.RightHeld = diagnostics.RightHeld;
                current.Eligible = diagnostics.Eligible || (profile != null && profile.Eligible);
                current.LastDecision = apply ? (press ? "scopedPress" : "scopedRelease") : "noOp";
                current.LastReason = decision.Reason ?? string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.ItemType = profile == null ? diagnostics.ItemType : profile.ItemType;
                current.ProjectileType = frame.ProjectileType;
                current.ProjectileAi0 = frame.ProjectileAi0;
                current.HitDetected = frame.HitDetected;
                current.CollisionDetected = frame.CollisionDetected;
                current.VanillaRightClickBlocked = diagnostics.VanillaRightClickBlocked || (profile != null && profile.VanillaRightClickBlocked);
                current.UiBlocked = diagnostics.UiBlocked;
                current.ScopedPress = press;
                current.ScopedRelease = release;
                current.RestoreOk = false;
                if (apply)
                {
                    current.AppliedCount++;
                }
                else
                {
                    current.SkippedCount++;
                }

                _diagnostics = current;
            }

            RecordEventIfUseful(decision, profile, frame);
        }

        private static void RecordEventIfUseful(
            CombatFlailComboDecision decision,
            CombatFlailComboProfile profile,
            CombatFlailRuntimeFrame frame)
        {
            if (decision == null)
            {
                return;
            }

            var important = decision.ApplyTakeover ||
                            string.Equals(decision.Reason, "itemHasRightFire", StringComparison.Ordinal) ||
                            string.Equals(decision.Reason, "itemAllowsRepeatedRightClick", StringComparison.Ordinal) ||
                            (decision.Reason ?? string.Empty).StartsWith("rightClickSet", StringComparison.Ordinal);
            var key = (profile == null ? 0 : profile.ItemType).ToString(CultureInfo.InvariantCulture) + "|" +
                      decision.State + "|" +
                      decision.Reason + "|" +
                      decision.ApplyTakeover + "|" +
                      decision.PressAttack + "|" +
                      (frame == null ? 0 : frame.ProjectileType).ToString(CultureInfo.InvariantCulture);

            var now = DateTime.UtcNow;
            if (!important &&
                string.Equals(key, _lastEventKey, StringComparison.Ordinal) &&
                now - _lastEventUtc < TimeSpan.FromSeconds(2))
            {
                return;
            }

            if (!important && !decision.ApplyTakeover)
            {
                return;
            }

            _lastEventKey = key;
            _lastEventUtc = now;
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                ScenarioNames.CombatFlailCombo,
                "RawInput",
                string.Empty,
                decision.ApplyTakeover ? "Applied" : "Observed",
                decision.ApplyTakeover ? DiagnosticResultCode.Succeeded.ToString() : DiagnosticResultCode.NotApplicable.ToString(),
                "Combat flail combo decision: " + decision.State,
                0,
                "{}",
                "{}",
                BuildDecisionJson(decision, profile, frame),
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static string BuildDecisionJson(
            CombatFlailComboDecision decision,
            CombatFlailComboProfile profile,
            CombatFlailRuntimeFrame frame)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "state", decision == null ? string.Empty : decision.State, true);
            AppendString(builder, "reason", decision == null ? string.Empty : decision.Reason, true);
            AppendRaw(builder, "applyTakeover", BoolRaw(decision != null && decision.ApplyTakeover), true);
            AppendRaw(builder, "pressAttack", BoolRaw(decision != null && decision.PressAttack), true);
            AppendRaw(builder, "itemType", IntRaw(profile == null ? 0 : profile.ItemType), true);
            AppendRaw(builder, "projectileType", IntRaw(frame == null ? 0 : frame.ProjectileType), true);
            AppendRaw(builder, "projectileAi0", FloatRaw(frame == null ? 0f : frame.ProjectileAi0), true);
            AppendRaw(builder, "hitDetected", BoolRaw(frame != null && frame.HitDetected), true);
            AppendRaw(builder, "collisionDetected", BoolRaw(frame != null && frame.CollisionDetected), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(name).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(name).Append("\":\"").Append(Escape(value)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FloatRaw(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    public static class FlailComboStates
    {
        public const string Idle = "Idle";
        public const string LaunchPress = "LaunchPress";
        public const string LaunchRelease = "LaunchRelease";
        public const string InFlight = "InFlight";
        public const string RecallPress = "RecallPress";
        public const string RecallRelease = "RecallRelease";
        public const string Cooldown = "Cooldown";
        public const string Disabled = "Disabled";
    }

    public sealed class CombatFlailComboProfile
    {
        public bool Available { get; set; }
        public bool Eligible { get; set; }
        public string Reason { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int Shoot { get; set; }
        public int ProjectileAiStyle { get; set; }
        public bool IsYoyo { get; set; }
        public bool ItemReady { get; set; }
        public bool StuckRecovery { get; set; }
        public bool VanillaRightClickBlocked { get; set; }
        public string VanillaRightClickReason { get; set; }

        public CombatFlailComboProfile()
        {
            Available = true;
            Reason = string.Empty;
            ItemName = string.Empty;
            VanillaRightClickReason = string.Empty;
        }

        internal static CombatFlailComboProfile FromWeapon(CombatAimWeaponProfile weapon)
        {
            if (weapon == null)
            {
                return new CombatFlailComboProfile { Available = false, Reason = "profileUnavailable" };
            }

            return new CombatFlailComboProfile
            {
                Available = true,
                ItemType = weapon.ItemType,
                ItemName = weapon.Name ?? string.Empty,
                Shoot = weapon.Shoot
            };
        }
    }

    public sealed class CombatFlailComboDecision
    {
        private CombatFlailComboDecision(bool applyTakeover, bool pressAttack, string state, string reason, bool resetState)
        {
            ApplyTakeover = applyTakeover;
            PressAttack = pressAttack;
            State = state ?? string.Empty;
            Reason = reason ?? string.Empty;
            ResetState = resetState;
        }

        public bool ApplyTakeover { get; private set; }
        public bool PressAttack { get; private set; }
        public string State { get; private set; }
        public string Reason { get; private set; }
        public bool ResetState { get; private set; }

        public static CombatFlailComboDecision NoOp(string state, string reason, bool resetState)
        {
            return new CombatFlailComboDecision(false, false, state, reason, resetState);
        }

        public static CombatFlailComboDecision Press(string state, string reason)
        {
            return new CombatFlailComboDecision(true, true, state, reason, false);
        }

        public static CombatFlailComboDecision Release(string state, string reason)
        {
            return new CombatFlailComboDecision(true, false, state, reason, false);
        }
    }

    public sealed class CombatFlailComboDiagnostics
    {
        public bool Enabled { get; set; }
        public bool RightHeld { get; set; }
        public bool Eligible { get; set; }
        public string LastDecision { get; set; }
        public string LastReason { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public int ItemType { get; set; }
        public int ProjectileType { get; set; }
        public double ProjectileAi0 { get; set; }
        public bool HitDetected { get; set; }
        public bool CollisionDetected { get; set; }
        public bool VanillaRightClickBlocked { get; set; }
        public bool UiBlocked { get; set; }
        public bool ScopedPress { get; set; }
        public bool ScopedRelease { get; set; }
        public bool RestoreOk { get; set; }
        public long AppliedCount { get; set; }
        public long SkippedCount { get; set; }

        public static CombatFlailComboDiagnostics Empty()
        {
            return new CombatFlailComboDiagnostics
            {
                LastDecision = string.Empty,
                LastReason = string.Empty,
                ItemType = 0,
                ProjectileType = 0,
                RestoreOk = true
            };
        }

        public CombatFlailComboDiagnostics Clone()
        {
            return (CombatFlailComboDiagnostics)MemberwiseClone();
        }
    }
}
