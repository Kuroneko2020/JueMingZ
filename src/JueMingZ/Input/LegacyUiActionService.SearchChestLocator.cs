using System;
using System.Globalization;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Search.ChestLocator;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void HandleSearchChestLocatorCommand(LegacyUiCommand command)
        {
            var before = SearchChestLocatorUiState.BuildUiStateJson();
            if (command == null)
            {
                return;
            }

            if (string.Equals(command.ElementId, SearchChestLocatorUiState.InputId, StringComparison.Ordinal))
            {
                LegacyTextInput.Focus(SearchChestLocatorUiState.InputId, SearchChestLocatorUiState.QueryText);
                RecordSearchChestLocatorCommand(
                    command,
                    "Ui.SearchChestLocator.Input.Focus",
                    "Succeeded",
                    "箱内物品定位输入框已进入编辑状态。",
                    before,
                    "{\"action\":\"input\",\"inputActive\":true}");
                return;
            }

            if (string.Equals(command.ElementId, SearchChestLocatorUiState.ClearButtonId, StringComparison.Ordinal))
            {
                SearchChestLocatorUiState.Clear();
                if (LegacyTextInput.IsFocused(SearchChestLocatorUiState.InputId))
                {
                    LegacyTextInput.ClearFocus();
                }

                RecordSearchChestLocatorCommand(
                    command,
                    "Ui.SearchChestLocator.Clear",
                    "Succeeded",
                    "箱内物品定位已清空。",
                    before,
                    "{\"action\":\"clear\",\"inputActive\":false}");
                return;
            }

            int itemType;
            if (TryParseSearchQueryItemType(command.ElementId, SearchChestLocatorUiState.CandidateElementPrefix, out itemType))
            {
                SubmitSearchChestLocatorCandidate(command, before, itemType);
                return;
            }

            if (string.Equals(command.ElementId, SearchChestLocatorUiState.SubmitButtonId, StringComparison.Ordinal) ||
                string.Equals(command.ElementId, "search-chest-locator:refresh", StringComparison.Ordinal))
            {
                SubmitSearchChestLocatorQuery(command, before, "submit");
                return;
            }

            RecordSearchChestLocatorCommand(
                command,
                "Ui.SearchChestLocator.Unknown",
                "NotApplicable",
                "未知箱内物品定位命令。",
                before,
                "{\"action\":\"unknown\",\"elementId\":\"" + EscapeJson(command.ElementId) + "\"}");
        }

        private static void SubmitSearchChestLocatorCandidate(LegacyUiCommand command, string before, int itemType)
        {
            ChestItemLocatorQueryResult queryResult;
            long queryVersion;
            string message;
            if (!SearchChestLocatorUiState.SelectCandidateForSubmit(itemType, out queryResult, out queryVersion, out message))
            {
                RecordSearchChestLocatorCommand(
                    command,
                    "Ui.SearchChestLocator.Candidate.Select",
                    "NotApplicable",
                    message,
                    before,
                    "{\"action\":\"candidate\",\"itemType\":" + itemType.ToString(CultureInfo.InvariantCulture) + ",\"submitted\":false}");
                return;
            }

            SubmitSearchChestLocatorResolvedQuery(command, before, queryResult, queryVersion, "candidate", itemType);
        }

        private static void SubmitSearchChestLocatorQuery(LegacyUiCommand command, string before, string action)
        {
            ChestItemLocatorQueryResult queryResult;
            long queryVersion;
            string message;
            if (!SearchChestLocatorUiState.TryBeginSubmit(out queryResult, out queryVersion, out message))
            {
                RecordSearchChestLocatorCommand(
                    command,
                    "Ui.SearchChestLocator.Submit",
                    "NotApplicable",
                    message,
                    before,
                    "{\"action\":\"" + EscapeJson(action) + "\",\"submitted\":false}");
                return;
            }

            SubmitSearchChestLocatorResolvedQuery(command, before, queryResult, queryVersion, action, 0);
        }

        private static void SubmitSearchChestLocatorResolvedQuery(
            LegacyUiCommand command,
            string before,
            ChestItemLocatorQueryResult queryResult,
            long queryVersion,
            string action,
            int itemType)
        {
            LegacyTextInput.ClearFocus();

            InformationWorldContext context;
            string skipReason;
            if (!InformationWorldContextProvider.TryBuild(out context, out skipReason))
            {
                SearchChestLocatorUiState.ApplyContextUnavailable(queryVersion, skipReason);
                RecordSearchChestLocatorCommand(
                    command,
                    "Ui.SearchChestLocator.Submit",
                    "Skipped",
                    "箱内物品定位暂不能读取世界上下文。",
                    before,
                    BuildSearchChestLocatorMetadata(action, itemType, queryVersion, false, skipReason, null));
                return;
            }

            var sectionRequest = ChestItemLocatorSectionRequestService.TryRequestForQuery(
                context,
                ConfigService.AppSettings ?? AppSettings.CreateDefault(),
                queryVersion);
            var snapshot = ChestItemLocatorService.GetSnapshot(queryResult, context, queryVersion);
            SearchChestLocatorUiState.ApplySnapshot(queryVersion, snapshot);
            SearchChestLocatorUiState.ApplySectionRequestResult(queryVersion, sectionRequest);
            RecordSearchChestLocatorCommand(
                command,
                "Ui.SearchChestLocator.Submit",
                "Succeeded",
                "箱内物品定位已刷新附近箱子快照。",
                before,
                BuildSearchChestLocatorMetadata(action, itemType, queryVersion, true, string.Empty, snapshot, sectionRequest));
        }

        private static string BuildSearchChestLocatorMetadata(
            string action,
            int itemType,
            long queryVersion,
            bool submitted,
            string skipReason,
            ChestItemLocatorSnapshot snapshot)
        {
            return BuildSearchChestLocatorMetadata(action, itemType, queryVersion, submitted, skipReason, snapshot, null);
        }

        private static string BuildSearchChestLocatorMetadata(
            string action,
            int itemType,
            long queryVersion,
            bool submitted,
            string skipReason,
            ChestItemLocatorSnapshot snapshot,
            ChestItemLocatorSectionRequestResult sectionRequest)
        {
            return "{" +
                   "\"action\":\"" + EscapeJson(action) + "\"," +
                   "\"submitted\":" + BoolRaw(submitted) + "," +
                   "\"itemType\":" + itemType.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"queryVersion\":" + queryVersion.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"skipReason\":\"" + EscapeJson(skipReason) + "\"," +
                   "\"hitCount\":" + (snapshot == null ? "0" : snapshot.HitCount.ToString(CultureInfo.InvariantCulture)) + "," +
                   "\"sectionRequestStatus\":\"" + EscapeJson(sectionRequest == null ? string.Empty : sectionRequest.Status) + "\"," +
                   "\"sectionRequestSent\":" + BoolRaw(sectionRequest != null && sectionRequest.Sent) + "," +
                   "\"sectionRequestThrottled\":" + BoolRaw(sectionRequest != null && sectionRequest.Throttled) + "," +
                   "\"sectionKey\":\"" + EscapeJson(sectionRequest == null ? string.Empty : sectionRequest.SectionKey) + "\"," +
                   "\"sectionRequestFailureReason\":\"" + EscapeJson(sectionRequest == null ? string.Empty : sectionRequest.FailureReason) + "\"" +
                   "}";
        }

        private static void RecordSearchChestLocatorCommand(
            LegacyUiCommand command,
            string eventName,
            string status,
            string message,
            string before,
            string metadata)
        {
            Record(
                command,
                eventName,
                "UI",
                status,
                message,
                before,
                SearchChestLocatorUiState.BuildUiStateJson(),
                "{" +
                    "\"submitted\":false," +
                    "\"implemented\":true," +
                    "\"uiOnly\":true," +
                    "\"featureId\":\"" + FeatureIds.SearchChestItemLocator + "\"," +
                    "\"mouseCaptured\":" + BoolRaw(command != null && command.MouseCaptured) + "," +
                    "\"metadata\":" + (string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata) +
                "}",
                "Button");
        }
    }
}
