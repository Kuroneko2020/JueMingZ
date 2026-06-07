using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using Terraria;

namespace JueMingZ.Automation.Combat
{
    // Auto-facing chooses direction from snapshots and queues work; direct direction writes belong to action executors.
    public static class CombatAutoFacingService
    {
        private const string FeatureId = FeatureIds.CombatAutoFacing;
        private const int CooldownTicks = 10;
        private const int FallbackRadiusTiles = 36;
        private const float DirectionDeadZonePixels = 2f;
        private static readonly TimeSpan ManualInputQueueTimeout = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan TargetQueueTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly object SyncRoot = new object();
        private static readonly object DiagnosticsSyncRoot = new object();
        private static long _nextAllowedTick;
        private static CombatAutoFacingDiagnosticInfo _diagnostics = new CombatAutoFacingDiagnosticInfo();

        public static CombatAutoFacingDiagnosticInfo GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            Tick(queue, snapshot, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            try
            {
                settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
                var settings = settingsSnapshot.SourceSettings ?? AppSettings.CreateDefault();
                if (!settingsSnapshot.CombatAutoFacingEnabled)
                {
                    ResetThrottle();
                    RecordSkip(false, "disabled", tick, null, null, null, 0, "disabled");
                    return;
                }

                if (queue == null)
                {
                    RecordSkip(true, "queueUnavailable", tick, null, null, null, 0, "skipped");
                    return;
                }

                if (snapshot == null || !snapshot.IsInWorld)
                {
                    RecordSkip(true, "notInWorld", tick, null, null, null, 0, "skipped");
                    return;
                }

                if (CombatAutomationDecisionDiagnostics.IsBlockingSnapshotUiForCombat(snapshot.Ui))
                {
                    RecordSkip(true, CombatAutomationDecisionDiagnostics.BuildSnapshotUiSkipReason(snapshot.Ui), tick, null, null, null, 0, "skipped");
                    return;
                }

                var queueSnapshot = queue.GetFastState();
                if (queue.IsSourcePendingOrRunning(FeatureId))
                {
                    RecordSkip(true, "sourceBusy", tick, queueSnapshot, null, null, 0, "skipped");
                    return;
                }

                if (queue.IsAnyChannelBusy(InputActionChannel.UseItem | InputActionChannel.BridgeItemUse | InputActionChannel.BridgeUseItemPulse))
                {
                    RecordSkip(true, "useItemChannelBusy", tick, queueSnapshot, null, null, 0, "skipped");
                    return;
                }

                object player;
                if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
                {
                    SetCooldown(tick);
                    RecordSkip(true, "localPlayerUnavailable", tick, queueSnapshot, null, null, 0, "skipped");
                    return;
                }

                var ui = TerrariaInputCompat.ReadUiInputContext(player);
                if (ui.MainTypeUnavailable ||
                    ui.GameMenu ||
                    ui.ChatOpen ||
                    ui.NpcChatOpen ||
                    ui.MouseCapturedByUi)
                {
                    SetCooldown(tick);
                    RecordSkip(true, CombatAutomationDecisionDiagnostics.BuildUiSkipReason(ui), tick, queueSnapshot, null, null, 0, "skipped");
                    return;
                }

                AutoFacingProfile profile;
                string reason;
                if (!TryReadProfile(player, out profile, out reason))
                {
                    SetCooldown(tick);
                    RecordSkip(true, "profile:" + reason, tick, queueSnapshot, profile, null, 0, "skipped");
                    return;
                }

                if (!IsCooldownReady(tick))
                {
                    RecordSkip(true, "cooldown", tick, queueSnapshot, profile, null, 0, "skipped");
                    return;
                }

                if (!IsEligible(profile, out reason))
                {
                    SetCooldown(tick);
                    RecordSkip(true, "ineligible:" + reason, tick, queueSnapshot, profile, null, 0, "skipped");
                    return;
                }

                AutoFacingTarget manualTarget;
                int manualDirection;
                if (TryResolveManualMovementFacing(player, profile, out manualTarget, out manualDirection))
                {
                    if (profile.CurrentDirection == manualDirection)
                    {
                        RecordSkip(true, "manualMovementAlreadyFacing", tick, queueSnapshot, profile, manualTarget, manualDirection, "skipped");
                        return;
                    }

                    string channelReason;
                    if (!TryEnqueueAutoFacingRequest(
                        queue,
                        profile,
                        manualTarget,
                        manualDirection,
                        InputActionPriority.High,
                        "Combat auto facing turns the local player by manual A/D input",
                        "manualMovementInput",
                        ManualInputQueueTimeout,
                        snapshot,
                        out channelReason))
                    {
                        RecordSkip(true, channelReason, tick, queueSnapshot, profile, manualTarget, manualDirection, "skipped");
                        return;
                    }

                    RecordSubmitted(true, tick, queueSnapshot, profile, manualTarget, manualDirection);
                    return;
                }

                if (!IsCooldownReady(tick))
                {
                    RecordSkip(true, "cooldown", tick, queueSnapshot, profile, null, 0, "skipped");
                    return;
                }

                AutoFacingTarget target;
                if (!TryResolveFacingTarget(player, settings, profile, out target, out reason))
                {
                    SetCooldown(tick);
                    RecordSkip(true, "target:" + reason, tick, queueSnapshot, profile, null, 0, "skipped");
                    return;
                }

                int desiredDirection;
                if (!TryResolveDesiredDirection(profile, target.CenterX, out desiredDirection))
                {
                    SetCooldown(tick);
                    RecordSkip(true, "directionDeadZone", tick, queueSnapshot, profile, target, 0, "skipped");
                    return;
                }

                if (profile.CurrentDirection == desiredDirection)
                {
                    SetCooldown(tick);
                    RecordSkip(true, "alreadyFacing", tick, queueSnapshot, profile, target, desiredDirection, "skipped");
                    return;
                }

                string targetChannelReason;
                if (!TryEnqueueAutoFacingRequest(
                    queue,
                    profile,
                    target,
                    desiredDirection,
                    InputActionPriority.Low,
                    "Combat auto facing turns the local player toward the selected target",
                    "targetOrCursor",
                    TargetQueueTimeout,
                    snapshot,
                    out targetChannelReason))
                {
                    RecordSkip(true, targetChannelReason, tick, queueSnapshot, profile, target, desiredDirection, "skipped");
                    return;
                }

                SetCooldown(tick);
                RecordSubmitted(true, tick, queueSnapshot, profile, target, desiredDirection);
            }
            catch (Exception error)
            {
                RecordSkip(true, "exception:" + error.GetType().Name, tick, null, null, null, 0, "exception");
                RuntimeDiagnostics.RecordError("CombatAutoFacingService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "combat-auto-facing-tick-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAutoFacingService",
                    "Combat auto facing tick failed; exception swallowed.", error);
            }
        }

