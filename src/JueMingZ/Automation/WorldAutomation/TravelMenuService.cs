using System;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.WorldAutomation
{
    public static class TravelMenuService
    {
        public const string SuspendedResultCode = "suspended";

        private const string SuspendedMessage = "Travel menu is available; suspended fallback is inactive.";
        private const string PlayerHurtScope = "Terraria.Player.Hurt";
        private static readonly bool FeatureSuspended = false;
        private static readonly object SyncRoot = new object();
        private static TravelMenuContext _activeOriginalContext;
        private static TravelMenuDiagnosticInfo _diagnostics = new TravelMenuDiagnosticInfo();
        private static int _saveGuardDepth;
        private static int _creativeUiIgnoreMouseOverrideDepth;
        private static bool _inventoryStateCaptured;
        private static bool _inventoryOpenBeforeSession;
        private static bool _inventoryForcedOpen;

        public static string SuspendedReason
        {
            get { return SuspendedMessage; }
        }

        public static bool IsSuspended
        {
            get { return FeatureSuspended; }
        }

        public static void MarkSuspendedAtBootstrap()
        {
            DisableConfigIfNeeded();
            lock (SyncRoot)
            {
                _activeOriginalContext = null;
                _saveGuardDepth = 0;
                _creativeUiIgnoreMouseOverrideDepth = 0;
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                ClearInventorySessionStateLocked();
                EnsureDiagnosticsLocked();
                ApplySuspendedDiagnostics(_diagnostics);
                RecordDecisionLocked("suspended", "featureSuspended", SuspendedMessage, null);
            }
        }

        public static TravelMenuDiagnosticInfo GetDiagnostics()
        {
            lock (SyncRoot)
            {
                var copy = _diagnostics == null ? new TravelMenuDiagnosticInfo() : _diagnostics.Clone();
                if (IsSuspended)
                {
                    ApplySuspendedDiagnostics(copy);
                }
                else
                {
                    copy.Enabled = ConfigService.AppSettings != null && ConfigService.AppSettings.WorldAutomationTravelMenuEnabled;
                    copy.SessionActive = _activeOriginalContext != null;
                }

                return copy;
            }
        }

        public static void RecordSaveGuardHook(bool installed, string message)
        {
            lock (SyncRoot)
            {
                EnsureDiagnosticsLocked();
                _diagnostics.SaveGuardHookInstalled = installed;
                _diagnostics.SaveGuardHookMessage = message ?? string.Empty;
            }
        }

        public static void RecordCreativeUiHook(bool installed, string message)
        {
            lock (SyncRoot)
            {
                EnsureDiagnosticsLocked();
                _diagnostics.CreativeUiHookInstalled = installed;
                _diagnostics.CreativeUiHookMessage = message ?? string.Empty;
            }
        }

        public static void RecordScopedPowerHook(bool installed, string message)
        {
            lock (SyncRoot)
            {
                EnsureDiagnosticsLocked();
                _diagnostics.ScopedPowerHookInstalled = installed;
                _diagnostics.ScopedPowerHookMessage = message ?? string.Empty;
            }
        }

        public static TravelMenuToggleResult SetEnabledFromUi(bool enabled, bool openMenu)
        {
            if (IsSuspended)
            {
                return CreateSuspendedToggleResult();
            }

            return enabled ? EnableFromUi(openMenu) : DisableFromUi();
        }

        public static bool TryBeginCreativeUiItemCheckGuard(object player, out TerrariaInputCompat.ScopedUseItemTakeover takeover, out string message)
        {
            takeover = null;
            message = string.Empty;
            if (IsSuspended)
            {
                message = SuspendedMessage;
                return false;
            }

            lock (SyncRoot)
            {
                if (_activeOriginalContext == null || _saveGuardDepth > 0)
                {
                    return false;
                }

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.WorldAutomationTravelMenuEnabled)
                {
                    return false;
                }
            }

            bool creativeMenuEnabled;
            string creativeMenuMessage;
            if (!TravelMenuCompat.TryReadCreativeMenuEnabled(out creativeMenuEnabled, out creativeMenuMessage) || !creativeMenuEnabled)
            {
                message = string.IsNullOrWhiteSpace(creativeMenuMessage)
                    ? "Creative menu is not open."
                    : creativeMenuMessage;
                return false;
            }

            bool inventoryOpen;
            string inventoryMessage;
            if (!TravelMenuCompat.TryReadPlayerInventoryOpen(out inventoryOpen, out inventoryMessage) || !inventoryOpen)
            {
                message = string.IsNullOrWhiteSpace(inventoryMessage)
                    ? "Main.playerInventory is not open."
                    : inventoryMessage;
                return false;
            }

            return TravelMenuCompat.TryBeginScopedCreativeUiWorldItemUseGuard(player, out takeover, out message);
        }

        public static bool BeginScopedJourney(string scope, out TravelMenuScopedJourneyState state)
        {
            state = null;
            if (IsSuspended)
            {
                return false;
            }

            TravelMenuContext active;
            var nativeJourneyScope = false;
            lock (SyncRoot)
            {
                if (_saveGuardDepth > 0)
                {
                    return false;
                }

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (_activeOriginalContext != null)
                {
                    if (!settings.WorldAutomationTravelMenuEnabled)
                    {
                        return false;
                    }

                    active = _activeOriginalContext.Clone();
                }
                else
                {
                    active = null;
                    nativeJourneyScope = ShouldApplyCreativeUiInputBypass(scope);
                }
            }

            var applyMessage = string.Empty;
            if (active != null && !nativeJourneyScope && RequiresGodmodePowerScopedJourney(scope))
            {
                bool godmodeEnabled;
                string godmodeMessage;
                if (!TravelMenuCompat.TryReadGodmodePowerEnabled(out godmodeEnabled, out godmodeMessage) || !godmodeEnabled)
                {
                    var skipMessage = string.IsNullOrWhiteSpace(godmodeMessage)
                        ? "Player.Hurt scoped journey skipped: Journey godmode power is disabled."
                        : "Player.Hurt scoped journey skipped: " + godmodeMessage;
                    RecordDecision("idle", "playerHurtScopedJourneySkipped", skipMessage);
                    return false;
                }

                applyMessage = "Player.Hurt scoped journey allowed: Journey godmode power is enabled.";
            }

            if (nativeJourneyScope)
            {
                if (!TravelMenuCompat.TryBeginNativeJourneyCreativeUiScope(scope, out state, out applyMessage))
                {
                    return false;
                }
            }
            else if (active == null)
            {
                return false;
            }

            if (nativeJourneyScope || TravelMenuCompat.TryBeginScopedJourneyState(active, scope, out state, out applyMessage))
            {
                ApplyCreativeUiInputBypassIfNeeded(scope, state, ref applyMessage);

                lock (SyncRoot)
                {
                    EnsureDiagnosticsLocked();
                    _diagnostics.ApplyCount++;
                    _diagnostics.ScopedApplyCount++;
                    RecordDecisionLocked("scopedBegin", string.IsNullOrWhiteSpace(scope) ? "scopedJourney" : scope, applyMessage, active);
                }

                return true;
            }

            var reason = string.IsNullOrWhiteSpace(scope) ? "scopedJourneyApplyFailed" : scope + "ApplyFailed";
            RecordDecision("failed", reason, applyMessage);
            return false;
        }

        private static void ApplyCreativeUiInputBypassIfNeeded(string scope, TravelMenuScopedJourneyState state, ref string applyMessage)
        {
            if (!ShouldApplyCreativeUiInputBypass(scope))
            {
                return;
            }

            string ignoreMouseOverrideMessage;
            var ignoreMouseOverrideApplied = TryBeginScopedIgnoreMouseInterfaceOverride(state, out ignoreMouseOverrideMessage);
            applyMessage = (applyMessage + " " + ignoreMouseOverrideMessage).Trim();
            if (!ignoreMouseOverrideApplied)
            {
                LogThrottle.WarnThrottled(
                    "travel-menu-ignore-mouse-interface-override-apply-failed",
                    TimeSpan.FromSeconds(5),
                    "TravelMenuService",
                    ignoreMouseOverrideMessage);
            }

            string inputBypassMessage;
            var inputBypassApplied = TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(state, out inputBypassMessage);
            applyMessage = (applyMessage + " " + inputBypassMessage).Trim();
            if (!inputBypassApplied)
            {
                LogThrottle.WarnThrottled(
                    "travel-menu-creative-ui-input-bypass-apply-failed",
                    TimeSpan.FromSeconds(5),
                    "TravelMenuService",
                    inputBypassMessage);
            }
        }

        public static void EndScopedJourney(TravelMenuScopedJourneyState state)
        {
            if (state == null)
            {
                return;
            }

            var scope = state.Scope;
            var shouldApplyCreativeUiInputBypass = ShouldApplyCreativeUiInputBypass(scope);
            var inputBypassRestored = true;
            string inputBypassMessage = string.Empty;
            if (shouldApplyCreativeUiInputBypass)
            {
                inputBypassRestored = TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(state, out inputBypassMessage);
            }

            var wasApplied = state.Applied;
            string restoreMessage;
            var restored = TravelMenuCompat.TryRestoreScopedJourneyState(state, out restoreMessage);
            if (!string.IsNullOrWhiteSpace(inputBypassMessage))
            {
                restoreMessage = (restoreMessage + " " + inputBypassMessage).Trim();
            }

            if (!inputBypassRestored)
            {
                restored = false;
            }

            if (ShouldApplyCreativeUiWorldInputGuard(scope))
            {
                string worldInputGuardMessage;
                var worldInputGuardApplied = TravelMenuCompat.TryApplyCreativeUiWorldInputGuard(state, out worldInputGuardMessage);
                if (!string.IsNullOrWhiteSpace(worldInputGuardMessage))
                {
                    restoreMessage = (restoreMessage + " " + worldInputGuardMessage).Trim();
                }

                if (!worldInputGuardApplied)
                {
                    LogThrottle.WarnThrottled(
                        "travel-menu-creative-ui-world-input-guard-failed",
                        TimeSpan.FromSeconds(5),
                        "TravelMenuService",
                        worldInputGuardMessage);
                }
            }

            if (shouldApplyCreativeUiInputBypass)
            {
                string ignoreMouseOverrideRestoreMessage;
                var ignoreMouseOverrideRestored = TryEndScopedIgnoreMouseInterfaceOverride(state, out ignoreMouseOverrideRestoreMessage);
                if (!string.IsNullOrWhiteSpace(ignoreMouseOverrideRestoreMessage))
                {
                    restoreMessage = (restoreMessage + " " + ignoreMouseOverrideRestoreMessage).Trim();
                }

                if (!ignoreMouseOverrideRestored)
                {
                    restored = false;
                }
            }

            lock (SyncRoot)
            {
                EnsureDiagnosticsLocked();
                if (restored && wasApplied)
                {
                    _diagnostics.RestoreCount++;
                    _diagnostics.ScopedRestoreCount++;
                }

                RecordDecisionLocked(
                    restored ? "scopedEnd" : "scopedRestoreFailed",
                    string.IsNullOrWhiteSpace(state.Scope) ? "scopedJourney" : state.Scope,
                    restoreMessage,
                    _activeOriginalContext);
            }
        }

        public static void Tick(GameStateSnapshot gameState, RuntimeState runtimeState)
        {
            if (IsSuspended)
            {
                return;
            }

            var inWorld = gameState != null && gameState.IsInWorld;
            if (!inWorld)
            {
                TravelMenuContext previous;
                lock (SyncRoot)
                {
                    previous = _activeOriginalContext == null ? null : _activeOriginalContext.Clone();
                }

                if (previous != null)
                {
                    CompleteSessionAfterWorldExit(previous);
                }

                return;
            }

            TravelMenuContext current;
            string captureMessage;
            if (!TravelMenuCompat.TryCaptureCurrentContext(out current, out captureMessage))
            {
                RecordDecision("skipped", "captureFailed", captureMessage);
                return;
            }

            TravelMenuContext activeForCleanup = null;
            lock (SyncRoot)
            {
                if (_activeOriginalContext != null)
                {
                    var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                    if (!settings.WorldAutomationTravelMenuEnabled)
                    {
                        // Fall through outside the lock to run restore through the normal path.
                    }
                    else if (!TravelMenuCompat.IsSamePlayerWorld(_activeOriginalContext, current))
                    {
                        // Fall through outside the lock to restore old session and leave marker recovery to the new world.
                    }
                    else if (_saveGuardDepth <= 0)
                    {
                        activeForCleanup = _activeOriginalContext.Clone();
                    }
                    else
                    {
                        RecordDecisionLocked("skipped", "saveGuardActive", "Travel menu reapply deferred while save guard is active.", _activeOriginalContext);
                        return;
                    }
                }
            }

            if (activeForCleanup != null)
            {
                bool matchesOriginal;
                string stateMessage;
                if (TravelMenuCompat.TryCurrentStateMatchesOriginal(activeForCleanup, out matchesOriginal, out stateMessage))
                {
                    if (!matchesOriginal)
                    {
                        string restoreMessage;
                        var restored = TravelMenuCompat.TryRestoreOriginalState(activeForCleanup, out restoreMessage);
                        lock (SyncRoot)
                        {
                            EnsureDiagnosticsLocked();
                            if (restored)
                            {
                                _diagnostics.RestoreCount++;
                                _diagnostics.ScopedCleanupCount++;
                            }

                            RecordDecisionLocked(restored ? "scopedCleanup" : "scopedCleanupFailed", "tickOriginalStateGuard", restoreMessage, activeForCleanup);
                        }
                    }

                    return;
                }

                RecordDecision("failed", "tickOriginalStateCheckFailed", stateMessage);
                return;
            }

            bool activeSession;
            bool sameSession;
            bool configEnabled;
            lock (SyncRoot)
            {
                activeSession = _activeOriginalContext != null;
                sameSession = activeSession && TravelMenuCompat.IsSamePlayerWorld(_activeOriginalContext, current);
                configEnabled = ConfigService.AppSettings != null && ConfigService.AppSettings.WorldAutomationTravelMenuEnabled;
            }

            if (activeSession && (!sameSession || !configEnabled))
            {
                StopActiveSession(configEnabled ? "worldChanged" : "configDisabled", true, true);
                return;
            }

            TravelMenuRestoreMarker marker;
            if (TravelMenuStateStore.TryFindActiveMarker(current, out marker))
            {
                var original = TravelMenuContext.FromMarker(current, marker);
                string restoreMessage;
                var restored = TravelMenuCompat.TryRestoreOriginalState(original, out restoreMessage);
                TravelMenuStateStore.MarkRestored(original, restoreMessage);
                DisableConfigIfNeeded();
                lock (SyncRoot)
                {
                    _activeOriginalContext = null;
                    EnsureDiagnosticsLocked();
                    if (restored)
                    {
                        _diagnostics.RestoreCount++;
                    }

                    RecordDecisionLocked(
                        restored ? "recovered" : "recoveryFailed",
                        restored ? "orphanMarker" : "orphanMarkerRestoreFailed",
                        restoreMessage,
                        original);
                }

                Logger.Info("TravelMenuService", "Travel menu orphan marker recovery: " + restoreMessage);
                return;
            }

            if (configEnabled)
            {
                DisableConfigIfNeeded();
                RecordDecision("disabled", "staleConfigWithoutMarker", "Travel menu config was enabled without an active marker; disabled for safety.");
            }
        }

        public static bool BeginSaveGuard(string saveKind, out TravelMenuSaveGuardState state)
        {
            state = new TravelMenuSaveGuardState { SaveKind = saveKind ?? string.Empty };
            if (IsSuspended)
            {
                state.Message = SuspendedMessage;
                return false;
            }

            lock (SyncRoot)
            {
                if (_activeOriginalContext == null)
                {
                    state.Message = "No active travel menu session.";
                    return false;
                }

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.WorldAutomationTravelMenuEnabled)
                {
                    state.Message = "Travel menu disabled.";
                    return false;
                }

                string message;
                if (_saveGuardDepth == 0 && !TravelMenuCompat.TryRestoreOriginalState(_activeOriginalContext, out message))
                {
                    state.Message = message;
                    RecordDecisionLocked("failed", "saveGuardRestoreFailed", message, _activeOriginalContext);
                    return false;
                }

                _saveGuardDepth++;
                state.GuardApplied = true;
                state.Message = "Travel menu save guard restored original state before " + state.SaveKind + ".";
                EnsureDiagnosticsLocked();
                _diagnostics.SaveGuardCount++;
                RecordDecisionLocked("saveGuardBegin", state.SaveKind, state.Message, _activeOriginalContext);
                return true;
            }
        }

        public static void EndSaveGuard(TravelMenuSaveGuardState state)
        {
            if (state == null || !state.GuardApplied)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_saveGuardDepth > 0)
                {
                    _saveGuardDepth--;
                }

                if (_saveGuardDepth > 0 || _activeOriginalContext == null)
                {
                    return;
                }

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.WorldAutomationTravelMenuEnabled)
                {
                    return;
                }

                RecordDecisionLocked("saveGuardEnd", state.SaveKind, "Travel menu save guard ended; original state remains active until the next scoped journey hook.", _activeOriginalContext);
            }
        }

        private static TravelMenuToggleResult EnableFromUi(bool openMenu)
        {
            var result = new TravelMenuToggleResult { Enabled = false, ResultCode = "failed" };
            lock (SyncRoot)
            {
                EnsureDiagnosticsLocked();
                if (!_diagnostics.SaveGuardHookInstalled)
                {
                    result.ResultCode = "saveGuardMissing";
                    result.Message = "Travel menu save guard hook is not installed.";
                    result.Detail = _diagnostics.SaveGuardHookMessage;
                    RecordDecisionLocked("rejected", result.ResultCode, result.Message + " " + result.Detail, _activeOriginalContext);
                    return result;
                }
            }

            TravelMenuContext current;
            string captureMessage;
            if (!TravelMenuCompat.TryCaptureCurrentContext(out current, out captureMessage))
            {
                result.ResultCode = "captureFailed";
                result.Message = captureMessage;
                RecordDecision("rejected", result.ResultCode, captureMessage);
                return result;
            }

            TravelMenuRestoreMarker marker;
            var original = TravelMenuStateStore.TryFindActiveMarker(current, out marker)
                ? TravelMenuContext.FromMarker(current, marker)
                : current.Clone();

            TravelMenuStateStore.UpsertActiveMarker(original, "Travel menu enabled from UI.");
            EnableConfigIfNeeded();

            lock (SyncRoot)
            {
                _activeOriginalContext = original;
            }

            var inventoryMessage = EnsureInventoryOpenForSession();
            var opened = true;
            var openMessage = string.IsNullOrWhiteSpace(inventoryMessage)
                ? "Travel menu session armed for scoped journey hooks."
                : inventoryMessage;
            if (openMenu)
            {
                TravelMenuScopedJourneyState openState;
                string scopedOpenMessage;
                if (BeginScopedJourney("uiOpen.ToggleCreativeMenu", out openState))
                {
                    try
                    {
                        opened = TravelMenuCompat.TryOpenCreativeMenu(out scopedOpenMessage);
                    }
                    finally
                    {
                        EndScopedJourney(openState);
                    }

                    openMessage = ((openState == null ? string.Empty : openState.Message) + " " + scopedOpenMessage).Trim();
                }
                else
                {
                    opened = false;
                    scopedOpenMessage = openState == null ? "Scoped journey state could not be applied." : openState.Message;
                    openMessage = scopedOpenMessage;
                }
            }

            lock (SyncRoot)
            {
                EnsureDiagnosticsLocked();
                RecordDecisionLocked(opened ? "enabled" : "enabledMenuOpenUnverified", opened ? "uiOpen" : "openMenuFailed", openMessage, original);
            }

            result.Succeeded = true;
            result.Enabled = true;
            result.OpenedMenu = opened;
            result.ResultCode = opened ? "enabled" : "enabledMenuOpenUnverified";
            result.Message = opened ? "旅行菜单已打开。" : "旅行菜单已启用，但原版菜单打开未确认。";
            result.Detail = openMessage;
            return result;
        }

        private static TravelMenuToggleResult DisableFromUi()
        {
            var result = StopActiveSession("uiClose", true, true);
            if (!result.Succeeded && string.IsNullOrWhiteSpace(result.Message))
            {
                result.Message = "旅行菜单关闭失败。";
            }

            return result;
        }

        private static TravelMenuToggleResult StopActiveSession(string reason, bool closeMenu, bool forceDisableConfig)
        {
            var result = new TravelMenuToggleResult
            {
                Enabled = false,
                ResultCode = "disabled"
            };

            TravelMenuContext original;
            lock (SyncRoot)
            {
                original = _activeOriginalContext;
            }

            if (closeMenu)
            {
                string closeMessage;
                TravelMenuCompat.TryCloseCreativeMenu(out closeMessage);
                result.Detail = closeMessage;
            }

            if (original == null)
            {
                TravelMenuContext current;
                string captureMessage;
                if (TravelMenuCompat.TryCaptureCurrentContext(out current, out captureMessage))
                {
                    TravelMenuRestoreMarker marker;
                    if (TravelMenuStateStore.TryFindActiveMarker(current, out marker))
                    {
                        original = TravelMenuContext.FromMarker(current, marker);
                    }
                }
            }

            var restored = true;
            var restoreMessage = "No active travel menu session.";
            if (original != null)
            {
                restored = TravelMenuCompat.TryRestoreOriginalState(original, out restoreMessage);
                if (restored)
                {
                    TravelMenuStateStore.MarkRestored(original, restoreMessage);
                }
            }

            if (forceDisableConfig || restored)
            {
                DisableConfigIfNeeded();
            }

            string inventoryRestoreMessage = string.Empty;
            var inventoryRestoreAttempted = false;
            var inventoryRestored = true;
            if (forceDisableConfig || restored)
            {
                inventoryRestoreAttempted = true;
                inventoryRestored = TryRestoreInventoryAfterSession(out inventoryRestoreMessage);
                if (!inventoryRestored)
                {
                    restored = false;
                    restoreMessage = (restoreMessage + " " + inventoryRestoreMessage).Trim();
                }
            }

            lock (SyncRoot)
            {
                if (forceDisableConfig || restored)
                {
                    _activeOriginalContext = null;
                    _saveGuardDepth = 0;
                    _creativeUiIgnoreMouseOverrideDepth = 0;
                    TravelMenuCompat.ResetCreativeUiReleasePulseState();
                }

                if (restored)
                {
                    EnsureDiagnosticsLocked();
                    _diagnostics.RestoreCount++;
                }

                RecordDecisionLocked(restored ? "disabled" : "disableRestoreFailed", reason, restoreMessage, original);
            }

            result.Succeeded = restored;
            result.Restored = restored;
            result.ResultCode = restored ? "disabled" : "restoreFailed";
            result.Message = restored ? "旅行菜单已关闭并还原。" : "旅行菜单关闭时还原未确认。";
            result.Detail = (result.Detail + " " + restoreMessage).Trim();
            if (inventoryRestoreAttempted)
            {
                result.Detail = (result.Detail + " " + inventoryRestoreMessage).Trim();
            }
            return result;
        }

        private static void CompleteSessionAfterWorldExit(TravelMenuContext context)
        {
            DisableConfigIfNeeded();
            string inventoryMessage;
            TryRestoreInventoryAfterSession(out inventoryMessage);
            lock (SyncRoot)
            {
                _activeOriginalContext = null;
                _saveGuardDepth = 0;
                _creativeUiIgnoreMouseOverrideDepth = 0;
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                RecordDecisionLocked(
                    "disabled",
                    "leftWorld",
                    "Travel menu session cleared after leaving world; marker kept for crash-safe recovery. " + (inventoryMessage ?? string.Empty),
                    context);
            }
        }

        private static void EnableConfigIfNeeded()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (settings.WorldAutomationTravelMenuEnabled)
            {
                return;
            }

            settings.WorldAutomationTravelMenuEnabled = true;
            ConfigService.SaveAll();
        }

        private static void DisableConfigIfNeeded()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (!settings.WorldAutomationTravelMenuEnabled)
            {
                return;
            }

            settings.WorldAutomationTravelMenuEnabled = false;
            ConfigService.SaveAll();
        }

        private static void RecordDecision(string decision, string reason, string message)
        {
            lock (SyncRoot)
            {
                RecordDecisionLocked(decision, reason, message, _activeOriginalContext);
            }
        }

        private static void RecordDecisionLocked(string decision, string reason, string message, TravelMenuContext context)
        {
            EnsureDiagnosticsLocked();
            _diagnostics.LastDecision = decision ?? string.Empty;
            _diagnostics.LastReason = reason ?? string.Empty;
            _diagnostics.LastMessage = message ?? string.Empty;
            _diagnostics.LastDecisionUtc = DateTime.UtcNow;
            if (context != null)
            {
                _diagnostics.PlayerPath = context.PlayerPath ?? string.Empty;
                _diagnostics.WorldPath = context.WorldPath ?? string.Empty;
                _diagnostics.OriginalPlayerDifficulty = context.PlayerDifficulty;
                _diagnostics.OriginalWorldGameMode = context.WorldGameMode;
                _diagnostics.OriginalMainGameMode = context.MainGameMode;
            }
        }

        private static void EnsureDiagnosticsLocked()
        {
            if (_diagnostics == null)
            {
                _diagnostics = new TravelMenuDiagnosticInfo();
            }
        }

        private static string EnsureInventoryOpenForSession()
        {
            bool inventoryOpen;
            string readMessage;
            if (!TravelMenuCompat.TryReadPlayerInventoryOpen(out inventoryOpen, out readMessage))
            {
                lock (SyncRoot)
                {
                    ClearInventorySessionStateLocked();
                }

                return "Travel menu inventory read failed: " + readMessage;
            }

            if (!inventoryOpen)
            {
                string setMessage;
                if (!TravelMenuCompat.TrySetPlayerInventoryOpen(true, out setMessage))
                {
                    lock (SyncRoot)
                    {
                        ClearInventorySessionStateLocked();
                    }

                    return "Travel menu inventory open failed: " + setMessage;
                }
            }

            lock (SyncRoot)
            {
                _inventoryStateCaptured = true;
                _inventoryOpenBeforeSession = inventoryOpen;
                _inventoryForcedOpen = !inventoryOpen;
            }

            return inventoryOpen
                ? "Main.playerInventory already open."
                : "Main.playerInventory opened for travel menu.";
        }

        private static bool TryRestoreInventoryAfterSession(out string message)
        {
            bool restoreNeeded;
            bool originalOpen;
            lock (SyncRoot)
            {
                restoreNeeded = _inventoryStateCaptured && _inventoryForcedOpen;
                originalOpen = _inventoryOpenBeforeSession;
            }

            if (!restoreNeeded)
            {
                lock (SyncRoot)
                {
                    ClearInventorySessionStateLocked();
                }

                message = "Main.playerInventory kept unchanged.";
                return true;
            }

            string restoreMessage;
            var restored = TravelMenuCompat.TrySetPlayerInventoryOpen(originalOpen, out restoreMessage);
            lock (SyncRoot)
            {
                ClearInventorySessionStateLocked();
            }

            message = restored
                ? "Main.playerInventory restored after travel menu session."
                : "Main.playerInventory restore failed: " + restoreMessage;
            return restored;
        }

        private static void ClearInventorySessionStateLocked()
        {
            _inventoryStateCaptured = false;
            _inventoryOpenBeforeSession = false;
            _inventoryForcedOpen = false;
        }

        private static TravelMenuToggleResult CreateSuspendedToggleResult()
        {
            DisableConfigIfNeeded();
            lock (SyncRoot)
            {
                _activeOriginalContext = null;
                _saveGuardDepth = 0;
                _creativeUiIgnoreMouseOverrideDepth = 0;
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                ClearInventorySessionStateLocked();
                EnsureDiagnosticsLocked();
                ApplySuspendedDiagnostics(_diagnostics);
                RecordDecisionLocked("suspended", "featureSuspended", SuspendedMessage, null);
            }

            return new TravelMenuToggleResult
            {
                Succeeded = false,
                Enabled = false,
                OpenedMenu = false,
                Restored = true,
                ResultCode = SuspendedResultCode,
                Message = "旅行菜单已搁置，整理完成前不会启用。",
                Detail = SuspendedMessage
            };
        }

        private static void ApplySuspendedDiagnostics(TravelMenuDiagnosticInfo diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            diagnostics.Enabled = false;
            diagnostics.SessionActive = false;
            diagnostics.SaveGuardHookInstalled = false;
            diagnostics.SaveGuardHookMessage = SuspendedMessage;
            diagnostics.CreativeUiHookInstalled = false;
            diagnostics.CreativeUiHookMessage = SuspendedMessage;
            diagnostics.ScopedPowerHookInstalled = false;
            diagnostics.ScopedPowerHookMessage = SuspendedMessage;
        }

        public static bool ShouldOverrideIgnoreMouseInterfaceForTravelMenu()
        {
            if (IsSuspended)
            {
                return false;
            }

            bool scopeActive;
            bool hasSession;
            bool saveGuardInactive;
            bool configEnabled;
            lock (SyncRoot)
            {
                scopeActive = _creativeUiIgnoreMouseOverrideDepth > 0;
                hasSession = _activeOriginalContext != null;
                saveGuardInactive = _saveGuardDepth <= 0;
                configEnabled = ConfigService.AppSettings != null && ConfigService.AppSettings.WorldAutomationTravelMenuEnabled;
            }

            if (!scopeActive || !saveGuardInactive)
            {
                return false;
            }

            if (hasSession)
            {
                if (!configEnabled)
                {
                    return false;
                }
            }
            else
            {
                bool nativeJourney;
                string nativeJourneyMessage;
                if (!TravelMenuCompat.TryReadCurrentJourneyMode(out nativeJourney, out nativeJourneyMessage) || !nativeJourney)
                {
                    return false;
                }
            }

            bool creativeMenuEnabled;
            string creativeMenuMessage;
            if (!TravelMenuCompat.TryReadCreativeMenuEnabled(out creativeMenuEnabled, out creativeMenuMessage) || !creativeMenuEnabled)
            {
                return false;
            }

            return true;
        }

        public static bool ShouldOverrideCreativeResearchForTravelMenu()
        {
            return ShouldOverrideScopedJueMingZTravelMenu();
        }

        private static bool ShouldOverrideScopedJueMingZTravelMenu()
        {
            if (IsSuspended)
            {
                return false;
            }

            bool scopeActive;
            bool hasSession;
            bool saveGuardInactive;
            bool configEnabled;
            lock (SyncRoot)
            {
                scopeActive = _creativeUiIgnoreMouseOverrideDepth > 0;
                hasSession = _activeOriginalContext != null;
                saveGuardInactive = _saveGuardDepth <= 0;
                configEnabled = ConfigService.AppSettings != null && ConfigService.AppSettings.WorldAutomationTravelMenuEnabled;
            }

            if (!scopeActive || !hasSession || !saveGuardInactive || !configEnabled)
            {
                return false;
            }

            bool creativeMenuEnabled;
            string creativeMenuMessage;
            return TravelMenuCompat.TryReadCreativeMenuEnabled(out creativeMenuEnabled, out creativeMenuMessage) && creativeMenuEnabled;
        }

        public static bool ShouldPauseAutomationForTravelMenu()
        {
            if (IsSuspended)
            {
                return false;
            }

            bool shouldCheckUi;
            lock (SyncRoot)
            {
                shouldCheckUi = _activeOriginalContext != null &&
                                ConfigService.AppSettings != null &&
                                ConfigService.AppSettings.WorldAutomationTravelMenuEnabled;
            }

            if (!shouldCheckUi)
            {
                return false;
            }

            bool creativeMenuEnabled;
            string creativeMenuMessage;
            if (!TravelMenuCompat.TryReadCreativeMenuEnabled(out creativeMenuEnabled, out creativeMenuMessage) || !creativeMenuEnabled)
            {
                return false;
            }

            bool inventoryOpen;
            string inventoryMessage;
            return TravelMenuCompat.TryReadPlayerInventoryOpen(out inventoryOpen, out inventoryMessage) && inventoryOpen;
        }

        private static bool TryBeginScopedIgnoreMouseInterfaceOverride(TravelMenuScopedJourneyState state, out string message)
        {
            message = string.Empty;
            if (state == null)
            {
                message = "Scoped journey state unavailable for IgnoreMouseInterface override.";
                return false;
            }

            lock (SyncRoot)
            {
                _creativeUiIgnoreMouseOverrideDepth++;
            }

            state.IgnoreMouseInterfaceOverrideApplied = true;
            message = "IgnoreMouseInterface getter override armed for scoped creative UI execution.";
            return true;
        }

        private static bool TryEndScopedIgnoreMouseInterfaceOverride(TravelMenuScopedJourneyState state, out string message)
        {
            message = string.Empty;
            if (state == null || !state.IgnoreMouseInterfaceOverrideApplied)
            {
                return true;
            }

            int depthAfter;
            bool decremented;
            lock (SyncRoot)
            {
                decremented = _creativeUiIgnoreMouseOverrideDepth > 0;
                if (_creativeUiIgnoreMouseOverrideDepth > 0)
                {
                    _creativeUiIgnoreMouseOverrideDepth--;
                }

                depthAfter = _creativeUiIgnoreMouseOverrideDepth;
            }

            state.IgnoreMouseInterfaceOverrideApplied = false;
            if (decremented)
            {
                message = "IgnoreMouseInterface getter override scope released.";
                return true;
            }

            message = "IgnoreMouseInterface getter override scope release underflow; depth=" + depthAfter + ".";
            return false;
        }

        private static bool ShouldApplyCreativeUiInputBypass(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return false;
            }

            return string.Equals(scope, "CreativeUI.Update", StringComparison.Ordinal) ||
                   string.Equals(scope, "CreativeUI.Draw", StringComparison.Ordinal);
        }

        private static bool ShouldApplyCreativeUiWorldInputGuard(string scope)
        {
            return string.Equals(scope, "CreativeUI.Update", StringComparison.Ordinal);
        }

        private static bool RequiresGodmodePowerScopedJourney(string scope)
        {
            return string.Equals(scope, PlayerHurtScope, StringComparison.Ordinal);
        }
    }
}
