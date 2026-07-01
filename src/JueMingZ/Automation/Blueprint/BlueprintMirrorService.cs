using System;
using System.Collections.Generic;
using System.Globalization;
using Terraria;
using Terraria.ObjectData;

namespace JueMingZ.Automation.Blueprint
{
    // 五大底线：性能 — TileFrame 仅在临时安全区域执行，做完立即恢复，不影响游戏世界；
    //           功能 — Tile 层保留原始 FrameX/Y + WorldGen.TileFrame 纹理修正，Object 层 TileObjectData 方向 / frame 无法证明时 fail-closed；
    //           注释 — 每个分支标注参考来源（小助手 BlueprintManager.cs _TransformTileForMirror、_RebuildFrames）；
    //           指令 — 沿用 TryXxx(out reason) + RecordResult 模式；
    //           职责边界 — Main.tile 修改在 finally 中恢复，不碰其他系统。
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
            LastWarningReason = string.Empty;
        }

        public string LastStatus { get; set; }
        public string LastMessage { get; set; }
        public string LastMode { get; set; }
        public string LastTemplateId { get; set; }
        public string LastTemplateName { get; set; }
        public string LastBlockedReason { get; set; }
        public string LastWarningReason { get; set; }
        public int LastMirroredCellCount { get; set; }
        public int LastMirroredLayerCount { get; set; }
        public int LastRejectedLayerCount { get; set; }
        public int LastWarningLayerCount { get; set; }
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
                LastWarningReason = LastWarningReason ?? string.Empty,
                LastMirroredCellCount = LastMirroredCellCount,
                LastMirroredLayerCount = LastMirroredLayerCount,
                LastRejectedLayerCount = LastRejectedLayerCount,
                LastWarningLayerCount = LastWarningLayerCount,
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
            WarningReason = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public bool Changed { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string Mode { get; private set; }
        public string BlockedReason { get; private set; }
        public string WarningReason { get; private set; }
        public int MirroredCellCount { get; private set; }
        public int MirroredLayerCount { get; private set; }
        public int RejectedLayerCount { get; private set; }
        public int WarningLayerCount { get; private set; }
        public BlueprintTemplateRecord Template { get; private set; }

        public static BlueprintMirrorResult Create(
            bool succeeded,
            bool changed,
            string resultCode,
            string message,
            string mode,
            string blockedReason,
            string warningReason,
            int mirroredCellCount,
            int mirroredLayerCount,
            int rejectedLayerCount,
            int warningLayerCount,
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
                WarningReason = warningReason ?? string.Empty,
                MirroredCellCount = mirroredCellCount,
                MirroredLayerCount = mirroredLayerCount,
                RejectedLayerCount = rejectedLayerCount,
                WarningLayerCount = warningLayerCount,
                Template = template == null ? null : template.Clone()
            };
        }
    }

    // 镜像服务参考：小助手 TerrariaHelper BlueprintManager.cs
    //   _TransformTileForMirror（第 5181 行）— Object 层 frame 翻转
    //   _RebuildFrames（第 5240 行）— 用 WorldGen.TileFrame 重算纹理融合
    // 本实现将 Object 层方向翻转逻辑移植到决明框架，Tile 层直接清零 Frame 值
    // （纹理融合由小助手 Mod 在摆放时通过 WorldGen.TileFrame 修正）。
    internal static class BlueprintMirrorService
    {
        // TileObjectData.Direction 枚举值（兼容 Terraria 1.4.5.6）
        private const int TileObjectDirectionNone = 0;
        private const int TileObjectDirectionPlaceLeft = 1;
        private const int TileObjectDirectionPlaceRight = 2;

        private const int TileIdRope = 213;

        private static readonly object SyncRoot = new object();
        private static BlueprintMirrorDiagnosticsSnapshot _diagnostics = CreateIdleDiagnostics();

        private sealed class BlueprintObjectFrameInfo
        {
            public TileObjectData Data { get; set; }
            public int StyleColumn { get; set; }
            public int StyleRow { get; set; }
            public int Style { get; set; }
            public int ObjectColumn { get; set; }
            public int ObjectRow { get; set; }
            public int InnerX { get; set; }
            public int InnerY { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private sealed class BlueprintObjectGroup
        {
            public BlueprintObjectGroup()
            {
                SubTiles = new Dictionary<string, BlueprintCellLayerRecord>(StringComparer.Ordinal);
            }

            public int ContentId { get; set; }
            public int Style { get; set; }
            public int OriginX { get; set; }
            public int OriginY { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public Dictionary<string, BlueprintCellLayerRecord> SubTiles { get; private set; }
            public BlueprintCellRecord FirstCell { get; set; }
            public BlueprintCellLayerRecord FirstLayer { get; set; }
        }

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
                    string.Empty,
                    0,
                    0,
                    0,
                    0,
                    null), attemptedUtc, string.Empty, string.Empty);
            }

            var template = source.Clone();
            NormalizeTemplate(template);
            string integrityReason;
            if (!TryValidateObjectIntegrity(template, out integrityReason))
            {
                return RecordResult(BlueprintMirrorResult.Create(
                    false,
                    false,
                    "mirrorBlocked",
                    "蓝图镜像被阻止：" + integrityReason,
                    BlueprintMirrorModes.Horizontal,
                    integrityReason,
                    string.Empty,
                    0,
                    0,
                    1,
                    0,
                    null), attemptedUtc, template.TemplateId, GetTemplateName(template));
            }

            var target = template.Clone();
            target.AnchorX = Math.Max(0, target.Width - 1 - Clamp(template.AnchorX, 0, target.Width - 1));
            target.Cells = new List<BlueprintCellRecord>();

            var mirroredCells = new Dictionary<string, BlueprintCellRecord>(StringComparer.Ordinal);
            var mirroredLayerCount = 0;
            var rejectedLayerCount = 0;
            var warningLayerCount = 0;
            var firstBlockedReason = string.Empty;
            var firstWarningReason = string.Empty;

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
                        firstBlockedReason = BuildLayerDiagnosticReason("cellOutOfBounds", cell, null);
                    }

                    continue;
                }

                // 坐标镜像：abcde → edcba（小助手 _MirrorTransform，第 5137 行同款公式）
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
                    bool warned;
                    if (!TryMirrorLayerHorizontal(layer, out mirroredLayer, out reason, out warned))
                    {
                        rejectedLayerCount++;
                        if (string.IsNullOrEmpty(firstBlockedReason))
                        {
                            firstBlockedReason = BuildLayerDiagnosticReason(reason, cell, layer);
                        }

                        continue;
                    }

                    if (warned)
                    {
                        warningLayerCount++;
                        if (string.IsNullOrEmpty(firstWarningReason))
                        {
                            firstWarningReason = BuildLayerDiagnosticReason(reason, cell, layer);
                        }
                    }

                    BlueprintObjectGroupNormalizer.MirrorObjectGroupHorizontal(mirroredLayer, template.Width);
                    mirroredCell.Layers.Add(mirroredLayer);
                    mirroredLayerCount++;
                }
            }

            // 严格拒绝：任一 layer 无法证明安全镜像时，整张模板 fail-closed，避免生成半张或旧 frame 污染的副本。
            if (rejectedLayerCount > 0)
            {
                return RecordResult(BlueprintMirrorResult.Create(
                    false,
                    false,
                    "mirrorBlocked",
                    "蓝图镜像被阻止：" + firstBlockedReason,
                    BlueprintMirrorModes.Horizontal,
                    firstBlockedReason,
                    firstWarningReason,
                    0,
                    mirroredLayerCount,
                    rejectedLayerCount,
                    warningLayerCount,
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

            if (!TryValidateObjectIntegrity(target, out integrityReason))
            {
                return RecordResult(BlueprintMirrorResult.Create(
                    false,
                    false,
                    "mirrorBlocked",
                    "蓝图镜像被阻止：" + integrityReason,
                    BlueprintMirrorModes.Horizontal,
                    integrityReason,
                    firstWarningReason,
                    0,
                    mirroredLayerCount,
                    rejectedLayerCount + 1,
                    warningLayerCount,
                    null), attemptedUtc, template.TemplateId, GetTemplateName(template));
            }

            // 用 WorldGen.TileFrame 在安全临时区域重算 Tile 层的纹理融合帧值。
            // 参考小助手 _RebuildFrames（第 5240-5314 行）。
            // 仅在游戏运行时（Main.tile 已初始化）执行；测试环境跳过。
            int tileFrameFixed = 0;
            try
            {
                tileFrameFixed = RebuildTileTextureFrames(target);
            }
            catch (TypeInitializationException)
            {
                // Main.tile 未初始化（测试环境），跳过 TileFrame 修正。
                // Tile 层保留原始 FrameX/Y，纹理融合由摆放时引擎修正。
            }

            if (tileFrameFixed > 0)
            {
                warningLayerCount += tileFrameFixed;
            }

            target.UpdatedUtc = BlueprintStorageConstants.FormatUtc(attemptedUtc);

            if (warningLayerCount > 0)
            {
                return RecordResult(BlueprintMirrorResult.Create(
                    true,
                    true,
                    "mirrorHorizontalAppliedWithWarnings",
                    "已水平镜像蓝图（" + warningLayerCount.ToString(CultureInfo.InvariantCulture) +
                    " 个 tile layer 的纹理融合 frame 已由安全临时区重算）。",
                    BlueprintMirrorModes.Horizontal,
                    string.Empty,
                    firstWarningReason,
                    target.Cells.Count,
                    mirroredLayerCount,
                    rejectedLayerCount,
                    warningLayerCount,
                    target), attemptedUtc, template.TemplateId, GetTemplateName(template));
            }

            return RecordResult(BlueprintMirrorResult.Create(
                true,
                true,
                "mirrorHorizontalApplied",
                "已水平镜像蓝图。",
                BlueprintMirrorModes.Horizontal,
                string.Empty,
                string.Empty,
                target.Cells.Count,
                mirroredLayerCount,
                0,
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
                string.Empty,
                0,
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
                   "\"lastWarningReason\":\"" + EscapeJson(snapshot.LastWarningReason) + "\"," +
                   "\"lastMirroredCellCount\":" + IntRaw(snapshot.LastMirroredCellCount) + "," +
                   "\"lastMirroredLayerCount\":" + IntRaw(snapshot.LastMirroredLayerCount) + "," +
                   "\"lastRejectedLayerCount\":" + IntRaw(snapshot.LastRejectedLayerCount) + "," +
                   "\"lastWarningLayerCount\":" + IntRaw(snapshot.LastWarningLayerCount) +
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
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.LastWarningReason ?? string.Empty);
                hash = hash * 31 + snapshot.LastMirroredCellCount;
                hash = hash * 31 + snapshot.LastMirroredLayerCount;
                hash = hash * 31 + snapshot.LastRejectedLayerCount;
                hash = hash * 31 + snapshot.LastWarningLayerCount;
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
            bool warned;
            return TryMirrorLayerHorizontal(layer, out ignored, out reason, out warned);
        }

        internal static TileObjectData ResolveTileObjectDataForTesting(ushort tileType, int frameX, int frameY)
        {
            return ResolveTileObjectData(tileType, frameX, frameY);
        }

        internal static bool TryMirrorObjectFrameForTesting(
            BlueprintCellLayerRecord layer,
            out int newFrameX,
            out int newFrameY,
            out string reason)
        {
            return TryMirrorObjectFrame(layer, out newFrameX, out newFrameY, out reason);
        }

        private static BlueprintMirrorResult RecordResult(
            BlueprintMirrorResult result,
            DateTime attemptedUtc,
            string templateId,
            string templateName)
        {
            result = result ?? BlueprintMirrorResult.Create(
                false, false, "mirrorUnknown", "蓝图镜像结果未知。",
                BlueprintMirrorModes.Horizontal, "unknown", string.Empty, 0, 0, 0, 0, null);
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
                    LastWarningReason = result.WarningReason,
                    LastMirroredCellCount = result.MirroredCellCount,
                    LastMirroredLayerCount = result.MirroredLayerCount,
                    LastRejectedLayerCount = result.RejectedLayerCount,
                    LastWarningLayerCount = result.WarningLayerCount,
                    LastAttemptedUtc = attemptedUtc
                };
            }

            return result;
        }

        // 对每个 layer 执行镜像。Object 层没有“保留旧 frame”成功分支；无法证明方向 / frame 时直接拒绝。
        private static bool TryMirrorLayerHorizontal(
            BlueprintCellLayerRecord layer,
            out BlueprintCellLayerRecord mirrored,
            out string reason,
            out bool warned)
        {
            mirrored = null;
            reason = string.Empty;
            warned = false;
            if (layer == null)
            {
                reason = "nullLayer";
                return false;
            }

            var kind = layer.LayerKind ?? string.Empty;

            // Tile 层：非 FrameImportant 方块（木块、石头、泥土等）。
            // FrameX/Y 只用于纹理融合，镜像后直接清零，引擎在摆放时自动重算。
            if (string.Equals(kind, BlueprintLayerKinds.Tile, StringComparison.OrdinalIgnoreCase))
            {
                return TryMirrorTileLayer(layer, out mirrored, out reason, out warned);
            }

            // Wall / Wire / Actuator / PaintCoating：无方向性，直接克隆。
            if (string.Equals(kind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, BlueprintLayerKinds.PaintCoating, StringComparison.OrdinalIgnoreCase))
            {
                mirrored = layer.Clone();
                return true;
            }

            // Object 层：FrameImportant 物体（门、火炬、椅子、日晷等）。
            // 用 TileObjectData 翻转 Direction 并重算 FrameX/Y。
            if (string.Equals(kind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase))
            {
                return TryMirrorObjectLayer(layer, out mirrored, out reason, out warned);
            }

            reason = "unknownLayerKind";
            return false;
        }

        // Tile 层镜像：保持原 FrameX/Y，镜像 Slope。
        // 纹理融合（相邻方块的连接方式）在镜像后由 RebuildTileTextureFrames 通过
        // WorldGen.TileFrame 在安全临时区域重算。参考小助手 _RebuildFrames（第 5240-5314 行）。
        private static bool TryMirrorTileLayer(
            BlueprintCellLayerRecord layer,
            out BlueprintCellLayerRecord mirrored,
            out string reason,
            out bool warned)
        {
            reason = string.Empty;
            mirrored = null;
            warned = false;

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

            mirrored = layer.Clone();
            mirrored.Slope = mirroredSlope;
            // 保持原始 FrameX/Y；纹理融合在 RebuildTileTextureFrames 中用 WorldGen.TileFrame 修正。
            return true;
        }

        // Object 层镜像：用 TileObjectData 翻转 Direction 并重算 FrameX/Y。
        // 参考小助手 _TransformTileForMirror（第 5181-5230 行）和 _FindMirroredDirection
        // （第 5522-5591 行）。
        private static bool TryMirrorObjectLayer(
            BlueprintCellLayerRecord layer,
            out BlueprintCellLayerRecord mirrored,
            out string reason,
            out bool warned)
        {
            reason = string.Empty;
            mirrored = null;
            warned = false;

            if (layer.ContentId <= 0)
            {
                reason = "objectContentUnavailable";
                return false;
            }

            mirrored = layer.Clone();

            // 绳索没有方向性，直接返回。
            if (layer.ContentId == TileIdRope)
            {
                return true;
            }

            int newFrameX;
            int newFrameY;
            string frameReason;
            if (TryMirrorObjectFrame(layer, out newFrameX, out newFrameY, out frameReason))
            {
                mirrored.FrameX = newFrameX;
                mirrored.FrameY = newFrameY;
                return true;
            }

            // 不能保留旧 FrameX/Y 伪装成功：多格 object 的每个子格 frame 都承载整件家具的位置与方向。
            // 猜错会让实例副本继续污染投影、材料和自动放置候选，因此这里必须 fail-closed。
            reason = frameReason;
            return false;
        }

        // 用 TileObjectData 为 Object 层计算镜像后的 FrameX/Y。
        // 返回 false 表示无法精确计算（数据不可用、方向无匹配等），调用方必须 fail-closed。
        private static bool TryMirrorObjectFrame(
            BlueprintCellLayerRecord layer,
            out int newFrameX,
            out int newFrameY,
            out string reason)
        {
            newFrameX = 0;
            newFrameY = 0;
            reason = string.Empty;

            try
            {
                // Step 1：根据当前 FrameX/Y 查找 TileObjectData，获取 direction、style 和 object 子格位置。
                var sourceData = ResolveTileObjectData((ushort)layer.ContentId, layer.FrameX, layer.FrameY);
                BlueprintObjectFrameInfo sourceInfo;
                if (!TryReadObjectFrameInfo(layer, sourceData, out sourceInfo, out reason))
                {
                    return false;
                }

                if (Math.Max(0, layer.Style) != sourceInfo.Style)
                {
                    reason = "objectStyleMismatch";
                    return false;
                }

                // Step 2：判断是否需要翻转 Direction。
                var sourceDirection = (int)sourceData.Direction;
                TileObjectData resolved;
                int targetDirection;
                if (sourceDirection == TileObjectDirectionPlaceLeft || sourceDirection == TileObjectDirectionPlaceRight)
                {
                    // Step 3：翻转 Direction（PlaceLeft ↔ PlaceRight）。
                    // 参考小助手 _FlipDirection（第 5538 行）。
                    targetDirection = sourceDirection == TileObjectDirectionPlaceLeft
                        ? TileObjectDirectionPlaceRight
                        : TileObjectDirectionPlaceLeft;

                    // Step 4：用翻转后的 direction 查找 target TileObjectData。
                    // 参考小助手 _FindMirroredDirection（第 5522 行）。
                    resolved = ResolveTileObjectDataForDirection(
                        (ushort)layer.ContentId, sourceInfo.StyleColumn, sourceInfo.StyleRow, targetDirection);
                    if (resolved == null)
                    {
                        reason = "objectDirectionAlternateUnavailable";
                        return false;
                    }
                }
                else if (sourceDirection == TileObjectDirectionNone)
                {
                    targetDirection = TileObjectDirectionNone;
                    resolved = sourceData;
                }
                else
                {
                    reason = "objectDirectionUnsupported";
                    return false;
                }

                if (resolved.Width != sourceInfo.Width || resolved.Height != sourceInfo.Height)
                {
                    reason = "objectDirectionAlternateShapeMismatch";
                    return false;
                }

                // Step 5：在 target TileObjectData 的坐标系中计算新的 FrameX/Y。
                // 镜像后 sub-tile 的 X 位置翻转：subTileX' = width - subTileX - CoordinateWidth
                // 参考小助手 _ComputeMirroredFrameX（第 5379 行）。
                var targetFullWidth = Math.Max(1, resolved.CoordinateFullWidth);
                var targetFullHeight = Math.Max(1, resolved.CoordinateFullHeight);
                var targetWidth = Math.Max(1, resolved.Width);
                var coordinateWidth = Math.Max(1, resolved.CoordinateWidth);
                var coordinatePadding = resolved.CoordinatePadding;
                if (coordinatePadding < 0)
                {
                    reason = "objectFrameDimensionsInvalid";
                    return false;
                }

                // sub-tile 在行内的翻转位置
                var mirroredColumn = targetWidth - 1 - sourceInfo.ObjectColumn;
                var newSubTileX = mirroredColumn * (coordinateWidth + coordinatePadding) + sourceInfo.InnerX;
                int newSubTileY;
                if (!TryBuildObjectSubTileY(resolved, sourceInfo.ObjectRow, sourceInfo.InnerY, out newSubTileY, out reason))
                {
                    return false;
                }

                newFrameX = sourceInfo.StyleColumn * targetFullWidth + Clamp(newSubTileX, 0, targetFullWidth - 1);
                newFrameY = sourceInfo.StyleRow * targetFullHeight + Clamp(newSubTileY, 0, targetFullHeight - 1);

                var verifyData = ResolveTileObjectData((ushort)layer.ContentId, newFrameX, newFrameY);
                if (verifyData == null || (int)verifyData.Direction != targetDirection)
                {
                    reason = "objectMirroredFrameUnverifiable";
                    return false;
                }

                return true;
            }
            catch
            {
                reason = "objectFrameMirrorException";
                return false;
            }
        }

        // 根据 tileType / frameX / frameY 解析 Terraria TileObjectData。
        // 使用 Terraria 的 TileObjectData.GetTileData(Tile) API，与小助手 _GetTileData（第 5359 行）同款手法。
        private static TileObjectData ResolveTileObjectData(ushort tileType, int frameX, int frameY)
        {
            try
            {
                var tile = new Tile();
                tile.active(true);
                tile.type = tileType;
                tile.frameX = (short)Clamp(frameX, 0, 32767);
                tile.frameY = (short)Clamp(frameY, 0, 32767);
                var data = TryGetTileData(tile);
                if (data != null)
                {
                    return data;
                }

                // Production runtime must never initialize vanilla TileObjectData.
                // If Terraria has not finished its own startup writer yet, object
                // mirror/repair must fail closed instead of locking startup data.
                return null;
            }
            catch
            {
                return null;
            }
        }

        // 根据 tileType、style column/row、targetDirection 查找匹配的 TileObjectData alternate。
        // 参考小助手 _FindMirroredDirection（第 5522-5591 行）。
        private static TileObjectData ResolveTileObjectDataForDirection(
            ushort tileType, int styleColumn, int styleRow, int targetDirection)
        {
            try
            {
                var baseData = TryGetTileData((int)tileType, 0, 0);
                if (baseData == null)
                {
                    return null;
                }

                var tileStyle = ComputeTileStyle(baseData, styleColumn, styleRow);
                var alternatesCount = baseData.AlternatesCount;

                // 遍历 base + all alternates，找 Direction 匹配且 style 最接近的。
                TileObjectData best = null;
                var bestStyleDistance = int.MaxValue;
                for (var i = 0; i <= alternatesCount; i++)
                {
                    var candidate = TryGetTileData((int)tileType, 0, i);
                    if (candidate == null || (int)candidate.Direction != targetDirection)
                    {
                        continue;
                    }

                    var distance = Math.Abs(candidate.Style - tileStyle);
                    if (best == null || distance < bestStyleDistance)
                    {
                        best = candidate;
                        bestStyleDistance = distance;
                    }
                }

                return best;
            }
            catch
            {
                return null;
            }
        }

        private static TileObjectData TryGetTileData(Tile tile)
        {
            try
            {
                return TileObjectData.GetTileData(tile);
            }
            catch
            {
                return null;
            }
        }

        private static TileObjectData TryGetTileData(int tileType, int style, int alternate)
        {
            try
            {
                return TileObjectData.GetTileData(tileType, style, alternate);
            }
            catch
            {
                return null;
            }
        }

        // 将 styleColumn / styleRow 映射为 Terraria 的 style 值。
        // 参考小助手 _ComputePlacementStyle（第 5430 行）和小助手 _ResolveStyle（第 686 行）。
        private static int ComputeTileStyle(TileObjectData data, int styleColumn, int styleRow)
        {
            if (data == null)
            {
                return 0;
            }

            var wrapLimit = data.StyleWrapLimit <= 0 ? 1 : data.StyleWrapLimit;
            var placementStyle = data.StyleHorizontal
                ? styleRow * wrapLimit + styleColumn
                : styleColumn * wrapLimit + styleRow;
            var multiplier = data.StyleMultiplier <= 0 ? 1 : data.StyleMultiplier;
            return Math.Max(0, placementStyle / multiplier);
        }

        private static bool TryValidateObjectIntegrity(BlueprintTemplateRecord template, out string reason)
        {
            reason = string.Empty;
            var groups = new Dictionary<string, BlueprintObjectGroup>(StringComparer.Ordinal);
            for (var cellIndex = 0; template != null && template.Cells != null && cellIndex < template.Cells.Count; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                if (cell == null || cell.Layers == null)
                {
                    continue;
                }

                for (var layerIndex = 0; layerIndex < cell.Layers.Count; layerIndex++)
                {
                    var layer = cell.Layers[layerIndex];
                    if (layer == null || !string.Equals(layer.LayerKind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (layer.ContentId == TileIdRope)
                    {
                        continue;
                    }

                    var data = ResolveTileObjectData((ushort)layer.ContentId, layer.FrameX, layer.FrameY);
                    BlueprintObjectFrameInfo info;
                    if (!TryReadObjectFrameInfo(layer, data, out info, out reason))
                    {
                        reason = BuildLayerDiagnosticReason(reason, cell, layer);
                        return false;
                    }

                    if (Math.Max(0, layer.Style) != info.Style)
                    {
                        reason = BuildLayerDiagnosticReason("objectStyleMismatch", cell, layer);
                        return false;
                    }

                    var originX = cell.X - info.ObjectColumn;
                    var originY = cell.Y - info.ObjectRow;
                    var groupKey = BuildObjectGroupKey(layer.ContentId, info.Style, originX, originY, info.Width, info.Height);
                    BlueprintObjectGroup group;
                    if (!groups.TryGetValue(groupKey, out group))
                    {
                        group = new BlueprintObjectGroup
                        {
                            ContentId = layer.ContentId,
                            Style = info.Style,
                            OriginX = originX,
                            OriginY = originY,
                            Width = info.Width,
                            Height = info.Height,
                            FirstCell = cell,
                            FirstLayer = layer
                        };
                        groups.Add(groupKey, group);
                    }

                    var subKey = BuildCellKey(info.ObjectColumn, info.ObjectRow);
                    if (group.SubTiles.ContainsKey(subKey))
                    {
                        reason = BuildLayerDiagnosticReason("objectDuplicateSubTile", cell, layer);
                        return false;
                    }

                    group.SubTiles.Add(subKey, layer);
                }
            }

            foreach (var pair in groups)
            {
                var group = pair.Value;
                if (group == null || group.Width <= 1 && group.Height <= 1)
                {
                    continue;
                }

                for (var x = 0; x < group.Width; x++)
                {
                    for (var y = 0; y < group.Height; y++)
                    {
                        if (!group.SubTiles.ContainsKey(BuildCellKey(x, y)))
                        {
                            reason = BuildLayerDiagnosticReason("objectIncomplete", group.FirstCell, group.FirstLayer);
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool TryReadObjectFrameInfo(
            BlueprintCellLayerRecord layer,
            TileObjectData data,
            out BlueprintObjectFrameInfo info,
            out string reason)
        {
            info = null;
            reason = string.Empty;
            if (layer == null)
            {
                reason = "nullLayer";
                return false;
            }

            if (data == null)
            {
                reason = "objectTileDataUnresolvable";
                return false;
            }

            if (data.Width <= 0 ||
                data.Height <= 0 ||
                data.CoordinateWidth <= 0 ||
                data.CoordinatePadding < 0 ||
                data.CoordinateFullWidth <= 0 ||
                data.CoordinateFullHeight <= 0)
            {
                reason = "objectFrameDimensionsInvalid";
                return false;
            }

            var frameX = Math.Max(0, layer.FrameX);
            var frameY = Math.Max(0, layer.FrameY);
            var styleColumn = frameX / data.CoordinateFullWidth;
            var styleRow = frameY / data.CoordinateFullHeight;
            var subTileX = frameX % data.CoordinateFullWidth;
            var subTileY = frameY % data.CoordinateFullHeight;

            int objectColumn;
            int innerX;
            if (!TryResolveObjectColumn(data, subTileX, out objectColumn, out innerX))
            {
                reason = "objectFrameSubTileXInvalid";
                return false;
            }

            int objectRow;
            int innerY;
            if (!TryResolveObjectRow(data, subTileY, out objectRow, out innerY))
            {
                reason = "objectFrameSubTileYInvalid";
                return false;
            }

            info = new BlueprintObjectFrameInfo
            {
                Data = data,
                StyleColumn = styleColumn,
                StyleRow = styleRow,
                Style = ComputeTileStyle(data, styleColumn, styleRow),
                ObjectColumn = objectColumn,
                ObjectRow = objectRow,
                InnerX = innerX,
                InnerY = innerY,
                Width = Math.Max(1, data.Width),
                Height = Math.Max(1, data.Height)
            };
            return true;
        }

        private static bool TryResolveObjectColumn(TileObjectData data, int subTileX, out int column, out int innerX)
        {
            column = 0;
            innerX = 0;
            if (data == null || data.Width <= 0 || data.CoordinateWidth <= 0 || data.CoordinatePadding < 0)
            {
                return false;
            }

            var step = data.CoordinateWidth + data.CoordinatePadding;
            if (step <= 0)
            {
                return false;
            }

            column = subTileX / step;
            innerX = subTileX - column * step;
            return column >= 0 && column < data.Width && innerX >= 0 && innerX < data.CoordinateWidth;
        }

        private static bool TryResolveObjectRow(TileObjectData data, int subTileY, out int row, out int innerY)
        {
            row = 0;
            innerY = 0;
            if (data == null || data.Height <= 0 || data.CoordinatePadding < 0)
            {
                return false;
            }

            var offset = 0;
            for (var index = 0; index < data.Height; index++)
            {
                var rowHeight = GetCoordinateHeight(data, index);
                if (rowHeight <= 0)
                {
                    return false;
                }

                if (subTileY >= offset && subTileY < offset + rowHeight)
                {
                    row = index;
                    innerY = subTileY - offset;
                    return true;
                }

                offset += rowHeight;
                if (index < data.Height - 1)
                {
                    if (subTileY >= offset && subTileY < offset + data.CoordinatePadding)
                    {
                        return false;
                    }

                    offset += data.CoordinatePadding;
                }
            }

            return false;
        }

        private static bool TryBuildObjectSubTileY(TileObjectData data, int row, int innerY, out int subTileY, out string reason)
        {
            subTileY = 0;
            reason = string.Empty;
            if (data == null || row < 0 || row >= data.Height || data.CoordinatePadding < 0)
            {
                reason = "objectFrameSubTileYInvalid";
                return false;
            }

            var rowHeight = GetCoordinateHeight(data, row);
            if (rowHeight <= 0 || innerY < 0 || innerY >= rowHeight)
            {
                reason = "objectFrameSubTileYInvalid";
                return false;
            }

            var offset = 0;
            for (var index = 0; index < row; index++)
            {
                var height = GetCoordinateHeight(data, index);
                if (height <= 0)
                {
                    reason = "objectFrameSubTileYInvalid";
                    return false;
                }

                offset += height + data.CoordinatePadding;
            }

            subTileY = offset + innerY;
            return true;
        }

        private static int GetCoordinateHeight(TileObjectData data, int row)
        {
            if (data == null || data.CoordinateHeights == null || data.CoordinateHeights.Length == 0)
            {
                return 0;
            }

            if (row < data.CoordinateHeights.Length)
            {
                return data.CoordinateHeights[row];
            }

            return data.CoordinateHeights[data.CoordinateHeights.Length - 1];
        }

        private static string BuildObjectGroupKey(int contentId, int style, int originX, int originY, int width, int height)
        {
            return contentId.ToString(CultureInfo.InvariantCulture) + "|" +
                   style.ToString(CultureInfo.InvariantCulture) + "|" +
                   originX.ToString(CultureInfo.InvariantCulture) + "," +
                   originY.ToString(CultureInfo.InvariantCulture) + "|" +
                   width.ToString(CultureInfo.InvariantCulture) + "x" +
                   height.ToString(CultureInfo.InvariantCulture);
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

        // 在安全临时区域放置 Tile 层，用 WorldGen.TileFrame 重算纹理融合帧值。
        // 这是小助手 _RebuildFrames（第 5240-5314 行）的核心思路：
        //   1. 备份临时区域 (6,6) 的原始 tile
        //   2. 写入镜像后的 tile
        //   3. 调 WorldGen.TileFrame 让引擎算出正确纹理连接
        //   4. 读回修正后的 FrameX/Y
        //   5. finally 恢复原始 tile
        // 返回修正的 tile layer 数量（0 表示无需修正或失败）。
        private static int RebuildTileTextureFrames(BlueprintTemplateRecord template)
        {
            if (template?.Cells == null || template.Cells.Count == 0)
            {
                return 0;
            }

            if (Main.tile == null || Main.maxTilesX <= 20 || Main.maxTilesY <= 20)
            {
                return 0;
            }

            // 临时区域起点（与 Terraria 世界边缘保持安全距离，同小助手 _GetTempArea 第 5316 行）
            const int tempOriginX = 6;
            const int tempOriginY = 6;
            var width = Math.Max(1, template.Width);
            var height = Math.Max(1, template.Height);

            if (tempOriginX + width >= Main.maxTilesX || tempOriginY + height >= Main.maxTilesY)
            {
                return 0;
            }

            // 备份临时区域
            var backup = new Tile[width, height];
            try
            {
                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        backup[x, y] = Main.tile[tempOriginX + x, tempOriginY + y];
                    }
                }

                // 写入镜像后的 Tile 层到临时区域
                for (var ci = 0; ci < template.Cells.Count; ci++)
                {
                    var cell = template.Cells[ci];
                    if (cell?.Layers == null || cell.X < 0 || cell.X >= width || cell.Y < 0 || cell.Y >= height)
                    {
                        continue;
                    }

                    for (var li = 0; li < cell.Layers.Count; li++)
                    {
                        var layer = cell.Layers[li];
                        if (layer == null || layer.ContentId <= 0 ||
                            !string.Equals(layer.LayerKind, BlueprintLayerKinds.Tile, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var tile = new Tile();
                        tile.active(true);
                        tile.type = (ushort)layer.ContentId;
                        tile.frameX = (short)Clamp(layer.FrameX, 0, 32767);
                        tile.frameY = (short)Clamp(layer.FrameY, 0, 32767);
                        tile.slope((byte)Clamp(layer.Slope, 0, 4));
                        tile.halfBrick(layer.HalfBrick);
                        Main.tile[tempOriginX + cell.X, tempOriginY + cell.Y] = tile;
                    }
                }

                // WorldGen.TileFrame 重算纹理融合
                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        WorldGen.TileFrame(tempOriginX + x, tempOriginY + y, true, true);
                    }
                }

                // 读回修正后的 FrameX/Y 并更新 template
                var fixedCount = 0;
                for (var ci = 0; ci < template.Cells.Count; ci++)
                {
                    var cell = template.Cells[ci];
                    if (cell?.Layers == null || cell.X < 0 || cell.X >= width || cell.Y < 0 || cell.Y >= height)
                    {
                        continue;
                    }

                    for (var li = 0; li < cell.Layers.Count; li++)
                    {
                        var layer = cell.Layers[li];
                        if (layer == null || layer.ContentId <= 0 ||
                            !string.Equals(layer.LayerKind, BlueprintLayerKinds.Tile, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var updatedTile = Main.tile[tempOriginX + cell.X, tempOriginY + cell.Y];
                        if (updatedTile == null || !updatedTile.active())
                        {
                            continue;
                        }

                        layer.FrameX = updatedTile.frameX;
                        layer.FrameY = updatedTile.frameY;
                        fixedCount++;
                    }
                }

                return fixedCount;
            }
            catch
            {
                return 0;
            }
            finally
            {
                // 恢复临时区域原始 tile
                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        Main.tile[tempOriginX + x, tempOriginY + y] = backup[x, y];
                    }
                }
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

        private static string BuildLayerDiagnosticReason(string reason, BlueprintCellRecord cell, BlueprintCellLayerRecord layer)
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
