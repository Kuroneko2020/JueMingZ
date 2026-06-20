using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Records;

namespace JueMingZ.Automation.Blueprint
{
    internal sealed class BlueprintPlacementWorldContext
    {
        public BlueprintPlacementWorldContext()
        {
            WorldPairKey = string.Empty;
            WorldKey = string.Empty;
            FailureReason = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string WorldPairKey { get; private set; }
        public string WorldKey { get; private set; }
        public string FailureReason { get; private set; }

        public static BlueprintPlacementWorldContext Success(string worldPairKey, string worldKey)
        {
            return new BlueprintPlacementWorldContext
            {
                Succeeded = true,
                WorldPairKey = worldPairKey ?? string.Empty,
                WorldKey = worldKey ?? string.Empty,
                FailureReason = string.Empty
            };
        }

        public static BlueprintPlacementWorldContext Failure(string failureReason)
        {
            return new BlueprintPlacementWorldContext
            {
                Succeeded = false,
                WorldPairKey = string.Empty,
                WorldKey = string.Empty,
                FailureReason = string.IsNullOrWhiteSpace(failureReason) ? "worldIdentityUnavailable" : failureReason
            };
        }
    }

    internal sealed class BlueprintPlacementPreviewSnapshot
    {
        public BlueprintPlacementPreviewSnapshot()
        {
            TemplateId = string.Empty;
            TemplateName = string.Empty;
            LastNotice = string.Empty;
            LastInputOwner = string.Empty;
            LastResultCode = string.Empty;
            LastPlacedInstanceId = string.Empty;
            LastPlacedInstanceName = string.Empty;
            TemplateSnapshot = new BlueprintTemplateRecord();
        }

        public bool Active { get; set; }
        public string TemplateId { get; set; }
        public string TemplateName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int AnchorX { get; set; }
        public int AnchorY { get; set; }
        public bool HoverTileHit { get; set; }
        public int HoverTileX { get; set; }
        public int HoverTileY { get; set; }
        public int OriginTileX { get; set; }
        public int OriginTileY { get; set; }
        public string LastNotice { get; set; }
        public string LastInputOwner { get; set; }
        public string LastResultCode { get; set; }
        public string LastPlacedInstanceId { get; set; }
        public string LastPlacedInstanceName { get; set; }
        public string MirrorLastStatus { get; set; }
        public string MirrorBlockedReason { get; set; }
        public BlueprintTemplateRecord TemplateSnapshot { get; set; }
    }

