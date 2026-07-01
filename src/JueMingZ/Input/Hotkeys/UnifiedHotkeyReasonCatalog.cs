using System;
using System.Text;
using JueMingZ.Config;

namespace JueMingZ.Input.Hotkeys
{
    // Single contract for unified hotkey reasons: update this file when adding
    // capture, save, conflict, or runtime gate reasons so UI copy and diagnostics stay aligned.
    public static class UnifiedHotkeyReasonCatalog
    {
        public const string ReservedKey = "reservedKey";
        public const string InvalidToken = "invalidToken";
        public const string UnsupportedToken = "unsupportedToken";
        public const string SaveFailed = "saveFailed";
        public const string EmptyBinding = "emptyBinding";
        public const string Cancelled = "cancelled";
        public const string ConflictWithPrefix = "conflictWith:";

        public static string NormalizeReasonCode(string resultCode)
        {
            var code = NormalizeRaw(resultCode);
            if (code.StartsWith(ConflictWithPrefix, StringComparison.Ordinal))
            {
                return code;
            }

            switch (code)
            {
                case "alreadyEmpty":
                    return EmptyBinding;
                case "duplicateModifier":
                case "missingPrimaryKey":
                case "tooManyPrimaryKeys":
                case "":
                    return InvalidToken;
                default:
                    return code;
            }
        }

        public static string NormalizeRuntimeReasonCode(string reason)
        {
            var code = NormalizeRaw(reason);
            if (code.Length <= 0)
            {
                return string.Empty;
            }

            if (code.StartsWith("terrariaTextInput:", StringComparison.Ordinal) ||
                string.Equals(code, "terrariaTextInput", StringComparison.Ordinal) ||
                string.Equals(code, "legacyTextInput", StringComparison.Ordinal) ||
                string.Equals(code, "chatOpen", StringComparison.Ordinal))
            {
                return UnifiedHotkeyRuntimeGate.TextInputFocused;
            }

            return NormalizeReasonCode(code);
        }

        public static string BuildCaptureCancelledMessage()
        {
            return "已取消录入";
        }

        public static string BuildUpdateMessage(UnifiedHotkeyBindingUpdateResult result)
        {
            if (result == null)
            {
                return "保存失败";
            }

            switch (result.ResultCode)
            {
                case "updated":
                    return "已保存 " + (result.Display ?? string.Empty);
                case "unchanged":
                    return "未变化";
                case "cleared":
                    return "已清除绑定";
                case "alreadyEmpty":
                    return "当前未绑定";
                case SaveFailed:
                    return "保存失败" + DetailSuffix(result.Message);
                default:
                    return BuildCaptureFailureMessage(result.ResultCode);
            }
        }

        public static string BuildCaptureFailureMessage(string resultCode)
        {
            var original = NormalizeRaw(resultCode);
            var normalized = NormalizeReasonCode(original);
            if (normalized.StartsWith(ConflictWithPrefix, StringComparison.Ordinal))
            {
                var owner = normalized.Substring(ConflictWithPrefix.Length);
                return "与 " +
                       (owner.Length <= 0 ? "其它决明快捷键" : owner) +
                       " 冲突";
            }

            switch (normalized)
            {
                case ReservedKey:
                    return "这个键保留给系统功能";
                case UnsupportedToken:
                    return "暂不支持这个按键";
                case InvalidToken:
                    return DescribeInvalidToken(original);
                case SaveFailed:
                    return "保存失败";
                case EmptyBinding:
                    return "当前未绑定";
                case Cancelled:
                    return BuildCaptureCancelledMessage();
                default:
                    return "录入失败";
            }
        }

        public static string BuildRuntimeGateMessage(string subject, string reason)
        {
            var safeSubject = string.IsNullOrWhiteSpace(subject) ? "快捷键" : subject.Trim();
            var safeReason = NormalizeRuntimeReasonCode(reason);
            return safeSubject + " 被阻止：" + DescribeRuntimeReason(safeReason);
        }

        public static bool IsUiGateReason(string reason)
        {
            var code = NormalizeRaw(reason);
            return string.Equals(code, UnifiedHotkeyRuntimeGate.TextInputFocused, StringComparison.Ordinal) ||
                   string.Equals(code, UnifiedHotkeyRuntimeGate.F5TextInputFocused, StringComparison.Ordinal) ||
                   string.Equals(code, UnifiedHotkeyRuntimeGate.ColorInputFocused, StringComparison.Ordinal) ||
                   string.Equals(code, UnifiedHotkeyRuntimeGate.NameInputFocused, StringComparison.Ordinal) ||
                   string.Equals(code, UnifiedHotkeyRuntimeGate.NpcChatOpen, StringComparison.Ordinal) ||
                   string.Equals(code, UnifiedHotkeyRuntimeGate.LegacyModalOpen, StringComparison.Ordinal) ||
                   string.Equals(code, "legacyModalActive", StringComparison.Ordinal) ||
                   string.Equals(code, "legacyUiActive", StringComparison.Ordinal) ||
                   string.Equals(code, "legacyUiVisible", StringComparison.Ordinal) ||
                   string.Equals(code, "legacyUiInteraction", StringComparison.Ordinal) ||
                   string.Equals(code, "legacyTextInput", StringComparison.Ordinal) ||
                   string.Equals(code, "hotkeyCaptureActive", StringComparison.Ordinal) ||
                   string.Equals(code, "searchItemSelection", StringComparison.Ordinal) ||
                   string.Equals(code, "terrariaTextInput", StringComparison.Ordinal) ||
                   string.Equals(code, "chatOpen", StringComparison.Ordinal) ||
                   string.Equals(code, "npcChat", StringComparison.Ordinal) ||
                   code.StartsWith("terrariaTextInput:", StringComparison.Ordinal);
        }

