using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.AutoRecovery
{
    // Immediate auto-buff observes recent manual use from snapshots, then still queues controlled buff-potion work.
    public static partial class AutoRecoveryService
    {
        private static void DetectImmediateAutoBuffTriggers(AutoRecoverySettings settings, GameStateSnapshot snapshot, long tick)
        {
            if (settings == null || !settings.AutoBuffEnabled || snapshot == null || BuffPotionWhitelistService.Count <= 0)
            {
                return;
            }

            var shouldReadInventorySignature = ShouldReadImmediateAutoBuffInventorySignature(tick);
            var inventorySignature = shouldReadInventorySignature ? BuildInventorySignature(snapshot) : null;
            var buffSignature = BuildBuffSignature(snapshot.ActiveBuffs);
            lock (SyncRoot)
            {
                if (shouldReadInventorySignature)
                {
                    _lastInventorySignatureTick = tick;
                    if (!_hasLastInventorySignature)
                    {
                        _lastInventorySignature = inventorySignature;
                        _hasLastInventorySignature = true;
                        RequestImmediateAutoBuffReconcileLocked("InventoryChanged");
                    }
                    else if (!string.Equals(_lastInventorySignature, inventorySignature, StringComparison.Ordinal))
                    {
                        _lastInventorySignature = inventorySignature;
                        RequestImmediateAutoBuffReconcileLocked("InventoryChanged");
                    }
                }

                if (!_hasLastBuffSignature)
                {
                    _lastBuffSignature = buffSignature;
                    _hasLastBuffSignature = true;
                }
                else if (!string.Equals(_lastBuffSignature, buffSignature, StringComparison.Ordinal))
                {
                    var previousBuffSignature = _lastBuffSignature;
                    _lastBuffSignature = buffSignature;
                    var missingWhitelistedBuff = HasMissingWhitelistedBuff(snapshot.ActiveBuffs);
                    RecordLiveBuffStateChanged(previousBuffSignature, buffSignature, missingWhitelistedBuff);
                    if (missingWhitelistedBuff)
                    {
                        RequestImmediateAutoBuffReconcileLocked("BuffMissing");
                    }
                }
            }
        }

        private static bool ShouldReadImmediateAutoBuffInventorySignature(long tick)
        {
            lock (SyncRoot)
            {
                return !_hasLastInventorySignature ||
                       _lastInventorySignatureTick == ForceDueTick ||
                       tick <= 0 ||
                       tick < _lastInventorySignatureTick ||
                       tick - _lastInventorySignatureTick >= ImmediateAutoBuffInventorySignatureIntervalTicks;
            }
        }

        private static bool TryProcessImmediateAutoBuff(InputActionQueue queue, AutoRecoverySettings settings, GameStateSnapshot snapshot, long tick, InputActionQueueFastState queueSnapshot)
        {
            if (queue == null || settings == null || !settings.AutoBuffEnabled || snapshot == null || snapshot.Player == null)
            {
                return false;
            }

            string triggerReason;
            lock (SyncRoot)
            {
                ExpireAutoBuffInflightLocked(tick);
                if (!_immediateBuffReconcileRequested)
                {
                    State.ImmediateBuffInflightCount = AutoBuffInflightItemTypes.Count;
                    return false;
                }

                triggerReason = string.IsNullOrWhiteSpace(_immediateBuffTriggerReason)
                    ? "Unknown"
                    : _immediateBuffTriggerReason;
                _immediateBuffReconcileRequested = false;
                _immediateBuffTriggerReason = string.Empty;
                State.ImmediateBuffReconcileRequested = false;
                State.ImmediateBuffTriggerReason = triggerReason;
            }

            var scan = BuffPotionCatalog.RefreshCandidates();
            RecordMissingWhitelistItemIfNeeded(scan, tick);
            var queuedCount = 0;
            var skippedCount = 0;
            var skippedReason = "None";
            var inflightCount = 0;
            var pendingCount = queueSnapshot == null ? 0 : queueSnapshot.PendingCount;

            if (scan != null && scan.Candidates != null)
            {
                var seen = new HashSet<int>();
                for (var index = 0; index < scan.Candidates.Count; index++)
                {
                    var candidate = scan.Candidates[index];
                    if (candidate == null || candidate.ItemType <= 0 || !candidate.IsWhitelisted)
                    {
                        continue;
                    }

                    if (!seen.Add(candidate.ItemType))
                    {
                        continue;
                    }

                    if (!candidate.CanApply)
                    {
                        skippedCount++;
                        skippedReason = FirstSkip(skippedReason, string.IsNullOrWhiteSpace(candidate.SkipReason) ? "AlreadyActiveOrConflict" : candidate.SkipReason);
                        continue;
                    }

                    string reserveSkipReason;
                    if (!TryReserveAutoBuffItem(candidate.ItemType, tick, triggerReason, out inflightCount, out reserveSkipReason))
                    {
                        skippedCount++;
                        skippedReason = FirstSkip(skippedReason, reserveSkipReason);
                        continue;
                    }

                    var decision = CreateAutoBuffDecision(settings, snapshot, candidate, "Immediate:" + triggerReason, true, triggerReason);
                    var requestId = EnqueueDecision(queue, decision, tick);
                    RecordImmediateEnqueue(decision, requestId, triggerReason, queuedCount + 1, pendingCount, inflightCount);
                    queuedCount++;
                }
            }

            if (queuedCount <= 0 && HasMissingWhitelistedItem(scan))
            {
                skippedReason = FirstSkip(skippedReason, "MissingItem");
            }

            lock (SyncRoot)
            {
                State.ImmediateBuffPendingCount = pendingCount + queuedCount;
                State.ImmediateBuffInflightCount = AutoBuffInflightItemTypes.Count;
                State.LastAutoBuffResult = queuedCount > 0
                    ? "Immediate AutoBuff queued " + queuedCount.ToString(CultureInfo.InvariantCulture) + " whitelisted potion(s)."
                    : "Immediate AutoBuff reconcile found no usable missing whitelisted potion.";
            }

            RecordImmediateReconcile(triggerReason, queuedCount, skippedCount, pendingCount, inflightCount, skippedReason);
            return queuedCount > 0;
        }

        private static bool TryReserveAutoBuffItem(int itemType, long tick, string triggerReason, out int inflightCount, out string skippedReason)
        {
            skippedReason = "None";
            lock (SyncRoot)
            {
                ExpireAutoBuffInflightLocked(tick);
                if (AutoBuffInflightItemTypes.Contains(itemType))
                {
                    inflightCount = AutoBuffInflightItemTypes.Count;
                    skippedReason = "Inflight";
                    return false;
                }

                long lastFailed;
                if (!IsBuffMissingTrigger(triggerReason) &&
                    AutoBuffLastFailedTicks.TryGetValue(itemType, out lastFailed) &&
                    tick - lastFailed < AutoBuffFailedRetryThrottleTicks)
                {
                    inflightCount = AutoBuffInflightItemTypes.Count;
                    skippedReason = "RetryThrottle";
                    return false;
                }

                AutoBuffInflightItemTypes.Add(itemType);
                AutoBuffInflightTicks[itemType] = tick;
                State.ImmediateBuffInflightCount = AutoBuffInflightItemTypes.Count;
                inflightCount = AutoBuffInflightItemTypes.Count;
                return true;
            }
        }

        private static void ExpireAutoBuffInflightLocked(long tick)
        {
            if (AutoBuffInflightItemTypes.Count <= 0)
            {
                return;
            }

            var expired = new List<int>();
            foreach (var itemType in AutoBuffInflightItemTypes)
            {
                long inflightTick;
                if (!AutoBuffInflightTicks.TryGetValue(itemType, out inflightTick) ||
                    tick - inflightTick >= AutoBuffInflightExpiryTicks)
                {
                    expired.Add(itemType);
                }
            }

            for (var index = 0; index < expired.Count; index++)
            {
                AutoBuffInflightItemTypes.Remove(expired[index]);
                AutoBuffInflightTicks.Remove(expired[index]);
            }
        }

        private static bool HasMissingWhitelistedBuff(IReadOnlyList<BuffSnapshot> activeBuffs)
        {
            var active = new HashSet<int>();
            if (activeBuffs != null)
            {
                for (var index = 0; index < activeBuffs.Count; index++)
                {
                    var buff = activeBuffs[index];
                    if (buff != null && buff.BuffType > 0)
                    {
                        active.Add(buff.BuffType);
                    }
                }
            }

            var settings = ConfigService.AppSettings;
            if (settings == null || settings.AutoBuffWhitelist == null)
            {
                return false;
            }

            for (var index = 0; index < settings.AutoBuffWhitelist.Count; index++)
            {
                var entry = settings.AutoBuffWhitelist[index];
                if (entry != null && entry.BuffType > 0 && !active.Contains(entry.BuffType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMissingWhitelistedItem(BuffPotionScanResult scan)
        {
            var live = new HashSet<int>();
            if (scan != null && scan.Candidates != null)
            {
                for (var index = 0; index < scan.Candidates.Count; index++)
                {
                    var candidate = scan.Candidates[index];
                    if (candidate != null && candidate.ItemType > 0)
                    {
                        live.Add(candidate.ItemType);
                    }
                }
            }

            var settings = ConfigService.AppSettings;
            if (settings == null || settings.AutoBuffWhitelist == null)
            {
                return false;
            }

            for (var index = 0; index < settings.AutoBuffWhitelist.Count; index++)
            {
                var entry = settings.AutoBuffWhitelist[index];
                if (entry != null && entry.ItemType > 0 && !live.Contains(entry.ItemType))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FirstSkip(string current, string next)
        {
            if (!string.IsNullOrWhiteSpace(current) && !string.Equals(current, "None", StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            return string.IsNullOrWhiteSpace(next) ? "None" : next;
        }

        private static bool IsAutoBuffScenario(string scenario)
        {
            return string.Equals(scenario, "AutoRecovery.AutoBuff", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(scenario, "AutoRecovery.AutoBuffImmediate", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSuccessfulAutoBuffResult(InputActionResult result)
        {
            return result != null && result.Status == InputActionStatus.Succeeded;
        }

        private static bool IsBuffMissingTrigger(string triggerReason)
        {
            return !string.IsNullOrWhiteSpace(triggerReason) &&
                   triggerReason.IndexOf("BuffMissing", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void RecordImmediateReconcile(string triggerReason, int queuedCount, int skippedCount, int pendingCount, int inflightCount, string skippedReason)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "BuffPotion.ImmediateReconcile",
                "BuffPotion",
                string.Empty,
                "Succeeded",
                queuedCount > 0 ? "Succeeded" : "NotApplicable",
                "Immediate AutoBuff reconcile completed.",
                0,
                "{" +
                    "\"immediateReconcileTriggered\":true," +
                    "\"triggerReason\":\"" + EscapeJson(triggerReason) + "\"," +
                    "\"whitelistCount\":" + BuffPotionWhitelistService.Count.ToString(CultureInfo.InvariantCulture) +
                "}",
                "{" +
                    "\"queuedCount\":" + queuedCount.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"skippedCount\":" + skippedCount.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"pendingCount\":" + pendingCount.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"inflightCount\":" + inflightCount.ToString(CultureInfo.InvariantCulture) +
                "}",
                "{" +
                    "\"immediateReconcileTriggered\":true," +
                    "\"triggerReason\":\"" + EscapeJson(triggerReason) + "\"," +
                    "\"skippedReason\":\"" + EscapeJson(skippedReason) + "\"" +
                "}",
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static void RecordLiveBuffStateChanged(string beforeSignature, string afterSignature, bool missingWhitelistedBuff)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "BuffPotion.LiveBuffStateChanged",
                "BuffPotion",
                string.Empty,
                "Succeeded",
                missingWhitelistedBuff ? "MissingRequiredBuff" : "Succeeded",
                missingWhitelistedBuff
                    ? "Live whitelisted buff state changed and at least one maintained buff is missing."
                    : "Live whitelisted buff state changed.",
                0,
                "{" +
                    "\"buffSignatureBefore\":\"" + EscapeJson(beforeSignature) + "\"" +
                "}",
                "{" +
                    "\"buffSignatureAfter\":\"" + EscapeJson(afterSignature) + "\"," +
                    "\"missingBuff\":" + (missingWhitelistedBuff ? "true" : "false") +
                "}",
                "{" +
                    "\"immediateReconcileTriggered\":" + (missingWhitelistedBuff ? "true" : "false") + "," +
                    "\"triggerReason\":\"" + (missingWhitelistedBuff ? "BuffMissing" : string.Empty) + "\"" +
                "}",
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static void RecordImmediateEnqueue(AutoRecoveryDecision decision, Guid requestId, string triggerReason, int queuedCount, int pendingCount, int inflightCount)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                requestId,
                "BuffPotion.ImmediateEnqueue",
                InputActionKind.BuffPotionDirectUse.ToString(),
                string.Empty,
                "Succeeded",
                "Succeeded",
                "Immediate AutoBuff enqueued a whitelisted missing buff potion.",
                0,
                "{" +
                    "\"immediateReconcileTriggered\":true," +
                    "\"triggerReason\":\"" + EscapeJson(triggerReason) + "\"," +
                    "\"itemType\":" + decision.ItemType.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"itemName\":\"" + EscapeJson(decision.ItemName) + "\"," +
                    "\"buffType\":" + decision.BuffType.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"buffName\":\"" + EscapeJson(decision.BuffName) + "\"" +
                "}",
                "{" +
                    "\"queuedCount\":" + queuedCount.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"pendingCount\":" + pendingCount.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"inflightCount\":" + inflightCount.ToString(CultureInfo.InvariantCulture) +
                "}",
                "{" +
                    "\"submitted\":true," +
                    "\"isWhitelisted\":true," +
                    "\"isActive\":false," +
                    "\"itemAvailable\":true," +
                    "\"missingBuff\":true" +
                "}",
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static void RecordMissingWhitelistItemIfNeeded(BuffPotionScanResult scan, long tick)
        {
            lock (SyncRoot)
            {
                if (tick - _lastAutoBuffMissingEventTick < 600)
                {
                    return;
                }
            }

            var settings = ConfigService.AppSettings;
            if (settings == null || settings.AutoBuffWhitelist == null || settings.AutoBuffWhitelist.Count <= 0)
            {
                return;
            }

            var live = new HashSet<int>();
            if (scan != null && scan.Candidates != null)
            {
                for (var index = 0; index < scan.Candidates.Count; index++)
                {
                    var candidate = scan.Candidates[index];
                    if (candidate != null && candidate.ItemType > 0)
                    {
                        live.Add(candidate.ItemType);
                    }
                }
            }

            BuffPotionWhitelistEntry missing = null;
            for (var index = 0; index < settings.AutoBuffWhitelist.Count; index++)
            {
                var entry = settings.AutoBuffWhitelist[index];
                if (entry != null && entry.ItemType > 0 && !live.Contains(entry.ItemType))
                {
                    missing = entry;
                    break;
                }
            }

            if (missing == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _lastAutoBuffMissingEventTick = tick;
            }

            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "BuffPotion.MissingWhitelistItem",
                "BuffPotion",
                string.Empty,
                "NotApplicable",
                "MissingRequiredItem",
                "Whitelisted buff potion is missing from inventory and void bag.",
                0,
                "{" +
                    "\"itemType\":" + missing.ItemType.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"itemName\":\"" + EscapeJson(missing.ItemName) + "\"," +
                    "\"buffType\":" + missing.BuffType.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"buffName\":\"" + EscapeJson(missing.BuffName) + "\"" +
                "}",
                "{" +
                    "\"missingItem\":true," +
                    "\"whitelistCount\":" + BuffPotionWhitelistService.Count.ToString(CultureInfo.InvariantCulture) +
                "}",
                "{\"submitted\":false,\"missingItem\":true}",
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }
    }
}