    internal sealed class BlueprintPlacementCommandResult
    {
        private BlueprintPlacementCommandResult()
        {
            ResultCode = string.Empty;
            Message = string.Empty;
            TemplateId = string.Empty;
            TemplateName = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public bool Changed { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string TemplateId { get; private set; }
        public string TemplateName { get; private set; }

        public static BlueprintPlacementCommandResult Create(
            bool succeeded,
            bool changed,
            string resultCode,
            string message,
            string templateId,
            string templateName)
        {
            return new BlueprintPlacementCommandResult
            {
                Succeeded = succeeded,
                Changed = changed,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                TemplateId = templateId ?? string.Empty,
                TemplateName = templateName ?? string.Empty
            };
        }
    }

    internal sealed class BlueprintPlacementPointerInput
    {
        public bool UiOwned { get; set; }
        public bool WorldTileHit { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public bool LeftDown { get; set; }
        public bool LeftPressed { get; set; }
        public bool LeftReleased { get; set; }
    }

    internal sealed class BlueprintPlacementInteractionResult
    {
        public bool Succeeded { get; set; }
        public bool Changed { get; set; }
        public bool ShouldConsumeLeftInput { get; set; }
        public bool InputActive { get; set; }
        public bool PlacedInstance { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
        public BlueprintWorldInstanceRecord Instance { get; set; }
    }

    internal static class BlueprintPlacementPreviewState
    {
        private static readonly object SyncRoot = new object();
        private static readonly object TestingSyncRoot = new object();

        private static BlueprintTemplateLibraryStore _testingTemplateStore;
        private static BlueprintWorldInstanceStore _testingInstanceStore;
        private static BlueprintPlacementWorldContext _testingWorldContext;

        private static bool _active;
        private static BlueprintTemplateRecord _template = new BlueprintTemplateRecord();
        private static bool _hoverTileHit;
        private static int _hoverTileX;
        private static int _hoverTileY;
        private static int _originTileX;
        private static int _originTileY;
        private static string _lastNotice = "摆放预览待命。";
        private static string _lastInputOwner = string.Empty;
        private static string _lastResultCode = "idle";
        private static string _lastPlacedInstanceId = string.Empty;
        private static string _lastPlacedInstanceName = string.Empty;

        public static BlueprintPlacementPreviewSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return BuildSnapshotLocked();
            }
        }

        public static BlueprintPlacementCommandResult BeginPreview(BlueprintTemplateRecord template, string source)
        {
            if (!IsUsableTemplate(template))
            {
                return BlueprintPlacementCommandResult.Create(
                    false,
                    false,
                    "invalidTemplate",
                    "没有可用于摆放预览的蓝图模板。",
                    string.Empty,
                    string.Empty);
            }

            var clone = template.Clone();
            NormalizePreviewAnchor(clone);
            lock (SyncRoot)
            {
                var changed = !_active ||
                              !string.Equals(_template.TemplateId, clone.TemplateId, StringComparison.Ordinal) ||
                              !string.Equals(_template.UpdatedUtc, clone.UpdatedUtc, StringComparison.Ordinal);
                _active = true;
                _template = clone;
                _hoverTileHit = false;
                _hoverTileX = 0;
                _hoverTileY = 0;
                _originTileX = 0;
                _originTileY = 0;
                _lastInputOwner = source ?? string.Empty;
                _lastResultCode = "previewStarted";
                _lastPlacedInstanceId = string.Empty;
                _lastPlacedInstanceName = string.Empty;
                _lastNotice = "模板 " + GetTemplateName(clone) + " 已进入摆放预览；中心锚点随鼠标世界格对齐。";
                return BlueprintPlacementCommandResult.Create(
                    true,
                    changed,
                    _lastResultCode,
                    _lastNotice,
                    clone.TemplateId,
                    GetTemplateName(clone));
            }
        }

        public static BlueprintPlacementCommandResult BeginPreviewFromTemplateId(string templateId, string source)
        {
            var id = BlueprintTemplateLibraryStore.NormalizeId(templateId);
            if (string.IsNullOrEmpty(id))
            {
                return BlueprintPlacementCommandResult.Create(false, false, "invalidTemplate", "没有可使用的蓝图模板。", string.Empty, string.Empty);
            }

            var store = ResolveTemplateStore();
            BlueprintTemplateLibrarySnapshot snapshot;
            var load = store.TryLoad(out snapshot);
            if (!load.Succeeded)
            {
                return BlueprintPlacementCommandResult.Create(false, false, load.ResultCode, "蓝图库读取失败：" + load.Message, id, string.Empty);
            }

            var template = FindTemplate(snapshot == null ? null : snapshot.Templates, id);
            if (template == null)
            {
                return BlueprintPlacementCommandResult.Create(false, false, "missingTemplate", "蓝图模板不存在。", id, string.Empty);
            }

            return BeginPreview(template, source);
        }

        public static BlueprintPlacementCommandResult Cancel()
        {
            lock (SyncRoot)
            {
                var changed = _active || _hoverTileHit;
                _active = false;
                _template = new BlueprintTemplateRecord();
                _hoverTileHit = false;
                _hoverTileX = 0;
                _hoverTileY = 0;
                _originTileX = 0;
                _originTileY = 0;
                _lastInputOwner = "ui";
                _lastResultCode = "previewCancelled";
                _lastNotice = "已取消蓝图摆放预览。";
                return BlueprintPlacementCommandResult.Create(true, changed, _lastResultCode, _lastNotice, string.Empty, string.Empty);
            }
        }

        public static BlueprintPlacementCommandResult MirrorHorizontal()
        {
            lock (SyncRoot)
            {
                if (!_active || !IsUsableTemplate(_template))
                {
                    var skipped = BlueprintMirrorService.RecordSkipped(
                        "mirrorInactivePreview",
                        "当前没有可镜像的摆放预览。",
                        "inactivePreview");
                    _lastInputOwner = "ui";
                    _lastResultCode = skipped.ResultCode;
                    _lastNotice = skipped.Message;
                    return BlueprintPlacementCommandResult.Create(false, false, _lastResultCode, _lastNotice, string.Empty, string.Empty);
                }

                var result = BlueprintMirrorService.TryMirrorHorizontal(_template);
                _lastInputOwner = "ui";
                _lastResultCode = result.ResultCode;
                _lastNotice = result.Message;
                if (!result.Succeeded || result.Template == null)
                {
                    return BlueprintPlacementCommandResult.Create(false, false, _lastResultCode, _lastNotice, _template.TemplateId, GetTemplateName(_template));
                }

                _template = result.Template.Clone();
                NormalizePreviewAnchor(_template);
                if (_hoverTileHit)
                {
                    CalculateOrigin(_template, _hoverTileX, _hoverTileY, out _originTileX, out _originTileY);
                }

                return BlueprintPlacementCommandResult.Create(true, true, _lastResultCode, _lastNotice, _template.TemplateId, GetTemplateName(_template));
            }
        }

        public static BlueprintPlacementInteractionResult HandlePointer(BlueprintPlacementPointerInput input)
        {
            input = input ?? new BlueprintPlacementPointerInput();
            lock (SyncRoot)
            {
                if (!_active)
                {
                    return BuildInteractionResultLocked(true, false, false, false, false, "inactive", _lastNotice, null);
                }

                if (input.UiOwned)
                {
                    _lastInputOwner = "ui";
                    _lastResultCode = "uiOwned";
                    _lastNotice = "鼠标命中 UI；摆放预览未确认。";
                    return BuildInteractionResultLocked(true, false, input.LeftDown || input.LeftPressed || input.LeftReleased, true, false, _lastResultCode, _lastNotice, null);
                }

                if (input.WorldTileHit)
                {
                    UpdateHoverLocked(input.TileX, input.TileY, "world");
                }
                else if (input.LeftPressed)
                {
                    _lastInputOwner = "world-outside";
                    _lastResultCode = "worldMiss";
                    _lastNotice = "鼠标未命中有效世界格；摆放预览未确认。";
                    return BuildInteractionResultLocked(true, false, true, true, false, _lastResultCode, _lastNotice, null);
                }

                if (input.LeftPressed)
                {
                    return ConfirmPlacementLocked(input.TileX, input.TileY);
                }

                if (input.LeftDown || input.LeftReleased)
                {
                    _lastInputOwner = input.WorldTileHit ? "world" : "world-outside";
                    _lastResultCode = "heldIgnored";
                    _lastNotice = "等待新的左键按下后确认蓝图实例。";
                    return BuildInteractionResultLocked(true, false, true, true, false, _lastResultCode, _lastNotice, null);
                }

                return BuildInteractionResultLocked(true, input.WorldTileHit, false, true, false, "hoverUpdated", _lastNotice, null);
            }
        }

        public static string BuildUiStateJson()
        {
            var snapshot = GetSnapshot();
            return "{" +
                   "\"active\":" + BoolRaw(snapshot.Active) + "," +
                   "\"templateId\":\"" + EscapeJson(snapshot.TemplateId) + "\"," +
                   "\"templateName\":\"" + EscapeJson(snapshot.TemplateName) + "\"," +
                   "\"width\":" + IntRaw(snapshot.Width) + "," +
                   "\"height\":" + IntRaw(snapshot.Height) + "," +
                   "\"anchorX\":" + IntRaw(snapshot.AnchorX) + "," +
                   "\"anchorY\":" + IntRaw(snapshot.AnchorY) + "," +
                   "\"hoverTileHit\":" + BoolRaw(snapshot.HoverTileHit) + "," +
                   "\"hoverTileX\":" + IntRaw(snapshot.HoverTileX) + "," +
                   "\"hoverTileY\":" + IntRaw(snapshot.HoverTileY) + "," +
                   "\"originTileX\":" + IntRaw(snapshot.OriginTileX) + "," +
                   "\"originTileY\":" + IntRaw(snapshot.OriginTileY) + "," +
                   "\"lastResultCode\":\"" + EscapeJson(snapshot.LastResultCode) + "\"," +
                   "\"lastInputOwner\":\"" + EscapeJson(snapshot.LastInputOwner) + "\"," +
                   "\"lastPlacedInstanceId\":\"" + EscapeJson(snapshot.LastPlacedInstanceId) + "\"," +
                   "\"mirrorLastStatus\":\"" + EscapeJson(snapshot.MirrorLastStatus) + "\"," +
                   "\"mirrorBlockedReason\":\"" + EscapeJson(snapshot.MirrorBlockedReason) + "\"" +
                   "}";
        }

        public static int BuildStateSignature()
        {
            var snapshot = GetSnapshot();
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (snapshot.Active ? 1 : 0);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.TemplateId ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.TemplateName ?? string.Empty);
                hash = hash * 31 + snapshot.Width;
                hash = hash * 31 + snapshot.Height;
                hash = hash * 31 + snapshot.AnchorX;
                hash = hash * 31 + snapshot.AnchorY;
                hash = hash * 31 + (snapshot.HoverTileHit ? 1 : 0);
                hash = hash * 31 + snapshot.HoverTileX;
                hash = hash * 31 + snapshot.HoverTileY;
                hash = hash * 31 + snapshot.OriginTileX;
                hash = hash * 31 + snapshot.OriginTileY;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.LastResultCode ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.LastPlacedInstanceId ?? string.Empty);
                hash = hash * 31 + BlueprintMirrorService.BuildStateSignature();
                return hash;
            }
        }

