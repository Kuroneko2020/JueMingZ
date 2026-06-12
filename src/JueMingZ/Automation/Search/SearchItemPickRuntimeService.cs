using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Search
{
    internal static class SearchItemPickRuntimeService
    {
        private const ulong PickUiHoverPendingTtlUpdates = 6;
        private static SearchItemPickPendingClick PendingClick;

        public static void UpdatePrefixGuard()
        {
            UpdateGuard("SearchItemPickRuntimeService.UpdatePrefixGuard");
        }

        public static void UpdateAfterPlayerInputGuard()
        {
            UpdateGuard("SearchItemPickRuntimeService.UpdateAfterPlayerInputGuard");
        }

        private static void UpdateGuard(string diagnosticSource)
        {
            if (!SearchItemQueryUiState.IsSelectionPending)
            {
                PendingClick = null;
                return;
            }

            try
            {
                var result = Tick(BuildCurrentInput(), BuildCurrentPorts());
                if (result != null &&
                    result.InputConsumeAttempted &&
                    !result.InputConsumed)
                {
                    LogThrottle.WarnThrottled(
                        "search-item-pick-input-consume-failed",
                        TimeSpan.FromSeconds(10),
                        "SearchItemPickRuntime",
                        "Search item pick tried to consume the target click but failed: " +
                        (result.InputConsumeMessage ?? string.Empty));
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError(diagnosticSource ?? "SearchItemPickRuntimeService.UpdateGuard", error);
                LogThrottle.ErrorThrottled(
                    "search-item-pick-runtime-error",
                    TimeSpan.FromSeconds(10),
                    "SearchItemPickRuntime",
                    "Search item pick runtime failed; exception swallowed.", error);
                FailAndRestore("选择物品失败，请点击“选择物品”重试。", "exception");
            }
        }

        internal static SearchItemPickRuntimeResult TickForTesting(
            SearchItemPickRuntimeInput input,
            SearchItemPickRuntimePorts ports)
        {
            return Tick(input, ports);
        }

        private static SearchItemPickRuntimeResult Tick(
            SearchItemPickRuntimeInput input,
            SearchItemPickRuntimePorts ports)
        {
            input = input ?? new SearchItemPickRuntimeInput();
            ports = ports ?? new SearchItemPickRuntimePorts();
            var result = new SearchItemPickRuntimeResult
            {
                SelectionStateBefore = SearchItemQueryUiState.SelectionState.ToString()
            };

            if (!SearchItemQueryUiState.IsSelectionPending)
            {
                PendingClick = null;
                return result.Skip("idle");
            }

            if (!input.IsInWorld)
            {
                PendingClick = null;
                FailAndRestore("选择物品已取消：当前不在世界中。", "notInWorld", ports);
                return result.Fail("notInWorld");
            }

            if (!input.GameInputAvailable)
            {
                return result.Skip("gameInputUnavailable");
            }

            if (!input.MouseReadAvailable)
            {
                PendingClick = null;
                FailAndRestore("选择物品失败：无法读取鼠标状态。", "mouseReadUnavailable", ports);
                return result.Fail("mouseReadUnavailable");
            }

            var state = SearchItemQueryUiState.SelectionState;
            if (state == SearchItemPickSelectionState.WaitingButtonRelease)
            {
                PendingClick = null;
                if (input.MouseLeftDown)
                {
                    return result.Skip("waitingButtonRelease");
                }

                SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(input.CurrentGameUpdateCount);
                result.SelectionArmed = true;
                result.SelectionStateAfter = SearchItemQueryUiState.SelectionState.ToString();
                return result.Skip("armedForNextLeftClick");
            }

            if (PendingClick != null)
            {
                return ContinuePendingClick(input, ports, result);
            }

            if (state != SearchItemPickSelectionState.ArmedForNextLeftClick)
            {
                PendingClick = null;
                return result.Skip("state:" + state);
            }

            if (!input.MouseLeftDown)
            {
                return result.Skip("waitingNextLeftClick");
            }

            PendingClick = SearchItemPickPendingClick.Create(input, CaptureClickContext(input, ports));

            // Resolve the cached ItemSlot proof before consuming the click: the
            // consume path must still run, but its UI-capture flags can stop
            // vanilla from producing fresh slot hover evidence for this press.
            var resolve = ResolvePendingUi(PendingClick, input.CurrentGameUpdateCount, ports);
            result.ResolveAttempted = true;
            ConsumeTargetClick(ports, result);

            if (TryCompleteResolvedSelection(resolve, ports, result))
            {
                PendingClick = null;
                return result;
            }

            if (TryFailFreshUiEmptySlot(resolve, ports, result))
            {
                PendingClick = null;
                return result;
            }

            PendingClick.LastFailureReason = NormalizeFailureReason(resolve);
            return result.Skip("pendingUiHover");
        }

        private static SearchItemPickRuntimeResult ContinuePendingClick(
            SearchItemPickRuntimeInput input,
            SearchItemPickRuntimePorts ports,
            SearchItemPickRuntimeResult result)
        {
            var pending = PendingClick;
            if (pending == null)
            {
                return result.Skip("pendingUnavailable");
            }

            var resolve = ResolvePendingUi(pending, input.CurrentGameUpdateCount, ports);
            result.ResolveAttempted = true;
            if (input.MouseLeftDown)
            {
                ConsumeTargetClick(ports, result);
            }

            if (TryCompleteResolvedSelection(resolve, ports, result))
            {
                PendingClick = null;
                return result;
            }

            if (TryFailFreshUiEmptySlot(resolve, ports, result))
            {
                PendingClick = null;
                return result;
            }

            pending.LastFailureReason = NormalizeFailureReason(resolve);
            if (!IsPendingExpired(pending, input.CurrentGameUpdateCount))
            {
                return result.Skip("pendingUiHover");
            }

            var fallback = ResolvePendingFallback(pending, input.CurrentGameUpdateCount, ports);
            if (TryCompleteResolvedSelection(fallback, ports, result))
            {
                PendingClick = null;
                return result;
            }

            var reason = NormalizeFailureReason(fallback);
            SearchItemQueryUiState.CompletePendingSelectionFailed(
                "未识别到可查询物品，请点击“选择物品”重试。",
                reason);
            RestoreSearchWindow(ports);
            PendingClick = null;
            return result.Fail(reason);
        }

        private static SearchItemPickClickContext CaptureClickContext(
            SearchItemPickRuntimeInput input,
            SearchItemPickRuntimePorts ports)
        {
            input = input ?? new SearchItemPickRuntimeInput();
            if (ports == null || ports.CaptureClickContext == null)
            {
                return SearchItemPickClickContext.Failed(
                    "click context port unavailable",
                    input.MouseX,
                    input.MouseY,
                    input.CurrentGameUpdateCount);
            }

            var context = ports.CaptureClickContext(input);
            return context ?? SearchItemPickClickContext.Failed(
                "click context unavailable",
                input.MouseX,
                input.MouseY,
                input.CurrentGameUpdateCount);
        }

        private static SearchItemPickResolveAttempt ResolvePendingUi(
            SearchItemPickPendingClick pending,
            ulong currentGameUpdateCount,
            SearchItemPickRuntimePorts ports)
        {
            return ports == null || ports.ResolvePendingUi == null
                ? SearchItemPickResolveAttempt.Failed("ui hover resolve port unavailable")
                : ports.ResolvePendingUi(pending, currentGameUpdateCount) ??
                  SearchItemPickResolveAttempt.Failed("ui hover resolve result unavailable");
        }

        private static SearchItemPickResolveAttempt ResolvePendingFallback(
            SearchItemPickPendingClick pending,
            ulong currentGameUpdateCount,
            SearchItemPickRuntimePorts ports)
        {
            return ports == null || ports.ResolvePendingFallback == null
                ? SearchItemPickResolveAttempt.Failed("pending fallback resolve port unavailable")
                : ports.ResolvePendingFallback(pending, currentGameUpdateCount) ??
                  SearchItemPickResolveAttempt.Failed("pending fallback resolve result unavailable");
        }

        private static void ConsumeTargetClick(
            SearchItemPickRuntimePorts ports,
            SearchItemPickRuntimeResult result)
        {
            if (result == null)
            {
                return;
            }

            result.InputConsumeAttempted = true;
            var consume = ports == null || ports.ConsumeMouseTriggerInput == null
                ? SearchItemPickInputConsumeResult.Failed("consume port unavailable")
                : ports.ConsumeMouseTriggerInput("MouseLeft");
            consume = consume ?? SearchItemPickInputConsumeResult.Failed("consume result unavailable");
            result.InputConsumed = consume.Succeeded;
            result.InputConsumeMessage = consume.Message ?? string.Empty;
        }

        private static bool TryCompleteResolvedSelection(
            SearchItemPickResolveAttempt resolve,
            SearchItemPickRuntimePorts ports,
            SearchItemPickRuntimeResult result)
        {
            if (resolve == null ||
                !resolve.Succeeded ||
                resolve.Result == null ||
                resolve.Result.ItemType <= 0)
            {
                return false;
            }

            result.ItemType = resolve.Result.ItemType;
            result.ResolveSource = resolve.Result.SourceSummary ?? string.Empty;
            var found = SearchItemQueryUiState.CompletePendingSelectionWithItem(
                resolve.Result.ItemType,
                resolve.Result.SourceSummary);
            RestoreSearchWindow(ports);
            result.SelectionStateAfter = SearchItemQueryUiState.SelectionState.ToString();
            if (found)
            {
                result.Resolve();
            }
            else
            {
                result.Fail("queryNotFound");
            }

            return true;
        }

        private static bool TryFailFreshUiEmptySlot(
            SearchItemPickResolveAttempt resolve,
            SearchItemPickRuntimePorts ports,
            SearchItemPickRuntimeResult result)
        {
            if (resolve == null ||
                !string.Equals(resolve.FailureReason, "uiEmptySlot", StringComparison.Ordinal))
            {
                return false;
            }

            SearchItemQueryUiState.CompletePendingSelectionFailed(
                "未识别到可查询物品，请点击“选择物品”重试。",
                "uiEmptySlot");
            RestoreSearchWindow(ports);
            result.Fail("uiEmptySlot");
            return true;
        }

        private static bool IsPendingExpired(SearchItemPickPendingClick pending, ulong currentGameUpdateCount)
        {
            if (pending == null)
            {
                return true;
            }

            if (currentGameUpdateCount < pending.StartGameUpdateCount)
            {
                currentGameUpdateCount = pending.StartGameUpdateCount;
            }

            return currentGameUpdateCount - pending.StartGameUpdateCount > PickUiHoverPendingTtlUpdates;
        }

        private static string NormalizeFailureReason(SearchItemPickResolveAttempt resolve)
        {
            if (resolve == null || string.IsNullOrWhiteSpace(resolve.FailureReason))
            {
                return "noSearchableItem";
            }

            return resolve.FailureReason;
        }

        private static SearchItemPickRuntimeInput BuildCurrentInput()
        {
            int mouseX;
            int mouseY;
            bool leftDown;
            var mouseReadAvailable = TerrariaUiMouseCompat.TryReadMouseState(out mouseX, out mouseY, out leftDown);
            ulong updateCount;
            TerrariaMainCompat.TryReadGameUpdateCount(out updateCount);
            return new SearchItemPickRuntimeInput
            {
                IsInWorld = TerrariaMainCompat.IsWorldReady,
                GameInputAvailable = TerrariaMainCompat.AllowsInputProcessing,
                MouseReadAvailable = mouseReadAvailable,
                MouseLeftDown = leftDown,
                MouseX = mouseX,
                MouseY = mouseY,
                CurrentGameUpdateCount = updateCount
            };
        }

        private static SearchItemPickRuntimePorts BuildCurrentPorts()
        {
            return new SearchItemPickRuntimePorts
            {
                CaptureClickContext = CaptureClickContext,
                ResolvePendingUi = ResolvePendingUi,
                ResolvePendingFallback = ResolvePendingFallback,
                ConsumeMouseTriggerInput = ConsumeMouseTriggerInput,
                RestoreSearchWindow = RestoreSearchWindow
            };
        }

        private static SearchItemPickClickContext CaptureClickContext(SearchItemPickRuntimeInput input)
        {
            input = input ?? new SearchItemPickRuntimeInput();
            SearchItemPickClickContext context;
            string skipReason;
            if (SearchItemPickTargetResolver.TryCaptureCurrentClickContext(
                    input.MouseX,
                    input.MouseY,
                    input.CurrentGameUpdateCount,
                    out context,
                    out skipReason))
            {
                return context;
            }

            return context ?? SearchItemPickClickContext.Failed(
                skipReason,
                input.MouseX,
                input.MouseY,
                input.CurrentGameUpdateCount);
        }

        private static SearchItemPickResolveAttempt ResolvePendingUi(
            SearchItemPickPendingClick pending,
            ulong currentGameUpdateCount)
        {
            return SearchItemPickTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount);
        }

        private static SearchItemPickResolveAttempt ResolvePendingFallback(
            SearchItemPickPendingClick pending,
            ulong currentGameUpdateCount)
        {
            SearchItemPickResolveResult result;
            string skipReason;
            if (!SearchItemPickTargetResolver.TryResolvePendingFallback(
                    pending,
                    currentGameUpdateCount,
                    out result,
                    out skipReason))
            {
                return SearchItemPickResolveAttempt.Failed(skipReason);
            }

            return SearchItemPickResolveAttempt.Success(result);
        }

        private static SearchItemPickInputConsumeResult ConsumeMouseTriggerInput(string triggerToken)
        {
            string message;
            return TerrariaUiMouseCompat.TryConsumeMouseTriggerInput(triggerToken, out message)
                ? SearchItemPickInputConsumeResult.Success(message)
                : SearchItemPickInputConsumeResult.Failed(message);
        }

        private static void RestoreSearchWindow(SearchItemPickRuntimePorts ports)
        {
            if (ports != null && ports.RestoreSearchWindow != null)
            {
                ports.RestoreSearchWindow();
                return;
            }

            RestoreSearchWindow();
        }

        private static void RestoreSearchWindow()
        {
            LegacyMainUiState.SelectPage("search");
            LegacyMainUiState.SetVisible(true);
            LegacyTextInput.ClearFocus();
        }

        private static void FailAndRestore(string message, string sourceSummary)
        {
            FailAndRestore(message, sourceSummary, null);
        }

        private static void FailAndRestore(string message, string sourceSummary, SearchItemPickRuntimePorts ports)
        {
            SearchItemQueryUiState.CompletePendingSelectionFailed(message, sourceSummary);
            RestoreSearchWindow(ports);
        }
    }

    internal sealed class SearchItemPickRuntimeInput
    {
        public bool IsInWorld { get; set; }
        public bool GameInputAvailable { get; set; }
        public bool MouseReadAvailable { get; set; }
        public bool MouseLeftDown { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public ulong CurrentGameUpdateCount { get; set; }
    }

    internal sealed class SearchItemPickPendingClick
    {
        public ulong StartGameUpdateCount { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public int UiMouseX { get; set; }
        public int UiMouseY { get; set; }
        public string CoordinateSourceSummary { get; set; }
        public string LastFailureReason { get; set; }
        public SearchItemPickClickContext ClickContext { get; set; }

        public static SearchItemPickPendingClick Create(
            SearchItemPickRuntimeInput input,
            SearchItemPickClickContext clickContext)
        {
            input = input ?? new SearchItemPickRuntimeInput();
            clickContext = clickContext ?? SearchItemPickClickContext.Failed(
                "clickContextUnavailable",
                input.MouseX,
                input.MouseY,
                input.CurrentGameUpdateCount);
            return new SearchItemPickPendingClick
            {
                StartGameUpdateCount = clickContext.Succeeded
                    ? clickContext.GameUpdateCount
                    : input.CurrentGameUpdateCount,
                MouseX = clickContext.MouseScreenX,
                MouseY = clickContext.MouseScreenY,
                UiMouseX = clickContext.UiMouseX,
                UiMouseY = clickContext.UiMouseY,
                CoordinateSourceSummary = clickContext.CoordinateSourceSummary ?? string.Empty,
                ClickContext = clickContext,
                LastFailureReason = string.Empty
            };
        }
    }

    internal sealed class SearchItemPickRuntimePorts
    {
        public Func<SearchItemPickRuntimeInput, SearchItemPickClickContext> CaptureClickContext { get; set; }
        public Func<SearchItemPickPendingClick, ulong, SearchItemPickResolveAttempt> ResolvePendingUi { get; set; }
        public Func<SearchItemPickPendingClick, ulong, SearchItemPickResolveAttempt> ResolvePendingFallback { get; set; }
        public Func<string, SearchItemPickInputConsumeResult> ConsumeMouseTriggerInput { get; set; }
        public Action RestoreSearchWindow { get; set; }
    }

    internal sealed class SearchItemPickRuntimeResult
    {
        public SearchItemPickRuntimeResult()
        {
            ResultCode = "skipped";
            SkipReason = string.Empty;
            FailureReason = string.Empty;
            InputConsumeMessage = string.Empty;
            ResolveSource = string.Empty;
            SelectionStateBefore = string.Empty;
            SelectionStateAfter = string.Empty;
        }

        public string ResultCode { get; set; }
        public string SkipReason { get; set; }
        public string FailureReason { get; set; }
        public bool SelectionArmed { get; set; }
        public bool InputConsumeAttempted { get; set; }
        public bool InputConsumed { get; set; }
        public string InputConsumeMessage { get; set; }
        public bool ResolveAttempted { get; set; }
        public int ItemType { get; set; }
        public string ResolveSource { get; set; }
        public string SelectionStateBefore { get; set; }
        public string SelectionStateAfter { get; set; }

        public SearchItemPickRuntimeResult Skip(string reason)
        {
            ResultCode = "skipped";
            SkipReason = reason ?? string.Empty;
            SelectionStateAfter = SearchItemQueryUiState.SelectionState.ToString();
            return this;
        }

        public SearchItemPickRuntimeResult Resolve()
        {
            ResultCode = "resolved";
            SelectionStateAfter = SearchItemQueryUiState.SelectionState.ToString();
            return this;
        }

        public SearchItemPickRuntimeResult Fail(string reason)
        {
            ResultCode = "failed";
            FailureReason = reason ?? string.Empty;
            SelectionStateAfter = SearchItemQueryUiState.SelectionState.ToString();
            return this;
        }
    }
}
