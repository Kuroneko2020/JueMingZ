using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaUiMouseCompat
    {
        // UI mouse helpers may mark capture flags to stop click-through;
        // gameplay actions remain outside this layer.
        private const int VkLeftButton = 0x01;
        private const int VkRightButton = 0x02;
        private const int VkMiddleButton = 0x04;
        private const int VkXButton1 = 0x05;
        private const int VkXButton2 = 0x06;
        private const ulong MaxActiveTriggerSuppressionUpdates = 90;
        private static bool _mainMouseResolved;
        private static FieldInfo _mouseXField;
        private static FieldInfo _mouseYField;
        private static FieldInfo _mouseLeftField;
        private static FieldInfo _mouseScrollWheelField;
        private static FieldInfo _oldMouseScrollWheelField;
        private static PropertyInfo _mouseXProperty;
        private static PropertyInfo _mouseYProperty;
        private static PropertyInfo _mouseLeftProperty;
        private static PropertyInfo _mouseScrollWheelProperty;
        private static PropertyInfo _oldMouseScrollWheelProperty;
        private static bool _playerInputResolved;
        private static Type _playerInputType;
        private static FieldInfo _scrollWheelDeltaField;
        private static FieldInfo _scrollWheelDeltaForUiField;
        private static PropertyInfo _scrollWheelDeltaProperty;
        private static PropertyInfo _scrollWheelDeltaForUiProperty;
        private static string _mouseReadLastMessage = string.Empty;
        private static string _mouseCaptureLastMessage = string.Empty;
        private static string _scrollSuppressLastMessage = string.Empty;
        private static string _mainScrollCandidateSummary = string.Empty;
        private static string _playerInputScrollCandidateSummary = string.Empty;
        private static bool _scrollCandidateSummaryLogged;
        private static int _lastPlayerInputScrollDelta;
        private static int _lastPlayerInputScrollDeltaForUI;
        private static int _lastMainScrollDelta;
        private static bool _lastPlayerInputCleared;
        private static bool _lastMainScrollSuppressed;
        private static bool _lastScrollHotbarHookSuppressed;
        private const ulong MaxHoverSnapshotAgeUpdates = 6;
        private const int HoverSnapshotMouseTolerancePixels = 4;
        private static readonly object UiMouseAccessorSyncRoot = new object();
        private static readonly object HoverSnapshotSyncRoot = new object();
        private static readonly object ActiveTriggerSuppressionSyncRoot = new object();
        private static readonly Dictionary<Type, object> EmptyHoverItemsByType = new Dictionary<Type, object>();
        private static TerrariaUiHoverSlotSnapshot _lastItemSlotHoverSlotSnapshot;
        private static string _activeTriggerSuppressionToken = string.Empty;
        private static ulong _activeTriggerSuppressionStartUpdateCount;
        private static bool _itemSlotHoverHookInstalled;
        private static string _itemSlotHoverHookStatus = "notAttempted";
        private static string _itemSlotHoverHookCandidateSummary = string.Empty;
        private static Type _uiMouseAccessorMainType;
        private static Type _uiMouseAccessorMainInstanceType;
        private static Type _localPlayerMouseInterfaceType;
        private static BooleanMemberAccessor _localPlayerMouseInterfaceAccessor = BooleanMemberAccessor.Empty;
        private static BooleanMemberAccessor _mainMouseInterfaceAccessor = BooleanMemberAccessor.Empty;
        private static BooleanMemberAccessor _mainBlockMouseAccessor = BooleanMemberAccessor.Empty;
        private static BooleanMemberAccessor _mainMouseLeftAccessor = BooleanMemberAccessor.Empty;
        private static BooleanMemberAccessor _mainMouseRightAccessor = BooleanMemberAccessor.Empty;
        private static BooleanMemberAccessor _mainMouseLeftReleaseAccessor = BooleanMemberAccessor.Empty;
        private static BooleanMemberAccessor _mainMouseRightReleaseAccessor = BooleanMemberAccessor.Empty;
        private static BooleanMemberAccessor _mainMouseTextAccessor = BooleanMemberAccessor.Empty;
        private static BooleanMemberAccessor _mainHoveringOverNpcAccessor = BooleanMemberAccessor.Empty;
        private static StringMemberAccessor _mainHoverItemNameAccessor = StringMemberAccessor.Empty;
        private static StringMemberAccessor _mainHoverItemName2Accessor = StringMemberAccessor.Empty;
        private static ObjectMemberAccessor _mainHoverItemAccessor = ObjectMemberAccessor.Empty;
        private static ObjectMemberAccessor _mainHoverItemLowerAccessor = ObjectMemberAccessor.Empty;
        private static ObjectMemberAccessor _mainInstanceAccessor = ObjectMemberAccessor.Empty;
        private static ObjectMemberAccessor _mainInstanceCapsAccessor = ObjectMemberAccessor.Empty;
        private static ObjectMemberAccessor _mainInstanceHoverItemAccessor = ObjectMemberAccessor.Empty;
        private static ObjectMemberAccessor _mainInstanceHoverItemLowerAccessor = ObjectMemberAccessor.Empty;
        private static ObjectMemberAccessor _mainInstanceMouseTextCacheAccessor = ObjectMemberAccessor.Empty;
        private static IntegerMemberAccessor _mainInstanceMouseNpcIndexAccessor = IntegerMemberAccessor.Empty;
        private static IntegerMemberAccessor _mainInstanceMouseNpcTypeAccessor = IntegerMemberAccessor.Empty;
        private static IntegerMemberAccessor _mainInstanceCurrentNpcShowingChatBubbleAccessor = IntegerMemberAccessor.Empty;

        public static bool UiMouseReadAvailable { get; private set; }
        public static string UiMouseReadLastMessage { get { return _mouseReadLastMessage; } }
        public static bool UiMouseCaptureAvailable { get; private set; }
        public static string UiMouseCaptureLastMessage { get { return _mouseCaptureLastMessage; } }
        public static bool UiScrollSuppressAvailable { get; private set; }
        public static string UiScrollSuppressLastMessage { get { return _scrollSuppressLastMessage; } }
        public static int LastPlayerInputScrollDelta { get { return _lastPlayerInputScrollDelta; } }
        public static int LastPlayerInputScrollDeltaForUI { get { return _lastPlayerInputScrollDeltaForUI; } }
        public static int LastMainScrollDelta { get { return _lastMainScrollDelta; } }
        public static bool LastPlayerInputCleared { get { return _lastPlayerInputCleared; } }
        public static bool LastMainScrollSuppressed { get { return _lastMainScrollSuppressed; } }
        public static bool LastScrollHotbarHookSuppressed { get { return _lastScrollHotbarHookSuppressed; } }
        public static string ScrollCandidateSummary
        {
            get
            {
                return "PlayerInput=" + (string.IsNullOrWhiteSpace(_playerInputScrollCandidateSummary) ? "<unresolved>" : _playerInputScrollCandidateSummary) +
                       "; Main=" + (string.IsNullOrWhiteSpace(_mainScrollCandidateSummary) ? "<unresolved>" : _mainScrollCandidateSummary);
            }
        }

        public static bool TryReadMouseState(out int mouseX, out int mouseY, out bool leftDown)
        {
            mouseX = 0;
            mouseY = 0;
            leftDown = false;

            try
            {
                if (!EnsureMainMouseAccessors())
                {
                    leftDown = IsLeftButtonDownFallback();
                    return FailRead("Terraria mouse position fields unavailable; using OS left-button fallback only.");
                }

                mouseX = ReadInt(_mouseXField, _mouseXProperty, 0);
                mouseY = ReadInt(_mouseYField, _mouseYProperty, 0);
                leftDown = ReadBool(_mouseLeftField, _mouseLeftProperty, IsLeftButtonDownFallback());
                UiMouseReadAvailable = true;
                _mouseReadLastMessage = "UI mouse state read OK.";
                return true;
            }
            catch (Exception error)
            {
                leftDown = IsLeftButtonDownFallback();
                return FailRead("Read UI mouse state failed: " + error.Message);
            }
        }

        public static bool TryReadHoverItemSnapshot(out TerrariaUiHoverItemSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType == null)
                {
                    return false;
                }

                EnsureUiMouseAccessors(mainType);
                object hoverItem;
                if (!TryReadHoverItemObject(out hoverItem))
                {
                    return false;
                }

                return TryBuildHoverItemSnapshot(hoverItem, out snapshot);
            }
            catch (Exception error)
            {
                _mouseReadLastMessage = "Read UI hover item failed: " + error.Message;
                LogThrottle.WarnThrottled(
                    "ui-hover-item-read-failed",
                    TimeSpan.FromSeconds(10),
                    "TerrariaUiMouseCompat",
                    _mouseReadLastMessage);
                return false;
            }
        }

        public static bool TryReadFreshHoverItemSnapshot(
            ulong currentGameUpdateCount,
            int mouseX,
            int mouseY,
            out TerrariaUiHoverItemSnapshot snapshot)
        {
            snapshot = null;

            TerrariaUiHoverSlotSnapshot slotSnapshot;
            if (!TryReadFreshHoverSlotSnapshot(currentGameUpdateCount, mouseX, mouseY, out slotSnapshot) ||
                slotSnapshot == null ||
                !slotSnapshot.HasActiveItem ||
                slotSnapshot.ItemSnapshot == null)
            {
                return false;
            }

            snapshot = CloneHoverItemSnapshot(slotSnapshot.ItemSnapshot);
            return true;
        }

        public static bool TryReadFreshHoverSlotSnapshot(
            ulong currentGameUpdateCount,
            int mouseX,
            int mouseY,
            out TerrariaUiHoverSlotSnapshot snapshot)
        {
            TerrariaUiHoverSlotReadResult readResult;
            return TryReadFreshHoverSlotSnapshot(
                currentGameUpdateCount,
                mouseX,
                mouseY,
                out snapshot,
                out readResult);
        }

        public static bool TryReadFreshHoverSlotSnapshot(
            ulong currentGameUpdateCount,
            int mouseX,
            int mouseY,
            out TerrariaUiHoverSlotSnapshot snapshot,
            out TerrariaUiHoverSlotReadResult readResult)
        {
            snapshot = null;

            TerrariaUiHoverSlotSnapshot cached;
            lock (HoverSnapshotSyncRoot)
            {
                cached = CloneHoverSlotSnapshot(_lastItemSlotHoverSlotSnapshot);
            }

            readResult = EvaluateHoverSlotSnapshot(cached, currentGameUpdateCount, mouseX, mouseY);
            if (readResult == null || !readResult.IsFresh)
            {
                return false;
            }

            snapshot = cached;
            return true;
        }

        public static bool ItemSlotHoverHookInstalled
        {
            get
            {
                lock (HoverSnapshotSyncRoot)
                {
                    return _itemSlotHoverHookInstalled;
                }
            }
        }

        public static string ItemSlotHoverHookStatus
        {
            get
            {
                lock (HoverSnapshotSyncRoot)
                {
                    return _itemSlotHoverHookStatus ?? string.Empty;
                }
            }
        }

        public static string ItemSlotHoverHookCandidateSummary
        {
            get
            {
                lock (HoverSnapshotSyncRoot)
                {
                    return _itemSlotHoverHookCandidateSummary ?? string.Empty;
                }
            }
        }

        internal static bool TryCaptureItemSlotHoverSnapshot(object inventory, int context, int slot)
        {
            object item;
            if (!TryGetInventoryItem(inventory, slot, out item))
            {
                return false;
            }

            return TryCaptureItemSlotHoverSnapshot(
                item,
                context,
                slot,
                TerrariaMainCompat.GameUpdateCount,
                TerrariaMainCompat.MouseX,
                TerrariaMainCompat.MouseY,
                "ItemSlot");
        }

        internal static bool TryCaptureItemSlotHoverSnapshotForTesting(
            object item,
            int context,
            int slot,
            ulong gameUpdateCount,
            int mouseX,
            int mouseY)
        {
            return TryCaptureItemSlotHoverSnapshot(item, context, slot, gameUpdateCount, mouseX, mouseY, "ItemSlot");
        }

        internal static void ResetHoverItemSnapshotForTesting()
        {
            lock (HoverSnapshotSyncRoot)
            {
                _lastItemSlotHoverSlotSnapshot = null;
            }
        }

        internal static void RecordItemSlotHoverHookInstallResult(
            bool installed,
            string status,
            string candidateSummary)
        {
            lock (HoverSnapshotSyncRoot)
            {
                _itemSlotHoverHookInstalled = installed;
                _itemSlotHoverHookStatus = string.IsNullOrWhiteSpace(status)
                    ? (installed ? "installed" : "notInstalled")
                    : status.Trim();
                _itemSlotHoverHookCandidateSummary = candidateSummary ?? string.Empty;
            }
        }

        // UI capture may mark Terraria mouse flags only to prevent click-through;
        // it must not execute world, inventory, or item actions.
        public static bool TryMarkUiMouseCapture()
        {
            try
            {
                object player;
                var captured = false;
                if (TerrariaInputCompat.TryGetLocalPlayer(out player))
                {
                    captured |= TrySetLocalPlayerMouseInterface(player, true);
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType != null)
                {
                    EnsureUiMouseAccessors(mainType);
                    captured |= _mainMouseInterfaceAccessor.TrySet(null, true);
                    captured |= _mainBlockMouseAccessor.TrySet(null, true);
                    _mainMouseLeftAccessor.TrySet(null, false);
                    _mainMouseRightAccessor.TrySet(null, false);
                    _mainMouseLeftReleaseAccessor.TrySet(null, false);
                    _mainMouseRightReleaseAccessor.TrySet(null, false);
                }

                TrySuppressMouseText();
                UiMouseCaptureAvailable = captured;
                _mouseCaptureLastMessage = captured
                    ? "UI mouse capture marked."
                    : "UI mouse capture unavailable.";

                if (!captured)
                {
                    LogThrottle.WarnThrottled(
                        "ui-mouse-capture-unavailable",
                        TimeSpan.FromSeconds(10),
                        "TerrariaUiMouseCompat",
                        _mouseCaptureLastMessage);
                }

                return captured;
            }
            catch (Exception error)
            {
                UiMouseCaptureAvailable = false;
                _mouseCaptureLastMessage = "UI mouse capture failed: " + error.Message;
                LogThrottle.WarnThrottled(
                    "ui-mouse-capture-failed",
                    TimeSpan.FromSeconds(10),
                    "TerrariaUiMouseCompat",
                    _mouseCaptureLastMessage);
                return false;
            }
        }

        public static void UpdateActiveTriggerSuppressionPrefixGuard()
        {
            UpdateActiveTriggerSuppressionGuard("Main.Update.Prefix");
        }

        public static void UpdateActiveTriggerSuppressionAfterPlayerInputGuard()
        {
            UpdateActiveTriggerSuppressionGuard("PlayerInput.Postfix");
        }

        private static void UpdateActiveTriggerSuppressionGuard(string source)
        {
            try
            {
                string token;
                ulong startUpdateCount;
                lock (ActiveTriggerSuppressionSyncRoot)
                {
                    token = _activeTriggerSuppressionToken;
                    startUpdateCount = _activeTriggerSuppressionStartUpdateCount;
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                ulong currentUpdateCount;
                if (!TerrariaMainCompat.TryReadGameUpdateCount(out currentUpdateCount))
                {
                    currentUpdateCount = startUpdateCount;
                }

                if (currentUpdateCount >= startUpdateCount &&
                    currentUpdateCount - startUpdateCount > MaxActiveTriggerSuppressionUpdates)
                {
                    ClearActiveTriggerSuppression("expired");
                    return;
                }

                if (!IsMouseTriggerStillDown(token))
                {
                    ClearActiveTriggerSuppression("released");
                    return;
                }

                string message;
                ApplyMouseTriggerInputSuppression(token, out message);
            }
            catch (Exception error)
            {
                ClearActiveTriggerSuppression("error");
                _mouseCaptureLastMessage = "Active trigger suppression failed at " +
                                           (source ?? string.Empty) +
                                           ": " +
                                           error.Message;
                LogThrottle.WarnThrottled(
                    "ui-active-trigger-suppression-failed",
                    TimeSpan.FromSeconds(10),
                    "TerrariaUiMouseCompat",
                    _mouseCaptureLastMessage);
            }
        }

        // One-shot trigger consumption for quick, non-UI commands: mark this
        // click as mouse-captured and clear only the vanilla button pulse that
        // matches the trigger until the physical button is released. Do not use
        // this as a generic gameplay input API.
        public static bool TryConsumeMouseTriggerInput(string triggerToken, out string message)
        {
            message = string.Empty;
            var normalizedToken = NormalizeMouseTriggerToken(triggerToken);
            if (normalizedToken.Length <= 0)
            {
                message = "Unsupported mouse trigger token: " + (triggerToken ?? string.Empty);
                UiMouseCaptureAvailable = false;
                _mouseCaptureLastMessage = message;
                return false;
            }

            try
            {
                var consumed = ApplyMouseTriggerInputSuppression(normalizedToken, out message);
                if (consumed)
                {
                    StartActiveTriggerSuppression(normalizedToken);
                }

                UiMouseCaptureAvailable = consumed;
                _mouseCaptureLastMessage = message;
                message = _mouseCaptureLastMessage;
                return consumed;
            }
            catch (Exception error)
            {
                UiMouseCaptureAvailable = false;
                _mouseCaptureLastMessage = "Mouse trigger input consume failed: " + error.Message;
                message = _mouseCaptureLastMessage;
                LogThrottle.WarnThrottled(
                    "ui-mouse-trigger-consume-failed",
                    TimeSpan.FromSeconds(10),
                    "TerrariaUiMouseCompat",
                    _mouseCaptureLastMessage);
                return false;
            }
        }

        public static bool TryReleaseUiMouseCapture()
        {
            try
            {
                object player;
                var released = false;
                if (TerrariaInputCompat.TryGetLocalPlayer(out player))
                {
                    released |= TrySetLocalPlayerMouseInterface(player, false);
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType != null)
                {
                    EnsureUiMouseAccessors(mainType);
                    released |= _mainMouseInterfaceAccessor.TrySet(null, false);
                    released |= _mainBlockMouseAccessor.TrySet(null, false);
                }

                UiMouseCaptureAvailable = false;
                _mouseCaptureLastMessage = released
                    ? "UI mouse capture released."
                    : "UI mouse capture release unavailable.";
                return released;
            }
            catch (Exception error)
            {
                UiMouseCaptureAvailable = false;
                _mouseCaptureLastMessage = "UI mouse capture release failed: " + error.Message;
                LogThrottle.WarnThrottled(
                    "ui-mouse-capture-release-failed",
                    TimeSpan.FromSeconds(10),
                    "TerrariaUiMouseCompat",
                    _mouseCaptureLastMessage);
                return false;
            }
        }

        private static bool ApplyMouseTriggerInputSuppression(string normalizedToken, out string message)
        {
            message = string.Empty;
            object player;
            var captured = false;
            var cleared = false;
            var playerUseSuppressed = false;
            if (TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                captured |= TrySetLocalPlayerMouseInterface(player, true);
                playerUseSuppressed |= SuppressPlayerUseInputForTrigger(player, normalizedToken);
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType != null)
            {
                EnsureUiMouseAccessors(mainType);
                captured |= _mainMouseInterfaceAccessor.TrySet(null, true);
                captured |= _mainBlockMouseAccessor.TrySet(null, true);

                if (string.Equals(normalizedToken, "MouseLeft", StringComparison.Ordinal))
                {
                    cleared |= _mainMouseLeftAccessor.TrySet(null, false);
                    cleared |= _mainMouseLeftReleaseAccessor.TrySet(null, false);
                }
                else if (string.Equals(normalizedToken, "MouseRight", StringComparison.Ordinal))
                {
                    cleared |= _mainMouseRightAccessor.TrySet(null, false);
                    cleared |= _mainMouseRightReleaseAccessor.TrySet(null, false);
                }
            }

            cleared |= TrySetPlayerInputTriggerForMouseToken("Current", normalizedToken, false);
            cleared |= TrySetPlayerInputTriggerForMouseToken("JustPressed", normalizedToken, false);

            TrySuppressMouseText();

            var consumed = captured || cleared || playerUseSuppressed;
            message = consumed
                ? "Mouse trigger input consumed for " + normalizedToken +
                  " (captured=" + captured +
                  ", cleared=" + cleared +
                  ", playerUseSuppressed=" + playerUseSuppressed + ")."
                : "Mouse trigger input consume unavailable for " + normalizedToken + ".";
            return consumed;
        }

        private static bool SuppressPlayerUseInputForTrigger(object player, string normalizedToken)
        {
            if (player == null)
            {
                return false;
            }

            var changed = false;
            if (string.Equals(normalizedToken, "MouseLeft", StringComparison.Ordinal))
            {
                changed |= TrySetMember(player, "controlUseItem", false);
                changed |= TrySetMember(player, "releaseUseItem", true);
                // Channel is an input-held use state; clearing it only for the
                // consumed click lets vanilla ItemSlot hover run without keeping
                // a held item in "using" mode.
                TrySetMember(player, "channel", false);
            }

            return changed;
        }

        private static void StartActiveTriggerSuppression(string normalizedToken)
        {
            ulong updateCount;
            if (!TerrariaMainCompat.TryReadGameUpdateCount(out updateCount))
            {
                updateCount = 0;
            }

            lock (ActiveTriggerSuppressionSyncRoot)
            {
                _activeTriggerSuppressionToken = normalizedToken ?? string.Empty;
                _activeTriggerSuppressionStartUpdateCount = updateCount;
            }
        }

        private static void ClearActiveTriggerSuppression(string reason)
        {
            lock (ActiveTriggerSuppressionSyncRoot)
            {
                _activeTriggerSuppressionToken = string.Empty;
                _activeTriggerSuppressionStartUpdateCount = 0;
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                _mouseCaptureLastMessage = "Active trigger suppression ended: " + reason + ".";
            }
        }

        public static bool TrySuppressMouseText()
        {
            return TrySuppressPendingMouseTextForUi();
        }

        // Use only from F5/UI capture boundaries. This clears late vanilla hover
        // caches; it is not a general switch for disabling world interaction.
        public static bool TrySuppressPendingMouseTextForUi()
        {
            try
            {
                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType == null)
                {
                    return false;
                }

                EnsureUiMouseAccessors(mainType);
                var suppressed = false;
                suppressed |= _mainHoverItemNameAccessor.TrySet(null, string.Empty);
                suppressed |= _mainHoverItemName2Accessor.TrySet(null, string.Empty);
                suppressed |= _mainMouseTextAccessor.TrySet(null, false);
                suppressed |= _mainHoveringOverNpcAccessor.TrySet(null, false);
                suppressed |= _mainHoverItemAccessor.TrySet(null, GetEmptyHoverItem(_mainHoverItemAccessor.MemberType));
                suppressed |= _mainHoverItemLowerAccessor.TrySet(null, GetEmptyHoverItem(_mainHoverItemLowerAccessor.MemberType));
                object mainInstance;
                if (TryGetMainInstance(out mainInstance))
                {
                    EnsureUiMouseMainInstanceAccessors(mainInstance.GetType());
                    // MouseTextHackZoom writes a private pending cache after vanilla
                    // Mouse Over; clearing only Main.mouseText is too early for F5.
                    suppressed |= TryClearMouseTextCache(mainInstance);
                    suppressed |= _mainInstanceMouseNpcIndexAccessor.TrySet(mainInstance, -1);
                    suppressed |= _mainInstanceMouseNpcTypeAccessor.TrySet(mainInstance, -1);
                    suppressed |= _mainInstanceCurrentNpcShowingChatBubbleAccessor.TrySet(mainInstance, -1);
                    suppressed |= _mainInstanceHoverItemAccessor.TrySet(mainInstance, GetEmptyHoverItem(_mainInstanceHoverItemAccessor.MemberType));
                    suppressed |= _mainInstanceHoverItemLowerAccessor.TrySet(mainInstance, GetEmptyHoverItem(_mainInstanceHoverItemLowerAccessor.MemberType));
                }

                if (!suppressed)
                {
                    LogThrottle.WarnThrottled(
                        "ui-pending-mouse-text-suppress-unavailable",
                        TimeSpan.FromSeconds(10),
                        "TerrariaUiMouseCompat",
                        "UI pending mouse text suppression found no writable Terraria mouse text members.");
                }

                return suppressed;
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Suppress mouse text failed: " + error.Message;
                LogThrottle.WarnThrottled(
                    "ui-mouse-text-suppress-failed",
                    TimeSpan.FromSeconds(10),
                    "TerrariaUiMouseCompat",
                    _mouseCaptureLastMessage);
                return false;
            }
        }

        public static bool TrySuppressHotbarScroll()
        {
            try
            {
                if (!EnsureMainMouseAccessors())
                {
                    return FailScrollSuppress("Terraria mouse scroll fields unavailable.");
                }

                if ((_mouseScrollWheelField == null && _mouseScrollWheelProperty == null) ||
                    (_oldMouseScrollWheelField == null && _oldMouseScrollWheelProperty == null))
                {
                    return FailScrollSuppress("Terraria mouseScrollWheel/oldMouseScrollWheel unavailable.");
                }

                var current = ReadInt(_mouseScrollWheelField, _mouseScrollWheelProperty, 0);
                var old = ReadInt(_oldMouseScrollWheelField, _oldMouseScrollWheelProperty, current);
                _lastMainScrollDelta = current - old;
                var oldSynced = TrySetInt(_oldMouseScrollWheelField, _oldMouseScrollWheelProperty, current);
                var target = oldSynced ? current : old;
                var currentSynced = TrySetInt(_mouseScrollWheelField, _mouseScrollWheelProperty, target);
                var changed = oldSynced || currentSynced;

                _lastMainScrollSuppressed = changed;
                UiScrollSuppressAvailable = changed;
                _scrollSuppressLastMessage = changed
                    ? "UI scroll consumed; Main mouse scroll fields synchronized."
                    : "UI scroll suppression field write unavailable.";
                return changed;
            }
            catch (Exception error)
            {
                return FailScrollSuppress("UI scroll suppression failed: " + error.Message);
            }
        }

        public static bool TryReadPlayerInputScrollDelta(out int delta)
        {
            delta = 0;
            int deltaForUi;
            if (!TryReadPlayerInputScrollDeltas(out delta, out deltaForUi))
            {
                return false;
            }

            if (delta == 0)
            {
                delta = deltaForUi;
            }

            return true;
        }

        public static bool TryReadPlayerInputScrollDeltas(out int delta, out int deltaForUi)
        {
            delta = 0;
            deltaForUi = 0;
            try
            {
                if (!EnsurePlayerInputAccessors())
                {
                    return false;
                }

                delta = ReadInt(_scrollWheelDeltaField, _scrollWheelDeltaProperty, 0);
                deltaForUi = ReadInt(_scrollWheelDeltaForUiField, _scrollWheelDeltaForUiProperty, 0);
                _lastPlayerInputScrollDelta = delta;
                _lastPlayerInputScrollDeltaForUI = deltaForUi;
                return true;
            }
            catch (Exception error)
            {
                _scrollSuppressLastMessage = "Read PlayerInput scroll deltas failed: " + error.Message;
                return false;
            }
        }

        public static bool TryReadMainScrollDelta(out int delta)
        {
            delta = 0;
            try
            {
                if (!EnsureMainMouseAccessors() ||
                    (_mouseScrollWheelField == null && _mouseScrollWheelProperty == null) ||
                    (_oldMouseScrollWheelField == null && _oldMouseScrollWheelProperty == null))
                {
                    return false;
                }

                var current = ReadInt(_mouseScrollWheelField, _mouseScrollWheelProperty, 0);
                var old = ReadInt(_oldMouseScrollWheelField, _oldMouseScrollWheelProperty, current);
                delta = current - old;
                _lastMainScrollDelta = delta;
                return true;
            }
            catch (Exception error)
            {
                _scrollSuppressLastMessage = "Read Main mouse scroll delta failed: " + error.Message;
                return false;
            }
        }

        public static UiScrollDeltaSnapshot ReadScrollSnapshot(int diagnosticMainScrollDelta)
        {
            int playerInputDelta;
            int playerInputDeltaForUi;
            TryReadPlayerInputScrollDeltas(out playerInputDelta, out playerInputDeltaForUi);

            int mainScrollDelta;
            if (!TryReadMainScrollDelta(out mainScrollDelta))
            {
                mainScrollDelta = diagnosticMainScrollDelta;
                _lastMainScrollDelta = mainScrollDelta;
            }

            return new UiScrollDeltaSnapshot
            {
                PlayerInputScrollDelta = playerInputDelta,
                PlayerInputScrollDeltaForUI = playerInputDeltaForUi,
                MainScrollDelta = mainScrollDelta,
                DiagnosticMainScrollDelta = diagnosticMainScrollDelta,
                EffectiveScrollDelta = FirstNonZero(playerInputDelta, playerInputDeltaForUi, mainScrollDelta, diagnosticMainScrollDelta),
                CandidateSummary = ScrollCandidateSummary
            };
        }

        public static bool TryClearPlayerInputScrollDelta()
        {
            try
            {
                if (!EnsurePlayerInputAccessors())
                {
                    _lastPlayerInputCleared = false;
                    return false;
                }

                var cleared = false;
                if (_scrollWheelDeltaField != null || _scrollWheelDeltaProperty != null)
                {
                    cleared |= TrySetInt(_scrollWheelDeltaField, _scrollWheelDeltaProperty, 0);
                }

                if (_scrollWheelDeltaForUiField != null || _scrollWheelDeltaForUiProperty != null)
                {
                    cleared |= TrySetInt(_scrollWheelDeltaForUiField, _scrollWheelDeltaForUiProperty, 0);
                }

                _lastPlayerInputCleared = cleared;
                return cleared;
            }
            catch (Exception error)
            {
                _lastPlayerInputCleared = false;
                _scrollSuppressLastMessage = "Clear PlayerInput scroll deltas failed: " + error.Message;
                return false;
            }
        }

        public static bool TryConsumeUiScroll()
        {
            _lastScrollHotbarHookSuppressed = false;
            var playerInputCleared = TryClearPlayerInputScrollDelta();
            var mainSuppressed = TrySuppressHotbarScroll();
            var mouseCaptured = TryMarkUiMouseCapture();
            _lastPlayerInputCleared = playerInputCleared;
            _lastMainScrollSuppressed = mainSuppressed;
            UiScrollSuppressAvailable = playerInputCleared || mainSuppressed;
            _scrollSuppressLastMessage = UiScrollSuppressAvailable
                ? "UI scroll consumed through PlayerInput/Main scroll suppression."
                : "UI scroll consume attempted but no scroll field was writable.";
            return playerInputCleared || mainSuppressed || mouseCaptured;
        }

        public static void MarkScrollHotbarHookSuppressed()
        {
            _lastScrollHotbarHookSuppressed = true;
        }

        internal static void ResetUiMouseCaptureAccessorsForTesting()
        {
            ClearActiveTriggerSuppression("testReset");
            lock (UiMouseAccessorSyncRoot)
            {
                _uiMouseAccessorMainType = null;
                _uiMouseAccessorMainInstanceType = null;
                _localPlayerMouseInterfaceType = null;
                _localPlayerMouseInterfaceAccessor = BooleanMemberAccessor.Empty;
                _mainMouseInterfaceAccessor = BooleanMemberAccessor.Empty;
                _mainBlockMouseAccessor = BooleanMemberAccessor.Empty;
                _mainMouseLeftAccessor = BooleanMemberAccessor.Empty;
                _mainMouseRightAccessor = BooleanMemberAccessor.Empty;
                _mainMouseLeftReleaseAccessor = BooleanMemberAccessor.Empty;
                _mainMouseRightReleaseAccessor = BooleanMemberAccessor.Empty;
                _mainMouseTextAccessor = BooleanMemberAccessor.Empty;
                _mainHoveringOverNpcAccessor = BooleanMemberAccessor.Empty;
                _mainHoverItemNameAccessor = StringMemberAccessor.Empty;
                _mainHoverItemName2Accessor = StringMemberAccessor.Empty;
                _mainHoverItemAccessor = ObjectMemberAccessor.Empty;
                _mainHoverItemLowerAccessor = ObjectMemberAccessor.Empty;
                _mainInstanceAccessor = ObjectMemberAccessor.Empty;
                _mainInstanceCapsAccessor = ObjectMemberAccessor.Empty;
                _mainInstanceHoverItemAccessor = ObjectMemberAccessor.Empty;
                _mainInstanceHoverItemLowerAccessor = ObjectMemberAccessor.Empty;
                _mainInstanceMouseTextCacheAccessor = ObjectMemberAccessor.Empty;
                _mainInstanceMouseNpcIndexAccessor = IntegerMemberAccessor.Empty;
                _mainInstanceMouseNpcTypeAccessor = IntegerMemberAccessor.Empty;
                _mainInstanceCurrentNpcShowingChatBubbleAccessor = IntegerMemberAccessor.Empty;
                EmptyHoverItemsByType.Clear();
            }

            ResetHoverItemSnapshotForTesting();
        }

        private static void EnsureUiMouseAccessors(Type mainType)
        {
            if (mainType == null)
            {
                return;
            }

            if (_uiMouseAccessorMainType == mainType)
            {
                return;
            }

            lock (UiMouseAccessorSyncRoot)
            {
                if (_uiMouseAccessorMainType == mainType)
                {
                    return;
                }

                _uiMouseAccessorMainType = mainType;
                _uiMouseAccessorMainInstanceType = null;
                _mainMouseInterfaceAccessor = BooleanMemberAccessor.Resolve(mainType, "mouseInterface", true);
                _mainBlockMouseAccessor = BooleanMemberAccessor.Resolve(mainType, "blockMouse", true);
                _mainMouseLeftAccessor = BooleanMemberAccessor.Resolve(mainType, "mouseLeft", true);
                _mainMouseRightAccessor = BooleanMemberAccessor.Resolve(mainType, "mouseRight", true);
                _mainMouseLeftReleaseAccessor = BooleanMemberAccessor.Resolve(mainType, "mouseLeftRelease", true);
                _mainMouseRightReleaseAccessor = BooleanMemberAccessor.Resolve(mainType, "mouseRightRelease", true);
                _mainMouseTextAccessor = BooleanMemberAccessor.Resolve(mainType, "mouseText", true);
                _mainHoveringOverNpcAccessor = BooleanMemberAccessor.Resolve(mainType, "HoveringOverAnNPC", true);
                _mainHoverItemNameAccessor = StringMemberAccessor.Resolve(mainType, "hoverItemName", true);
                _mainHoverItemName2Accessor = StringMemberAccessor.Resolve(mainType, "hoverItemName2", true);
                _mainHoverItemAccessor = ObjectMemberAccessor.Resolve(mainType, "HoverItem", true, true);
                _mainHoverItemLowerAccessor = ObjectMemberAccessor.Resolve(mainType, "hoverItem", true, true);
                _mainInstanceAccessor = ObjectMemberAccessor.Resolve(mainType, "instance", true, false);
                _mainInstanceCapsAccessor = ObjectMemberAccessor.Resolve(mainType, "Instance", true, false);
                _mainInstanceHoverItemAccessor = ObjectMemberAccessor.Empty;
                _mainInstanceHoverItemLowerAccessor = ObjectMemberAccessor.Empty;
                _mainInstanceMouseTextCacheAccessor = ObjectMemberAccessor.Empty;
                _mainInstanceMouseNpcIndexAccessor = IntegerMemberAccessor.Empty;
                _mainInstanceMouseNpcTypeAccessor = IntegerMemberAccessor.Empty;
                _mainInstanceCurrentNpcShowingChatBubbleAccessor = IntegerMemberAccessor.Empty;
            }
        }

        private static void EnsureUiMouseMainInstanceAccessors(Type instanceType)
        {
            if (instanceType == null)
            {
                return;
            }

            if (_uiMouseAccessorMainInstanceType == instanceType)
            {
                return;
            }

            lock (UiMouseAccessorSyncRoot)
            {
                if (_uiMouseAccessorMainInstanceType == instanceType)
                {
                    return;
                }

                _uiMouseAccessorMainInstanceType = instanceType;
                _mainInstanceHoverItemAccessor = ObjectMemberAccessor.Resolve(instanceType, "HoverItem", false, true);
                _mainInstanceHoverItemLowerAccessor = ObjectMemberAccessor.Resolve(instanceType, "hoverItem", false, true);
                _mainInstanceMouseTextCacheAccessor = ObjectMemberAccessor.Resolve(instanceType, "_mouseTextCache", false, true);
                _mainInstanceMouseNpcIndexAccessor = IntegerMemberAccessor.Resolve(instanceType, "mouseNPCIndex", false);
                _mainInstanceMouseNpcTypeAccessor = IntegerMemberAccessor.Resolve(instanceType, "mouseNPCType", false);
                _mainInstanceCurrentNpcShowingChatBubbleAccessor = IntegerMemberAccessor.Resolve(instanceType, "currentNPCShowingChatBubble", false);
            }
        }

        private static bool TrySetLocalPlayerMouseInterface(object player, bool value)
        {
            if (player == null)
            {
                return false;
            }

            try
            {
                var playerType = player.GetType();
                if (_localPlayerMouseInterfaceType != playerType)
                {
                    lock (UiMouseAccessorSyncRoot)
                    {
                        if (_localPlayerMouseInterfaceType != playerType)
                        {
                            _localPlayerMouseInterfaceType = playerType;
                            _localPlayerMouseInterfaceAccessor = BooleanMemberAccessor.Resolve(playerType, "mouseInterface", false);
                        }
                    }
                }

                return _localPlayerMouseInterfaceAccessor.TrySet(player, value);
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Set UI mouse member failed: " + error.Message;
                return false;
            }
        }

        private static string NormalizeMouseTriggerToken(string token)
        {
            token = string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim();
            if (token.Length <= 0)
            {
                return string.Empty;
            }

            if (string.Equals(token, "MouseLeft", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "LeftMouse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Mouse1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "LButton", StringComparison.OrdinalIgnoreCase))
            {
                return "MouseLeft";
            }

            if (string.Equals(token, "MouseRight", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "RightMouse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Mouse2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "RButton", StringComparison.OrdinalIgnoreCase))
            {
                return "MouseRight";
            }

            if (string.Equals(token, "MouseMiddle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "MiddleMouse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Mouse3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "MButton", StringComparison.OrdinalIgnoreCase))
            {
                return "MouseMiddle";
            }

            if (string.Equals(token, "Mouse4", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "XButton1", StringComparison.OrdinalIgnoreCase))
            {
                return "Mouse4";
            }

            if (string.Equals(token, "Mouse5", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "XButton2", StringComparison.OrdinalIgnoreCase))
            {
                return "Mouse5";
            }

            return string.Empty;
        }

        private static bool IsMouseTriggerStillDown(string normalizedToken)
        {
            if (string.Equals(normalizedToken, "MouseLeft", StringComparison.Ordinal))
            {
                bool mainValue;
                if (_mainMouseLeftAccessor.TryGet(null, out mainValue) && mainValue)
                {
                    return true;
                }

                bool triggerValue;
                if (TryReadPlayerInputTriggerForMouseToken("Current", normalizedToken, out triggerValue) && triggerValue)
                {
                    return true;
                }

                return IsMouseButtonDownFallback(VkLeftButton);
            }

            if (string.Equals(normalizedToken, "MouseRight", StringComparison.Ordinal))
            {
                bool mainValue;
                if (_mainMouseRightAccessor.TryGet(null, out mainValue) && mainValue)
                {
                    return true;
                }

                bool triggerValue;
                if (TryReadPlayerInputTriggerForMouseToken("Current", normalizedToken, out triggerValue) && triggerValue)
                {
                    return true;
                }

                return IsMouseButtonDownFallback(VkRightButton);
            }

            bool value;
            if (TryReadPlayerInputTriggerForMouseToken("Current", normalizedToken, out value) && value)
            {
                return true;
            }

            var virtualKey = GetMouseTriggerVirtualKey(normalizedToken);
            return virtualKey > 0 && IsMouseButtonDownFallback(virtualKey);
        }

        private static bool TryReadPlayerInputTriggerForMouseToken(string packName, string normalizedToken, out bool value)
        {
            value = false;
            var triggerName = GetPlayerInputTriggerName(normalizedToken);
            if (triggerName.Length <= 0)
            {
                return false;
            }

            object triggerSet;
            if (!TryGetPlayerInputTriggerSet(packName, out triggerSet) || triggerSet == null)
            {
                return false;
            }

            return TryReadBoolMember(triggerSet, triggerName, out value);
        }

        private static bool TrySetPlayerInputTriggerForMouseToken(string packName, string normalizedToken, bool value)
        {
            var triggerName = GetPlayerInputTriggerName(normalizedToken);
            if (triggerName.Length <= 0)
            {
                return false;
            }

            object triggerSet;
            return TryGetPlayerInputTriggerSet(packName, out triggerSet) &&
                   triggerSet != null &&
                   TrySetMember(triggerSet, triggerName, value);
        }

        private static bool TryGetPlayerInputTriggerSet(string packName, out object triggerSet)
        {
            triggerSet = null;
            if (string.IsNullOrWhiteSpace(packName))
            {
                return false;
            }

            try
            {
                var mainType = TerrariaRuntimeTypes.MainType;
                var assembly = mainType == null ? null : mainType.Assembly;
                var playerInputType = assembly == null ? null : assembly.GetType("Terraria.GameInput.PlayerInput", false);
                object triggersPack;
                if (!TryGetStaticObject(playerInputType, "Triggers", out triggersPack) || triggersPack == null)
                {
                    return false;
                }

                return TryGetObjectMember(triggersPack, packName, out triggerSet) && triggerSet != null;
            }
            catch
            {
                return false;
            }
        }

        private static string GetPlayerInputTriggerName(string normalizedToken)
        {
            if (string.Equals(normalizedToken, "MouseLeft", StringComparison.Ordinal))
            {
                return "MouseLeft";
            }

            if (string.Equals(normalizedToken, "MouseRight", StringComparison.Ordinal))
            {
                return "MouseRight";
            }

            if (string.Equals(normalizedToken, "MouseMiddle", StringComparison.Ordinal))
            {
                return "MouseMiddle";
            }

            if (string.Equals(normalizedToken, "Mouse4", StringComparison.Ordinal))
            {
                return "Mouse4";
            }

            if (string.Equals(normalizedToken, "Mouse5", StringComparison.Ordinal))
            {
                return "Mouse5";
            }

            return string.Empty;
        }

        private static int GetMouseTriggerVirtualKey(string normalizedToken)
        {
            if (string.Equals(normalizedToken, "MouseLeft", StringComparison.Ordinal))
            {
                return VkLeftButton;
            }

            if (string.Equals(normalizedToken, "MouseRight", StringComparison.Ordinal))
            {
                return VkRightButton;
            }

            if (string.Equals(normalizedToken, "MouseMiddle", StringComparison.Ordinal))
            {
                return VkMiddleButton;
            }

            if (string.Equals(normalizedToken, "Mouse4", StringComparison.Ordinal))
            {
                return VkXButton1;
            }

            if (string.Equals(normalizedToken, "Mouse5", StringComparison.Ordinal))
            {
                return VkXButton2;
            }

            return 0;
        }

        private static bool TryGetMainInstance(out object mainInstance)
        {
            mainInstance = null;
            if (_mainInstanceAccessor.TryGet(null, out mainInstance) && mainInstance != null)
            {
                return true;
            }

            return _mainInstanceCapsAccessor.TryGet(null, out mainInstance) && mainInstance != null;
        }

        private static bool TryReadHoverItemObject(out object hoverItem)
        {
            hoverItem = null;
            if (_mainHoverItemAccessor.TryGet(null, out hoverItem) && IsActiveHoverItem(hoverItem))
            {
                return true;
            }

            if (_mainHoverItemLowerAccessor.TryGet(null, out hoverItem) && IsActiveHoverItem(hoverItem))
            {
                return true;
            }

            object mainInstance;
            if (TryGetMainInstance(out mainInstance))
            {
                EnsureUiMouseMainInstanceAccessors(mainInstance.GetType());
                if (_mainInstanceHoverItemAccessor.TryGet(mainInstance, out hoverItem) && IsActiveHoverItem(hoverItem))
                {
                    return true;
                }

                if (_mainInstanceHoverItemLowerAccessor.TryGet(mainInstance, out hoverItem) && IsActiveHoverItem(hoverItem))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildHoverItemSnapshot(object hoverItem, out TerrariaUiHoverItemSnapshot snapshot)
        {
            snapshot = null;
            if (!IsActiveHoverItem(hoverItem))
            {
                return false;
            }

            int type;
            int stack;
            int prefix;
            TryReadIntMember(hoverItem, "type", out type);
            TryReadIntMember(hoverItem, "stack", out stack);
            TryReadIntMember(hoverItem, "prefix", out prefix);
            snapshot = new TerrariaUiHoverItemSnapshot
            {
                ItemType = type,
                Stack = Math.Max(1, stack),
                Prefix = Math.Max(0, prefix),
                Name = FirstNonEmpty(
                    TryReadStringMember(hoverItem, "Name"),
                    TryReadStringMember(hoverItem, "HoverName"),
                    TryReadStringMember(hoverItem, "name"))
            };
            return snapshot.ItemType > 0 && snapshot.Stack > 0;
        }

        private static bool TryCaptureItemSlotHoverSnapshot(
            object item,
            int context,
            int slot,
            ulong gameUpdateCount,
            int mouseX,
            int mouseY,
            string source)
        {
            TerrariaUiHoverSlotSnapshot slotSnapshot;
            if (!TryBuildHoverSlotSnapshot(item, context, slot, gameUpdateCount, mouseX, mouseY, source, out slotSnapshot))
            {
                return false;
            }

            lock (HoverSnapshotSyncRoot)
            {
                _lastItemSlotHoverSlotSnapshot = CloneHoverSlotSnapshot(slotSnapshot);
            }

            return slotSnapshot.HasActiveItem;
        }

        private static bool TryBuildHoverSlotSnapshot(
            object item,
            int context,
            int slot,
            ulong gameUpdateCount,
            int mouseX,
            int mouseY,
            string source,
            out TerrariaUiHoverSlotSnapshot slotSnapshot)
        {
            slotSnapshot = null;
            if (item == null)
            {
                return false;
            }

            TerrariaUiHoverItemSnapshot itemSnapshot;
            var hasActiveItem = TryBuildHoverItemSnapshot(item, out itemSnapshot);
            var resolvedSource =
                (string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim()) +
                ":" +
                context.ToString(CultureInfo.InvariantCulture) +
                ":" +
                slot.ToString(CultureInfo.InvariantCulture);

            if (hasActiveItem)
            {
                itemSnapshot.Context = context;
                itemSnapshot.Slot = slot;
                itemSnapshot.GameUpdateCount = gameUpdateCount;
                itemSnapshot.MouseX = mouseX;
                itemSnapshot.MouseY = mouseY;
                itemSnapshot.Source = resolvedSource;
            }

            slotSnapshot = new TerrariaUiHoverSlotSnapshot
            {
                HasActiveItem = hasActiveItem,
                ItemSnapshot = hasActiveItem ? CloneHoverItemSnapshot(itemSnapshot) : null,
                Source = resolvedSource,
                Context = context,
                Slot = slot,
                GameUpdateCount = gameUpdateCount,
                MouseX = mouseX,
                MouseY = mouseY
            };
            return true;
        }

        private static bool TryGetInventoryItem(object inventory, int slot, out object item)
        {
            item = null;
            if (inventory == null || slot < 0)
            {
                return false;
            }

            var array = inventory as Array;
            if (array != null)
            {
                if (array.Rank != 1 || slot >= array.GetLength(0))
                {
                    return false;
                }

                item = array.GetValue(slot);
                return item != null;
            }

            var list = inventory as System.Collections.IList;
            if (list == null || slot >= list.Count)
            {
                return false;
            }

            item = list[slot];
            return item != null;
        }

        private static bool IsFreshHoverSlotSnapshot(
            TerrariaUiHoverSlotSnapshot snapshot,
            ulong currentGameUpdateCount,
            int mouseX,
            int mouseY)
        {
            var result = EvaluateHoverSlotSnapshot(snapshot, currentGameUpdateCount, mouseX, mouseY);
            return result != null && result.IsFresh;
        }

        private static TerrariaUiHoverSlotReadResult EvaluateHoverSlotSnapshot(
            TerrariaUiHoverSlotSnapshot snapshot,
            ulong currentGameUpdateCount,
            int mouseX,
            int mouseY)
        {
            var result = new TerrariaUiHoverSlotReadResult
            {
                Status = "noSnapshot",
                AgeUpdates = -1
            };
            if (snapshot == null)
            {
                return result;
            }

            result.HasSnapshot = true;
            result.HasActiveItem = snapshot.HasActiveItem;
            result.Source = snapshot.Source ?? string.Empty;
            result.Context = snapshot.Context;
            result.Slot = snapshot.Slot;

            if (currentGameUpdateCount < snapshot.GameUpdateCount)
            {
                result.Status = "futureSnapshot";
                return result;
            }

            var age = currentGameUpdateCount - snapshot.GameUpdateCount;
            result.AgeUpdates = age > int.MaxValue ? int.MaxValue : (int)age;

            // Empty UI slots are real ItemSlot hits too. Keeping their short-lived
            // proof prevents quick announcement from treating an empty slot click as
            // permission to announce the world tile behind the UI.
            if (Math.Abs(snapshot.MouseX - mouseX) > HoverSnapshotMouseTolerancePixels ||
                Math.Abs(snapshot.MouseY - mouseY) > HoverSnapshotMouseTolerancePixels)
            {
                result.Status = "mouseLeft";
                return result;
            }

            if (age > MaxHoverSnapshotAgeUpdates)
            {
                result.Status = "expired";
                return result;
            }

            result.IsFresh = true;
            result.Status = snapshot.HasActiveItem ? "freshItem" : "freshEmptySlot";
            return result;
        }

        private static TerrariaUiHoverItemSnapshot CloneHoverItemSnapshot(TerrariaUiHoverItemSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new TerrariaUiHoverItemSnapshot
            {
                ItemType = snapshot.ItemType,
                Stack = snapshot.Stack,
                Prefix = snapshot.Prefix,
                Name = snapshot.Name ?? string.Empty,
                Source = snapshot.Source ?? string.Empty,
                Context = snapshot.Context,
                Slot = snapshot.Slot,
                GameUpdateCount = snapshot.GameUpdateCount,
                MouseX = snapshot.MouseX,
                MouseY = snapshot.MouseY
            };
        }

        private static TerrariaUiHoverSlotSnapshot CloneHoverSlotSnapshot(TerrariaUiHoverSlotSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new TerrariaUiHoverSlotSnapshot
            {
                HasActiveItem = snapshot.HasActiveItem,
                ItemSnapshot = CloneHoverItemSnapshot(snapshot.ItemSnapshot),
                Source = snapshot.Source ?? string.Empty,
                Context = snapshot.Context,
                Slot = snapshot.Slot,
                GameUpdateCount = snapshot.GameUpdateCount,
                MouseX = snapshot.MouseX,
                MouseY = snapshot.MouseY
            };
        }

        private static bool IsActiveHoverItem(object hoverItem)
        {
            int type;
            int stack;
            return hoverItem != null &&
                   TryReadIntMember(hoverItem, "type", out type) &&
                   TryReadIntMember(hoverItem, "stack", out stack) &&
                   type > 0 &&
                   stack > 0;
        }

        private static bool TryReadIntMember(object instance, string name, out int value)
        {
            value = 0;
            object raw;
            if (!TryReadInstanceMember(instance, name, out raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string TryReadStringMember(object instance, string name)
        {
            object raw;
            if (!TryReadInstanceMember(instance, name, out raw) || raw == null)
            {
                return string.Empty;
            }

            try
            {
                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryReadInstanceMember(object instance, string name, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    value = field.GetValue(instance);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanRead)
                {
                    value = property.GetValue(instance, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index].Trim();
                }
            }

            return string.Empty;
        }

        private static object GetEmptyHoverItem(Type itemType)
        {
            if (itemType == null || itemType == typeof(string) || itemType == typeof(bool))
            {
                return null;
            }

            object item;
            lock (UiMouseAccessorSyncRoot)
            {
                if (!EmptyHoverItemsByType.TryGetValue(itemType, out item))
                {
                    item = CreateEmptyItem(itemType);
                    EmptyHoverItemsByType[itemType] = item;
                }
            }

            ResetEmptyItem(item);
            return item;
        }

        private static void ResetEmptyItem(object item)
        {
            if (item == null)
            {
                return;
            }

            TrySetInstanceMember(item, "type", 0);
            TrySetInstanceMember(item, "stack", 0);
        }

        private static bool TryClearMouseTextCache(object mainInstance)
        {
            object cache;
            if (!_mainInstanceMouseTextCacheAccessor.TryGet(mainInstance, out cache) || cache == null)
            {
                return false;
            }

            var changed = false;
            changed |= TrySetInstanceBoolMember(cache, "isValid", false);
            changed |= TrySetInstanceBoolMember(cache, "noOverride", false);
            changed |= TrySetInstanceStringMember(cache, "cursorText", string.Empty);
            changed |= TrySetInstanceStringMember(cache, "buffTooltip", string.Empty);
            if (!changed)
            {
                return false;
            }

            return _mainInstanceMouseTextCacheAccessor.TrySet(mainInstance, cache);
        }

        private static bool TrySetInstanceBoolMember(object instance, string name, bool value)
        {
            if (instance == null)
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field) && field.FieldType == typeof(bool))
                {
                    field.SetValue(instance, value);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) &&
                    property.CanWrite &&
                    property.PropertyType == typeof(bool))
                {
                    property.SetValue(instance, value, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Set mouse text cache bool failed: " + error.Message;
            }

            return false;
        }

        private static bool TrySetInstanceStringMember(object instance, string name, string value)
        {
            if (instance == null)
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field) && field.FieldType == typeof(string))
                {
                    field.SetValue(instance, value ?? string.Empty);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) &&
                    property.CanWrite &&
                    property.PropertyType == typeof(string))
                {
                    property.SetValue(instance, value ?? string.Empty, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Set mouse text cache string failed: " + error.Message;
            }

            return false;
        }

        private static bool EnsureMainMouseAccessors()
        {
            if (_mainMouseResolved)
            {
                return HasMousePositionAccessor();
            }

            _mainMouseResolved = true;
            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                _mouseReadLastMessage = TerrariaRuntimeTypes.LastError;
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                _mouseReadLastMessage = "Terraria.Main unavailable.";
                return false;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            _mouseXField = mainType.GetField("mouseX", flags);
            _mouseYField = mainType.GetField("mouseY", flags);
            _mouseLeftField = mainType.GetField("mouseLeft", flags);
            _mouseScrollWheelField = mainType.GetField("mouseScrollWheel", flags);
            _oldMouseScrollWheelField = mainType.GetField("oldMouseScrollWheel", flags);
            _mouseXProperty = _mouseXField == null ? mainType.GetProperty("mouseX", flags) : null;
            _mouseYProperty = _mouseYField == null ? mainType.GetProperty("mouseY", flags) : null;
            _mouseLeftProperty = _mouseLeftField == null ? mainType.GetProperty("mouseLeft", flags) : null;
            _mouseScrollWheelProperty = _mouseScrollWheelField == null ? mainType.GetProperty("mouseScrollWheel", flags) : null;
            _oldMouseScrollWheelProperty = _oldMouseScrollWheelField == null ? mainType.GetProperty("oldMouseScrollWheel", flags) : null;
            _mainScrollCandidateSummary = BuildStaticIntScrollCandidateSummary(mainType);
            LogScrollCandidateSummaryOnce();
            return HasMousePositionAccessor();
        }

        private static bool EnsurePlayerInputAccessors()
        {
            if (_playerInputResolved)
            {
                return HasPlayerInputScrollAccessor();
            }

            _playerInputResolved = true;
            _playerInputType = FindType("Terraria.GameInput.PlayerInput");
            if (_playerInputType == null)
            {
                _scrollSuppressLastMessage = "Terraria.GameInput.PlayerInput unavailable.";
                _playerInputScrollCandidateSummary = "<none>";
                LogScrollCandidateSummaryOnce();
                return false;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            _scrollWheelDeltaField = _playerInputType.GetField("ScrollWheelDelta", flags);
            _scrollWheelDeltaProperty = _scrollWheelDeltaField == null ? _playerInputType.GetProperty("ScrollWheelDelta", flags) : null;
            _scrollWheelDeltaForUiField = _playerInputType.GetField("ScrollWheelDeltaForUI", flags);
            _scrollWheelDeltaForUiProperty = _scrollWheelDeltaForUiField == null ? _playerInputType.GetProperty("ScrollWheelDeltaForUI", flags) : null;
            _playerInputScrollCandidateSummary = BuildStaticIntScrollCandidateSummary(_playerInputType);
            LogScrollCandidateSummaryOnce();
            return HasPlayerInputScrollAccessor();
        }

        private static bool HasMousePositionAccessor()
        {
            return (_mouseXField != null || _mouseXProperty != null) &&
                   (_mouseYField != null || _mouseYProperty != null);
        }

        private static bool HasPlayerInputScrollAccessor()
        {
            return _scrollWheelDeltaField != null ||
                   _scrollWheelDeltaProperty != null ||
                   _scrollWheelDeltaForUiField != null ||
                   _scrollWheelDeltaForUiProperty != null;
        }

        private static int ReadInt(FieldInfo field, PropertyInfo property, int fallback)
        {
            var raw = ReadRaw(field, property);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(raw);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool ReadBool(FieldInfo field, PropertyInfo property, bool fallback)
        {
            var raw = ReadRaw(field, property);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(raw);
            }
            catch
            {
                return fallback;
            }
        }

        private static object ReadRaw(FieldInfo field, PropertyInfo property)
        {
            if (field != null)
            {
                return field.GetValue(null);
            }

            return property != null && property.CanRead ? property.GetValue(null, null) : null;
        }

        private static bool TrySetInt(FieldInfo field, PropertyInfo property, int value)
        {
            if (field != null)
            {
                field.SetValue(null, Convert.ChangeType(value, field.FieldType));
                return true;
            }

            if (property != null && property.CanWrite)
            {
                property.SetValue(null, Convert.ChangeType(value, property.PropertyType), null);
                return true;
            }

            return false;
        }

        private static int FirstNonZero(int first, int second, int third, int fourth)
        {
            if (first != 0)
            {
                return first;
            }

            if (second != 0)
            {
                return second;
            }

            if (third != 0)
            {
                return third;
            }

            return fourth;
        }

        private static string BuildStaticIntScrollCandidateSummary(Type type)
        {
            if (type == null)
            {
                return "<none>";
            }

            var builder = new StringBuilder();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            try
            {
                var fields = type.GetFields(flags);
                for (var index = 0; index < fields.Length; index++)
                {
                    var field = fields[index];
                    if (!IsScrollCandidateName(field.Name) || field.FieldType != typeof(int))
                    {
                        continue;
                    }

                    AppendCandidate(builder, "field:" + field.Name);
                }

                var properties = type.GetProperties(flags);
                for (var index = 0; index < properties.Length; index++)
                {
                    var property = properties[index];
                    if (!IsScrollCandidateName(property.Name) ||
                        property.PropertyType != typeof(int) ||
                        property.GetIndexParameters().Length != 0)
                    {
                        continue;
                    }

                    AppendCandidate(builder, "property:" + property.Name);
                }
            }
            catch (Exception error)
            {
                return "candidate-scan-failed:" + error.Message;
            }

            return builder.Length == 0 ? "<none>" : builder.ToString();
        }

        private static bool IsScrollCandidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return name.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AppendCandidate(StringBuilder builder, string value)
        {
            if (builder.Length > 0)
            {
                builder.Append(",");
            }

            builder.Append(value);
        }

        private static void LogScrollCandidateSummaryOnce()
        {
            if (_scrollCandidateSummaryLogged || !_mainMouseResolved || !_playerInputResolved)
            {
                return;
            }

            _scrollCandidateSummaryLogged = true;
            Logger.Info("TerrariaUiMouseCompat", "Scroll field candidates: " + ScrollCandidateSummary);
        }

        private static bool TrySetMember(object instance, string name, bool value)
        {
            if (instance == null)
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    field.SetValue(instance, value);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanWrite)
                {
                    property.SetValue(instance, value, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Set UI mouse member failed: " + error.Message;
            }

            return false;
        }

        private static bool TryReadBoolMember(object instance, string name, out bool value)
        {
            value = false;
            if (instance == null)
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field) && field.FieldType == typeof(bool))
                {
                    value = Convert.ToBoolean(field.GetValue(instance), CultureInfo.InvariantCulture);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) &&
                    property.CanRead &&
                    property.PropertyType == typeof(bool))
                {
                    value = Convert.ToBoolean(property.GetValue(instance, null), CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryGetStaticObject(Type type, string name, out object value)
        {
            value = null;
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    value = field.GetValue(null);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanRead)
                {
                    value = property.GetValue(null, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryGetObjectMember(object instance, string name, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    value = field.GetValue(instance);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanRead)
                {
                    value = property.GetValue(instance, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TrySetStatic(Type type, string name, bool value)
        {
            if (type == null)
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    field.SetValue(null, value);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanWrite)
                {
                    property.SetValue(null, value, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Set UI mouse static failed: " + error.Message;
            }

            return false;
        }

        private static bool TrySetStaticStringIfExists(Type type, string name, string value)
        {
            if (type == null)
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field) && field.FieldType == typeof(string))
                {
                    field.SetValue(null, value ?? string.Empty);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) &&
                    property.CanWrite &&
                    property.PropertyType == typeof(string))
                {
                    property.SetValue(null, value ?? string.Empty, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Set mouse text string failed: " + error.Message;
            }

            return false;
        }

        private static bool TrySetStaticBoolIfExists(Type type, string name, bool value)
        {
            if (type == null)
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field) && field.FieldType == typeof(bool))
                {
                    field.SetValue(null, value);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) &&
                    property.CanWrite &&
                    property.PropertyType == typeof(bool))
                {
                    property.SetValue(null, value, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Set mouse text bool failed: " + error.Message;
            }

            return false;
        }

        private static bool TryClearHoverItem(Type type, string name)
        {
            if (type == null)
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    var empty = CreateEmptyItem(field.FieldType);
                    if (empty == null && field.FieldType.IsValueType)
                    {
                        return false;
                    }

                    field.SetValue(null, empty);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanWrite)
                {
                    var empty = CreateEmptyItem(property.PropertyType);
                    if (empty == null && property.PropertyType.IsValueType)
                    {
                        return false;
                    }

                    property.SetValue(null, empty, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Clear hover item failed: " + error.Message;
            }

            return false;
        }

        private static bool TryClearHoverItem(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    var empty = CreateEmptyItem(field.FieldType);
                    if (empty == null && field.FieldType.IsValueType)
                    {
                        return false;
                    }

                    field.SetValue(instance, empty);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanWrite)
                {
                    var empty = CreateEmptyItem(property.PropertyType);
                    if (empty == null && property.PropertyType.IsValueType)
                    {
                        return false;
                    }

                    property.SetValue(instance, empty, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Clear hover item instance failed: " + error.Message;
            }

            return false;
        }

        private static bool TryGetStaticMember(Type type, string name, out object value)
        {
            value = null;
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    value = field.GetValue(null);
                    return value != null;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanRead)
                {
                    value = property.GetValue(null, null);
                    return value != null;
                }
            }
            catch (Exception error)
            {
                _mouseCaptureLastMessage = "Read static member failed: " + error.Message;
            }

            return false;
        }

        private static object CreateEmptyItem(Type itemType)
        {
            if (itemType == null || itemType == typeof(string) || itemType == typeof(bool))
            {
                return null;
            }

            try
            {
                var item = Activator.CreateInstance(itemType);
                TrySetInstanceMember(item, "type", 0);
                TrySetInstanceMember(item, "stack", 0);
                return item;
            }
            catch
            {
                try
                {
                    return itemType.IsValueType ? Activator.CreateInstance(itemType) : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        private static bool TrySetInstanceMember(object instance, string name, object value)
        {
            if (instance == null)
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    field.SetValue(instance, Convert.ChangeType(value, field.FieldType));
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanWrite)
                {
                    property.SetValue(instance, Convert.ChangeType(value, property.PropertyType), null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool FailScrollSuppress(string message)
        {
            UiScrollSuppressAvailable = false;
            _lastMainScrollSuppressed = false;
            _scrollSuppressLastMessage = message ?? string.Empty;
            LogThrottle.WarnThrottled(
                "ui-scroll-suppress-failed",
                TimeSpan.FromSeconds(10),
                "TerrariaUiMouseCompat",
                _scrollSuppressLastMessage);
            return false;
        }

        private static bool FailRead(string message)
        {
            UiMouseReadAvailable = false;
            _mouseReadLastMessage = message ?? string.Empty;
            LogThrottle.WarnThrottled(
                "ui-mouse-read-failed",
                TimeSpan.FromSeconds(10),
                "TerrariaUiMouseCompat",
                _mouseReadLastMessage);
            return false;
        }

        private static bool IsLeftButtonDownFallback()
        {
            return IsMouseButtonDownFallback(VkLeftButton);
        }

        private static bool IsMouseButtonDownFallback(int virtualKey)
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            return virtualKey > 0 && (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private sealed class BooleanMemberAccessor
        {
            public static readonly BooleanMemberAccessor Empty = new BooleanMemberAccessor(null, null);

            private readonly FieldInfo _field;
            private readonly PropertyInfo _property;

            private BooleanMemberAccessor(FieldInfo field, PropertyInfo property)
            {
                _field = field;
                _property = property;
            }

            public static BooleanMemberAccessor Resolve(Type type, string name, bool isStatic)
            {
                if (type == null || string.IsNullOrWhiteSpace(name))
                {
                    return Empty;
                }

                var flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                try
                {
                    var field = type.GetField(name, flags);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        return new BooleanMemberAccessor(field, null);
                    }

                    var property = type.GetProperty(name, flags);
                    if (property != null &&
                        property.CanWrite &&
                        property.PropertyType == typeof(bool) &&
                        property.GetIndexParameters().Length == 0)
                    {
                        return new BooleanMemberAccessor(null, property);
                    }
                }
                catch
                {
                }

                return Empty;
            }

            public bool TrySet(object instance, bool value)
            {
                try
                {
                    if (_field != null)
                    {
                        _field.SetValue(instance, value);
                        return true;
                    }

                    if (_property != null)
                    {
                        _property.SetValue(instance, value, null);
                        return true;
                    }
                }
                catch (Exception error)
                {
                    _mouseCaptureLastMessage = "Set UI mouse bool failed: " + error.Message;
                }

                return false;
            }

            public bool TryGet(object instance, out bool value)
            {
                value = false;
                try
                {
                    if (_field != null)
                    {
                        value = Convert.ToBoolean(_field.GetValue(instance), CultureInfo.InvariantCulture);
                        return true;
                    }

                    if (_property != null && _property.CanRead)
                    {
                        value = Convert.ToBoolean(_property.GetValue(instance, null), CultureInfo.InvariantCulture);
                        return true;
                    }
                }
                catch
                {
                }

                return false;
            }
        }

        private sealed class IntegerMemberAccessor
        {
            public static readonly IntegerMemberAccessor Empty = new IntegerMemberAccessor(null, null);

            private readonly FieldInfo _field;
            private readonly PropertyInfo _property;

            private IntegerMemberAccessor(FieldInfo field, PropertyInfo property)
            {
                _field = field;
                _property = property;
            }

            public static IntegerMemberAccessor Resolve(Type type, string name, bool isStatic)
            {
                if (type == null || string.IsNullOrWhiteSpace(name))
                {
                    return Empty;
                }

                var flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                try
                {
                    var field = type.GetField(name, flags);
                    if (field != null && IsSupportedIntegerType(field.FieldType))
                    {
                        return new IntegerMemberAccessor(field, null);
                    }

                    var property = type.GetProperty(name, flags);
                    if (property != null &&
                        property.CanWrite &&
                        IsSupportedIntegerType(property.PropertyType) &&
                        property.GetIndexParameters().Length == 0)
                    {
                        return new IntegerMemberAccessor(null, property);
                    }
                }
                catch
                {
                }

                return Empty;
            }

            public bool TrySet(object instance, int value)
            {
                try
                {
                    if (_field != null)
                    {
                        _field.SetValue(instance, Convert.ChangeType(value, _field.FieldType));
                        return true;
                    }

                    if (_property != null)
                    {
                        _property.SetValue(instance, Convert.ChangeType(value, _property.PropertyType), null);
                        return true;
                    }
                }
                catch (Exception error)
                {
                    _mouseCaptureLastMessage = "Set UI mouse integer failed: " + error.Message;
                }

                return false;
            }

            private static bool IsSupportedIntegerType(Type type)
            {
                return type == typeof(int) ||
                       type == typeof(short);
            }
        }

        private sealed class StringMemberAccessor
        {
            public static readonly StringMemberAccessor Empty = new StringMemberAccessor(null, null);

            private readonly FieldInfo _field;
            private readonly PropertyInfo _property;

            private StringMemberAccessor(FieldInfo field, PropertyInfo property)
            {
                _field = field;
                _property = property;
            }

            public static StringMemberAccessor Resolve(Type type, string name, bool isStatic)
            {
                if (type == null || string.IsNullOrWhiteSpace(name))
                {
                    return Empty;
                }

                var flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                try
                {
                    var field = type.GetField(name, flags);
                    if (field != null && field.FieldType == typeof(string))
                    {
                        return new StringMemberAccessor(field, null);
                    }

                    var property = type.GetProperty(name, flags);
                    if (property != null &&
                        property.CanWrite &&
                        property.PropertyType == typeof(string) &&
                        property.GetIndexParameters().Length == 0)
                    {
                        return new StringMemberAccessor(null, property);
                    }
                }
                catch
                {
                }

                return Empty;
            }

            public bool TrySet(object instance, string value)
            {
                try
                {
                    if (_field != null)
                    {
                        _field.SetValue(instance, value ?? string.Empty);
                        return true;
                    }

                    if (_property != null)
                    {
                        _property.SetValue(instance, value ?? string.Empty, null);
                        return true;
                    }
                }
                catch (Exception error)
                {
                    _mouseCaptureLastMessage = "Set UI mouse string failed: " + error.Message;
                }

                return false;
            }
        }

        private sealed class ObjectMemberAccessor
        {
            public static readonly ObjectMemberAccessor Empty = new ObjectMemberAccessor(null, null, null);

            private readonly FieldInfo _field;
            private readonly PropertyInfo _property;

            private ObjectMemberAccessor(FieldInfo field, PropertyInfo property, Type memberType)
            {
                _field = field;
                _property = property;
                MemberType = memberType;
            }

            public Type MemberType { get; private set; }

            public static ObjectMemberAccessor Resolve(Type type, string name, bool isStatic, bool requireWrite)
            {
                if (type == null || string.IsNullOrWhiteSpace(name))
                {
                    return Empty;
                }

                var flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                try
                {
                    var field = type.GetField(name, flags);
                    if (field != null)
                    {
                        return new ObjectMemberAccessor(field, null, field.FieldType);
                    }

                    var property = type.GetProperty(name, flags);
                    if (property != null &&
                        property.GetIndexParameters().Length == 0 &&
                        (!requireWrite || property.CanWrite))
                    {
                        return new ObjectMemberAccessor(null, property, property.PropertyType);
                    }
                }
                catch
                {
                }

                return Empty;
            }

            public bool TryGet(object instance, out object value)
            {
                value = null;
                try
                {
                    if (_field != null)
                    {
                        value = _field.GetValue(instance);
                        return true;
                    }

                    if (_property != null && _property.CanRead)
                    {
                        value = _property.GetValue(instance, null);
                        return true;
                    }
                }
                catch (Exception error)
                {
                    _mouseCaptureLastMessage = "Read UI mouse object failed: " + error.Message;
                }

                return false;
            }

            public bool TrySet(object instance, object value)
            {
                if (MemberType == null)
                {
                    return false;
                }

                try
                {
                    if (_field != null)
                    {
                        _field.SetValue(instance, value);
                        return true;
                    }

                    if (_property != null && _property.CanWrite)
                    {
                        _property.SetValue(instance, value, null);
                        return true;
                    }
                }
                catch (Exception error)
                {
                    _mouseCaptureLastMessage = "Set UI mouse object failed: " + error.Message;
                }

                return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }

    public sealed class UiScrollDeltaSnapshot
    {
        public int PlayerInputScrollDelta { get; set; }
        public int PlayerInputScrollDeltaForUI { get; set; }
        public int MainScrollDelta { get; set; }
        public int DiagnosticMainScrollDelta { get; set; }
        public int EffectiveScrollDelta { get; set; }
        public string CandidateSummary { get; set; }
    }

    public sealed class TerrariaUiHoverItemSnapshot
    {
        public int ItemType { get; set; }
        public int Stack { get; set; }
        public int Prefix { get; set; }
        public string Name { get; set; }
        public string Source { get; set; }
        public int Context { get; set; }
        public int Slot { get; set; }
        public ulong GameUpdateCount { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
    }

    public sealed class TerrariaUiHoverSlotSnapshot
    {
        public bool HasActiveItem { get; set; }
        public TerrariaUiHoverItemSnapshot ItemSnapshot { get; set; }
        public string Source { get; set; }
        public int Context { get; set; }
        public int Slot { get; set; }
        public ulong GameUpdateCount { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
    }

    public sealed class TerrariaUiHoverSlotReadResult
    {
        public TerrariaUiHoverSlotReadResult()
        {
            Status = string.Empty;
            Source = string.Empty;
            AgeUpdates = -1;
        }

        public bool IsFresh { get; set; }
        public bool HasSnapshot { get; set; }
        public bool HasActiveItem { get; set; }
        public string Status { get; set; }
        public string Source { get; set; }
        public int Context { get; set; }
        public int Slot { get; set; }
        public int AgeUpdates { get; set; }
    }
}