        internal static void SetPlacementDependenciesForTesting(
            BlueprintTemplateLibraryStore templateStore,
            BlueprintWorldInstanceStore instanceStore,
            BlueprintPlacementWorldContext worldContext)
        {
            lock (TestingSyncRoot)
            {
                _testingTemplateStore = templateStore;
                _testingInstanceStore = instanceStore;
                _testingWorldContext = worldContext;
            }
        }

        internal static void ResetForTesting()
        {
            lock (TestingSyncRoot)
            {
                _testingTemplateStore = null;
                _testingInstanceStore = null;
                _testingWorldContext = null;
            }

            lock (SyncRoot)
            {
                _active = false;
                _template = new BlueprintTemplateRecord();
                _hoverTileHit = false;
                _hoverTileX = 0;
                _hoverTileY = 0;
                _originTileX = 0;
                _originTileY = 0;
                _lastNotice = "摆放预览待命。";
                _lastInputOwner = string.Empty;
                _lastResultCode = "idle";
                _lastPlacedInstanceId = string.Empty;
                _lastPlacedInstanceName = string.Empty;
            }

            BlueprintMirrorService.ResetForTesting();
        }

        internal static void CalculateOriginForTesting(BlueprintTemplateRecord template, int tileX, int tileY, out int originX, out int originY)
        {
            template = template == null ? new BlueprintTemplateRecord() : template.Clone();
            NormalizePreviewAnchor(template);
            CalculateOrigin(template, tileX, tileY, out originX, out originY);
        }