        public static bool TryApplyManualMovementFacingForItemCheck(object player, out bool applied, out string message)
        {
            applied = false;
            message = string.Empty;
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.CombatAutoFacingEnabled)
                {
                    return false;
                }

                if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
                {
                    return false;
                }

                var ui = TerrariaInputCompat.ReadUiInputContext(player);
                if (ui.MainTypeUnavailable ||
                    ui.GameMenu ||
                    ui.ChatOpen ||
                    ui.NpcChatOpen ||
                    ui.MouseCapturedByUi)
                {
                    return false;
                }

                AutoFacingProfile profile;
                string reason;
                if (!TryReadProfile(player, out profile, out reason) || !profile.UseItemHeld)
                {
                    return false;
                }

                if (!IsEligible(profile, out reason))
                {
                    return false;
                }

                AutoFacingTarget target;
                int desiredDirection;
                if (!TryResolveManualMovementFacing(player, profile, out target, out desiredDirection))
                {
                    return false;
                }

                int beforeDirection;
                int afterDirection;
                string method;
                if (!TerrariaInputCompat.TryChangePlayerDirection(player, desiredDirection, true, out beforeDirection, out afterDirection, out method))
                {
                    message = "Manual movement facing failed: " + TerrariaInputCompat.LastInputCompatError;
                    RecordImmediateItemCheck(false, "itemCheckManualMovementFailed", profile.GameUpdateCount, profile, target, desiredDirection);
                    return false;
                }

