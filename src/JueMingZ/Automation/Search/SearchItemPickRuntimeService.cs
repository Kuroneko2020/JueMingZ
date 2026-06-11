using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Search
{
    internal static class SearchItemPickRuntimeService
    {
        public static void UpdatePrefixGuard()
        {
            if (!SearchItemQueryUiState.IsSelectionPending)
            {
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
                RuntimeDiagnostics.RecordError("SearchItemPickRuntimeService.UpdatePrefixGuard", error);
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
                return result.Skip("idle");
            }

            if (!input.IsInWorld)
            {
                FailAndRestore("选择物品已取消：当前不在世界中。", "notInWorld", ports);
                return result.Fail("notInWorld");
            }

            if (!input.GameInputAvailable)
            {
                return result.Skip("gameInputUnavailable");
            }

            if (!input.MouseReadAvailable)
            {
                FailAndRestore("选择物品失败：无法读取鼠标状态。", "mouseReadUnavailable", ports);
                return result.Fail("mouseReadUnavailable");
            }

            var state = SearchItemQueryUiState.SelectionState;
            if (state == SearchItemPickSelectionState.WaitingButtonRelease)
            {
                if (input.MouseLeftDown)
                {
                    return result.Skip("waitingButtonRelease");
                }

                SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(input.CurrentGameUpdateCount);
                result.SelectionArmed = true;
                result.SelectionStateAfter = SearchItemQueryUiState.SelectionState.ToString();
                return result.Skip("armedForNextLeftClick");
            }

            if (state != SearchItemPickSelectionState.ArmedForNextLeftClick)
            {
                return result.Skip("state:" + state);
            }

            if (!input.MouseLeftDown)
            {
                return result.Skip("waitingNextLeftClick");
            }

            result.InputConsumeAttempted = true;
            var consume = ports.ConsumeMouseTriggerInput == null
                ? SearchItemPickInputConsumeResult.Failed("consume port unavailable")
                : ports.ConsumeMouseTriggerInput("MouseLeft");
            consume = consume ?? SearchItemPickInputConsumeResult.Failed("consume result unavailable");
            result.InputConsumed = consume.Succeeded;
            result.InputConsumeMessage = consume.Message ?? string.Empty;

            var resolve = ports.ResolveCurrent == null
                ? SearchItemPickResolveAttempt.Failed("resolve port unavailable")
                : ports.ResolveCurrent();
            resolve = resolve ?? SearchItemPickResolveAttempt.Failed("resolve result unavailable");
            result.ResolveAttempted = true;
            if (!resolve.Succeeded || resolve.Result == null || resolve.Result.ItemType <= 0)
            {
                var reason = string.IsNullOrWhiteSpace(resolve.FailureReason)
                    ? "noSearchableItem"
                    : resolve.FailureReason;
                SearchItemQueryUiState.CompletePendingSelectionFailed(
                    "未识别到可查询物品，请点击“选择物品”重试。",
                    reason);
                RestoreSearchWindow(ports);
                return result.Fail(reason);
            }

            result.ItemType = resolve.Result.ItemType;
            result.ResolveSource = resolve.Result.SourceSummary ?? string.Empty;
            var found = SearchItemQueryUiState.CompletePendingSelectionWithItem(
                resolve.Result.ItemType,
                resolve.Result.SourceSummary);
            RestoreSearchWindow(ports);
            result.SelectionStateAfter = SearchItemQueryUiState.SelectionState.ToString();
            return found
                ? result.Resolve()
                : result.Fail("queryNotFound");
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
                ResolveCurrent = ResolveCurrent,
                ConsumeMouseTriggerInput = ConsumeMouseTriggerInput,
                RestoreSearchWindow = RestoreSearchWindow
            };
        }

        private static SearchItemPickResolveAttempt ResolveCurrent()
        {
            SearchItemPickResolveResult result;
            string skipReason;
            if (!SearchItemPickTargetResolver.TryResolveCurrent(out result, out skipReason))
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

    internal sealed class SearchItemPickRuntimePorts
    {
        public Func<SearchItemPickResolveAttempt> ResolveCurrent { get; set; }
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
