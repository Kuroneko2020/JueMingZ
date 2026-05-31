using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class QuickActionExecutor : InputActionExecutorBase
    {
        private readonly InputActionKind _kind;
        private readonly string _methodName;
        private readonly string _scenario;
        private bool _resolved;
        private MethodInfo _method;
        private string _resolveError = string.Empty;

        public QuickActionExecutor(InputActionKind kind, string methodName, string scenario)
        {
            _kind = kind;
            _methodName = methodName;
            _scenario = scenario;
        }

        public override InputActionKind Kind { get { return _kind; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            SetState(execution, "OriginalQuickMethodInvoked", "false");
            ItemUseVerificationState before = null;
            ItemUseVerificationState after = null;
            DiagnosticResultCode resultCode;
            InputActionStatus status;
            string message;

            if (IsBlockedForWorldInput(snapshot))
            {
                resultCode = DiagnosticResultCode.BlockedByUi;
                status = InputActionStatus.BlockedByUi;
                message = GetDisplayName() + " 未执行：当前不在世界内，或聊天框、箱子、NPC 对话等界面正在阻挡输入。";
                return Finish(execution, startedUtc, status, resultCode, message, before, after);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                resultCode = DiagnosticResultCode.BlockedByEnvironment;
                status = InputActionStatus.Failed;
                message = TerrariaInputCompat.LastInputCompatError;
                return Finish(execution, startedUtc, status, resultCode, message, before, after);
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                resultCode = DiagnosticResultCode.Failed;
                status = InputActionStatus.Failed;
                message = "无法读取当前手持格：" + TerrariaInputCompat.LastInputCompatError;
                return Finish(execution, startedUtc, status, resultCode, message, before, after);
            }

            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out before))
            {
                resultCode = DiagnosticResultCode.Failed;
                status = InputActionStatus.Failed;
                message = TerrariaInputCompat.LastInputCompatError;
                return Finish(execution, startedUtc, status, resultCode, message, before, after);
            }

            if (before.ItemAnimation > 0 || before.ItemTime > 0 || before.ReuseDelay > 0)
            {
                resultCode = DiagnosticResultCode.BlockedByCooldown;
                status = InputActionStatus.Failed;
                message = GetDisplayName() + " 暂未执行：物品动作或冷却尚未结束。";
                return Finish(execution, startedUtc, status, resultCode, message, before, after);
            }

            if (_kind == InputActionKind.QuickHeal && snapshot != null && snapshot.Player != null && snapshot.Player.LifeMax > 0 && before.Life >= snapshot.Player.LifeMax)
            {
                resultCode = DiagnosticResultCode.NotApplicable;
                status = InputActionStatus.NotApplicable;
                message = "当前生命已满，不需要执行 QuickHeal。";
                return Finish(execution, startedUtc, status, resultCode, message, before, after);
            }

            if (_kind == InputActionKind.QuickMana && snapshot != null && snapshot.Player != null && snapshot.Player.ManaMax > 0 && before.Mana >= snapshot.Player.ManaMax)
            {
                resultCode = DiagnosticResultCode.NotApplicable;
                status = InputActionStatus.NotApplicable;
                message = "当前魔力已满，不需要执行 QuickMana。";
                return Finish(execution, startedUtc, status, resultCode, message, before, after);
            }

            if (!Resolve(player.GetType()))
            {
                resultCode = DiagnosticResultCode.NotImplemented;
                status = InputActionStatus.NotImplemented;
                message = "当前 Terraria 版本没有找到原版 Player." + _methodName + " 方法：" + _resolveError;
                return Finish(execution, startedUtc, status, resultCode, message, before, after);
            }

            try
            {
                _method.Invoke(player, new object[0]);
                SetState(execution, "OriginalQuickMethodInvoked", "true");
            }
            catch (Exception error)
            {
                resultCode = DiagnosticResultCode.Failed;
                status = InputActionStatus.Failed;
                message = GetDisplayName() + " 调用失败：" + (error.InnerException == null ? error.Message : error.InnerException.Message);
                return Finish(execution, startedUtc, status, resultCode, message, before, after);
            }

            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out after))
            {
                resultCode = DiagnosticResultCode.Failed;
                status = InputActionStatus.Failed;
                message = GetDisplayName() + " 已调用，但读取执行后状态失败：" + TerrariaInputCompat.LastInputCompatError;
                return Finish(execution, startedUtc, status, resultCode, message, before, after);
            }

            if (HasObservableSuccess(before, after))
            {
                resultCode = DiagnosticResultCode.Succeeded;
                status = InputActionStatus.Succeeded;
                message = BuildSuccessMessage(before, after);
            }
            else
            {
                resultCode = DiagnosticResultCode.AttemptedButUnverified;
                status = InputActionStatus.AttemptedButUnverified;
                message = BuildUnverifiedMessage();
            }

            return Finish(execution, startedUtc, status, resultCode, message, before, after);
        }

        private InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode resultCode,
            string message,
            ItemUseVerificationState before,
            ItemUseVerificationState after)
        {
            SetResultCode(execution, resultCode);
            MarkActionEventRecorded(execution);
            QuickActionDiagnostics.Record(_kind.ToString(), status.ToString(), resultCode.ToString(), message);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            var verification = "{" +
                               "\"originalMethodInvoked\":" + (GetStateBool(execution, "OriginalQuickMethodInvoked", false) ? "true" : "false") + "," +
                               "\"observableChange\":" + (HasObservableSuccess(before, after) ? "true" : "false") + "," +
                               "\"changedFields\":" + BuildChangedFieldsJson(before, after) +
                               "}";
            if (IsAutoRecovery(execution))
            {
                DiagnosticActionRecorder.RecordCustomEvent(
                    execution.Request.RequestId,
                    GetMetadataString(execution, "Scenario", _scenario),
                    _kind.ToString(),
                    GetMetadataString(execution, "SourceHotkey", string.Empty),
                    status.ToString(),
                    resultCode.ToString(),
                    message,
                    duration,
                    BuildAutoRecoveryBeforeJson(execution, before),
                    BuildAutoRecoveryAfterJson(execution, before, after, resultCode.ToString(), message),
                    verification,
                    GetMetadataString(execution, "SourceKind", string.Empty),
                    GetMetadataString(execution, "SourceUi", string.Empty),
                    GetMetadataString(execution, "ButtonId", string.Empty),
                    GetMetadataString(execution, "ButtonLabel", string.Empty));
            }
            else
            {
                DiagnosticActionRecorder.RecordActionEvent(
                    execution.Request.RequestId,
                    GetMetadataString(execution, "Scenario", _scenario),
                    _kind.ToString(),
                    GetMetadataString(execution, "SourceHotkey", string.Empty),
                    status.ToString(),
                    resultCode.ToString(),
                    message,
                    duration,
                    before,
                    after,
                    verification,
                    GetMetadataString(execution, "SourceKind", string.Empty),
                    GetMetadataString(execution, "SourceUi", string.Empty),
                    GetMetadataString(execution, "ButtonId", string.Empty),
                    GetMetadataString(execution, "ButtonLabel", string.Empty));
            }

            return InputActionExecutionStepResult.Complete(status, message);
        }

        private bool Resolve(Type playerType)
        {
            if (_resolved)
            {
                return _method != null;
            }

            _resolved = true;
            if (playerType == null)
            {
                _resolveError = "Player type unavailable.";
                return false;
            }

            var methods = playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, _methodName, StringComparison.Ordinal) ||
                    method.GetParameters().Length != 0)
                {
                    continue;
                }

                if (method.ReturnType != typeof(void) && method.ReturnType != typeof(bool))
                {
                    continue;
                }

                _method = method;
                return true;
            }

            _resolveError = "No zero-parameter " + _methodName + " method with void/bool return exists on " + playerType.FullName + ".";
            return false;
        }

        private static bool HasObservableSuccess(ItemUseVerificationState before, ItemUseVerificationState after)
        {
            if (before == null || after == null)
            {
                return false;
            }

            return (before.ItemAnimation <= 0 && after.ItemAnimation > 0) ||
                   (before.ItemTime <= 0 && after.ItemTime > 0) ||
                   after.ReuseDelay > before.ReuseDelay ||
                   after.ItemType != before.ItemType ||
                   after.ItemStack < before.ItemStack ||
                   after.Life > before.Life ||
                   after.Mana > before.Mana ||
                   after.ActiveBuffCount > before.ActiveBuffCount ||
                   after.BuffTimeTotal > before.BuffTimeTotal;
        }

        private string GetDisplayName()
        {
            switch (_kind)
            {
                case InputActionKind.QuickHeal:
                    return "QuickHeal";
                case InputActionKind.QuickMana:
                    return "QuickMana";
                case InputActionKind.QuickBuff:
                    return "QuickBuff";
                default:
                    return _methodName;
            }
        }

        private string BuildSuccessMessage(ItemUseVerificationState before, ItemUseVerificationState after)
        {
            if (_kind == InputActionKind.QuickHeal)
            {
                return "已调用原版 QuickHeal，生命值从 " + before.Life + " 变为 " + after.Life + "。";
            }

            if (_kind == InputActionKind.QuickMana)
            {
                return "已调用原版 QuickMana，魔力从 " + before.Mana + " 变为 " + after.Mana + "。";
            }

            if (_kind == InputActionKind.QuickBuff)
            {
                return "已调用原版 QuickBuff，Buff 数或 Buff 时间发生变化。";
            }

            return "已调用原版 " + _methodName + "，并观察到状态变化。";
        }

        private string BuildUnverifiedMessage()
        {
            if (_kind == InputActionKind.QuickHeal)
            {
                return "已调用原版 QuickHeal，但生命值没有变化；可能没有可用红药、仍在冷却中，或当前版本行为无法判断。";
            }

            if (_kind == InputActionKind.QuickMana)
            {
                return "已调用原版 QuickMana，但魔力没有变化；可能没有可用蓝药、仍在冷却中，或当前版本行为无法判断。";
            }

            if (_kind == InputActionKind.QuickBuff)
            {
                return "已调用原版 QuickBuff，但 Buff 数或时间没有变化；可能没有 Buff 药、已有对应 Buff，或当前版本行为无法判断。";
            }

            return "已调用原版 " + _methodName + "，但没有观察到状态变化。";
        }

        private static string BuildChangedFieldsJson(ItemUseVerificationState before, ItemUseVerificationState after)
        {
            if (before == null || after == null)
            {
                return "[]";
            }

            var builder = new System.Text.StringBuilder();
            builder.Append("[");
            var first = true;
            AppendChanged(builder, ref first, "itemAnimation", before.ItemAnimation != after.ItemAnimation);
            AppendChanged(builder, ref first, "itemTime", before.ItemTime != after.ItemTime);
            AppendChanged(builder, ref first, "reuseDelay", before.ReuseDelay != after.ReuseDelay);
            AppendChanged(builder, ref first, "itemType", before.ItemType != after.ItemType);
            AppendChanged(builder, ref first, "itemStack", before.ItemStack != after.ItemStack);
            AppendChanged(builder, ref first, "life", before.Life != after.Life);
            AppendChanged(builder, ref first, "mana", before.Mana != after.Mana);
            AppendChanged(builder, ref first, "activeBuffCount", before.ActiveBuffCount != after.ActiveBuffCount);
            AppendChanged(builder, ref first, "buffTimeTotal", before.BuffTimeTotal != after.BuffTimeTotal);
            builder.Append("]");
            return builder.ToString();
        }

        private static void AppendChanged(System.Text.StringBuilder builder, ref bool first, string name, bool changed)
        {
            if (!changed)
            {
                return;
            }

            if (!first)
            {
                builder.Append(",");
            }

            builder.Append("\"").Append(name).Append("\"");
            first = false;
        }

        private static bool IsAutoRecovery(InputActionExecution execution)
        {
            return string.Equals(GetMetadataString(execution, "SourceKind", string.Empty), "Automation", StringComparison.OrdinalIgnoreCase) &&
                   GetMetadataString(execution, "Scenario", string.Empty).StartsWith("AutoRecovery.", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildAutoRecoveryBeforeJson(InputActionExecution execution, ItemUseVerificationState before)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "enabled", BoolRaw(GetMetadataBool(execution, "AutoRecoveryEnabled", true)), true);
            AppendRaw(builder, "thresholdPercent", GetThresholdRaw(execution), true);
            AppendRaw(builder, "currentLife", IntRaw(GetMetadataInt(execution, "CurrentLife", before == null ? 0 : before.Life)), true);
            AppendRaw(builder, "maxLife", IntRaw(GetMetadataInt(execution, "MaxLife", before == null ? 0 : before.LifeMax)), true);
            AppendRaw(builder, "lifePercent", IntRaw(GetMetadataInt(execution, "LifePercent", Percent(before == null ? 0 : before.Life, before == null ? 0 : before.LifeMax))), true);
            AppendRaw(builder, "currentMana", IntRaw(GetMetadataInt(execution, "CurrentMana", before == null ? 0 : before.Mana)), true);
            AppendRaw(builder, "maxMana", IntRaw(GetMetadataInt(execution, "MaxMana", before == null ? 0 : before.ManaMax)), true);
            AppendRaw(builder, "manaPercent", IntRaw(GetMetadataInt(execution, "ManaPercent", Percent(before == null ? 0 : before.Mana, before == null ? 0 : before.ManaMax))), true);
            AppendString(builder, "triggerReason", GetMetadataString(execution, "TriggerReason", string.Empty), true);
            AppendRaw(builder, "cooldownBlocked", BoolRaw(GetMetadataBool(execution, "CooldownBlocked", false)), true);
            AppendRaw(builder, "potionSicknessBlocked", BoolRaw(GetMetadataBool(execution, "PotionSicknessBlocked", false)), true);
            AppendRaw(builder, "manaSicknessBlocked", BoolRaw(GetMetadataBool(execution, "ManaSicknessBlocked", false)), true);
            AppendRaw(builder, "potionDelay", IntRaw(GetMetadataInt(execution, "PotionDelay", 0)), true);
            AppendRaw(builder, "manaSickTime", IntRaw(GetMetadataInt(execution, "ManaSickTime", 0)), true);
            AppendRaw(builder, "lifeBefore", IntRaw(before == null ? GetMetadataInt(execution, "CurrentLife", 0) : before.Life), true);
            AppendRaw(builder, "manaBefore", IntRaw(before == null ? GetMetadataInt(execution, "CurrentMana", 0) : before.Mana), true);
            AppendRaw(builder, "buffCountBefore", IntRaw(before == null ? GetMetadataInt(execution, "BuffCountBefore", 0) : before.ActiveBuffCount), true);
            AppendRaw(builder, "buffTypesBefore", GetBuffTypesBeforeRaw(execution, before), false);
            builder.Append("}");
            return builder.ToString();
        }

        private string BuildAutoRecoveryAfterJson(
            InputActionExecution execution,
            ItemUseVerificationState before,
            ItemUseVerificationState after,
            string resultCode,
            string message)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "quickHealAttempted", BoolRaw(_kind == InputActionKind.QuickHeal && GetStateBool(execution, "OriginalQuickMethodInvoked", false)), true);
            AppendRaw(builder, "quickManaAttempted", BoolRaw(_kind == InputActionKind.QuickMana && GetStateBool(execution, "OriginalQuickMethodInvoked", false)), true);
            AppendRaw(builder, "quickBuffAttempted", BoolRaw(_kind == InputActionKind.QuickBuff && GetStateBool(execution, "OriginalQuickMethodInvoked", false)), true);
            AppendString(builder, "quickHealResultCode", _kind == InputActionKind.QuickHeal ? resultCode : string.Empty, true);
            AppendString(builder, "quickManaResultCode", _kind == InputActionKind.QuickMana ? resultCode : string.Empty, true);
            AppendString(builder, "quickBuffResultCode", _kind == InputActionKind.QuickBuff ? resultCode : string.Empty, true);
            AppendRaw(builder, "lifeAfter", IntRaw(after == null ? (before == null ? GetMetadataInt(execution, "CurrentLife", 0) : before.Life) : after.Life), true);
            AppendRaw(builder, "manaAfter", IntRaw(after == null ? (before == null ? GetMetadataInt(execution, "CurrentMana", 0) : before.Mana) : after.Mana), true);
            AppendRaw(builder, "buffCountAfter", IntRaw(after == null ? (before == null ? GetMetadataInt(execution, "BuffCountBefore", 0) : before.ActiveBuffCount) : after.ActiveBuffCount), true);
            AppendRaw(builder, "buffTypesAfter", string.IsNullOrWhiteSpace(after == null ? string.Empty : after.BuffTypesJson) ? "[]" : after.BuffTypesJson, true);
            AppendRaw(builder, "itemStackChangeObservable", BoolRaw(before != null && after != null && after.ItemStack != before.ItemStack), true);
            AppendRaw(builder, "inventoryChangeObservable", BoolRaw(before != null && after != null && (after.ItemStack != before.ItemStack || after.ItemType != before.ItemType)), true);
            AppendRaw(builder, "observableChange", BoolRaw(HasObservableSuccess(before, after)), true);
            AppendString(builder, "message", message ?? string.Empty, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string GetThresholdRaw(InputActionExecution execution)
        {
            var threshold = GetMetadataInt(execution, "ThresholdPercent", 0);
            return threshold <= 0 ? "null" : threshold.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetBuffTypesBeforeRaw(InputActionExecution execution, ItemUseVerificationState before)
        {
            if (before != null && !string.IsNullOrWhiteSpace(before.BuffTypesJson))
            {
                return before.BuffTypesJson;
            }

            var raw = GetMetadataString(execution, "BuffTypesBeforeJson", "[]");
            return string.IsNullOrWhiteSpace(raw) ? "[]" : raw;
        }

        private static bool GetMetadataBool(InputActionExecution execution, string key, bool fallback)
        {
            bool value;
            return bool.TryParse(GetMetadataString(execution, key, string.Empty), out value) ? value : fallback;
        }

        private static void SetState(InputActionExecution execution, string key, string value)
        {
            if (execution == null || execution.State == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            execution.State[key] = value ?? string.Empty;
        }

        private static bool GetStateBool(InputActionExecution execution, string key, bool fallback)
        {
            if (execution == null || execution.State == null)
            {
                return fallback;
            }

            string value;
            return execution.State.TryGetValue(key, out value) && bool.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static int Percent(int current, int max)
        {
            if (max <= 0)
            {
                return 0;
            }

            return (int)Math.Floor((current * 100.0d) / max);
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":\"").Append(EscapeJson(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
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
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