        public static bool IsEnvironmentGateReason(string reason)
        {
            var code = NormalizeRaw(reason);
            return string.Equals(code, "gameStateUnavailable", StringComparison.Ordinal) ||
                   string.Equals(code, "worldUnavailable", StringComparison.Ordinal) ||
                   string.Equals(code, "notInWorld", StringComparison.Ordinal) ||
                   string.Equals(code, UnifiedHotkeyRuntimeGate.MainMenu, StringComparison.Ordinal) ||
                   string.Equals(code, "notForeground", StringComparison.Ordinal) ||
                   string.Equals(code, "gameInputUnavailable", StringComparison.Ordinal);
        }

        public static string BuildDiagnosticMetadataJson(params string[] nameValuePairs)
        {
            if (nameValuePairs == null || nameValuePairs.Length < 2)
            {
                return "{}";
            }

            var builder = new StringBuilder();
            builder.Append("{");
            var wrote = false;
            for (var index = 0; index + 1 < nameValuePairs.Length; index += 2)
            {
                var name = nameValuePairs[index];
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (wrote)
                {
                    builder.Append(",");
                }

                AppendJsonString(builder, name, nameValuePairs[index + 1]);
                wrote = true;
            }

            builder.Append("}");
            return wrote ? builder.ToString() : "{}";
        }

        public static string EscapeJson(string value)
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

        private static void AppendJsonString(StringBuilder builder, string name, string value)
        {
            builder.Append("\"")
                .Append(EscapeJson(name))
                .Append("\":\"")
                .Append(EscapeJson(value ?? string.Empty))
                .Append("\"");
        }

        private static string BuildReasonLabel(string normalized, string original)
        {
            normalized = NormalizeRaw(normalized);
            original = NormalizeRaw(original);
            if (original.Length <= 0 ||
                string.Equals(normalized, original, StringComparison.Ordinal) ||
                normalized.StartsWith(ConflictWithPrefix, StringComparison.Ordinal))
            {
                return normalized.Length <= 0 ? InvalidToken : normalized;
            }

            return normalized + "(" + original + ")";
        }

        private static string DescribeInvalidToken(string original)
        {
            switch (NormalizeRaw(original))
            {
                case "duplicateModifier":
                    return "重复修饰键";
                case "missingPrimaryKey":
                    return "需要一个主键";
                case "tooManyPrimaryKeys":
                    return "主键过多";
                default:
                    return "不支持这个组合";
            }
        }

        private static string DescribeRuntimeReason(string reason)
        {
            switch (NormalizeRaw(reason))
            {
                case UnifiedHotkeyRuntimeGate.TextInputFocused:
                case "terrariaTextInput":
                case "legacyTextInput":
                case "chatOpen":
                    return "正在输入文字";
                case UnifiedHotkeyRuntimeGate.F5TextInputFocused:
                    return "F5 输入框正在编辑";
                case UnifiedHotkeyRuntimeGate.ColorInputFocused:
                    return "颜色输入框正在编辑";
                case UnifiedHotkeyRuntimeGate.NameInputFocused:
                    return "名称输入框正在编辑";
                case UnifiedHotkeyRuntimeGate.NpcChatOpen:
                case "npcChat":
                    return "NPC 对话正在打开";
                case UnifiedHotkeyRuntimeGate.LegacyModalOpen:
                case "legacyModalActive":
                    return "决明弹窗正在打开";
                case "legacyUiActive":
                case "legacyUiInteraction":
                    return "决明界面正在交互";
                case "legacyUiVisible":
                    return "决明界面正在显示";
                case "hotkeyCaptureActive":
                    return "快捷键录入正在进行";
                case "searchItemSelection":
                    return "搜索候选选择正在进行";
                case "notInWorld":
                case "worldUnavailable":
                    return "当前不在可用世界内";
                case UnifiedHotkeyRuntimeGate.MainMenu:
                    return "当前在主菜单";
                case "notForeground":
                    return "游戏窗口不在前台";
                case "gameInputUnavailable":
                    return "游戏输入当前不可用";
                default:
                    return "当前环境阻挡快捷键";
            }
        }

        private static string DetailSuffix(string detail)
        {
            return string.IsNullOrWhiteSpace(detail) ? string.Empty : "：" + detail.Trim();
        }

        private static string NormalizeRaw(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
