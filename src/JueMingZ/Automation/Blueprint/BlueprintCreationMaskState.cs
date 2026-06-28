using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintCreationMaskState
    {
        // Creation owns the in-memory mask only; capture and placement stay in
        // their dedicated services so UI selection never mutates world content.
        public const int MaxSelectedCells = 16384;

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, BlueprintCreationMaskCell> SelectedCells =
            new Dictionary<string, BlueprintCreationMaskCell>(StringComparer.Ordinal);

        private static bool _active;
        private static bool _completedPendingCapture;
        private static bool _dragging;
        private static int _dragStartX;
        private static int _dragStartY;
        private static int _dragCurrentX;
        private static int _dragCurrentY;
        private static bool _hoverTileHit;
        private static int _hoverTileX;
        private static int _hoverTileY;
        private static string _lastNotice = "创建 mask 待命。";
        private static string _lastInputOwner = string.Empty;
        private static string _lastResultCode = "idle";

        public static BlueprintCreationMaskSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                var cells = new List<BlueprintCreationMaskCell>(SelectedCells.Count);
                foreach (var cell in SelectedCells.Values)
                {
                    cells.Add(cell.Clone());
                }

                cells.Sort(CompareCell);
                return BuildSnapshotLocked(cells);
            }
        }

        public static BlueprintCreationInteractionResult BeginCreate()
        {
            lock (SyncRoot)
            {
                var preservedCount = SelectedCells.Count;
                var changed = !_active || _completedPendingCapture || _dragging;
                _active = true;
                _completedPendingCapture = false;
                _dragging = false;
                ClearHoverLocked();
                _lastInputOwner = "ui";
                _lastResultCode = preservedCount > 0 ? "creationResumed" : "creationStarted";
                _lastNotice = BuildCreationNoticeLocked();
                return BuildResultLocked(true, changed, false, true, _lastResultCode, _lastNotice);
            }
        }

        public static BlueprintCreationInteractionResult ClearSelection()
        {
            lock (SyncRoot)
            {
                var changed = SelectedCells.Count > 0 || _dragging;
                SelectedCells.Clear();
                _dragging = false;
                ClearHoverLocked();
                _completedPendingCapture = false;
                _active = true;
                _lastInputOwner = "ui";
                _lastResultCode = "selectionCleared";
                _lastNotice = changed ? "已清空蓝图创建选区。" : "蓝图创建选区已经为空。";
                return BuildResultLocked(true, changed, false, true, _lastResultCode, _lastNotice);
            }
        }

        public static BlueprintCreationInteractionResult ExitCreatePreservingSelection()
        {
            lock (SyncRoot)
            {
                var preservedCount = SelectedCells.Count;
                var changed = _active || _completedPendingCapture || _dragging;
                // This is intentionally distinct from Cancel(): repeated create
                // toggles leave the in-memory mask intact for a later re-entry.
                _active = false;
                _completedPendingCapture = false;
                _dragging = false;
                ClearHoverLocked();
                _lastInputOwner = "ui";
                _lastResultCode = "creationExited";
                _lastNotice = preservedCount > 0
                    ? "已退出蓝图创建状态；已保留 " + preservedCount.ToString(CultureInfo.InvariantCulture) + " 格选区。"
                    : "已退出蓝图创建状态；当前没有选区。";
                return BuildResultLocked(true, changed, false, false, _lastResultCode, _lastNotice);
            }
        }

        public static BlueprintCreationInteractionResult Cancel()
        {
            lock (SyncRoot)
            {
                var changed = _active || _completedPendingCapture || SelectedCells.Count > 0 || _dragging;
                SelectedCells.Clear();
                _active = false;
                _completedPendingCapture = false;
                _dragging = false;
                ClearHoverLocked();
                _lastInputOwner = "ui";
                _lastResultCode = "creationCancelled";
                _lastNotice = "已取消蓝图创建选区。";
                return BuildResultLocked(true, changed, false, false, _lastResultCode, _lastNotice);
            }
        }

        public static BlueprintCreationInteractionResult FinishCreate(bool useAfterSave)
        {
            lock (SyncRoot)
            {
                if (SelectedCells.Count <= 0)
                {
                    _lastInputOwner = "ui";
                    _lastResultCode = "emptySelection";
                    _lastNotice = "至少选择一个世界格后才能完成创建。";
                    return BuildResultLocked(false, false, false, _active, _lastResultCode, _lastNotice);
                }

                var changed = _active || _dragging || !_completedPendingCapture;
                _active = false;
                _completedPendingCapture = true;
                _dragging = false;
                ClearHoverLocked();
                _lastInputOwner = "ui";
                _lastResultCode = useAfterSave ? "creationMaskPendingUse" : "creationMaskPendingSave";
                _lastNotice = "已完成 " + SelectedCells.Count.ToString(CultureInfo.InvariantCulture) +
                              " 格 mask；等待采集世界内容并保存模板。";
                if (useAfterSave)
                {
                    _lastNotice += " 保存后将进入摆放预览。";
                }

                return BuildResultLocked(true, changed, true, false, _lastResultCode, _lastNotice);
            }
        }

        public static BlueprintCreationInteractionResult HandlePointer(BlueprintCreationPointerInput input)
        {
            input = input ?? new BlueprintCreationPointerInput();
            lock (SyncRoot)
            {
                if (!_active)
                {
                    return BuildResultLocked(true, false, false, false, "inactive", _lastNotice);
                }

                if (input.UiOwned)
                {
                    var changed = _dragging;
                    _dragging = false;
                    ClearHoverLocked();
                    _lastInputOwner = "ui";
                    _lastResultCode = "uiOwned";
                    _lastNotice = BuildCreationNoticeLocked();
                    return BuildResultLocked(true, changed, true, true, _lastResultCode, _lastNotice);
                }

                UpdateHoverLocked(input);
                if (input.LeftPressed)
                {
                    if (!input.WorldTileHit)
                    {
                        var changed = _dragging;
                        _dragging = false;
                        ClearHoverLocked();
                        _lastInputOwner = "world-outside";
                        _lastResultCode = "worldMiss";
                        _lastNotice = BuildCreationNoticeLocked();
                        return BuildResultLocked(true, changed, true, true, _lastResultCode, _lastNotice);
                    }

                    if (!IsSelectableForInputLocked(input, input.TileX, input.TileY))
                    {
                        var changed = _dragging;
                        _dragging = false;
                        _lastInputOwner = "world";
                        _lastResultCode = "tileUnavailable";
                        _lastNotice = BuildCreationNoticeLocked();
                        return BuildResultLocked(true, changed, true, true, _lastResultCode, _lastNotice);
                    }

                    _dragging = true;
                    _dragStartX = input.TileX;
                    _dragStartY = input.TileY;
                    _dragCurrentX = input.TileX;
                    _dragCurrentY = input.TileY;
                    _lastInputOwner = "world";
                    _lastResultCode = "dragStarted";
                    _lastNotice = BuildCreationNoticeLocked();
                    return BuildResultLocked(true, true, true, true, _lastResultCode, _lastNotice);
                }

                if (_dragging && input.LeftDown)
                {
                    if (input.WorldTileHit &&
                        (_dragCurrentX != input.TileX || _dragCurrentY != input.TileY))
                    {
                        _dragCurrentX = input.TileX;
                        _dragCurrentY = input.TileY;
                        _lastInputOwner = "world";
                        _lastResultCode = "dragUpdated";
                        _lastNotice = BuildCreationNoticeLocked();
                        return BuildResultLocked(true, true, true, true, _lastResultCode, _lastNotice);
                    }

                    return BuildResultLocked(true, false, true, true, "dragHeld", _lastNotice);
                }

                if (_dragging && input.LeftReleased)
                {
                    if (!input.WorldTileHit)
                    {
                        _dragging = false;
                        _lastInputOwner = "world-outside";
                        _lastResultCode = "dragCancelled";
                        _lastNotice = BuildCreationNoticeLocked();
                        return BuildResultLocked(true, true, true, true, _lastResultCode, _lastNotice);
                    }

                    _dragCurrentX = input.TileX;
                    _dragCurrentY = input.TileY;
                    var toggle = ToggleDragRectangleLocked(input);
                    _dragging = false;
                    _lastInputOwner = "world";
                    _lastResultCode = toggle.ResultCode;
                    _lastNotice = BuildCreationNoticeLocked();
                    return BuildResultLocked(toggle.Succeeded, toggle.Changed, true, true, _lastResultCode, _lastNotice);
                }

                if (input.LeftDown || input.LeftReleased)
                {
                    _lastInputOwner = input.WorldTileHit ? "world" : "world-outside";
                    _lastResultCode = "heldIgnored";
                    _lastNotice = BuildCreationNoticeLocked();
                    return BuildResultLocked(true, false, true, true, _lastResultCode, _lastNotice);
                }

                return BuildResultLocked(true, false, false, true, "idle", _lastNotice);
            }
        }

        public static string BuildUiStateJson()
        {
            var snapshot = GetSnapshot();
            var builder = new StringBuilder();
            builder.Append('{');
            builder.Append("\"active\":").Append(snapshot.Active ? "true" : "false").Append(',');
            builder.Append("\"completedPendingCapture\":").Append(snapshot.CompletedPendingCapture ? "true" : "false").Append(',');
            builder.Append("\"selectedCount\":").Append(snapshot.SelectedCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"dragging\":").Append(snapshot.Dragging ? "true" : "false").Append(',');
            builder.Append("\"hoverTileHit\":").Append(snapshot.HoverTileHit ? "true" : "false").Append(',');
            builder.Append("\"hoverTileX\":").Append(snapshot.HoverTileX.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"hoverTileY\":").Append(snapshot.HoverTileY.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"lastInputOwner\":\"").Append(EscapeJson(snapshot.LastInputOwner)).Append("\",");
            builder.Append("\"lastResultCode\":\"").Append(EscapeJson(snapshot.LastResultCode)).Append("\",");
            builder.Append("\"lastNotice\":\"").Append(EscapeJson(snapshot.LastNotice)).Append("\"");
            builder.Append('}');
            return builder.ToString();
        }

        public static void MarkCaptureSaved(string templateName, bool useAfterSave)
        {
            lock (SyncRoot)
            {
                SelectedCells.Clear();
                _active = false;
                _completedPendingCapture = false;
                _dragging = false;
                ClearHoverLocked();
                _lastInputOwner = "capture";
                _lastResultCode = "templateSaved";
                _lastNotice = "蓝图模板已保存：" + (string.IsNullOrWhiteSpace(templateName) ? BlueprintStorageConstants.DefaultTemplateName : templateName.Trim()) + "。";
                if (useAfterSave)
                {
                    _lastNotice += " 已准备进入摆放预览。";
                }
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                SelectedCells.Clear();
                _active = false;
                _completedPendingCapture = false;
                _dragging = false;
                _dragStartX = 0;
                _dragStartY = 0;
                _dragCurrentX = 0;
                _dragCurrentY = 0;
                ClearHoverLocked();
                _lastNotice = "创建 mask 待命。";
                _lastInputOwner = string.Empty;
                _lastResultCode = "idle";
            }
        }

        private static ToggleResult ToggleDragRectangleLocked(BlueprintCreationPointerInput input)
        {
            var minX = Math.Min(_dragStartX, _dragCurrentX);
            var maxX = Math.Max(_dragStartX, _dragCurrentX);
            var minY = Math.Min(_dragStartY, _dragCurrentY);
            var maxY = Math.Max(_dragStartY, _dragCurrentY);
            var width = maxX - minX + 1;
            var height = maxY - minY + 1;
            var area = (long)width * height;
            if (area > MaxSelectedCells)
            {
                return new ToggleResult
                {
                    Succeeded = false,
                    Changed = false,
                    ResultCode = "selectionTooLarge",
                    Message = "拖选区域超过 " + MaxSelectedCells.ToString(CultureInfo.InvariantCulture) + " 格，已拒绝。"
                };
            }

            var added = 0;
            var removed = 0;
            var skippedUnavailable = 0;
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var key = BuildKey(x, y);
                    if (SelectedCells.ContainsKey(key))
                    {
                        SelectedCells.Remove(key);
                        removed++;
                    }
                    else if (!IsSelectableForInputLocked(input, x, y))
                    {
                        skippedUnavailable++;
                    }
                    else if (SelectedCells.Count < MaxSelectedCells)
                    {
                        SelectedCells[key] = new BlueprintCreationMaskCell(x, y);
                        added++;
                    }
                }
            }

            var changed = added > 0 || removed > 0;
            var message = "创建 mask 更新：+" + added.ToString(CultureInfo.InvariantCulture) +
                          " / -" + removed.ToString(CultureInfo.InvariantCulture) +
                          (skippedUnavailable > 0 ? " / 不可读取 " + skippedUnavailable.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                          "，当前 " + SelectedCells.Count.ToString(CultureInfo.InvariantCulture) + " 格。";
            return new ToggleResult
            {
                Succeeded = true,
                Changed = changed,
                ResultCode = changed ? "selectionToggled" : "selectionUnchanged",
                Message = message
            };
        }

        private static BlueprintCreationMaskSnapshot BuildSnapshotLocked(List<BlueprintCreationMaskCell> cells)
        {
            var snapshot = new BlueprintCreationMaskSnapshot
            {
                Active = _active,
                CompletedPendingCapture = _completedPendingCapture,
                Dragging = _dragging,
                DragStartX = _dragStartX,
                DragStartY = _dragStartY,
                DragCurrentX = _dragCurrentX,
                DragCurrentY = _dragCurrentY,
                HoverTileHit = _hoverTileHit,
                HoverTileX = _hoverTileX,
                HoverTileY = _hoverTileY,
                LastNotice = _lastNotice,
                LastInputOwner = _lastInputOwner,
                LastResultCode = _lastResultCode,
                SelectedCells = cells ?? new List<BlueprintCreationMaskCell>()
            };
            snapshot.SelectedCount = snapshot.SelectedCells.Count;
            FillBounds(snapshot);
            return snapshot;
        }

        private static void UpdateHoverLocked(BlueprintCreationPointerInput input)
        {
            if (input == null ||
                !input.WorldTileHit ||
                !IsSelectableForInputLocked(input, input.TileX, input.TileY))
            {
                ClearHoverLocked();
                return;
            }

            _hoverTileHit = true;
            _hoverTileX = input.TileX;
            _hoverTileY = input.TileY;
        }

        private static void ClearHoverLocked()
        {
            _hoverTileHit = false;
            _hoverTileX = 0;
            _hoverTileY = 0;
        }

        private static string BuildCreationNoticeLocked()
        {
            return "长按拖动选区，复选取消选区，当前已选" + SelectedCells.Count.ToString(CultureInfo.InvariantCulture) + "格";
        }

        private static bool IsSelectableForInputLocked(BlueprintCreationPointerInput input, int tileX, int tileY)
        {
            if (SelectedCells.ContainsKey(BuildKey(tileX, tileY)))
            {
                return true;
            }

            if (input != null && input.IsSelectableTile != null)
            {
                try
                {
                    return input.IsSelectableTile(tileX, tileY);
                }
                catch
                {
                    return false;
                }
            }

            if (input != null &&
                input.ContentKnown &&
                input.WorldTileHit &&
                input.TileX == tileX &&
                input.TileY == tileY)
            {
                return true;
            }

            return true;
        }

        private static BlueprintCreationInteractionResult BuildResultLocked(
            bool succeeded,
            bool changed,
            bool shouldConsumeLeftInput,
            bool inputActive,
            string resultCode,
            string message)
        {
            var cells = new List<BlueprintCreationMaskCell>(SelectedCells.Count);
            foreach (var cell in SelectedCells.Values)
            {
                cells.Add(cell.Clone());
            }

            cells.Sort(CompareCell);
            return new BlueprintCreationInteractionResult
            {
                Succeeded = succeeded,
                Changed = changed,
                ShouldConsumeLeftInput = shouldConsumeLeftInput,
                InputActive = inputActive,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                Snapshot = BuildSnapshotLocked(cells)
            };
        }

        private static void FillBounds(BlueprintCreationMaskSnapshot snapshot)
        {
            if (snapshot == null || snapshot.SelectedCells == null || snapshot.SelectedCells.Count <= 0)
            {
                snapshot.HasBounds = false;
                return;
            }

            var minX = snapshot.SelectedCells[0].X;
            var maxX = minX;
            var minY = snapshot.SelectedCells[0].Y;
            var maxY = minY;
            for (var index = 1; index < snapshot.SelectedCells.Count; index++)
            {
                var cell = snapshot.SelectedCells[index];
                if (cell.X < minX) minX = cell.X;
                if (cell.X > maxX) maxX = cell.X;
                if (cell.Y < minY) minY = cell.Y;
                if (cell.Y > maxY) maxY = cell.Y;
            }

            snapshot.HasBounds = true;
            snapshot.MinX = minX;
            snapshot.MinY = minY;
            snapshot.MaxX = maxX;
            snapshot.MaxY = maxY;
        }

        private static int CompareCell(BlueprintCreationMaskCell left, BlueprintCreationMaskCell right)
        {
            if (left == null && right == null) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            var y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        }

        private static string BuildKey(int x, int y)
        {
            return x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class ToggleResult
        {
            public bool Succeeded { get; set; }
            public bool Changed { get; set; }
            public string ResultCode { get; set; }
            public string Message { get; set; }
        }
    }

    internal sealed class BlueprintCreationMaskCell
    {
        public BlueprintCreationMaskCell()
        {
        }

        public BlueprintCreationMaskCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }

        public BlueprintCreationMaskCell Clone()
        {
            return new BlueprintCreationMaskCell(X, Y);
        }
    }

    internal sealed class BlueprintCreationMaskSnapshot
    {
        public bool Active { get; set; }
        public bool CompletedPendingCapture { get; set; }
        public bool Dragging { get; set; }
        public int DragStartX { get; set; }
        public int DragStartY { get; set; }
        public int DragCurrentX { get; set; }
        public int DragCurrentY { get; set; }
        public bool HoverTileHit { get; set; }
        public int HoverTileX { get; set; }
        public int HoverTileY { get; set; }
        public int SelectedCount { get; set; }
        public bool HasBounds { get; set; }
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public string LastNotice { get; set; }
        public string LastInputOwner { get; set; }
        public string LastResultCode { get; set; }
        public List<BlueprintCreationMaskCell> SelectedCells { get; set; }
    }

    internal sealed class BlueprintCreationPointerInput
    {
        public bool UiOwned { get; set; }
        public bool WorldTileHit { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public bool ContentKnown { get; set; }
        public bool HasSelectableContent { get; set; }
        public Func<int, int, bool> IsSelectableTile { get; set; }
        public bool WorldLeftDown { get; set; }
        public bool PhysicalLeftDown { get; set; }
        public bool LeftDown { get; set; }
        public bool LeftPressed { get; set; }
        public bool LeftReleased { get; set; }
    }

    internal sealed class BlueprintCreationInteractionResult
    {
        public bool Succeeded { get; set; }
        public bool Changed { get; set; }
        public bool ShouldConsumeLeftInput { get; set; }
        public bool InputActive { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
        public BlueprintCreationMaskSnapshot Snapshot { get; set; }
    }
}
