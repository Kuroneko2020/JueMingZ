using System;
using System.Globalization;
using System.Text;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Actions
{
    public static class ItemUseBridge
    {
        private static readonly object SyncRoot = new object();
        private static PendingUseRequest _pending;
        private static ItemUseBridgeResult _lastResult = ItemUseBridgeResult.None;
        private static string _lastMessage = string.Empty;
        private static int _consumeCount;
        private static int _succeededCount;
        private static int _attemptedButUnverifiedCount;
        private static int _failedCount;

        public static string LastStatus { get { lock (SyncRoot) { return _lastResult.Status.ToString(); } } }
        public static string LastMessage { get { lock (SyncRoot) { return _lastMessage; } } }
        public static string LastResultCode { get { lock (SyncRoot) { return _lastResult.ResultCode ?? string.Empty; } } }
        public static Guid LastRequestId { get { lock (SyncRoot) { return _lastResult.RequestId; } } }
        public static Guid PendingRequestId { get { lock (SyncRoot) { return _pending == null ? Guid.Empty : _pending.RequestId; } } }
        public static long PendingAgeMs
        {
            get
            {
                lock (SyncRoot)
                {
                    return _pending == null ? 0 : (long)(DateTime.UtcNow - _pending.CreatedUtc).TotalMilliseconds;
                }
            }
        }
        public static int ConsumeCount { get { lock (SyncRoot) { return _consumeCount; } } }
        public static int SucceededCount { get { lock (SyncRoot) { return _succeededCount; } } }
        public static int AttemptedButUnverifiedCount { get { lock (SyncRoot) { return _attemptedButUnverifiedCount; } } }
        public static int FailedCount { get { lock (SyncRoot) { return _failedCount; } } }

        public static bool TryEnqueueUseSelectedItem(
            Guid requestId,
            string sourceFeatureId,
            int targetSlot,
            int expectedItemType,
            int expectedStack,
            string itemName,
            TimeSpan timeout,
            int originalSelectedSlot,
            InputActionKind actionKind,
            string scenario,
            string sourceHotkey,
            string sourceKind,
            string sourceUi,
            string buttonId,
            string buttonLabel,
            out string message)
        {
            return TryEnqueueUseSelectedItem(
                requestId,
                sourceFeatureId,
                targetSlot,
                expectedItemType,
                expectedStack,
                itemName,
                timeout,
                originalSelectedSlot,
                actionKind,
                scenario,
                sourceHotkey,
                sourceKind,
                sourceUi,
                buttonId,
                buttonLabel,
                null,
                out message);
        }

        public static bool TryEnqueueUseSelectedItem(
            Guid requestId,
            string sourceFeatureId,
            int targetSlot,
            int expectedItemType,
            int expectedStack,
            string itemName,
            TimeSpan timeout,
            int originalSelectedSlot,
            InputActionKind actionKind,
            string scenario,
            string sourceHotkey,
            string sourceKind,
            string sourceUi,
            string buttonId,
            string buttonLabel,
            ItemUseBridgeOptions options,
            out string message)
        {
            message = string.Empty;
            if (requestId == Guid.Empty)
            {
                message = "ItemUseBridge request id is empty.";
                return false;
            }

            if (!TerrariaInputCompat.IsSupportedItemUseSlot(targetSlot))
            {
                message = "ItemUseBridge only supports inventory item-use slots 0-49 and the vanilla mouse item slot 58.";
                return false;
            }

            if (expectedItemType <= 0 || expectedStack <= 0)
            {
                message = "Selected item is empty.";
                return false;
            }

            lock (SyncRoot)
            {
                if (_pending != null && !_pending.Finished)
                {
                    message = "ItemUseBridge is busy with request " + _pending.RequestId;
                    return false;
                }

                _pending = new PendingUseRequest
                {
                    RequestId = requestId,
                    SourceFeatureId = sourceFeatureId ?? string.Empty,
                    CreatedUtc = DateTime.UtcNow,
                    Timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(3) : timeout,
                    TargetSlot = targetSlot,
                    ExpectedItemType = expectedItemType,
                    ExpectedStack = expectedStack,
                    ItemName = itemName ?? string.Empty,
                    OriginalSelectedSlot = originalSelectedSlot,
                    ActionKind = actionKind,
                    Scenario = scenario ?? string.Empty,
                    SourceHotkey = sourceHotkey ?? string.Empty,
                    SourceKind = sourceKind ?? string.Empty,
                    SourceUi = sourceUi ?? string.Empty,
                    ButtonId = buttonId ?? string.Empty,
                    ButtonLabel = buttonLabel ?? string.Empty,
                    SelectedSlotAtUseStart = options == null ? -1 : options.SelectedSlotAtUseStart,
                    SlotSwitchAttempted = options != null && options.SlotSwitchAttempted,
                    SlotSwitchSucceeded = options != null && options.SlotSwitchSucceeded,
                    SlotSwitchMethod = options == null ? string.Empty : options.SlotSwitchMethod ?? string.Empty,
                    SlotSwitchBefore = options == null ? -1 : options.SlotSwitchBefore,
                    SlotSwitchAfter = options == null ? -1 : options.SlotSwitchAfter,
                    WaitForMouseReleaseAttempted = options != null && options.WaitForMouseReleaseAttempted,
                    WaitedForMouseRelease = options != null && options.WaitedForMouseRelease,
                    MouseReleaseWaitTicks = options == null ? 0 : options.MouseReleaseWaitTicks,
                    SkipSelectInItemCheck = options != null && options.SkipSelectInItemCheck,
                    RequireUseItemHeld = options != null && options.RequireUseItemHeld,
                    ApplyMainMouseLeftForItemCheck = options != null && options.ApplyMainMouseLeftForItemCheck,
                    AllowEarlyItemCheck = options != null && options.AllowEarlyItemCheck,
                    EarlyItemCheckWindowTicks = options == null ? 0 : options.EarlyItemCheckWindowTicks,
                    AllowCombatAim = options != null && options.AllowCombatAim,
                    HasMouseScreenTarget = options != null && options.HasMouseScreenTarget,
                    MouseScreenX = options == null ? 0 : options.MouseScreenX,
                    MouseScreenY = options == null ? 0 : options.MouseScreenY,
                    HasMouseWorldTarget = options != null && options.HasMouseWorldTarget,
                    MouseWorldX = options == null ? 0f : options.MouseWorldX,
                    MouseWorldY = options == null ? 0f : options.MouseWorldY,
                    RestoreSelectedSlotOverride = options == null ? -1 : options.RestoreSelectedSlotOverride,
                    UiClickSuppressionAttempted = options != null && options.UiClickSuppressionAttempted,
                    UiClickSuppressionMode = options == null ? string.Empty : options.UiClickSuppressionMode ?? string.Empty,
                    UiClickSuppressionSucceeded = options != null && options.UiClickSuppressionSucceeded,
                    UiMouseCaptureAvailableAtClick = options != null && options.UiMouseCaptureAvailableAtClick,
                    HitTestModeAtClick = options == null ? string.Empty : options.HitTestModeAtClick ?? string.Empty,
                    ClickSourceAtClick = options == null ? string.Empty : options.ClickSourceAtClick ?? string.Empty,
                    Status = ItemUseBridgeStatus.WaitingForItemCheck,
                    LastMessage = "Waiting for Player.ItemCheck."
                };

                _lastMessage = _pending.LastMessage;
                _lastResult = new ItemUseBridgeResult
                {
                    RequestId = requestId,
                    Status = ItemUseBridgeStatus.WaitingForItemCheck,
                    ResultCode = DiagnosticResultCode.Failed.ToString(),
                    Message = _lastMessage,
                    TargetSlot = targetSlot,
                    OriginalSelectedSlot = originalSelectedSlot,
                    SelectedSlotAtUseStart = _pending.SelectedSlotAtUseStart,
                    SlotSwitchAttempted = _pending.SlotSwitchAttempted,
                    SlotSwitchSucceeded = _pending.SlotSwitchSucceeded,
                    UpdatedUtc = DateTime.UtcNow
                };
            }

            message = "ItemUseBridge queued: request=" + requestId + ", slot=" + targetSlot +
                      ", itemType=" + expectedItemType + ", stack=" + expectedStack;
            Logger.Info("ItemUseBridge", message);
            return true;
        }

        public static bool TryBeginFromItemCheck(object player, out ItemUseBridgeContext context)
        {
            context = null;
            PendingUseRequest pending;
            lock (SyncRoot)
            {
                pending = _pending;
            }

            if (pending == null || pending.Finished)
            {
                return false;
            }

            if (DateTime.UtcNow - pending.CreatedUtc > pending.Timeout)
            {
                Complete(pending.RequestId, ItemUseBridgeStatus.Expired, "ItemUseBridge request expired before Player.ItemCheck consumed it.");
                return false;
            }

            if (!TerrariaInputCompat.TryIsLocalPlayer(player))
            {
                return false;
            }

            if (pending.RequireUseItemHeld)
            {
                bool useItemHeld;
                if (!TerrariaInputCompat.TryReadUseItemHeld(player, out useItemHeld))
                {
                    Complete(pending.RequestId, ItemUseBridgeStatus.Failed, "Cannot verify held use input before ItemCheck: " + TerrariaInputCompat.LastInputCompatError);
                    return false;
                }

                if (!useItemHeld)
                {
                    Complete(pending.RequestId, ItemUseBridgeStatus.Cancelled, "Item use cancelled because use item is no longer held.");
                    return false;
                }
            }

            ItemUseVerificationState before;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, pending.TargetSlot, out before))
            {
                Complete(pending.RequestId, ItemUseBridgeStatus.Failed, TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            if (!before.PlayerActive || before.PlayerDead || before.PlayerGhost)
            {
                Complete(pending.RequestId, ItemUseBridgeStatus.Failed, "Player is not available for item use.");
                return false;
            }

            var expectedSelectedSlot = pending.SelectedSlotAtUseStart >= 0
                ? pending.SelectedSlotAtUseStart
                : pending.TargetSlot;
            if (pending.SkipSelectInItemCheck && before.SelectedSlot != expectedSelectedSlot)
            {
                Complete(
                    pending.RequestId,
                    ItemUseBridgeStatus.Failed,
                    "ItemCheck reached but selectedSlot was not targetSlot. selectedSlot=" +
                    before.SelectedSlot + ", expected=" + expectedSelectedSlot + ".");
                return false;
            }

            if (before.ReuseDelay > 0)
            {
                UpdatePendingMessage(pending.RequestId, "Waiting for item animation/time/reuseDelay to become idle.");
                return false;
            }

            if (before.ItemAnimation > 0 || before.ItemTime > 0)
            {
                if (!pending.AllowEarlyItemCheck ||
                    !IsWithinEarlyItemCheckWindow(before.ItemAnimation, before.ItemTime, pending.EarlyItemCheckWindowTicks))
                {
                    UpdatePendingMessage(pending.RequestId, "Waiting for item animation/time/reuseDelay to become idle.");
                    return false;
                }
            }

            bool delayUseItem;
            if (TerrariaInputCompat.TryReadDelayUseItem(player, out delayUseItem) && delayUseItem)
            {
                UseItemInputState delayRestoreState;
                if (!TerrariaInputCompat.TryCaptureUseItemInputState(player, out delayRestoreState))
                {
                    Complete(pending.RequestId, ItemUseBridgeStatus.Failed, "Cannot capture release-only restore state before delayUseItem wait: " + TerrariaInputCompat.LastInputCompatError);
                    return false;
                }

                var releaseApplied = TerrariaInputCompat.TryApplyUseItemReleaseForItemCheck(player);
                MarkDelayUseItemReleaseApplied(pending.RequestId, releaseApplied, releaseApplied ? player : null, releaseApplied ? delayRestoreState : null);
                UpdatePendingMessage(
                    pending.RequestId,
                    releaseApplied
                        ? "Waiting for delayUseItem to clear after applying release-only use input."
                        : "Waiting for delayUseItem to clear; release-only use input failed: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            if (before.ItemType != pending.ExpectedItemType || before.ItemStack <= 0)
            {
                Complete(pending.RequestId, ItemUseBridgeStatus.Failed, "Selected item no longer matches queued request.");
                return false;
            }

            lock (SyncRoot)
            {
                if (_pending == null || _pending.RequestId != pending.RequestId || _pending.Finished)
                {
                    return false;
                }

                _pending.ConsumedByItemCheck = true;
                _pending.BeforeState = before;
                _pending.Status = ItemUseBridgeStatus.Consumed;
                _pending.LastMessage = "Player.ItemCheck is consuming queued ItemUse.";
                _consumeCount++;
                _lastMessage = _pending.LastMessage;
                _lastResult = new ItemUseBridgeResult
                {
                    RequestId = pending.RequestId,
                    Status = ItemUseBridgeStatus.Consumed,
                    ResultCode = DiagnosticResultCode.Failed.ToString(),
                    Message = _pending.LastMessage,
                    DurationMs = (long)(DateTime.UtcNow - _pending.CreatedUtc).TotalMilliseconds,
                    TargetSlot = _pending.TargetSlot,
                    OriginalSelectedSlot = _pending.OriginalSelectedSlot,
                    SelectedSlotAtUseStart = _pending.SelectedSlotAtUseStart,
                    ConsumedByItemCheck = _pending.ConsumedByItemCheck,
                    SlotSwitchAttempted = _pending.SlotSwitchAttempted,
                    SlotSwitchSucceeded = _pending.SlotSwitchSucceeded,
                    BeforeState = _pending.BeforeState,
                    UpdatedUtc = DateTime.UtcNow
                };
            }

            context = new ItemUseBridgeContext
            {
                RequestId = pending.RequestId,
                SourceFeatureId = pending.SourceFeatureId,
                TargetSlot = pending.TargetSlot,
                ExpectedItemType = pending.ExpectedItemType,
                ExpectedStack = pending.ExpectedStack,
                SkipSelectInItemCheck = pending.SkipSelectInItemCheck,
                ApplyMainMouseLeftForItemCheck = pending.ApplyMainMouseLeftForItemCheck,
                AllowCombatAim = pending.AllowCombatAim,
                HasMouseScreenTarget = pending.HasMouseScreenTarget,
                MouseScreenX = pending.MouseScreenX,
                MouseScreenY = pending.MouseScreenY,
                HasMouseWorldTarget = pending.HasMouseWorldTarget,
                MouseWorldX = pending.MouseWorldX,
                MouseWorldY = pending.MouseWorldY,
                ExpectedSelectedSlot = expectedSelectedSlot,
                RestoreSelectedSlot = pending.RestoreSelectedSlotOverride,
                BeforeState = before
            };
            return true;
        }

        public static void NotifyItemCheckFinished(object player, Guid requestId)
        {
            PendingUseRequest pending;
            lock (SyncRoot)
            {
                pending = _pending;
                if (pending == null || pending.RequestId != requestId || pending.Finished)
                {
                    return;
                }
            }

            ItemUseVerificationState after;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, pending.TargetSlot, out after))
            {
                Complete(requestId, ItemUseBridgeStatus.Failed, "Player.ItemCheck consumed request but after-state read failed: " + TerrariaInputCompat.LastInputCompatError);
                return;
            }

            var before = pending.BeforeState;
            var status = HasObservableSuccess(before, after)
                ? ItemUseBridgeStatus.Succeeded
                : ItemUseBridgeStatus.AttemptedButUnverified;
            var message = status == ItemUseBridgeStatus.Succeeded
                ? "已进入 ItemCheck，并观察到物品使用相关状态变化。"
                : BuildAttemptedButUnverifiedMessage(before);

            Complete(requestId, status, message, after);
        }

        public static ItemUseBridgeResult GetResult(Guid requestId)
        {
            lock (SyncRoot)
            {
                if (_pending != null && _pending.RequestId == requestId && !_pending.Finished)
                {
                    if (DateTime.UtcNow - _pending.CreatedUtc > _pending.Timeout)
                    {
                        CompleteLocked(_pending, ItemUseBridgeStatus.Expired, "ItemUseBridge request expired.");
                    }
                    else
                    {
                        return new ItemUseBridgeResult
                        {
                            RequestId = _pending.RequestId,
                            Status = _pending.Status,
                            ResultCode = MapResultCode(_pending.Status),
                            Message = _pending.LastMessage,
                            DurationMs = (long)(DateTime.UtcNow - _pending.CreatedUtc).TotalMilliseconds,
                            TargetSlot = _pending.TargetSlot,
                            OriginalSelectedSlot = _pending.OriginalSelectedSlot,
                            SelectedSlotAtUseStart = _pending.SelectedSlotAtUseStart,
                            ConsumedByItemCheck = _pending.ConsumedByItemCheck,
                            SlotSwitchAttempted = _pending.SlotSwitchAttempted,
                            SlotSwitchSucceeded = _pending.SlotSwitchSucceeded,
                            BeforeState = _pending.BeforeState,
                            AfterState = _pending.AfterState,
                            UpdatedUtc = DateTime.UtcNow
                        };
                    }
                }

                return _lastResult != null && _lastResult.RequestId == requestId
                    ? _lastResult
                    : ItemUseBridgeResult.None;
            }
        }

        public static void Cancel(Guid requestId, string reason)
        {
            lock (SyncRoot)
            {
                if (_pending == null || _pending.RequestId != requestId || _pending.Finished)
                {
                    return;
                }

                CompleteLocked(_pending, ItemUseBridgeStatus.Cancelled, reason ?? "ItemUseBridge request cancelled.");
            }
        }

        public static bool CancelBySource(string sourceFeatureId, string reason)
        {
            if (string.IsNullOrWhiteSpace(sourceFeatureId))
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (_pending == null || _pending.Finished ||
                    !string.Equals(_pending.SourceFeatureId, sourceFeatureId, StringComparison.Ordinal))
                {
                    return false;
                }

                CompleteLocked(_pending, ItemUseBridgeStatus.Cancelled, reason ?? "ItemUseBridge request cancelled by source.");
                return true;
            }
        }

        public static void Fail(Guid requestId, string reason)
        {
            lock (SyncRoot)
            {
                if (_pending == null || _pending.RequestId != requestId || _pending.Finished)
                {
                    return;
                }

                CompleteLocked(_pending, ItemUseBridgeStatus.Failed, reason ?? "ItemUseBridge request failed.");
            }
        }

        private static bool HasObservableSuccess(ItemUseVerificationState before, ItemUseVerificationState after)
        {
            if (before == null || after == null)
            {
                return false;
            }

            return (before.ItemAnimation <= 0 && after.ItemAnimation > 0) ||
                   (before.ItemTime <= 0 && after.ItemTime > 0) ||
                   after.ReuseDelay > before.ReuseDelay ||
                   after.ItemType != before.ItemType ||
                   after.ItemStack < before.ItemStack ||
                   after.Life > before.Life ||
                   after.LifeMax > before.LifeMax ||
                   after.Mana > before.Mana ||
                   after.ManaMax > before.ManaMax ||
                   after.ActiveBuffCount > before.ActiveBuffCount ||
                   after.BuffTimeTotal > before.BuffTimeTotal;
        }

        private static void UpdatePendingMessage(Guid requestId, string message)
        {
            lock (SyncRoot)
            {
                if (_pending == null || _pending.RequestId != requestId || _pending.Finished)
                {
                    return;
                }

                _pending.LastMessage = message ?? string.Empty;
                _lastMessage = _pending.LastMessage;
                _lastResult = new ItemUseBridgeResult
                {
                    RequestId = requestId,
                    Status = _pending.Status,
                    ResultCode = MapResultCode(_pending.Status),
                    Message = _pending.LastMessage,
                    DurationMs = (long)(DateTime.UtcNow - _pending.CreatedUtc).TotalMilliseconds,
                    TargetSlot = _pending.TargetSlot,
                    OriginalSelectedSlot = _pending.OriginalSelectedSlot,
                    SelectedSlotAtUseStart = _pending.SelectedSlotAtUseStart,
                    ConsumedByItemCheck = _pending.ConsumedByItemCheck,
                    SlotSwitchAttempted = _pending.SlotSwitchAttempted,
                    SlotSwitchSucceeded = _pending.SlotSwitchSucceeded,
                    BeforeState = _pending.BeforeState,
                    AfterState = _pending.AfterState,
                    UpdatedUtc = DateTime.UtcNow
                };
            }
        }

        private static void MarkDelayUseItemReleaseApplied(Guid requestId, bool applied, object player, UseItemInputState restoreState)
        {
            lock (SyncRoot)
            {
                if (_pending == null || _pending.RequestId != requestId || _pending.Finished)
                {
                    return;
                }

                _pending.DelayUseItemReleaseAttempted = true;
                _pending.DelayUseItemReleaseApplied |= applied;
                if (applied && restoreState != null && !_pending.ConsumedByItemCheck)
                {
                    _pending.DelayUseItemReleasePlayer = player;
                    _pending.DelayUseItemReleaseRestoreState = restoreState;
                    _pending.DelayUseItemReleaseRestoreStateCaptured = true;
                }
            }
        }

        private static void Complete(Guid requestId, ItemUseBridgeStatus status, string message)
        {
            Complete(requestId, status, message, null);
        }

        private static void Complete(Guid requestId, ItemUseBridgeStatus status, string message, ItemUseVerificationState afterState)
        {
            lock (SyncRoot)
            {
                if (_pending == null || _pending.RequestId != requestId || _pending.Finished)
                {
                    return;
                }

                CompleteLocked(_pending, status, message, afterState);
            }
        }

        private static void CompleteLocked(PendingUseRequest pending, ItemUseBridgeStatus status, string message)
        {
            CompleteLocked(pending, status, message, null);
        }

        private static void CompleteLocked(PendingUseRequest pending, ItemUseBridgeStatus status, string message, ItemUseVerificationState afterState)
        {
            pending.Status = status;
            pending.LastMessage = message ?? string.Empty;
            pending.Finished = true;
            pending.AfterState = afterState;
            RestoreDelayUseItemReleaseIfNeeded(pending);
            _lastMessage = pending.LastMessage;
            _lastResult = new ItemUseBridgeResult
            {
                RequestId = pending.RequestId,
                Status = status,
                ResultCode = MapResultCode(status),
                Message = pending.LastMessage,
                DurationMs = (long)(DateTime.UtcNow - pending.CreatedUtc).TotalMilliseconds,
                TargetSlot = pending.TargetSlot,
                OriginalSelectedSlot = pending.OriginalSelectedSlot,
                SelectedSlotAtUseStart = pending.SelectedSlotAtUseStart,
                ConsumedByItemCheck = pending.ConsumedByItemCheck,
                SlotSwitchAttempted = pending.SlotSwitchAttempted,
                SlotSwitchSucceeded = pending.SlotSwitchSucceeded,
                BeforeState = pending.BeforeState,
                AfterState = pending.AfterState,
                UpdatedUtc = DateTime.UtcNow
            };

            if (status == ItemUseBridgeStatus.Succeeded)
            {
                _succeededCount++;
            }
            else if (status == ItemUseBridgeStatus.AttemptedButUnverified)
            {
                _attemptedButUnverifiedCount++;
            }
            else if (status == ItemUseBridgeStatus.Failed || status == ItemUseBridgeStatus.Expired)
            {
                _failedCount++;
            }

            _pending = null;
            Logger.Info("ItemUseBridge", "ItemUseBridge finished: " + status + " / " + pending.LastMessage);
            if (pending.ActionKind != InputActionKind.UseHotbarItem)
            {
                RecordActionEvent(pending);
            }
        }

        private static void RestoreDelayUseItemReleaseIfNeeded(PendingUseRequest pending)
        {
            if (pending == null ||
                pending.ConsumedByItemCheck ||
                !pending.DelayUseItemReleaseRestoreStateCaptured ||
                pending.DelayUseItemReleasePlayer == null ||
                pending.DelayUseItemReleaseRestoreState == null)
            {
                return;
            }

            TerrariaInputCompat.TryRestoreUseItemButtonInputState(
                pending.DelayUseItemReleasePlayer,
                pending.DelayUseItemReleaseRestoreState);
            pending.DelayUseItemReleaseRestoreStateCaptured = false;
            pending.DelayUseItemReleasePlayer = null;
            pending.DelayUseItemReleaseRestoreState = null;
        }

        private static string MapResultCode(ItemUseBridgeStatus status)
        {
            switch (status)
            {
                case ItemUseBridgeStatus.Succeeded:
                    return DiagnosticResultCode.Succeeded.ToString();
                case ItemUseBridgeStatus.AttemptedButUnverified:
                    return DiagnosticResultCode.AttemptedButUnverified.ToString();
                case ItemUseBridgeStatus.Expired:
                    return DiagnosticResultCode.TimedOut.ToString();
                case ItemUseBridgeStatus.Cancelled:
                case ItemUseBridgeStatus.Failed:
                default:
                    return DiagnosticResultCode.Failed.ToString();
            }
        }

        private static bool IsWithinEarlyItemCheckWindow(int itemAnimation, int itemTime, int windowTicks)
        {
            if (windowTicks <= 0)
            {
                return false;
            }

            return Math.Max(0, itemAnimation) <= windowTicks &&
                   Math.Max(0, itemTime) <= windowTicks;
        }

        private static void RecordActionEvent(PendingUseRequest pending)
        {
            if (pending == null)
            {
                return;
            }

            var selectedSlotRestored = pending.OriginalSelectedSlot < 0 ||
                                       (pending.AfterState != null && pending.AfterState.SelectedSlot == pending.OriginalSelectedSlot);
            var observableChange = HasObservableSuccess(pending.BeforeState, pending.AfterState);
            var selectedSlotAtUseStartMatchesTarget = pending.SelectedSlotAtUseStart == pending.TargetSlot ||
                                                      (pending.BeforeState != null && pending.BeforeState.SelectedSlot == pending.TargetSlot);
            var verification = "{" +
                               "\"itemCheckConsumed\":" + (pending.ConsumedByItemCheck ? "true" : "false") + "," +
                               "\"selectedSlotAtUseStartMatchesTarget\":" + (selectedSlotAtUseStartMatchesTarget ? "true" : "false") + "," +
                               "\"selectedSlotRestored\":" + (selectedSlotRestored ? "true" : "false") + "," +
                               "\"slotSwitchSucceeded\":" + (pending.SlotSwitchSucceeded ? "true" : "false") + "," +
                               "\"hasMouseWorldTarget\":" + (pending.HasMouseWorldTarget ? "true" : "false") + "," +
                               "\"mouseWorldX\":" + pending.MouseWorldX.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                               "\"mouseWorldY\":" + pending.MouseWorldY.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                               "\"requireUseItemHeld\":" + (pending.RequireUseItemHeld ? "true" : "false") + "," +
                               "\"mainMouseLeftApplied\":" + (pending.ApplyMainMouseLeftForItemCheck ? "true" : "false") + "," +
                               "\"allowEarlyItemCheck\":" + (pending.AllowEarlyItemCheck ? "true" : "false") + "," +
                               "\"earlyItemCheckWindowTicks\":" + pending.EarlyItemCheckWindowTicks.ToString(CultureInfo.InvariantCulture) + "," +
                               "\"delayUseItemReleaseApplied\":" + (pending.DelayUseItemReleaseApplied ? "true" : "false") + "," +
                               "\"allowCombatAim\":" + (pending.AllowCombatAim ? "true" : "false") + "," +
                               "\"observableChange\":" + (observableChange ? "true" : "false") + "," +
                               "\"buttonClickSuppressedGameInput\":" + (pending.UiClickSuppressionSucceeded ? "true" : "false") + "," +
                               "\"changedFields\":" + BuildChangedFieldsJson(pending.BeforeState, pending.AfterState) + "," +
                               "\"likelyReason\":\"" + EscapeJson(BuildLikelyReason(pending, observableChange)) + "\"" +
                               "}";
            var duration = (long)(DateTime.UtcNow - pending.CreatedUtc).TotalMilliseconds;
            if (pending.ActionKind == InputActionKind.UseHotbarItem)
            {
                DiagnosticActionRecorder.RecordCustomEvent(
                    pending.RequestId,
                    string.IsNullOrWhiteSpace(pending.Scenario) ? pending.ActionKind.ToString() : pending.Scenario,
                    pending.ActionKind.ToString(),
                    pending.SourceHotkey,
                    pending.Status.ToString(),
                    MapResultCode(pending.Status),
                    pending.LastMessage,
                    duration,
                    BuildUseHotbarBeforeJson(pending),
                    BuildUseHotbarAfterJson(pending),
                    verification,
                    pending.SourceKind,
                    pending.SourceUi,
                    pending.ButtonId,
                    pending.ButtonLabel);
            }
            else
            {
                DiagnosticActionRecorder.RecordActionEvent(
                    pending.RequestId,
                    string.IsNullOrWhiteSpace(pending.Scenario) ? pending.ActionKind.ToString() : pending.Scenario,
                    pending.ActionKind.ToString(),
                    pending.SourceHotkey,
                    pending.Status.ToString(),
                    MapResultCode(pending.Status),
                    pending.LastMessage,
                    duration,
                    pending.BeforeState,
                    pending.AfterState,
                    verification,
                    pending.SourceKind,
                    pending.SourceUi,
                    pending.ButtonId,
                    pending.ButtonLabel);
            }
        }

        private static string BuildAttemptedButUnverifiedMessage(ItemUseVerificationState before)
        {
            if (DiagnosticHotbarInfo.IsLikelyPlacementItem(before))
            {
                return "已进入 ItemCheck，但没有观察到状态变化；火把/方块/平台通常需要鼠标指向可放置位置，建议换成武器、工具或药水测试。";
            }

            return "已进入 ItemCheck，但没有观察到状态变化；物品可能不可用、目标无效、效果已存在，或效果没有可见状态变化。";
        }

        private static string BuildLikelyReason(PendingUseRequest pending, bool observableChange)
        {
            if (pending == null)
            {
                return string.Empty;
            }

            if (!pending.ConsumedByItemCheck)
            {
                return "Player.ItemCheck did not consume the request.";
            }

            if (observableChange)
            {
                return string.Empty;
            }

            return DiagnosticHotbarInfo.IsLikelyPlacementItem(pending.BeforeState)
                ? "Target item may require a valid tile placement target."
                : "No observable item-use state changed.";
        }

        private static string BuildUseHotbarBeforeJson(PendingUseRequest pending)
        {
            var state = pending == null ? null : pending.BeforeState;
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "originalSelectedSlot", SlotRaw(pending == null ? -1 : pending.OriginalSelectedSlot), true);
            AppendRaw(builder, "originalSelectedSlotDisplay", SlotDisplayRaw(pending == null ? -1 : pending.OriginalSelectedSlot), true);
            AppendRaw(builder, "targetSlot", SlotRaw(pending == null ? -1 : pending.TargetSlot), true);
            AppendRaw(builder, "targetSlotDisplay", SlotDisplayRaw(pending == null ? -1 : pending.TargetSlot), true);
            AppendRaw(builder, "targetItemType", IntRaw(state == null ? (pending == null ? 0 : pending.ExpectedItemType) : state.ItemType), true);
            AppendString(builder, "targetItemName", state == null ? (pending == null ? string.Empty : pending.ItemName) : state.ItemName, true);
            AppendRaw(builder, "targetItemStack", IntRaw(state == null ? (pending == null ? 0 : pending.ExpectedStack) : state.ItemStack), true);
            AppendRaw(builder, "slotSwitchAttempted", BoolRaw(pending != null && pending.SlotSwitchAttempted), true);
            AppendRaw(builder, "slotSwitchBefore", SlotRaw(pending == null ? -1 : pending.SlotSwitchBefore), true);
            AppendRaw(builder, "slotSwitchTarget", SlotRaw(pending == null ? -1 : pending.TargetSlot), true);
            AppendRaw(builder, "slotSwitchAfter", SlotRaw(pending == null ? -1 : pending.SlotSwitchAfter), true);
            AppendRaw(builder, "slotSwitchSucceeded", BoolRaw(pending != null && pending.SlotSwitchSucceeded), true);
            AppendString(builder, "slotSwitchMethod", pending == null ? string.Empty : pending.SlotSwitchMethod, true);
            AppendRaw(builder, "selectedSlotAtUseStart", SlotRaw(pending == null ? -1 : pending.SelectedSlotAtUseStart), true);
            AppendRaw(builder, "waitForMouseReleaseAttempted", BoolRaw(pending != null && pending.WaitForMouseReleaseAttempted), true);
            AppendRaw(builder, "waitedForMouseRelease", BoolRaw(pending != null && pending.WaitedForMouseRelease), true);
            AppendRaw(builder, "mouseReleaseWaitTicks", IntRaw(pending == null ? 0 : pending.MouseReleaseWaitTicks), true);
            AppendRaw(builder, "uiClickSuppressionAttempted", BoolRaw(pending != null && pending.UiClickSuppressionAttempted), true);
            AppendString(builder, "uiClickSuppressionMode", pending == null ? string.Empty : pending.UiClickSuppressionMode, true);
            AppendRaw(builder, "uiClickSuppressionSucceeded", BoolRaw(pending != null && pending.UiClickSuppressionSucceeded), true);
            AppendRaw(builder, "uiMouseCaptureAvailableAtClick", BoolRaw(pending != null && pending.UiMouseCaptureAvailableAtClick), true);
            AppendString(builder, "hitTestModeAtClick", pending == null ? string.Empty : pending.HitTestModeAtClick, true);
            AppendString(builder, "clickSourceAtClick", pending == null ? string.Empty : pending.ClickSourceAtClick, true);
            AppendRaw(builder, "life", IntRaw(state == null ? 0 : state.Life), true);
            AppendRaw(builder, "lifeMax", IntRaw(state == null ? 0 : state.LifeMax), true);
            AppendRaw(builder, "mana", IntRaw(state == null ? 0 : state.Mana), true);
            AppendRaw(builder, "manaMax", IntRaw(state == null ? 0 : state.ManaMax), true);
            AppendRaw(builder, "itemAnimation", IntRaw(state == null ? 0 : state.ItemAnimation), true);
            AppendRaw(builder, "itemTime", IntRaw(state == null ? 0 : state.ItemTime), true);
            AppendRaw(builder, "reuseDelay", IntRaw(state == null ? 0 : state.ReuseDelay), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildUseHotbarAfterJson(PendingUseRequest pending)
        {
            var state = pending == null ? null : pending.AfterState;
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "restoredSelectedSlot", SlotRaw(state == null ? -1 : state.SelectedSlot), true);
            AppendRaw(builder, "restoredSelectedSlotDisplay", SlotDisplayRaw(state == null ? -1 : state.SelectedSlot), true);
            AppendRaw(builder, "selectedSlotRestored", BoolRaw(pending == null || pending.OriginalSelectedSlot < 0 || (state != null && state.SelectedSlot == pending.OriginalSelectedSlot)), true);
            AppendRaw(builder, "targetSlot", SlotRaw(pending == null ? -1 : pending.TargetSlot), true);
            AppendRaw(builder, "targetSlotDisplay", SlotDisplayRaw(pending == null ? -1 : pending.TargetSlot), true);
            AppendRaw(builder, "targetItemType", IntRaw(state == null ? (pending == null ? 0 : pending.ExpectedItemType) : state.ItemType), true);
            AppendString(builder, "targetItemName", state == null ? (pending == null ? string.Empty : pending.ItemName) : state.ItemName, true);
            AppendRaw(builder, "targetItemStack", IntRaw(state == null ? (pending == null ? 0 : pending.ExpectedStack) : state.ItemStack), true);
            AppendRaw(builder, "life", IntRaw(state == null ? 0 : state.Life), true);
            AppendRaw(builder, "lifeMax", IntRaw(state == null ? 0 : state.LifeMax), true);
            AppendRaw(builder, "mana", IntRaw(state == null ? 0 : state.Mana), true);
            AppendRaw(builder, "manaMax", IntRaw(state == null ? 0 : state.ManaMax), true);
            AppendRaw(builder, "itemAnimation", IntRaw(state == null ? 0 : state.ItemAnimation), true);
            AppendRaw(builder, "itemTime", IntRaw(state == null ? 0 : state.ItemTime), true);
            AppendRaw(builder, "reuseDelay", IntRaw(state == null ? 0 : state.ReuseDelay), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildChangedFieldsJson(ItemUseVerificationState before, ItemUseVerificationState after)
        {
            if (before == null || after == null)
            {
                return "[]";
            }

            var builder = new StringBuilder();
            builder.Append("[");
            var first = true;
            AppendChanged(builder, ref first, "selectedSlot", before.SelectedSlot != after.SelectedSlot);
            AppendChanged(builder, ref first, "itemAnimation", before.ItemAnimation != after.ItemAnimation);
            AppendChanged(builder, ref first, "itemTime", before.ItemTime != after.ItemTime);
            AppendChanged(builder, ref first, "reuseDelay", before.ReuseDelay != after.ReuseDelay);
            AppendChanged(builder, ref first, "targetItemType", before.ItemType != after.ItemType);
            AppendChanged(builder, ref first, "targetItemStack", before.ItemStack != after.ItemStack);
            AppendChanged(builder, ref first, "life", before.Life != after.Life);
            AppendChanged(builder, ref first, "lifeMax", before.LifeMax != after.LifeMax);
            AppendChanged(builder, ref first, "mana", before.Mana != after.Mana);
            AppendChanged(builder, ref first, "manaMax", before.ManaMax != after.ManaMax);
            AppendChanged(builder, ref first, "activeBuffCount", before.ActiveBuffCount != after.ActiveBuffCount);
            AppendChanged(builder, ref first, "buffTimeTotal", before.BuffTimeTotal != after.BuffTimeTotal);
            builder.Append("]");
            return builder.ToString();
        }

        private static void AppendChanged(StringBuilder builder, ref bool first, string name, bool changed)
        {
            if (!changed)
            {
                return;
            }

            if (!first)
            {
                builder.Append(",");
            }

            builder.Append("\"").Append(name).Append("\"");
            first = false;
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

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string SlotRaw(int slot)
        {
            return TerrariaInputCompat.IsSupportedItemUseSlot(slot) ? slot.ToString(CultureInfo.InvariantCulture) : "null";
        }

        private static string SlotDisplayRaw(int slot)
        {
            return TerrariaInputCompat.IsSupportedItemUseSlot(slot) ? (slot + 1).ToString(CultureInfo.InvariantCulture) : "null";
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

        private sealed class PendingUseRequest
        {
            public Guid RequestId;
            public string SourceFeatureId;
            public DateTime CreatedUtc;
            public TimeSpan Timeout;
            public int TargetSlot;
            public int ExpectedItemType;
            public int ExpectedStack;
            public string ItemName;
            public int OriginalSelectedSlot;
            public InputActionKind ActionKind;
            public string Scenario;
            public string SourceHotkey;
            public string SourceKind;
            public string SourceUi;
            public string ButtonId;
            public string ButtonLabel;
            public int SelectedSlotAtUseStart;
            public bool SlotSwitchAttempted;
            public bool SlotSwitchSucceeded;
            public string SlotSwitchMethod;
            public int SlotSwitchBefore;
            public int SlotSwitchAfter;
            public bool WaitForMouseReleaseAttempted;
            public bool WaitedForMouseRelease;
            public int MouseReleaseWaitTicks;
            public bool SkipSelectInItemCheck;
            public bool RequireUseItemHeld;
            public bool ApplyMainMouseLeftForItemCheck;
            public bool AllowEarlyItemCheck;
            public int EarlyItemCheckWindowTicks;
            public bool DelayUseItemReleaseAttempted;
            public bool DelayUseItemReleaseApplied;
            public bool DelayUseItemReleaseRestoreStateCaptured;
            public object DelayUseItemReleasePlayer;
            public UseItemInputState DelayUseItemReleaseRestoreState;
            public bool AllowCombatAim;
            public bool HasMouseScreenTarget;
            public int MouseScreenX;
            public int MouseScreenY;
            public bool HasMouseWorldTarget;
            public float MouseWorldX;
            public float MouseWorldY;
            public int RestoreSelectedSlotOverride;
            public bool UiClickSuppressionAttempted;
            public string UiClickSuppressionMode;
            public bool UiClickSuppressionSucceeded;
            public bool UiMouseCaptureAvailableAtClick;
            public string HitTestModeAtClick;
            public string ClickSourceAtClick;
            public bool ConsumedByItemCheck;
            public bool Finished;
            public ItemUseBridgeStatus Status;
            public string LastMessage;
            public ItemUseVerificationState BeforeState;
            public ItemUseVerificationState AfterState;
        }
    }
}