        private static BlueprintPlacementInteractionResult ConfirmPlacementLocked(int tileX, int tileY)
        {
            if (!IsUsableTemplate(_template))
            {
                _lastInputOwner = "world";
                _lastResultCode = "invalidTemplate";
                _lastNotice = "当前没有可确认的蓝图模板。";
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice, null);
            }

            UpdateHoverLocked(tileX, tileY, "world");
            var context = ResolveWorldContext();
            if (context == null || !context.Succeeded)
            {
                _lastResultCode = "worldIdentityUnavailable";
                _lastNotice = "当前玩家-世界身份不可用，不能保存蓝图实例：" +
                              (context == null ? "unknown" : context.FailureReason);
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice, null);
            }

            var store = ResolveInstanceStore();
            BlueprintWorldInstanceSnapshot snapshot;
            var load = store.TryLoadWorld(context.WorldPairKey, out snapshot);
            if (!load.Succeeded)
            {
                _lastResultCode = load.ResultCode;
                _lastNotice = "蓝图实例读取失败：" + load.Message;
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice, null);
            }

            var layerOrder = ResolveNextLayerOrder(snapshot == null ? null : snapshot.Instances);
            BlueprintWorldInstanceRecord instance;
            var write = store.CreateInstanceFromTemplate(
                context.WorldPairKey,
                context.WorldKey,
                _template,
                _originTileX,
                _originTileY,
                layerOrder,
                out instance);
            if (!write.Succeeded)
            {
                _lastResultCode = write.ResultCode;
                _lastNotice = "蓝图实例保存失败：" + write.Message;
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice, null);
            }

            _active = false;
            _lastInputOwner = "world";
            _lastResultCode = "instanceCreated";
            _lastPlacedInstanceId = instance == null ? string.Empty : instance.InstanceId;
            _lastPlacedInstanceName = instance == null ? GetTemplateName(_template) : instance.Name;
            _lastNotice = "已创建蓝图实例 " + _lastPlacedInstanceName + "。";
            return BuildInteractionResultLocked(true, true, true, false, true, _lastResultCode, _lastNotice, instance == null ? null : instance.Clone());
        }

        private static void UpdateHoverLocked(int tileX, int tileY, string owner)
        {
            _hoverTileHit = true;
            _hoverTileX = tileX;
            _hoverTileY = tileY;
            CalculateOrigin(_template, tileX, tileY, out _originTileX, out _originTileY);
            _lastInputOwner = owner ?? string.Empty;
            _lastResultCode = "hoverUpdated";
            _lastNotice = "摆放预览：" + GetTemplateName(_template) +
                          " 原点 " + _originTileX.ToString(CultureInfo.InvariantCulture) +
                          "," + _originTileY.ToString(CultureInfo.InvariantCulture) + "。";
        }

        private static BlueprintPlacementPreviewSnapshot BuildSnapshotLocked()
        {
            var template = _template == null ? new BlueprintTemplateRecord() : _template.Clone();
            NormalizePreviewAnchor(template);
            var mirror = BlueprintMirrorService.GetDiagnostics();
            return new BlueprintPlacementPreviewSnapshot
            {
                Active = _active,
                TemplateId = template.TemplateId ?? string.Empty,
                TemplateName = GetTemplateName(template),
                Width = Math.Max(1, template.Width),
                Height = Math.Max(1, template.Height),
                AnchorX = template.AnchorX,
                AnchorY = template.AnchorY,
                HoverTileHit = _hoverTileHit,
                HoverTileX = _hoverTileX,
                HoverTileY = _hoverTileY,
                OriginTileX = _originTileX,
                OriginTileY = _originTileY,
                LastNotice = _lastNotice,
                LastInputOwner = _lastInputOwner,
                LastResultCode = _lastResultCode,
                LastPlacedInstanceId = _lastPlacedInstanceId,
                LastPlacedInstanceName = _lastPlacedInstanceName,
                MirrorLastStatus = mirror.LastStatus,
                MirrorBlockedReason = mirror.LastBlockedReason,
                TemplateSnapshot = template
            };
        }

        private static BlueprintPlacementInteractionResult BuildInteractionResultLocked(
            bool succeeded,
            bool changed,
            bool shouldConsumeLeftInput,
            bool inputActive,
            bool placedInstance,
            string resultCode,
            string message,
            BlueprintWorldInstanceRecord instance)
        {
            return new BlueprintPlacementInteractionResult
            {
                Succeeded = succeeded,
                Changed = changed,
                ShouldConsumeLeftInput = shouldConsumeLeftInput,
                InputActive = inputActive,
                PlacedInstance = placedInstance,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                Instance = instance == null ? null : instance.Clone()
            };
        }

        private static BlueprintPlacementWorldContext ResolveWorldContext()
        {
            lock (TestingSyncRoot)
            {
                if (_testingWorldContext != null)
                {
                    return _testingWorldContext;
                }
            }

            PlayerWorldIdentityResolution resolution;
            if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out resolution) ||
                resolution == null ||
                !resolution.IsResolved ||
                string.IsNullOrWhiteSpace(resolution.PairId) ||
                string.IsNullOrWhiteSpace(resolution.WorldId))
            {
                return BlueprintPlacementWorldContext.Failure(resolution == null ? "identityUnavailable" : resolution.FailureReason);
            }

            return BlueprintPlacementWorldContext.Success(resolution.PairId, resolution.WorldId);
        }

        private static BlueprintTemplateLibraryStore ResolveTemplateStore()
        {
            lock (TestingSyncRoot)
            {
                if (_testingTemplateStore != null)
                {
                    return _testingTemplateStore;
                }
            }

            return new BlueprintTemplateLibraryStore();
        }

        private static BlueprintWorldInstanceStore ResolveInstanceStore()
        {
            lock (TestingSyncRoot)
            {
                if (_testingInstanceStore != null)
                {
                    return _testingInstanceStore;
                }
            }

            return new BlueprintWorldInstanceStore();
        }

        private static int ResolveNextLayerOrder(IReadOnlyList<BlueprintWorldInstanceRecord> instances)
        {
            var next = 0;
            for (var index = 0; instances != null && index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance != null && instance.LayerOrder >= next)
                {
                    next = instance.LayerOrder + 1;
                }
            }

            return next;
        }

        private static BlueprintTemplateRecord FindTemplate(IReadOnlyList<BlueprintTemplateRecord> templates, string templateId)
        {
            for (var index = 0; templates != null && index < templates.Count; index++)
            {
                var template = templates[index];
                if (template != null &&
                    string.Equals(template.TemplateId, templateId, StringComparison.OrdinalIgnoreCase))
                {
                    return template;
                }
            }

            return null;
        }

        private static bool IsUsableTemplate(BlueprintTemplateRecord template)
        {
            return template != null &&
                   !string.IsNullOrWhiteSpace(template.TemplateId) &&
                   Math.Max(0, template.Width) > 0 &&
                   Math.Max(0, template.Height) > 0;
        }

        private static void NormalizePreviewAnchor(BlueprintTemplateRecord template)
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

        private static void CalculateOrigin(BlueprintTemplateRecord template, int tileX, int tileY, out int originX, out int originY)
        {
            template = template ?? new BlueprintTemplateRecord();
            originX = tileX - Clamp(template.AnchorX, 0, Math.Max(0, Math.Max(1, template.Width) - 1));
            originY = tileY - Clamp(template.AnchorY, 0, Math.Max(0, Math.Max(1, template.Height) - 1));
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

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
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