                applied = afterDirection == (desiredDirection >= 0 ? 1 : -1);
                message = applied
                    ? beforeDirection == afterDirection
                        ? "Manual movement facing already matched " + DirectionName(desiredDirection) + "."
                        : "Manual movement facing changed to " + DirectionName(desiredDirection) + " via " + method + "."
                    : "Manual movement facing attempted via " + method + ".";
                RecordImmediateItemCheck(true, applied ? "itemCheckManualMovementApplied" : "itemCheckManualMovementAttempted", profile.GameUpdateCount, profile, target, desiredDirection);
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("CombatAutoFacingService.TryApplyManualMovementFacingForItemCheck", error);
                message = "Manual movement facing failed with exception: " + error.GetType().Name;
                return false;
            }
        }

        private static bool TryEnqueueAutoFacingRequest(
            InputActionQueue queue,
            AutoFacingProfile profile,
            AutoFacingTarget target,
            int desiredDirection,
            InputActionPriority priority,
            string description,
            string directionSource,
            TimeSpan timeout,
            GameStateSnapshot snapshot,
            out string channelReason)
        {
            channelReason = string.Empty;
            var request = CreateRequest(profile, target, desiredDirection, priority, description, directionSource, timeout);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                channelReason = "admissionDenied:" + (admission == null ? "unknown" : admission.Reason);
                return false;
            }

            return true;
        }

        internal static InputActionRequest CreateRequestForTesting(int desiredDirection, string directionSource)
        {
            var profile = new AutoFacingProfile
            {
                SelectedSlot = 0,
                PlayerCenterX = 0f,
                PlayerCenterY = 0f,
                WeaponProfile = new CombatAimWeaponProfile()
            };

            var target = new AutoFacingTarget
            {
                WhoAmI = -1,
                Type = 0,
                Name = "Test",
                CenterX = desiredDirection < 0 ? -16f : 16f,
                CenterY = 0f,
                SelectionSource = directionSource ?? string.Empty,
                SelectionRadiusTiles = 0
            };
            return CreateRequest(
                profile,
                target,
                desiredDirection,
                InputActionPriority.Low,
                "Combat auto facing test request",
                directionSource,
                TargetQueueTimeout);
        }

        private static InputActionRequest CreateRequest(
            AutoFacingProfile profile,
            AutoFacingTarget target,
            int desiredDirection,
            InputActionPriority priority,
            string description,
            string directionSource,
            TimeSpan timeout)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.RawInput,
                Priority = priority,
                SourceFeatureId = FeatureId,
                Description = description,
                QueueTimeout = timeout,
                Timeout = timeout,
                IsExclusive = true
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.CombatAutoFacing;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.RawInputMode] = "AutoFacing";
            request.Metadata[ActionMetadataKeys.AutoFacingDirection] = desiredDirection.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoFacingDirectionSource"] = directionSource ?? string.Empty;
            request.Metadata["AutoFacingSelectedSlot"] = profile.SelectedSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoFacingItemType"] = profile.WeaponProfile == null ? "0" : profile.WeaponProfile.ItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoFacingItemName"] = profile.WeaponProfile == null ? string.Empty : profile.WeaponProfile.Name ?? string.Empty;
            request.Metadata["AutoFacingTargetWhoAmI"] = target.WhoAmI.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoFacingTargetType"] = target.Type.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoFacingTargetName"] = target.Name ?? string.Empty;
            request.Metadata["AutoFacingTargetCenterX"] = FloatString(target.CenterX);
            request.Metadata["AutoFacingTargetCenterY"] = FloatString(target.CenterY);
            request.Metadata["AutoFacingPlayerCenterX"] = FloatString(profile.PlayerCenterX);
            request.Metadata["AutoFacingPlayerCenterY"] = FloatString(profile.PlayerCenterY);
            request.Metadata["AutoFacingSelectionSource"] = target.SelectionSource ?? string.Empty;
            request.Metadata["AutoFacingSelectionRadiusTiles"] = target.SelectionRadiusTiles.ToString(CultureInfo.InvariantCulture);
            request.AdmissionKey = FeatureId + "|RawInput|" +
                                   desiredDirection.ToString(CultureInfo.InvariantCulture) +
                                   "|" + (directionSource ?? string.Empty);
            return request;
        }

        private static bool TryReadProfile(object player, out AutoFacingProfile profile, out string reason)
        {
            profile = new AutoFacingProfile();
            reason = string.Empty;
            if (player == null)
            {
                reason = "playerUnavailable";
                return false;
            }

            var typedPlayer = player as Player;
            bool active;
            bool dead;
            bool ghost;
            if (typedPlayer != null)
            {
                active = typedPlayer.active;
                dead = typedPlayer.dead;
                ghost = typedPlayer.ghost;
            }
            else
            {
                GameStateReflection.TryGetBool(player, "active", out active);
                GameStateReflection.TryGetBool(player, "dead", out dead);
                GameStateReflection.TryGetBool(player, "ghost", out ghost);
            }
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
            profile.ItemAnimation = input.ItemAnimation;
            profile.ItemTime = input.ItemTime;
            profile.GameUpdateCount = input.GameUpdateCount;
            if (!profile.UseItemHeld && profile.ItemAnimation <= 0 && profile.ItemTime <= 0)
            {
                reason = "notUsingCombatItem";
                return false;
            }

            if (!CombatAimPlayerContext.TryReadPlayerCenter(player, out var playerCenterX, out var playerCenterY))
            {
                reason = "playerCenterUnavailable";
                return false;
            }

            profile.PlayerCenterX = playerCenterX;
            profile.PlayerCenterY = playerCenterY;
            TerrariaInputCompat.TryReadPlayerDirection(player, out profile.CurrentDirection);

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

            object item;
            if (typedPlayer != null)
            {
                var inventory = typedPlayer.inventory;
                if (inventory == null || selectedSlot >= inventory.Length)
                {
                    reason = "inventoryUnavailable";
                    return false;
                }

                item = inventory[selectedSlot];
            }
            else
            {
                var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
                if (inventory == null || selectedSlot >= inventory.Count)
                {
                    reason = "inventoryUnavailable";
                    return false;
                }

                item = inventory[selectedSlot];
            }

            if (item == null)
            {
                reason = "selectedItemUnavailable";
                return false;
            }

            profile.WeaponProfile = CombatAimWeaponProfile.Read(player, item);
            return true;
        }

        private static bool IsEligible(AutoFacingProfile profile, out string reason)
        {
            reason = string.Empty;
            var weapon = profile == null ? null : profile.WeaponProfile;
            if (weapon == null || weapon.IsEmpty)
            {
                reason = "selectedItemEmpty";
                return false;
            }

            if (weapon.IsCoinGun && !weapon.CoinAmmoAvailable)
            {
                reason = "coinGunNoCoinAmmo";
                return false;
            }

            if (!weapon.IsCoinGun && weapon.Damage <= 0)
            {
                reason = "damageNotPositive";
                return false;
            }

            if (weapon.IsPlacementItem)
            {
                reason = "placementItem";
                return false;
            }

            if (weapon.IsToolOrFishingItem)
            {
                reason = "toolOrFishingItem";
                return false;
            }

            if (weapon.IsAmmoItem)
            {
                reason = "ammoItem";
                return false;
            }

            if (weapon.IsSentryPlacementWeapon)
            {
                reason = "sentryPlacementWeapon";
                return false;
            }

            if (weapon.IsSummonPlacementWeapon)
            {
                reason = "summonPlacementWeapon";
                return false;
            }

            if (!weapon.HasWeaponUseSemantics && !weapon.IsCoinGun)
            {
                reason = "notProjectileAmmoOrMelee";
                return false;
            }

            return true;
        }

        private static bool TryResolveManualMovementFacing(
            object player,
            AutoFacingProfile profile,
            out AutoFacingTarget target,
            out int desiredDirection)
        {
            target = null;
            desiredDirection = 0;
            if (player == null || profile == null || !profile.UseItemHeld)
            {
                return false;
            }

            bool leftHeld;
            bool rightHeld;
            if (!TerrariaInputCompat.TryReadHorizontalMovementDirection(player, out desiredDirection, out leftHeld, out rightHeld) ||
                desiredDirection == 0)
            {
                return false;
            }

            var source = leftHeld ? "manualMovementInput:left" : rightHeld ? "manualMovementInput:right" : "manualMovementInput";
            target = new AutoFacingTarget
            {
                WhoAmI = -1,
                Type = 0,
                Name = desiredDirection < 0 ? "MoveLeft" : "MoveRight",
                CenterX = profile.PlayerCenterX + (desiredDirection < 0 ? -16f : 16f),
                CenterY = profile.PlayerCenterY,
                SelectionSource = source,
                SelectionRadiusTiles = 0
            };
            return true;
        }

        private static bool TryResolveFacingTarget(
            object player,
            AppSettings settings,
            AutoFacingProfile profile,
            out AutoFacingTarget target,
            out string reason)
        {
            target = null;
            reason = string.Empty;

            var currentSelection = CombatAutoAimService.CurrentSelection;
            if (currentSelection != null &&
                currentSelection.Enabled &&
                currentSelection.Target != null &&
                string.Equals(currentSelection.ResultCode, "TargetSelected", StringComparison.OrdinalIgnoreCase))
            {
                target = FromSelection(currentSelection, "currentAutoAimSelection", currentSelection.RadiusTiles);
                return true;
            }

            var readResult = CombatAimTargetReader.Read(settings.CombatAimTrackDummyEnabled);
            CombatAimTargetHistoryService.Enrich(readResult == null ? null : readResult.Candidates);
            if (readResult == null)
            {
                reason = "targetReadUnavailable:null";
                return false;
            }

            if (!readResult.CanSearch)
            {
                if (readResult.HasCursorWorld)
                {
                    target = CreateCursorFallbackTarget(readResult, "cursorFallback:" + readResult.SkipReason);
                    return true;
                }

                reason = "targetReadUnavailable:" + readResult.SkipReason;
                return false;
            }

            var range = ResolveAutoFacingRange(settings, readResult, profile);
            var context = new CombatAimTargetSelectionContext
            {
                AimRangeOrigin = range.AimRangeOrigin,
                AimTargetPriority = CombatAimModes.NormalizeTargetPriority(settings.AimTargetPriority),
                CursorAimRadius = range.CursorAimRadius,
                PlayerAimRadius = range.PlayerAimRadius,
                Player = player,
                HasPlayerCenter = true,
                PlayerCenterX = profile.PlayerCenterX,
                PlayerCenterY = profile.PlayerCenterY,
                HasResolvedRange = true,
                Range = range,
                SelectionPurpose = "AutoFacing",
                WeaponProfile = profile.WeaponProfile
            };

            var selection = CombatAimTargetSelector.Select(
                readResult,
                range.RadiusTiles,
                settings.CombatAimTrackDummyEnabled,
                settings.CombatAimMarkerEnabled,
                context);

            if (selection != null && selection.Target != null)
            {
                target = FromSelection(
                    selection,
                    range.RangeMode == "AutoFacingFallbackPlayerRadius"
                        ? "playerFallbackRadius"
                        : "resolvedAutoAimRange",
                    range.RadiusTiles);
                return true;
            }

            if (!string.Equals(range.RangeMode, "AutoFacingFallbackPlayerRadius", StringComparison.OrdinalIgnoreCase) &&
                TryResolvePlayerFallbackTarget(
                    readResult,
                    settings,
                    player,
                    profile,
                    selection == null ? "selectionUnavailable" : selection.ResultCode,
                    out target))
            {
                return true;
            }

            target = CreateCursorFallbackTarget(
                readResult,
                selection == null ? "cursorFallback:selectionUnavailable" : "cursorFallback:" + selection.ResultCode);
            return true;
        }

        private static bool TryResolvePlayerFallbackTarget(
            CombatAimReadResult readResult,
            AppSettings settings,
            object player,
            AutoFacingProfile profile,
            string primaryResultCode,
            out AutoFacingTarget target)
        {
            target = null;
            if (readResult == null || profile == null)
            {
                return false;
            }

            var range = new CombatAimRangeResolveResult
            {
                Enabled = true,
                RangeMode = "AutoFacingPlayerFallbackAfterPrimaryMiss",
                AimRangeOrigin = CombatAimModes.RangeOriginPlayer,
                RadiusTiles = FallbackRadiusTiles,
                RadiusPixels = FallbackRadiusTiles * 16f,
                RangeCenterWorldX = profile.PlayerCenterX,
                RangeCenterWorldY = profile.PlayerCenterY,
                CursorAimRadius = FallbackRadiusTiles,
                PlayerAimRadius = FallbackRadiusTiles,
                PlayerScreenMarginTiles = CombatAimRangeResolver.PlayerScreenMarginTiles,
                PlayerScreenRadiusTiles = FallbackRadiusTiles
            };
            var context = new CombatAimTargetSelectionContext
            {
                AimRangeOrigin = CombatAimModes.RangeOriginPlayer,
                AimTargetPriority = CombatAimModes.NormalizeTargetPriority(settings.AimTargetPriority),
                CursorAimRadius = FallbackRadiusTiles,
                PlayerAimRadius = FallbackRadiusTiles,
                Player = player,
                HasPlayerCenter = true,
                PlayerCenterX = profile.PlayerCenterX,
                PlayerCenterY = profile.PlayerCenterY,
                HasResolvedRange = true,
                Range = range,
                SelectionPurpose = "AutoFacing",
                WeaponProfile = profile.WeaponProfile
            };

            var selection = CombatAimTargetSelector.Select(
                readResult,
                FallbackRadiusTiles,
                settings.CombatAimTrackDummyEnabled,
                settings.CombatAimMarkerEnabled,
                context);
            if (selection == null || selection.Target == null)
            {
                return false;
            }

            target = FromSelection(selection, "playerFallbackRadius:primary:" + (primaryResultCode ?? string.Empty), FallbackRadiusTiles);
            return target != null;
        }

        private static AutoFacingTarget CreateCursorFallbackTarget(CombatAimReadResult readResult, string source)
        {
            return new AutoFacingTarget
            {
                WhoAmI = -1,
                Type = 0,
                Name = "Cursor",
                CenterX = readResult == null ? 0f : readResult.CursorWorldX,
                CenterY = readResult == null ? 0f : readResult.CursorWorldY,
                SelectionSource = source ?? string.Empty,
                SelectionRadiusTiles = 0
            };
        }

        private static AutoFacingTarget FromSelection(CombatAimTargetSelection selection, string source, int radiusTiles)
        {
            var selectedTarget = selection == null ? null : selection.Target;
            if (selectedTarget == null)
            {
                return null;
            }

            return new AutoFacingTarget
            {
                WhoAmI = selectedTarget.WhoAmI,
                Type = selectedTarget.Type,
                Name = selectedTarget.Name ?? string.Empty,
                CenterX = selectedTarget.CenterX,
                CenterY = selectedTarget.CenterY,
                SelectionSource = source ?? string.Empty,
                SelectionRadiusTiles = radiusTiles
            };
        }

        private static bool TryResolveDesiredDirection(AutoFacingProfile profile, float targetCenterX, out int direction)
        {
            direction = 0;
            if (profile == null)
            {
                return false;
            }

            var deltaX = targetCenterX - profile.PlayerCenterX;
            if (deltaX > DirectionDeadZonePixels)
            {
                direction = 1;
                return true;
            }

            if (deltaX < -DirectionDeadZonePixels)
            {
                direction = -1;
                return true;
            }

            return false;
        }

        private static CombatAimRangeResolveResult ResolveAutoFacingRange(
            AppSettings settings,
            CombatAimReadResult readResult,
            AutoFacingProfile profile)
        {
            var resolved = CombatAimRangeResolver.Resolve(
                settings,
                readResult,
                true,
                profile.PlayerCenterX,
                profile.PlayerCenterY);
            if (resolved != null && resolved.Enabled)
            {
                return resolved;
            }

            return new CombatAimRangeResolveResult
            {
                Enabled = true,
                RangeMode = "AutoFacingFallbackPlayerRadius",
                AimRangeOrigin = CombatAimModes.RangeOriginPlayer,
                RadiusTiles = FallbackRadiusTiles,
                RadiusPixels = FallbackRadiusTiles * 16f,
                RangeCenterWorldX = profile.PlayerCenterX,
                RangeCenterWorldY = profile.PlayerCenterY,
                CursorAimRadius = FallbackRadiusTiles,
                PlayerAimRadius = FallbackRadiusTiles,
                PlayerScreenMarginTiles = CombatAimRangeResolver.PlayerScreenMarginTiles,
                PlayerScreenRadiusTiles = FallbackRadiusTiles
            };
        }

        private static bool IsCooldownReady(long tick)
        {
            lock (SyncRoot)
            {
                return tick >= _nextAllowedTick;
            }
        }

        private static void SetCooldown(long tick)
        {
            lock (SyncRoot)
            {
                _nextAllowedTick = tick + CooldownTicks;
            }
        }

        private static void ResetThrottle()
        {
            lock (SyncRoot)
            {
                _nextAllowedTick = 0;
            }
        }

        private static string FloatString(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void RecordSkip(
            bool enabled,
            string reason,
            long tick,
            InputActionQueueFastState queueSnapshot,
            AutoFacingProfile profile,
            AutoFacingTarget target,
            int desiredDirection,
            string decision)
        {
            RecordDecision(enabled, decision ?? "skipped", reason, tick, queueSnapshot, profile, target, desiredDirection, false);
        }

        private static void RecordSubmitted(
            bool enabled,
            long tick,
            InputActionQueueFastState queueSnapshot,
            AutoFacingProfile profile,
            AutoFacingTarget target,
            int desiredDirection)
        {
            RecordDecision(enabled, "submitted", string.Empty, tick, queueSnapshot, profile, target, desiredDirection, true);
        }

        private static void RecordDecision(
            bool enabled,
            string decision,
            string reason,
            long tick,
            InputActionQueueFastState queueSnapshot,
            AutoFacingProfile profile,
            AutoFacingTarget target,
            int desiredDirection,
            bool submitted)
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new CombatAutoFacingDiagnosticInfo() : _diagnostics.Clone();
                current.Enabled = enabled;
                current.LastDecision = decision ?? string.Empty;
                current.LastSkipReason = submitted ? string.Empty : reason ?? string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.LastTick = tick;
                current.PendingActionCount = queueSnapshot == null ? 0 : queueSnapshot.PendingCount;
                current.RunningActionKind = queueSnapshot == null ? string.Empty : queueSnapshot.RunningActionKind ?? string.Empty;
                current.ItemUseBridgeBusy = ItemUseBridge.PendingRequestId != Guid.Empty;
                current.DesiredDirection = desiredDirection;

                if (profile != null)
                {
                    current.SelectedSlot = profile.SelectedSlot;
                    current.CurrentDirection = profile.CurrentDirection;
                    if (profile.WeaponProfile != null)
                    {
                        current.ItemType = profile.WeaponProfile.ItemType;
                        current.ItemName = profile.WeaponProfile.Name ?? string.Empty;
                    }
                }

                if (target != null)
                {
                    current.TargetSource = target.SelectionSource ?? string.Empty;
                    current.TargetWhoAmI = target.WhoAmI;
                    current.TargetType = target.Type;
                    current.TargetName = target.Name ?? string.Empty;
                }

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

        private static void RecordImmediateItemCheck(
            bool enabled,
            string decision,
            long tick,
            AutoFacingProfile profile,
            AutoFacingTarget target,
            int desiredDirection)
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new CombatAutoFacingDiagnosticInfo() : _diagnostics.Clone();
                current.Enabled = enabled;
                current.LastDecision = decision ?? string.Empty;
                current.LastSkipReason = string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.LastTick = tick;
                current.PendingActionCount = 0;
                current.RunningActionKind = string.Empty;
                current.ItemUseBridgeBusy = ItemUseBridge.PendingRequestId != Guid.Empty;
                current.DesiredDirection = desiredDirection;

                if (profile != null)
                {
                    current.SelectedSlot = profile.SelectedSlot;
                    current.CurrentDirection = profile.CurrentDirection;
                    if (profile.WeaponProfile != null)
                    {
                        current.ItemType = profile.WeaponProfile.ItemType;
                        current.ItemName = profile.WeaponProfile.Name ?? string.Empty;
                    }
                }

                if (target != null)
                {
                    current.TargetSource = target.SelectionSource ?? string.Empty;
                    current.TargetWhoAmI = target.WhoAmI;
                    current.TargetType = target.Type;
                    current.TargetName = target.Name ?? string.Empty;
                }

                current.SubmittedCount++;
                _diagnostics = current;
            }
        }

        private static string DirectionName(int direction)
        {
            return direction < 0 ? "left" : "right";
        }

        private sealed class AutoFacingProfile
        {
            public bool UseItemHeld;
            public int ItemAnimation;
            public int ItemTime;
            public long GameUpdateCount;
            public int SelectedSlot = -1;
            public int CurrentDirection;
            public float PlayerCenterX;
            public float PlayerCenterY;
            public CombatAimWeaponProfile WeaponProfile;
        }

        private sealed class AutoFacingTarget
        {
            public int WhoAmI = -1;
            public int Type;
            public string Name = string.Empty;
            public float CenterX;
            public float CenterY;
            public string SelectionSource = string.Empty;
            public int SelectionRadiusTiles;
        }
    }
}
