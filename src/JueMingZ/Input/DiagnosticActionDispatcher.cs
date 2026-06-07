using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Input
{
    public static class DiagnosticActionDispatcher
    {
        // Diagnostic UI/hotkeys either update local diagnostics/config or enqueue requests;
        // they must not execute Terraria input or state mutations directly.
        public static void ToggleDiagnosticInput(DiagnosticActionSource source)
        {
            ConfigService.AppSettings.EnableDiagnosticInputTests = !ConfigService.AppSettings.EnableDiagnosticInputTests;
            ConfigService.SaveAll();
            var enabled = ConfigService.AppSettings.EnableDiagnosticInputTests;
            var message = enabled ? "诊断输入已开启。" : "诊断输入已关闭。";
            MarkSource(source);
            Logger.Info("DiagnosticActionDispatcher", GetSourceLabel(source) + ": " + message);

            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                IsButton(source) ? "DiagnosticsButton.ToggleDiagnosticInput" : "DiagnosticInputTests.Toggle",
                "Diagnostic",
                GetHotkey(source),
                DiagnosticResultCode.Succeeded.ToString(),
                DiagnosticResultCode.Succeeded.ToString(),
                message,
                0,
                "{}",
                "{\"diagnosticInputTests\":" + (enabled ? "true" : "false") + "}",
                "{\"submitted\":false}",
                GetSourceKind(source),
                GetSourceUi(source),
                GetButtonId(source),
                GetButtonLabel(source));

            if (IsButton(source))
            {
                RecordClick(source, DiagnosticResultCode.Succeeded, "已点击按钮：" + GetButtonLabel(source) + "，已切换诊断输入。", false, Guid.Empty);
            }
        }

        public static void ChangeDiagnosticTestSlot(int delta, GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            if (!EnsureDiagnosticEnabled(source))
            {
                return;
            }

            var oldSlot = ConfigService.AppSettings.DiagnosticInputTestSlot;
            var newSlot = oldSlot + delta;
            if (newSlot < 0)
            {
                newSlot = 9;
            }
            else if (newSlot > 9)
            {
                newSlot = 0;
            }

            ConfigService.AppSettings.DiagnosticInputTestSlot = newSlot;
            ConfigService.SaveAll();
            var info = DiagnosticHotbarInfo.FromSnapshot(snapshot, newSlot);
            var message = "测试快捷栏已切换到第 " + (newSlot + 1) + " 格：" + info.ItemDisplay + "。";
            MarkSource(source);
            Logger.Info("DiagnosticActionDispatcher", "Diagnostic test hotbar changed to 第 " + (newSlot + 1) + " 格: " + info.ItemDisplay + ".");

            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                IsButton(source) ? "DiagnosticsButton.ChangeTestSlot" : "DiagnosticTestSlot.Change",
                "Diagnostic",
                GetHotkey(source),
                DiagnosticResultCode.Succeeded.ToString(),
                DiagnosticResultCode.Succeeded.ToString(),
                message,
                0,
                "{\"oldSlot\":" + oldSlot.ToString(CultureInfo.InvariantCulture) +
                    ",\"oldSlotDisplay\":" + (oldSlot + 1).ToString(CultureInfo.InvariantCulture) + "}",
                "{\"newSlot\":" + newSlot.ToString(CultureInfo.InvariantCulture) +
                    ",\"newSlotDisplay\":" + (newSlot + 1).ToString(CultureInfo.InvariantCulture) +
                    ",\"itemName\":\"" + EscapeJson(info.ItemName) + "\",\"stack\":" + info.ItemStack.ToString(CultureInfo.InvariantCulture) + "}",
                "{\"submitted\":false}",
                GetSourceKind(source),
                GetSourceUi(source),
                GetButtonId(source),
                GetButtonLabel(source));

            if (IsButton(source))
            {
                RecordClick(source, DiagnosticResultCode.Succeeded, "已点击按钮：" + GetButtonLabel(source) + "，已切换测试快捷栏。", false, Guid.Empty);
            }
        }

        public static void EnqueueDiagnosticNoop(InputActionQueue queue, DiagnosticActionSource source)
        {
            if (!EnsureDiagnosticEnabled(source))
            {
                return;
            }

            var request = InputActionRequest.CreateDiagnosticNoop("diagnostics." + GetSourceKind(source).ToLowerInvariant(), "DiagnosticNoop test");
            AddSourceMetadata(request, source, IsButton(source) ? "Button.DiagnosticNoop" : "CtrlAltJ.DiagnosticNoop");
            Enqueue(queue, request, source, "空动作", "已点击按钮：空动作，已提交 DiagnosticNoop 动作。");
        }

        public static void EnqueueSelectHotbarSlot(InputActionQueue queue, GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            if (!EnsureDiagnosticEnabled(source) || !EnsureInWorld(snapshot, source))
            {
                return;
            }

            var slot = ConfigService.AppSettings.DiagnosticInputTestSlot;
            var request = CreateRequest(InputActionKind.SelectHotbarSlot, "SelectHotbarSlot test");
            request.Metadata["Slot"] = slot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["RestoreAfterTicks"] = "30";
            AddSourceMetadata(request, source, IsButton(source) ? "Button.SelectHotbarSlot" : "CtrlAltK.SelectHotbarSlot");
            Enqueue(queue, request, source, "切到测试格并恢复", "已点击按钮：切到测试格并恢复，已提交 SelectHotbarSlot 动作。");
        }

        public static void EnqueueUseSelectedItem(InputActionQueue queue, GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            if (!EnsureDiagnosticEnabled(source) || !EnsureInWorld(snapshot, source))
            {
                return;
            }

            var request = CreateRequest(InputActionKind.ItemUse, "UseSelectedItem test");
            request.Metadata["PressTicks"] = "2";
            request.Metadata["ReleaseTicks"] = "1";
            AddSourceMetadata(request, source, IsButton(source) ? "Button.UseSelectedItem" : "CtrlAltL.UseSelectedItem");
            Enqueue(queue, request, source, "使用手上物品", "已点击按钮：使用手上物品，已提交 UseSelectedItem 动作。");
        }

        public static void EnqueueUseHotbarItem(InputActionQueue queue, GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            if (!EnsureDiagnosticEnabled(source) || !EnsureInWorld(snapshot, source))
            {
                return;
            }

            var slot = ConfigService.AppSettings.DiagnosticInputTestSlot;
            var request = CreateRequest(InputActionKind.UseHotbarItem, "UseHotbarItem test");
            request.Metadata["Slot"] = slot.ToString(CultureInfo.InvariantCulture);
            request.Timeout = TimeSpan.FromSeconds(5);
            AddSourceMetadata(request, source, IsButton(source) ? "Button.UseHotbarItem" : "CtrlAltU.UseHotbarItem");
            Enqueue(queue, request, source, "使用测试格物品", "已点击按钮：使用测试格物品，已提交 UseHotbarItem 动作。");
        }

        public static void EnqueueQuickHeal(InputActionQueue queue, GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            EnqueueWorldAction(queue, snapshot, source, InputActionKind.QuickHeal, IsButton(source) ? "Button.QuickHeal" : "Hotkey.QuickHeal", "QuickHeal 回血", "已点击按钮：QuickHeal 回血，已提交 QuickHeal 动作。");
        }

        public static void EnqueueQuickMana(InputActionQueue queue, GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            EnqueueWorldAction(queue, snapshot, source, InputActionKind.QuickMana, IsButton(source) ? "Button.QuickMana" : "Hotkey.QuickMana", "QuickMana 回蓝", "已点击按钮：QuickMana 回蓝，已提交 QuickMana 动作。");
        }

        public static void EnqueueQuickBuff(InputActionQueue queue, GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            EnqueueWorldAction(queue, snapshot, source, InputActionKind.QuickBuff, IsButton(source) ? "Button.QuickBuff" : "Hotkey.QuickBuff", "QuickBuff 增益", "已点击按钮：QuickBuff 增益，已提交 QuickBuff 动作。");
        }

        public static void EnqueueQuickBuffOnce(InputActionQueue queue, GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            EnqueueWorldAction(
                queue,
                snapshot,
                source,
                InputActionKind.QuickBuff,
                IsButton(source) ? "Button.QuickBuffOnce" : "Hotkey.QuickBuff",
                "QuickBuff once",
                "QuickBuff once queued; this is equivalent to vanilla B and may drink multiple available buff potions.");
        }

        public static void RefreshBuffPotionCandidates(DiagnosticActionSource source)
        {
            if (!EnsureDiagnosticEnabled(source))
            {
                return;
            }

            var scan = BuffPotionCatalog.RefreshCandidates();
            var resultCode = scan.PlayerAvailable ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.BlockedByEnvironment;
            var message = scan.PlayerAvailable
                ? "Buff potion candidates refreshed: " + scan.Candidates.Count.ToString(CultureInfo.InvariantCulture) + "."
                : "Buff potion candidate scan failed: " + (string.IsNullOrWhiteSpace(scan.Error) ? scan.Message : scan.Error);
            MarkSource(source);
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                IsButton(source) ? "Button.BuffPotion.RefreshCandidates" : "BuffPotion.ScanCandidates",
                "BuffPotion.ScanCandidates",
                GetHotkey(source),
                resultCode.ToString(),
                resultCode.ToString(),
                message,
                0,
                "{}",
                BuildBuffPotionScanAfterJson(scan),
                "{\"submitted\":false}",
                GetSourceKind(source),
                GetSourceUi(source),
                GetButtonId(source),
                GetButtonLabel(source));

            if (IsButton(source))
            {
                RecordClick(source, resultCode, message, false, Guid.Empty);
            }
        }

        public static void MoveBuffPotionCandidate(int delta, DiagnosticActionSource source)
        {
            if (!EnsureDiagnosticEnabled(source))
            {
                return;
            }

            var selected = BuffPotionDiagnostics.MoveSelectedCandidate(delta);
            var resultCode = selected == null ? DiagnosticResultCode.NotApplicable : DiagnosticResultCode.Succeeded;
            var message = selected == null
                ? "No buff potion candidate is available; refresh candidates first."
                : "Selected buff potion candidate: " + selected.ItemName + " -> " + selected.BuffName + ".";
            MarkSource(source);
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                delta < 0 ? "Button.BuffPotion.PrevCandidate" : "Button.BuffPotion.NextCandidate",
                "BuffPotion.SelectCandidate",
                GetHotkey(source),
                resultCode.ToString(),
                resultCode.ToString(),
                message,
                0,
                "{}",
                BuildBuffPotionCandidateJson(selected),
                "{\"submitted\":false}",
                GetSourceKind(source),
                GetSourceUi(source),
                GetButtonId(source),
                GetButtonLabel(source));

            if (IsButton(source))
            {
                RecordClick(source, resultCode, message, false, Guid.Empty);
            }
        }

        public static void AddSelectedBuffPotionToWhitelist(DiagnosticActionSource source)
        {
            ChangeBuffPotionWhitelist(source, "Button.BuffPotion.AddWhitelist", "add");
        }

        public static void RemoveSelectedBuffPotionFromWhitelist(DiagnosticActionSource source)
        {
            ChangeBuffPotionWhitelist(source, "Button.BuffPotion.RemoveWhitelist", "remove");
        }

        public static void ClearBuffPotionWhitelist(DiagnosticActionSource source)
        {
            if (!EnsureDiagnosticEnabled(source))
            {
                return;
            }

            var removed = BuffPotionWhitelistService.Clear();
            var message = "Buff potion whitelist cleared; removed " + removed.ToString(CultureInfo.InvariantCulture) + " entries.";
            MarkSource(source);
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Button.BuffPotion.ClearWhitelist",
                "BuffPotion.WhitelistChanged",
                GetHotkey(source),
                DiagnosticResultCode.Succeeded.ToString(),
                DiagnosticResultCode.Succeeded.ToString(),
                message,
                0,
                "{}",
                BuildBuffPotionWhitelistAfterJson("clear", null),
                "{\"submitted\":false}",
                GetSourceKind(source),
                GetSourceUi(source),
                GetButtonId(source),
                GetButtonLabel(source));

            if (IsButton(source))
            {
                RecordClick(source, DiagnosticResultCode.Succeeded, message, false, Guid.Empty);
            }
        }

        public static void EnqueueBuffPotionUseSelectedOnce(InputActionQueue queue, GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            if (!EnsureDiagnosticEnabled(source) || !EnsureInWorld(snapshot, source))
            {
                return;
            }

            var selected = BuffPotionDiagnostics.GetSelectedCandidate();
            if (selected == null)
            {
                var message = "No selected buff potion candidate; refresh candidates first.";
                MarkSource(source);
                RecordClick(source, DiagnosticResultCode.NotApplicable, message, false, Guid.Empty);
                return;
            }

            var request = CreateRequest(InputActionKind.BuffPotionDirectUse, "Use selected buff potion once");
            request.SourceFeatureId = "buff.direct_potion_use";
            request.Timeout = TimeSpan.FromSeconds(3);
            request.Metadata["ExecutionMode"] = ActionExecutionModes.DirectLocalBuffPotion;
            request.Metadata["SourceContainer"] = selected.SourceContainer;
            request.Metadata["SourceSlot"] = selected.SourceSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["ItemType"] = selected.ItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["ItemName"] = selected.ItemName ?? string.Empty;
            request.Metadata["BuffType"] = selected.BuffType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["BuffName"] = selected.BuffName ?? string.Empty;
            request.Metadata["BuffTime"] = selected.BuffTime.ToString(CultureInfo.InvariantCulture);
            request.Metadata["SelectedCandidateIndex"] = BuffPotionDiagnostics.GetSnapshot().SelectedCandidateIndex.ToString(CultureInfo.InvariantCulture);
            AddSourceMetadata(request, source, IsButton(source) ? "Button.BuffPotion.UseSelectedOnce" : "BuffPotion.UseSelectedOnce");
            Enqueue(queue, request, source, "BuffPotionDirectUse", "Selected buff potion queued for one controlled local use.");
        }

        public static void RecordUnsupportedButton(DiagnosticActionSource source)
        {
            MarkSource(source);
            const string message = "This button is connected, but no executor is implemented for it yet.";
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "DiagnosticsButton.Unsupported",
                "Diagnostic",
                GetHotkey(source),
                DiagnosticResultCode.NotImplemented.ToString(),
                DiagnosticResultCode.NotImplemented.ToString(),
                message,
                0,
                "{}",
                "{}",
                "{\"submitted\":false}",
                GetSourceKind(source),
                GetSourceUi(source),
                GetButtonId(source),
                GetButtonLabel(source));

            if (IsButton(source))
            {
                RecordClick(source, DiagnosticResultCode.NotImplemented, message, false, Guid.Empty);
            }
        }

        public static void EnqueueMouseTargetDryRun(InputActionQueue queue, GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            EnqueueWorldAction(queue, snapshot, source, InputActionKind.MouseTargetDryRun, IsButton(source) ? "Button.MouseTargetDryRun" : "CtrlAltT.MouseTargetDryRun", "鼠标干跑", "已点击按钮：鼠标干跑，已提交 MouseTargetDryRun 动作。");
        }

        public static void ToggleAutoHeal(DiagnosticActionSource source)
        {
            var enabled = AutoRecoveryService.ToggleAutoHeal();
            RecordAutoRecoveryToggle(source, "AutoRecovery.ToggleAutoHeal", "AutoHeal", enabled, "自动回血");
        }

        public static void ToggleAutoMana(DiagnosticActionSource source)
        {
            var enabled = AutoRecoveryService.ToggleAutoMana();
            RecordAutoRecoveryToggle(source, "AutoRecovery.ToggleAutoMana", "AutoMana", enabled, "自动回蓝");
        }

        public static void ToggleAutoBuff(DiagnosticActionSource source)
        {
            var enabled = AutoRecoveryService.ToggleAutoBuff();
            RecordAutoRecoveryToggle(source, "AutoRecovery.ToggleAutoBuff", "AutoBuff", enabled, "自动增益");
        }

        private static void ChangeBuffPotionWhitelist(DiagnosticActionSource source, string scenario, string operation)
        {
            if (!EnsureDiagnosticEnabled(source))
            {
                return;
            }

            var selected = BuffPotionDiagnostics.GetSelectedCandidate();
            string message;
            bool changed;
            if (string.Equals(operation, "add", StringComparison.OrdinalIgnoreCase))
            {
                changed = BuffPotionWhitelistService.Add(selected, out message);
            }
            else
            {
                changed = BuffPotionWhitelistService.Remove(selected, out message);
            }

            var resultCode = changed ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable;
            MarkSource(source);
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                scenario,
                "BuffPotion.WhitelistChanged",
                GetHotkey(source),
                resultCode.ToString(),
                resultCode.ToString(),
                message,
                0,
                BuildBuffPotionCandidateJson(selected),
                BuildBuffPotionWhitelistAfterJson(operation, selected),
                "{\"submitted\":false}",
                GetSourceKind(source),
                GetSourceUi(source),
                GetButtonId(source),
                GetButtonLabel(source));

            if (IsButton(source))
            {
                RecordClick(source, resultCode, message, false, Guid.Empty);
            }
        }

        private static void EnqueueWorldAction(
            InputActionQueue queue,
            GameStateSnapshot snapshot,
            DiagnosticActionSource source,
            InputActionKind kind,
            string scenario,
            string label,
            string buttonMessage)
        {
            if (!EnsureDiagnosticEnabled(source) || !EnsureInWorld(snapshot, source))
            {
                return;
            }

            var request = CreateRequest(kind, label + " test");
            AddSourceMetadata(request, source, scenario);
            Enqueue(queue, request, source, label, buttonMessage);
        }

        private static void Enqueue(InputActionQueue queue, InputActionRequest request, DiagnosticActionSource source, string label, string buttonMessage)
        {
            if (queue == null || request == null)
            {
                RecordClick(source, DiagnosticResultCode.Failed, "无法提交动作：动作队列不可用。", false, Guid.Empty);
                return;
            }

            // ACTION_QUEUE_DIRECT_ENQUEUE_EXCEPTION: diagnostic button path; owner=diagnostics; migrate_after=02 no-op unless diagnostics admission testing is requested.
            var requestId = queue.Enqueue(request);
            MarkSource(source);
            Logger.Info("DiagnosticActionDispatcher", GetSourceLabel(source) + ": enqueued " + request.Kind + ".");
            if (IsButton(source))
            {
                RecordClick(source, DiagnosticResultCode.Queued, buttonMessage, true, requestId);
            }
        }

        private static InputActionRequest CreateRequest(InputActionKind kind, string description)
        {
            return new InputActionRequest
            {
                Kind = kind,
                Priority = InputActionPriority.Normal,
                SourceFeatureId = "diagnostics.ui",
                Description = description ?? string.Empty,
                Timeout = TimeSpan.FromSeconds(3)
            };
        }

        private static bool EnsureDiagnosticEnabled(DiagnosticActionSource source)
        {
            if (ConfigService.AppSettings.EnableDiagnosticInputTests)
            {
                return true;
            }

            const string message = "诊断输入关闭，请先点击“开启诊断”。";
            MarkSource(source);
            if (IsButton(source))
            {
                RecordClick(source, DiagnosticResultCode.BlockedByEnvironment, message, false, Guid.Empty);
            }
            else if (LogThrottle.ShouldLog("diagnostic-input-disabled-" + GetSourceLabel(source), TimeSpan.FromSeconds(2)))
            {
                DiagnosticActionRecorder.RecordCustomEvent(
                    Guid.Empty,
                    "DiagnosticInputTests.Disabled",
                    "Diagnostic",
                    GetHotkey(source),
                    DiagnosticResultCode.BlockedByEnvironment.ToString(),
                    DiagnosticResultCode.BlockedByEnvironment.ToString(),
                    message,
                    0,
                    "{}",
                    "{}",
                    "{\"submitted\":false}",
                    GetSourceKind(source),
                    GetSourceUi(source),
                    GetButtonId(source),
                    GetButtonLabel(source));
            }

            return false;
        }

        private static bool EnsureInWorld(GameStateSnapshot snapshot, DiagnosticActionSource source)
        {
            if (snapshot != null && snapshot.IsInWorld)
            {
                return true;
            }

            const string message = "请先进入世界。";
            MarkSource(source);
            if (IsButton(source))
            {
                RecordClick(source, DiagnosticResultCode.BlockedByEnvironment, message, false, Guid.Empty);
            }

            return false;
        }

        private static void RecordClick(DiagnosticActionSource source, DiagnosticResultCode resultCode, string message, bool submitted, Guid requestId)
        {
            if (IsButton(source))
            {
                DiagnosticInteractionDiagnostics.RecordButtonOutcome(
                    GetButtonId(source),
                    GetButtonLabel(source),
                    resultCode.ToString(),
                    message);
            }

            DiagnosticActionRecorder.RecordCustomEvent(
                requestId,
                "DiagnosticsButton.Click",
                "Diagnostic",
                GetHotkey(source),
                resultCode.ToString(),
                resultCode.ToString(),
                message,
                0,
                "{}",
                "{}",
                BuildButtonClickVerificationJson(source, submitted),
                GetSourceKind(source),
                GetSourceUi(source),
                GetButtonId(source),
                GetButtonLabel(source));
        }

        private static void RecordAutoRecoveryToggle(DiagnosticActionSource source, string scenario, string mode, bool enabled, string label)
        {
            MarkSource(source);
            var state = AutoRecoveryService.GetStateSnapshot();
            var message = BuildAutoRecoveryToggleMessage(mode, enabled, label);
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                scenario,
                "AutoRecovery",
                GetHotkey(source),
                DiagnosticResultCode.Succeeded.ToString(),
                DiagnosticResultCode.Succeeded.ToString(),
                message,
                0,
                "{}",
                BuildAutoRecoveryToggleAfterJson(mode, enabled, state),
                "{\"submitted\":false,\"source\":\"F5DiagnosticsOverlay\"}",
                GetSourceKind(source),
                GetSourceUi(source),
                GetButtonId(source),
                GetButtonLabel(source));

            if (IsButton(source))
            {
                RecordClick(source, DiagnosticResultCode.Succeeded, "Button clicked: " + GetButtonLabel(source) + ", " + message, false, Guid.Empty);
            }
        }

        private static string BuildAutoRecoveryToggleMessage(string mode, bool enabled, string label)
        {
            if (!enabled)
            {
                return label + " disabled.";
            }

            if (string.Equals(mode, "AutoHeal", StringComparison.OrdinalIgnoreCase))
            {
                return label + " enabled; automatic action selects a recovery potion from inventory.";
            }

            if (string.Equals(mode, "AutoMana", StringComparison.OrdinalIgnoreCase))
            {
                return label + " enabled; automatic action uses an inventory mana potion when the selected mana weapon cannot be used with current mana and mana can be restored.";
            }

            if (string.Equals(mode, "AutoBuff", StringComparison.OrdinalIgnoreCase))
            {
                return label + " enabled; whitelist strategy is active and will not immediately QuickBuff.";
            }

            return label + " enabled.";
        }

        private static void AddSourceMetadata(InputActionRequest request, DiagnosticActionSource source, string scenario)
        {
            if (request == null)
            {
                return;
            }

            request.Metadata["Scenario"] = scenario ?? string.Empty;
            request.Metadata["SourceKind"] = GetSourceKind(source);
            request.Metadata["SourceUi"] = GetSourceUi(source);
            request.Metadata["ButtonId"] = GetButtonId(source);
            request.Metadata["ButtonLabel"] = GetButtonLabel(source);
            request.Metadata["SourceHotkey"] = GetHotkey(source);
            request.Metadata["HitTestMode"] = GetHitTestMode(source);
            request.Metadata["ClickSource"] = GetClickSource(source);
            request.Metadata["HitTestX"] = GetHitTestX(source).ToString(CultureInfo.InvariantCulture);
            request.Metadata["HitTestY"] = GetHitTestY(source).ToString(CultureInfo.InvariantCulture);
            request.Metadata["HitTestConflict"] = GetHitTestConflict(source) ? "true" : "false";
            request.Metadata["CandidateHits"] = GetCandidateHits(source);
            request.Metadata["VisualRectX"] = GetVisualRectX(source).ToString(CultureInfo.InvariantCulture);
            request.Metadata["VisualRectY"] = GetVisualRectY(source).ToString(CultureInfo.InvariantCulture);
            request.Metadata["VisualRectWidth"] = GetVisualRectWidth(source).ToString(CultureInfo.InvariantCulture);
            request.Metadata["VisualRectHeight"] = GetVisualRectHeight(source).ToString(CultureInfo.InvariantCulture);
            request.Metadata["HitRectX"] = GetHitRectX(source).ToString(CultureInfo.InvariantCulture);
            request.Metadata["HitRectY"] = GetHitRectY(source).ToString(CultureInfo.InvariantCulture);
            request.Metadata["HitRectWidth"] = GetHitRectWidth(source).ToString(CultureInfo.InvariantCulture);
            request.Metadata["HitRectHeight"] = GetHitRectHeight(source).ToString(CultureInfo.InvariantCulture);
            request.Metadata["UiWindow"] = GetUiWindow(source);
            request.Metadata["UiElementId"] = GetUiElementId(source);
            request.Metadata["MouseCaptured"] = GetMouseCaptured(source) ? "true" : "false";
        }

        private static void MarkSource(DiagnosticActionSource source)
        {
            if (IsButton(source))
            {
                DiagnosticInteractionDiagnostics.RecordButton(GetButtonId(source), GetButtonLabel(source));
            }
            else
            {
                DiagnosticInteractionDiagnostics.RecordHotkey();
            }
        }

        private static bool IsButton(DiagnosticActionSource source)
        {
            return source != null && string.Equals(source.Kind, "Button", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSourceKind(DiagnosticActionSource source)
        {
            return source == null || string.IsNullOrWhiteSpace(source.Kind) ? "Unknown" : source.Kind;
        }

        private static string GetHotkey(DiagnosticActionSource source)
        {
            return source == null ? string.Empty : source.Hotkey ?? string.Empty;
        }

        private static string GetSourceUi(DiagnosticActionSource source)
        {
            return source == null ? string.Empty : source.Ui ?? string.Empty;
        }

        private static string GetButtonId(DiagnosticActionSource source)
        {
            return source == null ? string.Empty : source.ButtonId ?? string.Empty;
        }

        private static string GetButtonLabel(DiagnosticActionSource source)
        {
            return source == null ? string.Empty : source.ButtonLabel ?? string.Empty;
        }

        private static string GetHitTestMode(DiagnosticActionSource source)
        {
            return source == null ? string.Empty : source.HitTestMode ?? string.Empty;
        }

        private static int GetHitTestX(DiagnosticActionSource source)
        {
            return source == null ? -1 : source.HitTestX;
        }

        private static int GetHitTestY(DiagnosticActionSource source)
        {
            return source == null ? -1 : source.HitTestY;
        }

        private static bool GetHitTestConflict(DiagnosticActionSource source)
        {
            return source != null && source.HitTestConflict;
        }

        private static string GetCandidateHits(DiagnosticActionSource source)
        {
            return source == null ? string.Empty : source.CandidateHits ?? string.Empty;
        }

        private static string GetClickSource(DiagnosticActionSource source)
        {
            return source == null ? string.Empty : source.ClickSource ?? string.Empty;
        }

        private static int GetTerrariaMouseX(DiagnosticActionSource source)
        {
            return source == null ? -1 : source.TerrariaMouseX;
        }

        private static int GetTerrariaMouseY(DiagnosticActionSource source)
        {
            return source == null ? -1 : source.TerrariaMouseY;
        }

        private static int GetOsClientMouseX(DiagnosticActionSource source)
        {
            return source == null ? -1 : source.OsClientMouseX;
        }

        private static int GetOsClientMouseY(DiagnosticActionSource source)
        {
            return source == null ? -1 : source.OsClientMouseY;
        }

        private static int GetVisualRectX(DiagnosticActionSource source)
        {
            return source == null ? -1 : source.VisualRectX;
        }

        private static int GetVisualRectY(DiagnosticActionSource source)
        {
            return source == null ? -1 : source.VisualRectY;
        }

        private static int GetVisualRectWidth(DiagnosticActionSource source)
        {
            return source == null ? 0 : source.VisualRectWidth;
        }

        private static int GetVisualRectHeight(DiagnosticActionSource source)
        {
            return source == null ? 0 : source.VisualRectHeight;
        }

        private static int GetHitRectX(DiagnosticActionSource source)
        {
            return source == null ? -1 : source.HitRectX;
        }

        private static int GetHitRectY(DiagnosticActionSource source)
        {
            return source == null ? -1 : source.HitRectY;
        }

        private static int GetHitRectWidth(DiagnosticActionSource source)
        {
            return source == null ? 0 : source.HitRectWidth;
        }

        private static int GetHitRectHeight(DiagnosticActionSource source)
        {
            return source == null ? 0 : source.HitRectHeight;
        }

        private static string GetUiWindow(DiagnosticActionSource source)
        {
            return source == null ? string.Empty : source.UiWindow ?? string.Empty;
        }

        private static string GetUiElementId(DiagnosticActionSource source)
        {
            return source == null ? string.Empty : source.UiElementId ?? string.Empty;
        }

        private static bool GetMouseCaptured(DiagnosticActionSource source)
        {
            return source != null && source.MouseCaptured;
        }

        private static string BuildButtonClickVerificationJson(DiagnosticActionSource source, bool submitted)
        {
            return "{" +
                   "\"clicked\":true," +
                   "\"submitted\":" + (submitted ? "true" : "false") + "," +
                   "\"hitTestMode\":\"" + EscapeJson(GetHitTestMode(source)) + "\"," +
                   "\"hitTestConflict\":" + (GetHitTestConflict(source) ? "true" : "false") + "," +
                   "\"candidateHits\":\"" + EscapeJson(GetCandidateHits(source)) + "\"," +
                   "\"hitTestX\":" + GetHitTestX(source).ToString(CultureInfo.InvariantCulture) + "," +
                   "\"hitTestY\":" + GetHitTestY(source).ToString(CultureInfo.InvariantCulture) + "," +
                   "\"visualRect\":" + BuildRectJson(GetVisualRectX(source), GetVisualRectY(source), GetVisualRectWidth(source), GetVisualRectHeight(source)) + "," +
                   "\"hitRect\":" + BuildRectJson(GetHitRectX(source), GetHitRectY(source), GetHitRectWidth(source), GetHitRectHeight(source)) + "," +
                   "\"uiWindow\":\"" + EscapeJson(GetUiWindow(source)) + "\"," +
                   "\"uiElementId\":\"" + EscapeJson(GetUiElementId(source)) + "\"," +
                   "\"mouseCaptured\":" + (GetMouseCaptured(source) ? "true" : "false") + "," +
                   "\"clickSource\":\"" + EscapeJson(GetClickSource(source)) + "\"," +
                   "\"terrariaMouseX\":" + GetTerrariaMouseX(source).ToString(CultureInfo.InvariantCulture) + "," +
                   "\"terrariaMouseY\":" + GetTerrariaMouseY(source).ToString(CultureInfo.InvariantCulture) + "," +
                   "\"osClientMouseX\":" + GetOsClientMouseX(source).ToString(CultureInfo.InvariantCulture) + "," +
                   "\"osClientMouseY\":" + GetOsClientMouseY(source).ToString(CultureInfo.InvariantCulture) +
                   "}";
        }

        private static string BuildAutoRecoveryToggleAfterJson(string mode, bool enabled, AutoRecoveryState state)
        {
            return "{" +
                   "\"mode\":\"" + EscapeJson(mode) + "\"," +
                   "\"enabled\":" + (enabled ? "true" : "false") + "," +
                   "\"autoHealEnabled\":" + (state.AutoHealEnabled ? "true" : "false") + "," +
                   "\"autoManaEnabled\":" + (state.AutoManaEnabled ? "true" : "false") + "," +
                   "\"autoBuffEnabled\":" + (state.AutoBuffEnabled ? "true" : "false") + "," +
                   "\"autoHealThresholdPercent\":" + state.AutoHealThresholdPercent.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"autoManaThresholdPercent\":" + state.AutoManaThresholdPercent.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"autoHealCooldownTicks\":" + state.AutoHealCooldownTicks.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"autoManaCooldownTicks\":" + state.AutoManaCooldownTicks.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"autoBuffCooldownTicks\":" + state.AutoBuffCooldownTicks.ToString(CultureInfo.InvariantCulture) +
                   "}";
        }

        private static string BuildBuffPotionScanAfterJson(BuffPotionScanResult scan)
        {
            if (scan == null)
            {
                return "{" +
                       "\"candidateCount\":0," +
                       "\"playerAvailable\":false," +
                       "\"message\":\"\"," +
                       "\"error\":\"scan result unavailable\"," +
                       "\"networkMode\":\"Unknown\"," +
                       "\"voidBagScanned\":false," +
                       "\"unsupportedConflictCheck\":false" +
                       "}";
            }

            return "{" +
                   "\"candidateCount\":" + scan.Candidates.Count.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"playerAvailable\":" + (scan.PlayerAvailable ? "true" : "false") + "," +
                   "\"message\":\"" + EscapeJson(scan.Message) + "\"," +
                   "\"error\":\"" + EscapeJson(scan.Error) + "\"," +
                   "\"networkMode\":\"" + EscapeJson(scan.NetworkMode) + "\"," +
                   "\"voidBagScanned\":" + (scan.VoidBagScanned ? "true" : "false") + "," +
                   "\"unsupportedConflictCheck\":" + (scan.UnsupportedConflictCheck ? "true" : "false") +
                   "}";
        }

        private static string BuildBuffPotionCandidateJson(BuffPotionCandidate candidate)
        {
            if (candidate == null)
            {
                return "{" +
                       "\"sourceContainer\":\"\"," +
                       "\"sourceSlot\":-1," +
                       "\"itemType\":0," +
                       "\"itemName\":\"\"," +
                       "\"stack\":0," +
                       "\"buffType\":0," +
                       "\"buffName\":\"\"," +
                       "\"buffTime\":0," +
                       "\"isActive\":false," +
                       "\"isWhitelisted\":false," +
                       "\"canApply\":false," +
                       "\"skipReason\":\"NoSelectedCandidate\"" +
                       "}";
            }

            return "{" +
                   "\"sourceContainer\":\"" + EscapeJson(candidate.SourceContainer) + "\"," +
                   "\"sourceSlot\":" + candidate.SourceSlot.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"itemType\":" + candidate.ItemType.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"itemName\":\"" + EscapeJson(candidate.ItemName) + "\"," +
                   "\"stack\":" + candidate.Stack.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"buffType\":" + candidate.BuffType.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"buffName\":\"" + EscapeJson(candidate.BuffName) + "\"," +
                   "\"buffTime\":" + candidate.BuffTime.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"isActive\":" + (candidate.IsActive ? "true" : "false") + "," +
                   "\"isWhitelisted\":" + (candidate.IsWhitelisted ? "true" : "false") + "," +
                   "\"canApply\":" + (candidate.CanApply ? "true" : "false") + "," +
                   "\"skipReason\":\"" + EscapeJson(candidate.SkipReason) + "\"" +
                   "}";
        }

        private static string BuildBuffPotionWhitelistAfterJson(string operation, BuffPotionCandidate candidate)
        {
            return "{" +
                   "\"operation\":\"" + EscapeJson(operation) + "\"," +
                   "\"whitelistCount\":" + BuffPotionWhitelistService.Count.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"itemType\":" + (candidate == null ? "0" : candidate.ItemType.ToString(CultureInfo.InvariantCulture)) + "," +
                   "\"buffType\":" + (candidate == null ? "0" : candidate.BuffType.ToString(CultureInfo.InvariantCulture)) + "," +
                   "\"itemName\":\"" + EscapeJson(candidate == null ? string.Empty : candidate.ItemName) + "\"," +
                   "\"buffName\":\"" + EscapeJson(candidate == null ? string.Empty : candidate.BuffName) + "\"" +
                   "}";
        }

        private static string BuildRectJson(int x, int y, int width, int height)
        {
            return "{" +
                   "\"x\":" + x.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"y\":" + y.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"width\":" + width.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"height\":" + height.ToString(CultureInfo.InvariantCulture) +
                   "}";
        }

        private static string GetSourceLabel(DiagnosticActionSource source)
        {
            if (IsButton(source))
            {
                return "button " + GetButtonId(source);
            }

            return string.IsNullOrWhiteSpace(GetHotkey(source)) ? "unknown source" : GetHotkey(source);
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
