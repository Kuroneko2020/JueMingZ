using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Movement;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const int HoverTooltipDiagnosticCadenceMs = 1000;
        private static readonly object HoverTooltipCacheSyncRoot = new object();
        private static string _hoverTooltipCacheElementId = string.Empty;
        private static string _hoverTooltipCachePageId = string.Empty;
        private static int _hoverTooltipCacheSettingsVersion;
        private static string _hoverTooltipCacheFontSignature = string.Empty;
        private static int _hoverTooltipCacheFontGeneration;
        private static int _hoverTooltipCacheContentSignature;
        private static LegacyUiTooltipModel _hoverTooltipCacheModel;
        private static string _lastHoverTooltipDiagnosticElementId = string.Empty;
        private static int _lastHoverTooltipDiagnosticContentSignature;
        private static DateTime _lastHoverTooltipDiagnosticUtc = DateTime.MinValue;
        private static long _hoverTooltipCacheHitCount;
        private static long _hoverTooltipCacheMissCount;
        private static long _hoverTooltipDiagnosticSuppressedCount;

        internal static long HoverTooltipCacheHitCount
        {
            get { return Interlocked.Read(ref _hoverTooltipCacheHitCount); }
        }

        internal static long HoverTooltipCacheMissCount
        {
            get { return Interlocked.Read(ref _hoverTooltipCacheMissCount); }
        }

        internal static long HoverTooltipDiagnosticSuppressedCount
        {
            get { return Interlocked.Read(ref _hoverTooltipDiagnosticSuppressedCount); }
        }

        private static LegacyUiElement DrawEmptyPage(object spriteBatch, LegacyScrollArea area, string pageId, LegacyMouseSnapshot mouse)
        {
            var title = LegacyTabBar.GetDisplayName(pageId);
            var y = area.Viewport.Y;
            UiTextRenderer.DrawTextClipped(spriteBatch, title, area.Viewport.X + 4, y + 4, area.Viewport.Width - 8, 24, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 244, 238, 210, 255, 0.95f);
            UiTextRenderer.DrawTextClipped(spriteBatch, "此页暂未接入功能。", area.Viewport.X + 4, y + 36, area.Viewport.Width - 8, 24, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 205, 218, 238, 255, 0.88f);
            return null;
        }

        private static int GridHeight(LegacyScrollArea area, int itemCount)
        {
            var rows = GridRows(area.Viewport.Width, itemCount);
            return rows * (LegacyPotionGrid.CellHeight + LegacyUiMetrics.GridCellGap);
        }

        private static int DualGridHeight(int paneWidth, int availableCount, int whitelistCount)
        {
            var rows = Math.Max(GridRows(paneWidth, Math.Max(1, availableCount)), GridRows(paneWidth, Math.Max(1, whitelistCount)));
            return BuffPaneHeaderHeight + rows * (LegacyPotionGrid.CellHeight + LegacyUiMetrics.GridCellGap) + LegacyUiMetrics.GridCellGap * 2;
        }

        private static int GridRows(int width, int itemCount)
        {
            var columns = GridColumns(width);
            return Math.Max(1, (Math.Max(1, itemCount) + columns - 1) / columns);
        }

        private static int GridColumns(int width)
        {
            var innerWidth = Math.Max(1, width - LegacyUiMetrics.GridPanePadding * 2);
            return Math.Max(1, (innerWidth + LegacyUiMetrics.GridCellGap) / (LegacyPotionGrid.CellWidth + LegacyUiMetrics.GridCellGap));
        }

        private static int GridRowStartX(int paneX, int paneWidth, int itemCount)
        {
            var count = Math.Max(1, itemCount);
            var gridWidth = count * LegacyPotionGrid.CellWidth + Math.Max(0, count - 1) * LegacyUiMetrics.GridCellGap;
            var innerX = paneX + LegacyUiMetrics.GridPanePadding;
            var innerWidth = Math.Max(1, paneWidth - LegacyUiMetrics.GridPanePadding * 2);
            return innerX + Math.Max(0, (innerWidth - gridWidth) / 2);
        }

        private static void AddUiBlocker(List<LegacyUiElement> elements, string id, string label, LegacyUiRect rect)
        {
            if (elements == null || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            AddFrameElement(elements, id, label, "blocker", rect);
        }

        private static void HandleClicks(List<LegacyUiElement> elements, LegacyMouseSnapshot mouse, LegacyUiRect titleRect, LegacyUiRect resizeRect)
        {
            // Click handling stops at LegacyUiCommand enqueue so Draw never invokes
            // feature services directly.
            bool blocked;
            var element = ResolveClickableElement(elements, mouse, titleRect, resizeRect, out blocked);
            if (element == null || blocked)
            {
                return;
            }

            LegacyUiInput.EnqueueClick(element, mouse, true);
        }

        private static LegacyUiElement ResolveClickableElement(List<LegacyUiElement> elements, LegacyMouseSnapshot mouse, LegacyUiRect titleRect, LegacyUiRect resizeRect, out bool blocked)
        {
            blocked = false;
            if (elements == null || mouse == null || !mouse.LeftPressed ||
                (LegacyUiInput.IsActiveInteraction() && !LegacyTextInput.IsAnyFocused))
            {
                return null;
            }

            if (titleRect.Contains(mouse.X, mouse.Y) || resizeRect.Contains(mouse.X, mouse.Y))
            {
                blocked = true;
                return null;
            }

            LegacyUiElement overlayElement;
            if (LegacyUiOverlayCoordinator.Current.TryResolveClickElement(elements, mouse, out overlayElement, out blocked))
            {
                return blocked ? null : overlayElement;
            }

            for (var index = elements.Count - 1; index >= 0; index--)
            {
                var element = elements[index];
                if (element != null && element.Enabled && element.Rect.Contains(mouse.X, mouse.Y) && !string.Equals(element.Kind, "slider", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(element.Kind, "blocker", StringComparison.OrdinalIgnoreCase))
                    {
                        blocked = true;
                        return null;
                    }

                    return element;
                }
            }

            return null;
        }

        internal static string ResolveClickableElementIdForTesting(List<LegacyUiElement> elements, LegacyMouseSnapshot mouse, out bool blocked)
        {
            var element = ResolveClickableElement(elements, mouse, new LegacyUiRect(), new LegacyUiRect(), out blocked);
            return element == null ? string.Empty : element.Id ?? string.Empty;
        }

        private static void DrawFooter(object spriteBatch, LegacyUiRect window)
        {
            const float scale = 0.62f;
            var text = "版本 " + JueMingZRuntime.Version;
            var bounds = new LegacyUiRect(window.X + 142, window.Y + 4, window.Width - 158, LegacyUiMetrics.TitleHeight - 6);
            var display = UiTextRenderer.Ellipsize(text, bounds.Width, scale);
            var textWidth = UiTextRenderer.EstimateTextWidth(display, scale);
            var x = bounds.Right - Math.Min(bounds.Width, textWidth);
            UiTextRenderer.DrawTextClipped(spriteBatch, display, x, window.Y + 10, bounds.Right - x, 18, bounds.X, bounds.Y, bounds.Width, bounds.Height, 218, 230, 244, 230, scale);
        }

        private static void RecordUiScroll(int rawScrollDelta, int before, int after, bool hotbarScrollSuppressed)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Ui.Scroll",
                "UI",
                string.Empty,
                "Succeeded",
                "Succeeded",
                "Legacy main window consumed mouse wheel.",
                0,
                "{" +
                    "\"window\":\"LegacyMainWindow\"," +
                    "\"pageId\":\"" + EscapeJson(LegacyMainUiState.SelectedPageId) + "\"," +
                    "\"scrollDelta\":" + rawScrollDelta.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"playerInputScrollDelta\":" + TerrariaUiMouseCompat.LastPlayerInputScrollDelta.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"mainScrollDelta\":0," +
                    "\"scrollOffsetBefore\":" + before.ToString(CultureInfo.InvariantCulture) +
                "}",
                "{" +
                    "\"window\":\"LegacyMainWindow\"," +
                    "\"pageId\":\"" + EscapeJson(LegacyMainUiState.SelectedPageId) + "\"," +
                    "\"scrollOffsetAfter\":" + after.ToString(CultureInfo.InvariantCulture) +
                "}",
                "{" +
                    "\"pageId\":\"" + EscapeJson(LegacyMainUiState.SelectedPageId) + "\"," +
                    "\"mouseCaptured\":true," +
                    "\"wheelConsumedThisFrame\":" + (LegacyUiInput.WheelConsumedThisFrame ? "true" : "false") + "," +
                    "\"playerInputCleared\":" + (TerrariaUiMouseCompat.LastPlayerInputCleared ? "true" : "false") + "," +
                    "\"mainScrollSuppressed\":" + (TerrariaUiMouseCompat.LastMainScrollSuppressed ? "true" : "false") + "," +
                    "\"scrollHotbarHookSuppressed\":" + (TerrariaUiMouseCompat.LastScrollHotbarHookSuppressed ? "true" : "false") + "," +
                    "\"hotbarScrollSuppressed\":" + (hotbarScrollSuppressed ? "true" : "false") +
                "}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static void DrawTooltip(object spriteBatch, LegacyUiElement element, LegacyMouseSnapshot mouse)
        {
            if (mouse == null)
            {
                return;
            }

            var model = BuildTooltipModel(element, LegacyMainUiState.SelectedPageId, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            if (model == null || model.Lines == null || model.Lines.Length <= 0)
            {
                return;
            }

            var window = LegacyMainUiState.WindowRect;
            LegacyTooltipHostControl.Draw(spriteBatch, window, mouse, model);
        }

        private static LegacyUiTooltipModel BuildTooltipModel(LegacyUiElement element, string pageId, AppSettings settings)
        {
            // Tooltip layout is cached by visible content, settings, and font generation
            // so retained frames do not reuse stale text geometry.
            if (element == null ||
                (element.Candidate == null &&
                 element.WhitelistEntry == null &&
                 (element.TooltipLines == null || element.TooltipLines.Length <= 0)))
            {
                return null;
            }

            pageId = pageId ?? string.Empty;
            settings = settings ?? AppSettings.CreateDefault();
            var elementId = element.Id ?? string.Empty;
            var settingsVersion = settings.ConfigVersion;
            var fontSignature = UiTextRenderer.FontSignatureForLayoutCache ?? string.Empty;
            var fontGeneration = UiTextRenderer.CacheGenerationForLayoutCache;
            var contentSignature = BuildTooltipContentSignature(element);

            lock (HoverTooltipCacheSyncRoot)
            {
                if (_hoverTooltipCacheModel != null &&
                    string.Equals(_hoverTooltipCacheElementId, elementId, StringComparison.Ordinal) &&
                    string.Equals(_hoverTooltipCachePageId, pageId, StringComparison.Ordinal) &&
                    _hoverTooltipCacheSettingsVersion == settingsVersion &&
                    string.Equals(_hoverTooltipCacheFontSignature, fontSignature, StringComparison.Ordinal) &&
                    _hoverTooltipCacheFontGeneration == fontGeneration &&
                    _hoverTooltipCacheContentSignature == contentSignature)
                {
                    Interlocked.Increment(ref _hoverTooltipCacheHitCount);
                    RecordHoverTooltipDiagnosticDecisionLocked(elementId, contentSignature, false);
                    return _hoverTooltipCacheModel;
                }
            }

            var lines = BuildTooltipLines(element);
            if (lines == null || lines.Length <= 0)
            {
                return null;
            }

            var centered = element.Candidate == null && element.WhitelistEntry == null;
            var model = new LegacyUiTooltipModel(CloneTooltipLines(lines), centered);
            lock (HoverTooltipCacheSyncRoot)
            {
                _hoverTooltipCacheElementId = elementId;
                _hoverTooltipCachePageId = pageId;
                _hoverTooltipCacheSettingsVersion = settingsVersion;
                _hoverTooltipCacheFontSignature = fontSignature;
                _hoverTooltipCacheFontGeneration = fontGeneration;
                _hoverTooltipCacheContentSignature = contentSignature;
                _hoverTooltipCacheModel = model;
                Interlocked.Increment(ref _hoverTooltipCacheMissCount);
                RecordHoverTooltipDiagnosticDecisionLocked(elementId, contentSignature, true);
            }

            return model;
        }

        private static void RecordHoverTooltipDiagnosticDecisionLocked(string elementId, int contentSignature, bool changed)
        {
            var now = DateTime.UtcNow;
            var sameTooltip =
                string.Equals(_lastHoverTooltipDiagnosticElementId, elementId ?? string.Empty, StringComparison.Ordinal) &&
                _lastHoverTooltipDiagnosticContentSignature == contentSignature;
            if (!changed &&
                sameTooltip &&
                now - _lastHoverTooltipDiagnosticUtc < TimeSpan.FromMilliseconds(HoverTooltipDiagnosticCadenceMs))
            {
                Interlocked.Increment(ref _hoverTooltipDiagnosticSuppressedCount);
                return;
            }

            _lastHoverTooltipDiagnosticElementId = elementId ?? string.Empty;
            _lastHoverTooltipDiagnosticContentSignature = contentSignature;
            _lastHoverTooltipDiagnosticUtc = now;
        }

        private static string[] CloneTooltipLines(string[] lines)
        {
            if (lines == null || lines.Length <= 0)
            {
                return new string[0];
            }

            var clone = new string[lines.Length];
            for (var index = 0; index < lines.Length; index++)
            {
                clone[index] = lines[index] ?? string.Empty;
            }

            return clone;
        }

        private static int BuildTooltipContentSignature(LegacyUiElement element)
        {
            if (element == null)
            {
                return 0;
            }

            if (element.TooltipContentSignature != 0)
            {
                return element.TooltipContentSignature;
            }

            unchecked
            {
                var hash = 17;
                AddHash(ref hash, element.Id);
                AddHash(ref hash, element.Label);
                AddHash(ref hash, element.Kind);
                AddHash(ref hash, element.Selected);
                AddHash(ref hash, element.IntValue);
                AddHash(ref hash, element.MinValue);
                AddHash(ref hash, element.MaxValue);
                AddTooltipLinesHash(ref hash, element.TooltipLines);
                AddCandidateTooltipHash(ref hash, element.Candidate);
                AddWhitelistTooltipHash(ref hash, element.WhitelistEntry);
                return hash;
            }
        }

        private static int BuildTooltipContentSignature(BuffPotionCandidate candidate)
        {
            unchecked
            {
                var hash = 17;
                AddCandidateTooltipHash(ref hash, candidate);
                return hash;
            }
        }

        private static int BuildTooltipContentSignature(BuffPotionWhitelistEntry entry, BuffPotionCandidate liveCandidate, bool active)
        {
            unchecked
            {
                var hash = 17;
                AddWhitelistTooltipHash(ref hash, entry);
                AddCandidateTooltipHash(ref hash, liveCandidate);
                AddHash(ref hash, active);
                return hash;
            }
        }

        private static void AddTooltipLinesHash(ref int hash, string[] lines)
        {
            AddHash(ref hash, lines == null ? 0 : lines.Length);
            if (lines == null)
            {
                return;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                AddHash(ref hash, lines[index]);
            }
        }

        private static void AddCandidateTooltipHash(ref int hash, BuffPotionCandidate candidate)
        {
            if (candidate == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddHash(ref hash, 1);
            AddHash(ref hash, candidate.SourceContainer);
            AddHash(ref hash, candidate.SourceSlot);
            AddHash(ref hash, candidate.ItemType);
            AddHash(ref hash, candidate.ItemName);
            AddHash(ref hash, candidate.Stack);
            AddHash(ref hash, candidate.BuffType);
            AddHash(ref hash, candidate.BuffName);
            AddHash(ref hash, candidate.BuffTime);
            AddHash(ref hash, candidate.EstimatedDurationSeconds);
            AddHash(ref hash, candidate.IsActive);
            AddHash(ref hash, candidate.IsWhitelisted);
            AddHash(ref hash, candidate.CanApply);
            AddHash(ref hash, candidate.SkipReason);
            AddHash(ref hash, candidate.ConflictGroup);
            AddHash(ref hash, candidate.NetworkMode);
        }

        private static void AddWhitelistTooltipHash(ref int hash, BuffPotionWhitelistEntry entry)
        {
            if (entry == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddHash(ref hash, 1);
            AddHash(ref hash, entry.ItemType);
            AddHash(ref hash, entry.BuffType);
            AddHash(ref hash, entry.ItemName);
            AddHash(ref hash, entry.BuffName);
        }

        internal static LegacyUiTooltipModel BuildTooltipModelForTesting(LegacyUiElement element, string pageId, AppSettings settings)
        {
            return BuildTooltipModel(element, pageId, settings);
        }

        internal static void ResetHoverTooltipCacheForTesting()
        {
            lock (HoverTooltipCacheSyncRoot)
            {
                _hoverTooltipCacheElementId = string.Empty;
                _hoverTooltipCachePageId = string.Empty;
                _hoverTooltipCacheSettingsVersion = 0;
                _hoverTooltipCacheFontSignature = string.Empty;
                _hoverTooltipCacheFontGeneration = 0;
                _hoverTooltipCacheContentSignature = 0;
                _hoverTooltipCacheModel = null;
                _lastHoverTooltipDiagnosticElementId = string.Empty;
                _lastHoverTooltipDiagnosticContentSignature = 0;
                _lastHoverTooltipDiagnosticUtc = DateTime.MinValue;
                Interlocked.Exchange(ref _hoverTooltipCacheHitCount, 0);
                Interlocked.Exchange(ref _hoverTooltipCacheMissCount, 0);
                Interlocked.Exchange(ref _hoverTooltipDiagnosticSuppressedCount, 0);
            }
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static string[] BuildTooltipLines(LegacyUiElement element)
        {
            if (element.TooltipLines != null && element.TooltipLines.Length > 0)
            {
                return element.TooltipLines;
            }

            if (element.Candidate != null)
            {
                var c = element.Candidate;
                return new[]
                {
                    "药水名: " + c.ItemName,
                    "Buff 名: " + c.BuffName,
                    "数量: " + c.Stack.ToString(CultureInfo.InvariantCulture),
                    "持续时间: " + FormatDuration(c.EstimatedDurationSeconds, c.BuffTime),
                    "效果: " + FirstNonEmpty(BuffPotionCatalog.ReadBuffDescriptionSafe(c.BuffType), "暂未读取到描述"),
                    "buff ID: " + c.BuffType.ToString(CultureInfo.InvariantCulture),
                    "item ID: " + c.ItemType.ToString(CultureInfo.InvariantCulture),
                    "来源: " + SourceText(c.SourceContainer) + " #" + (c.SourceSlot + 1).ToString(CultureInfo.InvariantCulture),
                    "当前已有 Buff: " + (c.IsActive ? "是" : "否"),
                    "已加入自动增益列表: " + (c.IsWhitelisted ? "是" : "否")
                };
            }

            var entry = element.WhitelistEntry;
            var live = LegacyMainUiState.FindLiveCandidate(entry.ItemType);
            var missing = live == null;
            var active = entry.BuffType > 0 && BuffPotionDiagnostics.GetCurrentActiveBuffTypes().Contains(entry.BuffType);
            return new[]
            {
                "药水名: " + entry.ItemName,
                "Buff 名: " + entry.BuffName,
                "数量: " + (live == null ? "0" : live.Stack.ToString(CultureInfo.InvariantCulture)),
                "持续时间: " + (live == null ? "未知" : FormatDuration(live.EstimatedDurationSeconds, live.BuffTime)),
                "效果: " + FirstNonEmpty(BuffPotionCatalog.ReadBuffDescriptionSafe(entry.BuffType), "暂未读取到描述"),
                "buff ID: " + entry.BuffType.ToString(CultureInfo.InvariantCulture),
                "item ID: " + entry.ItemType.ToString(CultureInfo.InvariantCulture),
                "来源: " + (live == null ? "背包/虚空袋中未找到" : SourceText(live.SourceContainer) + " #" + (live.SourceSlot + 1).ToString(CultureInfo.InvariantCulture)),
                "当前已有 Buff: " + (active ? "是" : "否"),
                "已加入自动增益列表: 是",
                missing ? "缺失: 已加入自动增益列表，但背包/虚空袋中未找到该药水。" : "缺失: 否"
            };
        }

        private static string FormatDuration(int seconds, int ticks)
        {
            if (seconds > 0)
            {
                return seconds.ToString(CultureInfo.InvariantCulture) + " 秒";
            }

            return ticks > 0 ? ticks.ToString(CultureInfo.InvariantCulture) + " tick" : "未知";
        }

        private static string SourceText(string source)
        {
            return string.Equals(source, "VoidBag", StringComparison.OrdinalIgnoreCase) ? "虚空袋" : "背包";
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        }

        private static string NormalizeInformationNpcNameLabelsMode(string mode)
        {
            if (string.Equals(mode, "Name", StringComparison.OrdinalIgnoreCase))
            {
                return "Name";
            }

            if (string.Equals(mode, "Type", StringComparison.OrdinalIgnoreCase))
            {
                return "Type";
            }

            return "Off";
        }

        private static string NormalizeInformationChestNameLabelsMode(string mode)
        {
            if (string.Equals(mode, "Always", StringComparison.OrdinalIgnoreCase))
            {
                return "Always";
            }

            if (string.Equals(mode, "Opened", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Known", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Open", StringComparison.OrdinalIgnoreCase))
            {
                return "Opened";
            }

            return "Off";
        }

        private sealed class FishingFilterPresetView
        {
            public string Key { get; set; }
            public FishingFilterPreset Preset { get; set; }
            public int SettingsIndex { get; set; }
            public bool IsBuiltIn { get; set; }
        }

        private sealed class QuickItemInventoryCandidate
        {
            public int ItemType { get; set; }
            public string ItemName { get; set; }
            public int Slot { get; set; }
            public int Stack { get; set; }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr windowHandle, out int processId);

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
    }
}
