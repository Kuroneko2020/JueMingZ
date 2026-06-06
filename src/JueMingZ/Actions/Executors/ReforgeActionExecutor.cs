using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class ReforgeActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.Reforge; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var scenario = GetMetadataString(execution, ActionMetadataKeys.Scenario, string.Empty);
            if (!string.Equals(scenario, ScenarioNames.NpcQuickReforge, StringComparison.Ordinal))
            {
                return Finish(execution, startedUtc, InputActionStatus.NotImplemented, DiagnosticResultCode.NotImplemented, "Reforge only supports Npc.QuickReforge in this build.", null);
            }

            var targetPrefixes = ParsePrefixList(GetMetadataString(execution, "TargetPrefixes", string.Empty));
            if (targetPrefixes.Count <= 0)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Quick reforge request did not include target prefixes.", null);
            }

            QuickReforgeResult result;
            // Reforge must run through the vanilla NPC reforge path; matching a
            // target prefix never permits direct item prefix or inventory edits.
            var invoked = ReforgeCompat.TryQuickReforgeOnce(targetPrefixes, out result);
            if (invoked && result != null && result.ReforgeInvoked && result.MatchedTargetPrefix)
            {
                return Finish(execution, startedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, result.Message, result);
            }

            if (invoked && result != null && result.ReforgeInvoked)
            {
                return Finish(execution, startedUtc, InputActionStatus.AttemptedButUnverified, DiagnosticResultCode.AttemptedButUnverified, result.Message, result);
            }

            var message = result == null ? "Quick reforge failed." : result.Message;
            if (IsBlockedMessage(message))
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, message, result);
            }

            if (IsNotApplicableMessage(message))
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, message, result);
            }

            return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, message, result);
        }

        private InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            QuickReforgeResult result)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.NpcQuickReforge),
                InputActionKind.Reforge.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildBeforeJson(execution),
                BuildAfterJson(result, code.ToString(), message),
                BuildVerificationJson(result),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private static List<string> ParsePrefixList(string raw)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var parts = raw.Split(',');
            for (var index = 0; index < parts.Length; index++)
            {
                var part = parts[index];
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                var value = part.Trim();
                if (value.Length <= 0 || !seen.Add(value))
                {
                    continue;
                }

                result.Add(value);
            }

            return result;
        }

        private static bool IsBlockedMessage(string message)
        {
            return !string.IsNullOrWhiteSpace(message) &&
                   (message.IndexOf("not in reforge menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("reforge button not hovered", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsNotApplicableMessage(string message)
        {
            return !string.IsNullOrWhiteSpace(message) &&
                   (message.IndexOf("empty", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildBeforeJson(InputActionExecution execution)
        {
            return "{" +
                   "\"targetPrefixes\":\"" + EscapeJson(GetMetadataString(execution, "TargetPrefixes", string.Empty)) + "\"," +
                   "\"currentAffix\":\"" + EscapeJson(GetMetadataString(execution, "CurrentAffix", string.Empty)) + "\"" +
                   "}";
        }

        private static string BuildAfterJson(QuickReforgeResult result, string resultCode, string message)
        {
            return "{" +
                   "\"inReforgeMenu\":" + BoolRaw(result != null && result.InReforgeMenu) + "," +
                   "\"mouseReforge\":" + BoolRaw(result != null && result.MouseReforge) + "," +
                   "\"prefixBefore\":" + IntRaw(result == null ? 0 : result.PrefixBefore) + "," +
                   "\"prefixAfter\":" + IntRaw(result == null ? 0 : result.PrefixAfter) + "," +
                   "\"affixBefore\":\"" + EscapeJson(result == null ? string.Empty : result.AffixBefore) + "\"," +
                   "\"affixAfter\":\"" + EscapeJson(result == null ? string.Empty : result.AffixAfter) + "\"," +
                   "\"matchedPrefix\":\"" + EscapeJson(result == null ? string.Empty : result.MatchedPrefix) + "\"," +
                   "\"resultCode\":\"" + EscapeJson(resultCode) + "\"," +
                   "\"message\":\"" + EscapeJson(message) + "\"" +
                   "}";
        }

        private static string BuildVerificationJson(QuickReforgeResult result)
        {
            return "{" +
                   "\"reforgeInvoked\":" + BoolRaw(result != null && result.ReforgeInvoked) + "," +
                   "\"cooldownCleared\":" + BoolRaw(result != null && result.CooldownCleared) + "," +
                   "\"matchedTargetPrefix\":" + BoolRaw(result != null && result.MatchedTargetPrefix) +
                   "}";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
