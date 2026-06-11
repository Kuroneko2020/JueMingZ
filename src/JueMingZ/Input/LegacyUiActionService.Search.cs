using System;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void HandleSearchQueryCommand(LegacyUiCommand command)
        {
            var before = SearchItemQueryUiState.BuildUiStateJson();
            if (command == null)
            {
                return;
            }

            if (string.Equals(command.ElementId, SearchItemQueryUiState.InputId, StringComparison.Ordinal))
            {
                LegacyTextInput.Focus(SearchItemQueryUiState.InputId, SearchItemQueryUiState.QueryText);
                RecordSearchQueryCommand(
                    command,
                    "Ui.Search.Input.Focus",
                    "Succeeded",
                    "搜索查询输入框已进入编辑状态。",
                    before,
                    "{\"action\":\"input\",\"inputActive\":true}");
                return;
            }

            if (string.Equals(command.ElementId, "search-query:clear", StringComparison.Ordinal))
            {
                SearchItemQueryUiState.Clear();
                LegacyTextInput.ClearFocus();
                RecordSearchQueryCommand(
                    command,
                    "Ui.Search.Clear",
                    "Succeeded",
                    "搜索查询已清空。",
                    before,
                    "{\"action\":\"clear\",\"inputActive\":false}");
                return;
            }

            int itemType;
            if (string.Equals(command.ElementId, SearchItemQueryUiState.PickItemButtonId, StringComparison.Ordinal))
            {
                ulong startGameUpdateCount;
                TerrariaMainCompat.TryReadGameUpdateCount(out startGameUpdateCount);
                SearchItemQueryUiState.BeginPendingSelection(startGameUpdateCount, "legacyUiButton");
                LegacyTextInput.ClearFocus();
                LegacyMainUiState.SetVisible(false);
                RecordSearchQueryCommand(
                    command,
                    "Ui.Search.PickItem.Start",
                    "Succeeded",
                    "搜索查询已进入选择物品模式。",
                    before,
                    "{\"action\":\"pickItemStart\",\"startGameUpdateCount\":" + startGameUpdateCount.ToString(CultureInfo.InvariantCulture) + ",\"waitForRelease\":true,\"windowHidden\":true}");
                return;
            }

            if (string.Equals(command.ElementId, "search-query:hover-item", StringComparison.Ordinal))
            {
                var hoverSource = SearchItemQueryUiState.GetHoverItemSource();
                var found = SearchItemQueryUiState.SelectHoverItem(out itemType);
                LegacyTextInput.ClearFocus();
                RecordSearchQueryCommand(
                    command,
                    "Ui.Search.HoverItem.Select",
                    found ? "Succeeded" : "NotApplicable",
                    found ? "最近悬停物品已切换为当前查询。" : "没有可查询的最近悬停物品。",
                    before,
                    "{\"action\":\"hoverItem\",\"itemType\":" + itemType.ToString(CultureInfo.InvariantCulture) + ",\"found\":" + BoolRaw(found) + ",\"hoverSource\":\"" + EscapeJson(hoverSource) + "\"}");
                return;
            }

            if (TryParseSearchQueryItemType(command.ElementId, "search-query:candidate:", out itemType))
            {
                var found = SearchItemQueryUiState.SelectItem(itemType);
                LegacyTextInput.ClearFocus();
                RecordSearchQueryCommand(
                    command,
                    "Ui.Search.Candidate.Select",
                    found ? "Succeeded" : "NotApplicable",
                    found ? "搜索候选已选中。" : "搜索候选未找到物品资料。",
                    before,
                    "{\"action\":\"candidate\",\"itemType\":" + itemType.ToString(CultureInfo.InvariantCulture) + ",\"found\":" + BoolRaw(found) + "}");
                return;
            }

            if (TryParseSearchQueryItemType(command.ElementId, "search-query:item:", out itemType))
            {
                var found = SearchItemQueryUiState.SelectItem(itemType);
                LegacyTextInput.ClearFocus();
                RecordSearchQueryCommand(
                    command,
                    "Ui.Search.RelatedItem.Select",
                    found ? "Succeeded" : "NotApplicable",
                    found ? "相关物品已切换为当前查询。" : "相关物品未找到资料。",
                    before,
                    "{\"action\":\"relatedItem\",\"itemType\":" + itemType.ToString(CultureInfo.InvariantCulture) + ",\"found\":" + BoolRaw(found) + "}");
                return;
            }

            RecordSearchQueryCommand(
                command,
                "Ui.Search.Unknown",
                "NotApplicable",
                "未知搜索查询命令。",
                before,
                "{\"action\":\"unknown\",\"elementId\":\"" + EscapeJson(command.ElementId) + "\"}");
        }

        private static bool TryParseSearchQueryItemType(string elementId, string prefix, out int itemType)
        {
            itemType = 0;
            if (string.IsNullOrWhiteSpace(elementId) ||
                string.IsNullOrWhiteSpace(prefix) ||
                !elementId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var payload = elementId.Substring(prefix.Length);
            var separator = payload.IndexOf(':');
            if (separator >= 0)
            {
                payload = payload.Substring(0, separator);
            }

            return int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemType) &&
                   itemType > 0;
        }

        private static void RecordSearchQueryCommand(
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
                SearchItemQueryUiState.BuildUiStateJson(),
                "{" +
                    "\"submitted\":false," +
                    "\"implemented\":true," +
                    "\"uiOnly\":true," +
                    "\"featureId\":\"" + FeatureIds.SearchMain + "\"," +
                    "\"mouseCaptured\":" + BoolRaw(command != null && command.MouseCaptured) + "," +
                    "\"metadata\":" + (string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata) +
                "}",
                "Button");
        }
    }
}
