using System;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintMirrorModes
    {
        public const string Horizontal = "horizontal";
    }

    internal sealed class BlueprintMirrorDiagnosticsSnapshot
    {
        public BlueprintMirrorDiagnosticsSnapshot()
        {
            LastStatus = string.Empty;
            LastMessage = string.Empty;
            LastMode = string.Empty;
            LastTemplateId = string.Empty;
            LastTemplateName = string.Empty;
            LastBlockedReason = string.Empty;
        }

        public string LastStatus { get; set; }
        public string LastMessage { get; set; }
        public string LastMode { get; set; }
        public string LastTemplateId { get; set; }
        public string LastTemplateName { get; set; }
        public string LastBlockedReason { get; set; }
        public int LastMirroredCellCount { get; set; }
        public int LastMirroredLayerCount { get; set; }
        public int LastRejectedLayerCount { get; set; }
        public DateTime? LastAttemptedUtc { get; set; }

        public BlueprintMirrorDiagnosticsSnapshot Clone()
        {
            return new BlueprintMirrorDiagnosticsSnapshot
            {
                LastStatus = LastStatus ?? string.Empty,
                LastMessage = LastMessage ?? string.Empty,
                LastMode = LastMode ?? string.Empty,
                LastTemplateId = LastTemplateId ?? string.Empty,
                LastTemplateName = LastTemplateName ?? string.Empty,
                LastBlockedReason = LastBlockedReason ?? string.Empty,
                LastMirroredCellCount = LastMirroredCellCount,
                LastMirroredLayerCount = LastMirroredLayerCount,
                LastRejectedLayerCount = LastRejectedLayerCount,
                LastAttemptedUtc = LastAttemptedUtc
            };
        }
    }

    internal sealed class BlueprintMirrorResult
    {
        private BlueprintMirrorResult()
        {
            ResultCode = string.Empty;
            Message = string.Empty;
            Mode = string.Empty;
            BlockedReason = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public bool Changed { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string Mode { get; private set; }
        public string BlockedReason { get; private set; }
        public int MirroredCellCount { get; private set; }
        public int MirroredLayerCount { get; private set; }
        public int RejectedLayerCount { get; private set; }
        public BlueprintTemplateRecord Template { get; private set; }

        public static BlueprintMirrorResult Create(
            bool succeeded,
            bool changed,
            string resultCode,
            string message,
            string mode,
            string blockedReason,
            int mirroredCellCount,
            int mirroredLayerCount,
            int rejectedLayerCount,
            BlueprintTemplateRecord template)
        {
            return new BlueprintMirrorResult
            {
                Succeeded = succeeded,
                Changed = changed,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                Mode = mode ?? string.Empty,
                BlockedReason = blockedReason ?? string.Empty,
                MirroredCellCount = mirroredCellCount,
                MirroredLayerCount = mirroredLayerCount,
                RejectedLayerCount = rejectedLayerCount,
                Template = template == null ? null : template.Clone()
            };
        }
    }

    internal static class BlueprintMirrorService
    {
        private const int TileIdPlatform = 19;
        private const int TileIdClosedDoor = 10;
        private const int TileIdOpenDoor = 11;
        private const int TileIdChair = 15;
        private const int TileIdTorch = 4;
        private const int TileIdRope = 213;

        private static readonly object SyncRoot = new object();
        private static BlueprintMirrorDiagnosticsSnapshot _diagnostics = CreateIdleDiagnostics();

        public static BlueprintMirrorResult TryMirrorHorizontal(BlueprintTemplateRecord source)
        {
            var attemptedUtc = DateTime.UtcNow;
            if (!IsUsableTemplate(source))
            {
                return RecordResult(BlueprintMirrorResult.Create(
                    false,
                    false,
                    "mirrorInvalidTemplate",
                    "当前没有可镜像的蓝图模板。",
                    BlueprintMirrorModes.Horizontal,
                    "invalidTemplate",
                    0,
                    0,
                    0,
                    null), attemptedUtc, string.Empty, string.Empty);
            }

            var template = source.Clone();
            NormalizeTemplate(template);
            var target = template.Clone();
            target.AnchorX = Math.Max(0, target.Width - 1 - Clamp(template.AnchorX, 0, target.Width - 1));
            target.Cells = new List<BlueprintCellRecord>();

            var mirroredCells = new Dictionary<string, BlueprintCellRecord>(StringComparer.Ordinal);
            var mirroredLayerCount = 0;
            var rejectedLayerCount = 0;
            var firstBlockedReason = string.Empty;

            for (var cellIndex = 0; template.Cells != null && cellIndex < template.Cells.Count; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                if (cell == null)
                {
                    continue;
                }

                if (cell.X < 0 || cell.X >= template.Width || cell.Y < 0 || cell.Y >= template.Height)
                {
                    rejectedLayerCount++;
                    if (string.IsNullOrEmpty(firstBlockedReason))
                    {
                        firstBlockedReason = BuildLayerBlockedReason("cellOutOfBounds", cell, null);
                    }

                    continue;
                }

                var targetX = template.Width - 1 - cell.X;
                var key = BuildCellKey(targetX, cell.Y);
                BlueprintCellRecord mirroredCell;
                if (!mirroredCells.TryGetValue(key, out mirroredCell))
                {
                    mirroredCell = new BlueprintCellRecord
                    {
                        X = targetX,
                        Y = cell.Y
                    };
                    mirroredCells.Add(key, mirroredCell);
                }

                for (var layerIndex = 0; cell.Layers != null && layerIndex < cell.Layers.Count; layerIndex++)
                {
                    var layer = cell.Layers[layerIndex];
                    BlueprintCellLayerRecord mirroredLayer;
                    string reason;
                    if (!TryMirrorLayerHorizontal(layer, out mirroredLayer, out reason))
                    {
                        rejectedLayerCount++;
                        if (string.IsNullOrEmpty(firstBlockedReason))
                        {
                            firstBlockedReason = BuildLayerBlockedReason(reason, cell, layer);
                        }

                        continue;
                    }

                    mirroredCell.Layers.Add(mirroredLayer);
                    mirroredLayerCount++;
                }
            }

            if (rejectedLayerCount > 0)
            {
                return RecordResult(BlueprintMirrorResult.Create(
                    false,
                    false,
                    "mirrorBlocked",
                    "蓝图包含当前不可安全镜像的方向或 frame 内容：" + firstBlockedReason,
                    BlueprintMirrorModes.Horizontal,
                    firstBlockedReason,
                    0,
                    mirroredLayerCount,
                    rejectedLayerCount,
                    null), attemptedUtc, template.TemplateId, GetTemplateName(template));
            }

            foreach (var pair in mirroredCells)
            {
                if (pair.Value != null && pair.Value.Layers != null && pair.Value.Layers.Count > 0)
                {
                    target.Cells.Add(pair.Value);
                }
            }

            target.Cells.Sort(CompareCellsByPosition);
            target.UpdatedUtc = BlueprintStorageConstants.FormatUtc(attemptedUtc);
            return RecordResult(BlueprintMirrorResult.Create(
                true,
                true,
                "mirrorHorizontalApplied",
                "已在摆放预览中水平镜像模板；未知方向内容仍会禁止镜像。",
                BlueprintMirrorModes.Horizontal,
                string.Empty,
                target.Cells.Count,
                mirroredLayerCount,
                0,
                target), attemptedUtc, template.TemplateId, GetTemplateName(template));
        }

        public static BlueprintMirrorResult RecordSkipped(string resultCode, string message, string blockedReason)
        {
            return RecordResult(BlueprintMirrorResult.Create(
                false,
                false,
                string.IsNullOrWhiteSpace(resultCode) ? "mirrorSkipped" : resultCode,
                string.IsNullOrWhiteSpace(message) ? "蓝图镜像未执行。" : message,
                BlueprintMirrorModes.Horizontal,
                string.IsNullOrWhiteSpace(blockedReason) ? "skipped" : blockedReason,
                0,
                0,
                0,
                null), DateTime.UtcNow, string.Empty, string.Empty);
        }

        public static BlueprintMirrorDiagnosticsSnapshot GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        public static string BuildUiStateJson()
        {
            var snapshot = GetDiagnostics();
            return "{" +
                   "\"lastStatus\":\"" + EscapeJson(snapshot.LastStatus) + "\"," +
                   "\"lastMessage\":\"" + EscapeJson(snapshot.LastMessage) + "\"," +
                   "\"lastMode\":\"" + EscapeJson(snapshot.LastMode) + "\"," +
                   "\"lastTemplateId\":\"" + EscapeJson(snapshot.LastTemplateId) + "\"," +
                   "\"lastTemplateName\":\"" + EscapeJson(snapshot.LastTemplateName) + "\"," +
                   "\"lastBlockedReason\":\"" + EscapeJson(snapshot.LastBlockedReason) + "\"," +
                   "\"lastMirroredCellCount\":" + IntRaw(snapshot.LastMirroredCellCount) + "," +
                   "\"lastMirroredLayerCount\":" + IntRaw(snapshot.LastMirroredLayerCount) + "," +
                   "\"lastRejectedLayerCount\":" + IntRaw(snapshot.LastRejectedLayerCount) +
                   "}";
        }

        public static int BuildStateSignature()
        {
            var snapshot = GetDiagnostics();
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.LastStatus ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.LastMode ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.LastTemplateId ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.LastBlockedReason ?? string.Empty);
                hash = hash * 31 + snapshot.LastMirroredCellCount;
                hash = hash * 31 + snapshot.LastMirroredLayerCount;
                hash = hash * 31 + snapshot.LastRejectedLayerCount;
                return hash;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _diagnostics = CreateIdleDiagnostics();
            }
        }

        internal static int MirrorSlopeForTesting(int slope)
        {
            int mirrored;
            return TryMirrorSlope(slope, out mirrored) ? mirrored : -1;
        }

        internal static bool CanMirrorLayerForTesting(BlueprintCellLayerRecord layer, out string reason)
        {
            BlueprintCellLayerRecord ignored;
            return TryMirrorLayerHorizontal(layer, out ignored, out reason);
        }

        private static BlueprintMirrorResult RecordResult(
            BlueprintMirrorResult result,
            DateTime attemptedUtc,
            string templateId,
            string templateName)
        {
            result = result ?? BlueprintMirrorResult.Create(false, false, "mirrorUnknown", "蓝图镜像结果未知。", BlueprintMirrorModes.Horizontal, "unknown", 0, 0, 0, null);
            lock (SyncRoot)
            {
                _diagnostics = new BlueprintMirrorDiagnosticsSnapshot
                {
                    LastStatus = result.ResultCode,
                    LastMessage = result.Message,
                    LastMode = result.Mode,
                    LastTemplateId = templateId ?? string.Empty,
                    LastTemplateName = templateName ?? string.Empty,
                    LastBlockedReason = result.BlockedReason,
                    LastMirroredCellCount = result.MirroredCellCount,
                    LastMirroredLayerCount = result.MirroredLayerCount,
                    LastRejectedLayerCount = result.RejectedLayerCount,
                    LastAttemptedUtc = attemptedUtc
                };
            }

            return result;
        }

        private static bool TryMirrorLayerHorizontal(
            BlueprintCellLayerRecord layer,
            out BlueprintCellLayerRecord mirrored,
            out string reason)
        {
            mirrored = null;
            reason = string.Empty;
            if (layer == null)
            {
                reason = "nullLayer";
                return false;
            }

            var kind = layer.LayerKind ?? string.Empty;
            if (string.Equals(kind, BlueprintLayerKinds.Tile, StringComparison.OrdinalIgnoreCase))
            {
                if (!CanMirrorTileLayer(layer, out reason))
                {
                    return false;
                }

                mirrored = layer.Clone();
                int mirroredSlope;
                if (!TryMirrorSlope(layer.Slope, out mirroredSlope))
                {
                    reason = "tileSlopeUnsupported";
                    return false;
                }

                mirrored.Slope = mirroredSlope;
                return true;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, BlueprintLayerKinds.PaintCoating, StringComparison.OrdinalIgnoreCase))
            {
                mirrored = layer.Clone();
                return true;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase))
            {
                if (!CanMirrorObjectLayer(layer, out reason))
                {
                    return false;
                }

                mirrored = layer.Clone();
                return true;
            }

            reason = "unknownLayerKind";
            return false;
        }

        private static bool CanMirrorTileLayer(BlueprintCellLayerRecord layer, out string reason)
        {
            reason = string.Empty;
            if (layer.ContentId <= 0)
            {
                reason = "tileContentUnavailable";
                return false;
            }

            int mirroredSlope;
            if (!TryMirrorSlope(layer.Slope, out mirroredSlope))
            {
                reason = "tileSlopeUnsupported";
                return false;
            }

            if (layer.ContentId != TileIdPlatform && (layer.FrameX != 0 || layer.FrameY != 0))
            {
                reason = "tileFrameUnsupported";
                return false;
            }

            return true;
        }

        private static bool CanMirrorObjectLayer(BlueprintCellLayerRecord layer, out string reason)
        {
            reason = string.Empty;
            if (layer.ContentId == TileIdRope)
            {
                return true;
            }

            if (layer.FrameX != 0 || layer.FrameY != 0)
            {
                reason = "objectDirectionFrameUnsupported";
                return false;
            }

            if (IsCommonDirectionalObject(layer.ContentId))
            {
                reason = "objectDirectionUnsupported";
                return false;
            }

            reason = "objectUnsupportedByMirrorMatrix";
            return false;
        }

        private static bool IsCommonDirectionalObject(int tileId)
        {
            return tileId == TileIdClosedDoor ||
                   tileId == TileIdOpenDoor ||
                   tileId == TileIdChair ||
                   tileId == TileIdTorch;
        }

        private static bool TryMirrorSlope(int slope, out int mirrored)
        {
            // Terraria slope values pair horizontally as bottom-left/bottom-right and top-left/top-right.
            switch (slope)
            {
                case 0:
                    mirrored = 0;
                    return true;
                case 1:
                    mirrored = 2;
                    return true;
                case 2:
                    mirrored = 1;
                    return true;
                case 3:
                    mirrored = 4;
                    return true;
                case 4:
                    mirrored = 3;
                    return true;
                default:
                    mirrored = 0;
                    return false;
            }
        }

        private static bool IsUsableTemplate(BlueprintTemplateRecord template)
        {
            return template != null &&
                   !string.IsNullOrWhiteSpace(template.TemplateId) &&
                   Math.Max(0, template.Width) > 0 &&
                   Math.Max(0, template.Height) > 0;
        }

        private static void NormalizeTemplate(BlueprintTemplateRecord template)
        {
            if (template == null)
            {
                return;
            }

            template.Width = Math.Max(1, template.Width);
            template.Height = Math.Max(1, template.Height);
            template.AnchorX = Clamp(template.AnchorX, 0, Math.Max(0, template.Width - 1));
            template.AnchorY = Clamp(template.AnchorY, 0, Math.Max(0, template.Height - 1));
        }

        private static int CompareCellsByPosition(BlueprintCellRecord left, BlueprintCellRecord right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        }

        private static string BuildCellKey(int x, int y)
        {
            return x.ToString(CultureInfo.InvariantCulture) + ":" + y.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildLayerBlockedReason(string reason, BlueprintCellRecord cell, BlueprintCellLayerRecord layer)
        {
            var cellPart = cell == null
                ? "unknown"
                : cell.X.ToString(CultureInfo.InvariantCulture) + "," + cell.Y.ToString(CultureInfo.InvariantCulture);
            var layerPart = layer == null
                ? string.Empty
                : ":" + (layer.LayerKind ?? string.Empty) + "#" + layer.ContentId.ToString(CultureInfo.InvariantCulture);
            return (string.IsNullOrWhiteSpace(reason) ? "unknown" : reason) + "@" + cellPart + layerPart;
        }

        private static string GetTemplateName(BlueprintTemplateRecord template)
        {
            return template == null || string.IsNullOrWhiteSpace(template.Name)
                ? BlueprintStorageConstants.DefaultTemplateName
                : template.Name.Trim();
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static BlueprintMirrorDiagnosticsSnapshot CreateIdleDiagnostics()
        {
            return new BlueprintMirrorDiagnosticsSnapshot
            {
                LastStatus = "idle",
                LastMessage = "蓝图镜像待命。",
                LastMode = BlueprintMirrorModes.Horizontal
            };
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
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
                .Replace("\n", "\\n");
        }
    }
}
