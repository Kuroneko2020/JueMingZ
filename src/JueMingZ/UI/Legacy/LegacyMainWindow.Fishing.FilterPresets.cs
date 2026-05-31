using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawFishingFilterPresetNameActions(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect)
        {
            var buttonGap = GetFishingFilterActionButtonGap(rect.Width);
            var confirmWidth = Math.Max(42, Math.Min(60, rect.Width / 7));
            var cancelWidth = confirmWidth;
            var inputWidth = Math.Max(80, rect.Width - buttonGap * 2 - confirmWidth - cancelWidth);
            var inputRect = new LegacyUiRect(rect.X, rect.Y, inputWidth, rect.Height);
            DrawFishingFilterPresetNameInput(spriteBatch, area, elements, inputRect);
            var hovered = (LegacyUiElement)null;
            hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, new LegacyUiRect(inputRect.Right + buttonGap, rect.Y, confirmWidth, rect.Height), "fishing-filter-preset:save-confirm", "确认", false, true, "用该名称保存当前名单预设。") ?? hovered;
            hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, new LegacyUiRect(inputRect.Right + buttonGap + confirmWidth + buttonGap, rect.Y, cancelWidth, rect.Height), "fishing-filter-preset:save-cancel", "取消", false, true, "取消保存预设。") ?? hovered;
            return hovered;
        }

        private static void DrawFishingFilterPresetNameInput(object spriteBatch, LegacyScrollArea area, List<LegacyUiElement> elements, LegacyUiRect rect)
        {
            var hit = rect.Intersect(area.Viewport);
            LegacyUiTheme.DrawRowClipped(spriteBatch, rect, area.Viewport);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 1, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 218, 198, 128, 230);
            var text = LegacyTextInput.GetDisplayText(FishingFilterUiState.PresetNameInputId, "预设名称");
            if (string.IsNullOrEmpty(text))
            {
                text = "预设名称";
            }

            UiTextRenderer.DrawTextClipped(spriteBatch, text, rect.X + 8, rect.Y + 7, rect.Width - 16, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.64f);
            var message = LegacyTextInput.DiagnosticMessage;
            var tooltip = string.IsNullOrWhiteSpace(message)
                ? "输入预设名称。应用预设会覆盖当前名单，不会合并。"
                : message;
            elements.Add(new LegacyUiElement
            {
                Id = FishingFilterUiState.PresetNameInputId,
                Label = "预设名称",
                Kind = "blocker",
                Rect = hit.Width > 0 && hit.Height > 0 ? hit : rect,
                Enabled = true,
                TooltipLines = new[] { tooltip }
            });
        }

        private static void DrawFishingFilterPresetSaveNotice(object spriteBatch, LegacyScrollArea area, LegacyUiRect anchor)
        {
            var notice = FishingFilterUiState.PresetSaveNotice;
            if (string.IsNullOrWhiteSpace(notice))
            {
                return;
            }

            var width = Math.Max(54, Math.Min(82, anchor.Width + 18));
            var height = 20;
            var x = anchor.X + (anchor.Width - width) / 2;
            x = Math.Max(area.Viewport.X + 4, Math.Min(x, area.Viewport.Right - width - 4));
            var y = anchor.Y - height - 5;
            y = Math.Max(area.Viewport.Y + 4, y);
            var rect = new LegacyUiRect(x, y, width, height);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 4, 28, 31, 44, 226);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 1, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, FishingFilterLinkR, FishingFilterLinkG, FishingFilterLinkB, FishingFilterLinkA);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, notice, rect.X + 3, rect.Y + 1, rect.Width - 6, rect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 232, 194, 255, 0.68f);
        }

        private static LegacyUiElement DrawFishingFilterPresetList(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, AppSettings settings, string filterMode, string matchMode)
        {
            if (rect.Height <= 0 || rect.Width <= 0)
            {
                FishingFilterUiState.ClearPresetViewport();
                return null;
            }

            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
            DrawFishingFilterFloatingBorder(spriteBatch, area, rect);
            AddUiBlocker(elements, "fishing-filter-preset-list:blocker", "预设列表", rect.Intersect(area.Viewport));
            var normalizedMatch = FishingFilterMatchModes.Normalize(matchMode);
            var title = string.Equals(normalizedMatch, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase)
                ? "关键词预设"
                : "精确预设";
            UiTextRenderer.DrawTextClipped(spriteBatch, title, rect.X + 10, rect.Y + 8, rect.Width - 20, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.70f);

            var body = new LegacyUiRect(rect.X + 8, rect.Y + 30, rect.Width - 16, Math.Max(0, rect.Height - 37));
            if (body.Height <= 0)
            {
                FishingFilterUiState.ClearPresetViewport();
                return null;
            }

            var presets = BuildVisibleFishingFilterPresets(settings, filterMode, normalizedMatch);
            if (presets.Count <= 0)
            {
                FishingFilterUiState.SetPresetViewport(body, body.Height);
                UiTextRenderer.DrawTextClipped(spriteBatch, "暂无当前模式预设", body.X + 4, body.Y + 7, body.Width - 8, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 226, 218, 194, 245, 0.70f);
                UiTextRenderer.DrawTextClipped(spriteBatch, "预设按白/黑名单与精确/关键词分别保存。", body.X + 4, body.Y + 31, body.Width - 8, 24, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 196, 208, 226, 230, 0.60f);
                return null;
            }

            var contentHeight = presets.Count * FishingFilterPresetRowHeight;
            FishingFilterUiState.SetPresetViewport(body, contentHeight);
            var scrollOffset = FishingFilterUiState.PresetScrollOffset;
            var clip = body.Intersect(area.Viewport);
            var hovered = (LegacyUiElement)null;
            for (var index = 0; index < presets.Count; index++)
            {
                var preset = presets[index];
                if (preset == null || preset.Preset == null)
                {
                    continue;
                }

                var row = new LegacyUiRect(body.X, body.Y + index * FishingFilterPresetRowHeight - scrollOffset, body.Width, FishingFilterPresetRowHeight - 3);
                if (!row.Intersects(clip))
                {
                    continue;
                }

                hovered = DrawFishingFilterPresetRow(spriteBatch, mouse, elements, row, clip, preset) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawFishingFilterPresetRow(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect row, LegacyUiRect clip, FishingFilterPresetView preset)
        {
            var hit = row.Intersect(clip);
            var hovered = hit.Width > 0 && hit.Height > 0 && hit.Contains(mouse.X, mouse.Y);
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, clip);
            var deleteRect = preset.IsBuiltIn ? new LegacyUiRect(row.Right, row.Y, 0, 0) : new LegacyUiRect(row.Right - 25, row.Y + 3, 20, 20);
            var contentRight = preset.IsBuiltIn ? row.Right - 6 : deleteRect.X - 5;
            var contentRect = new LegacyUiRect(row.X + 6, row.Y + 2, Math.Max(1, contentRight - row.X - 6), row.Height - 4);
            var keywordPreset = string.Equals(FishingFilterMatchModes.Normalize(preset.Preset.MatchModeScope), FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase);
            if (keywordPreset)
            {
                var previewText = BuildFishingFilterKeywordPresetInlineText(preset.Preset);
                UiTextRenderer.DrawTextClipped(spriteBatch, previewText, contentRect.X + 2, contentRect.Y + 5, contentRect.Width - 4, 16, clip.X, clip.Y, clip.Width, clip.Height, 238, 236, 220, 248, 0.62f);
            }
            else
            {
                DrawFishingFilterExactPresetIcons(spriteBatch, preset.Preset, contentRect, clip);
            }

            var applyElement = new LegacyUiElement
            {
                Id = "fishing-filter-preset:apply:" + preset.Key,
                Label = "应用预设",
                Kind = "button",
                Rect = hit.Width > 0 && hit.Height > 0 ? (preset.IsBuiltIn ? hit : new LegacyUiRect(hit.X, hit.Y, Math.Max(0, Math.Min(hit.Width, deleteRect.X - hit.X - 2)), hit.Height)) : row,
                TooltipLines = BuildFishingFilterPresetTooltipLines(preset)
            };
            elements.Add(applyElement);
            var hoveredElement = hovered && (preset.IsBuiltIn || !deleteRect.Contains(mouse.X, mouse.Y)) ? applyElement : null;

            if (!preset.IsBuiltIn)
            {
                var deleteHovered = deleteRect.Intersect(clip).Contains(mouse.X, mouse.Y);
                LegacyUiTheme.DrawButtonClipped(spriteBatch, deleteRect, deleteHovered, deleteHovered && mouse.LeftDown, false, true, clip);
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "x", deleteRect.X + 2, deleteRect.Y + 1, deleteRect.Width - 4, deleteRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 238, 196, 180, 255, 0.74f);
                var deleteElement = new LegacyUiElement
                {
                    Id = "fishing-filter-preset:delete:" + preset.Key,
                    Label = "删除预设",
                    Kind = "button",
                    Rect = deleteRect.Intersect(clip),
                    TooltipLines = new[] { "删除这个预设。" }
                };
                elements.Add(deleteElement);
                if (deleteHovered)
                {
                    hoveredElement = deleteElement;
                }
            }

            return hoveredElement;
        }

        private static void DrawFishingFilterExactPresetIcons(object spriteBatch, FishingFilterPreset preset, LegacyUiRect rect, LegacyUiRect clip)
        {
            var entries = preset == null ? null : preset.ExactEntries;
            if (entries == null || entries.Count <= 0)
            {
                UiTextRenderer.DrawTextClipped(spriteBatch, "空预设", rect.X + 2, rect.Y + 5, rect.Width - 4, 16, clip.X, clip.Y, clip.Width, clip.Height, 202, 214, 232, 215, 0.62f);
                return;
            }

            const int iconSize = 20;
            const int iconGap = 4;
            var x = rect.X;
            var overflow = false;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || entry.Id <= 0)
                {
                    continue;
                }

                if (x + iconSize > rect.Right)
                {
                    overflow = true;
                    break;
                }

                DrawFishingFilterIcon(spriteBatch, entry.Kind, entry.Id, new LegacyUiRect(x, rect.Y + 2, iconSize, iconSize), clip);
                x += iconSize + iconGap;
            }

            if (overflow)
            {
                UiTextRenderer.DrawTextClipped(spriteBatch, "...", Math.Max(rect.X, rect.Right - 24), rect.Y + 5, 22, 16, clip.X, clip.Y, clip.Width, clip.Height, 238, 236, 220, 248, 0.62f);
            }
        }

        private static string BuildFishingFilterKeywordPresetInlineText(FishingFilterPreset preset)
        {
            var keywords = preset == null ? null : preset.Keywords;
            if (keywords == null || keywords.Count <= 0)
            {
                return "空预设";
            }

            var parts = new List<string>();
            for (var index = 0; index < keywords.Count; index++)
            {
                var keyword = string.IsNullOrWhiteSpace(keywords[index]) ? string.Empty : keywords[index].Trim();
                if (keyword.Length > 0)
                {
                    parts.Add(keyword);
                }
            }

            return parts.Count <= 0 ? "空预设" : string.Join(" | ", parts.ToArray());
        }

        private static string[] BuildFishingFilterPresetTooltipLines(FishingFilterPresetView view)
        {
            var preset = view == null ? null : view.Preset;
            var keywordPreset = preset != null &&
                string.Equals(FishingFilterMatchModes.Normalize(preset.MatchModeScope), FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase);
            var lines = new List<string>();
            lines.Add("点击应用该预设；应用会覆盖当前名单，不会合并。");
            if (view != null && view.IsBuiltIn)
            {
                lines.Add("这是内置默认预设，点击前不会写入当前黑名单。");
            }

            if (keywordPreset)
            {
                lines.Add("完整预览: " + BuildFishingFilterKeywordPresetInlineText(preset));
            }
            else
            {
                lines.Add("完整预览: " + BuildFishingFilterExactPresetPreviewText(preset));
            }

            return lines.ToArray();
        }

        private static string BuildFishingFilterExactPresetPreviewText(FishingFilterPreset preset)
        {
            var entries = preset == null ? null : preset.ExactEntries;
            if (entries == null || entries.Count <= 0)
            {
                return "空预设";
            }

            var parts = new List<string>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || entry.Id <= 0)
                {
                    continue;
                }

                parts.Add(BuildFishingFilterDisplayLabel(entry.Kind, entry.Id, entry.DisplayNameSnapshot));
            }

            return parts.Count <= 0 ? "空预设" : string.Join(" | ", parts.ToArray());
        }

        private static List<FishingFilterPresetView> BuildVisibleFishingFilterPresets(AppSettings settings, string filterMode, string matchMode)
        {
            var result = new List<FishingFilterPresetView>();
            var normalizedFilter = FishingFilterModes.Normalize(filterMode);
            var normalizedMatch = FishingFilterMatchModes.Normalize(matchMode);
            if (FishingFilterDefaultPresets.IsLowFishingPowerJunkScope(normalizedFilter, normalizedMatch))
            {
                FishingFilterPreset defaultPreset;
                if (FishingFilterDefaultPresets.TryGetLowFishingPowerJunkPreset(out defaultPreset))
                {
                    result.Add(new FishingFilterPresetView
                    {
                        Key = "default-low-junk",
                        Preset = defaultPreset,
                        SettingsIndex = -1,
                        IsBuiltIn = true
                    });
                }
            }

            var presets = settings == null ? null : settings.FishingFilterPresets;
            if (presets == null)
            {
                return result;
            }

            for (var index = 0; index < presets.Count; index++)
            {
                var preset = presets[index];
                if (preset == null ||
                    !string.Equals(FishingFilterModes.Normalize(preset.FilterModeScope), normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(FishingFilterMatchModes.Normalize(preset.MatchModeScope), normalizedMatch, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(preset.Name))
                {
                    continue;
                }

                result.Add(new FishingFilterPresetView
                {
                    Key = "saved-" + index.ToString(CultureInfo.InvariantCulture),
                    Preset = preset,
                    SettingsIndex = index,
                    IsBuiltIn = false
                });
            }

            return result;
        }
    }
}
