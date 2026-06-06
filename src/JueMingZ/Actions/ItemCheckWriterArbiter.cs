using System;
using System.Diagnostics;
using JueMingZ.Diagnostics;

namespace JueMingZ.Actions
{
    public enum ItemCheckWriterKind
    {
        None,
        ItemUseBridge,
        UseItemPulseBridge,
        AutoCaptureCritterSustainedUse,
        AutoHarvestSustainedUse,
        CombatPerfectRevolver,
        CombatFlailCombo,
        CombatItemCheckAutoClicker,
        CombatAim,
        CombatFlailRelease,
        TravelMenuGuard
    }

    public sealed class ItemCheckWriterArbiterContext
    {
        public bool BridgePendingAtStart { get; set; }
        public bool BridgePendingNow { get; set; }
        public bool UseItemPulseActive { get; set; }
        public bool AutoCaptureCritterActive { get; set; }
        public bool AutoHarvestActive { get; set; }
    }

    public sealed class ItemCheckWriterDecision
    {
        public static readonly ItemCheckWriterDecision None = new ItemCheckWriterDecision
        {
            Owner = ItemCheckWriterKind.None,
            Reason = "noActiveWriterOwner",
            Phase = string.Empty,
            BlockedCandidatesSummary = string.Empty
        };

        public ItemCheckWriterKind Owner { get; set; }
        public Guid OwnerRequestId { get; set; }
        public string Phase { get; set; }
        public string Reason { get; set; }
        public string BlockedCandidatesSummary { get; set; }
        public DateTime DecidedUtc { get; set; }

        public string OwnerName
        {
            get { return Owner == ItemCheckWriterKind.None ? string.Empty : Owner.ToString(); }
        }

        public ItemCheckWriterDecision Clone()
        {
            return (ItemCheckWriterDecision)MemberwiseClone();
        }

        public ItemCheckWriterDecision()
        {
            Phase = string.Empty;
            Reason = string.Empty;
            BlockedCandidatesSummary = string.Empty;
            DecidedUtc = DateTime.UtcNow;
        }
    }

    public static class ItemCheckWriterArbiter
    {
        private static readonly object SyncRoot = new object();
        private static ItemCheckWriterDecision _lastDecision = ItemCheckWriterDecision.None;

        public static ItemCheckWriterDecision GetLastDecision()
        {
            lock (SyncRoot)
            {
                return _lastDecision == null ? ItemCheckWriterDecision.None : _lastDecision.Clone();
            }
        }

        public static ItemCheckWriterDecision ResolveOwner(ItemCheckWriterArbiterContext context)
        {
            var operationStart = Stopwatch.GetTimestamp();
            var decision = ResolveOwnerCore(context);
            RecordResolvePerformance(operationStart, context, decision);
            return decision;
        }

        private static ItemCheckWriterDecision ResolveOwnerCore(ItemCheckWriterArbiterContext context)
        {
            context = context ?? new ItemCheckWriterArbiterContext();

            ItemCheckWriterDecision decision;
            var bridgeRequestId = ItemUseBridge.PendingRequestId;
            if (context.BridgePendingAtStart || context.BridgePendingNow || bridgeRequestId != Guid.Empty)
            {
                decision = BuildDecision(
                    ItemCheckWriterKind.ItemUseBridge,
                    bridgeRequestId,
                    "press",
                    context.BridgePendingAtStart ? "bridgePendingAtStart" : "bridgePending",
                    BuildBlockedCandidates(ItemCheckWriterKind.ItemUseBridge));
                RecordDecision(decision);
                return decision;
            }

            if (context.AutoCaptureCritterActive && context.AutoHarvestActive)
            {
                var winner = WorldAutomationFairnessCoordinator.ResolveItemCheckWriterOwner(true, true);
                if (winner == WorldAutomationFairnessKind.AutoHarvest)
                {
                    decision = BuildDecision(
                        ItemCheckWriterKind.AutoHarvestSustainedUse,
                        AutoHarvestSustainedUseBridge.ActiveRequestId,
                        "sustainedUse",
                        "worldAutomationFairness:autoHarvest",
                        "AutoCaptureCritterSustainedUse:notOwner; " + BuildBlockedCandidates(ItemCheckWriterKind.AutoHarvestSustainedUse));
                    RecordDecision(decision);
                    return decision;
                }

                decision = BuildDecision(
                    ItemCheckWriterKind.AutoCaptureCritterSustainedUse,
                    AutoCaptureCritterSustainedUseBridge.ActiveRequestId,
                    "sustainedUse",
                    "worldAutomationFairness:autoCapture",
                    "AutoHarvestSustainedUse:notOwner; " + BuildBlockedCandidates(ItemCheckWriterKind.AutoCaptureCritterSustainedUse));
                RecordDecision(decision);
                return decision;
            }

            if (context.AutoCaptureCritterActive)
            {
                WorldAutomationFairnessCoordinator.ResolveItemCheckWriterOwner(true, false);
                decision = BuildDecision(
                    ItemCheckWriterKind.AutoCaptureCritterSustainedUse,
                    AutoCaptureCritterSustainedUseBridge.ActiveRequestId,
                    "sustainedUse",
                    "autoCaptureActive",
                    BuildBlockedCandidates(ItemCheckWriterKind.AutoCaptureCritterSustainedUse));
                RecordDecision(decision);
                return decision;
            }

            if (context.AutoHarvestActive)
            {
                WorldAutomationFairnessCoordinator.ResolveItemCheckWriterOwner(false, true);
                decision = BuildDecision(
                    ItemCheckWriterKind.AutoHarvestSustainedUse,
                    AutoHarvestSustainedUseBridge.ActiveRequestId,
                    "sustainedUse",
                    "autoHarvestActive",
                    BuildBlockedCandidates(ItemCheckWriterKind.AutoHarvestSustainedUse));
                RecordDecision(decision);
                return decision;
            }

            if (context.UseItemPulseActive)
            {
                decision = BuildDecision(
                    ItemCheckWriterKind.UseItemPulseBridge,
                    UseItemPulseBridge.ActiveRequestId,
                    "pulse",
                    "useItemPulseActive",
                    BuildBlockedCandidates(ItemCheckWriterKind.UseItemPulseBridge));
                RecordDecision(decision);
                return decision;
            }

            decision = ItemCheckWriterDecision.None.Clone();
            decision.DecidedUtc = DateTime.UtcNow;
            RecordDecision(decision);
            return decision;
        }

        private static void RecordResolvePerformance(long operationStart, ItemCheckWriterArbiterContext context, ItemCheckWriterDecision decision)
        {
            var elapsedMs = PerformanceHitchRecorder.ElapsedMilliseconds(operationStart, Stopwatch.GetTimestamp());
            if (!PerformanceHitchRecorder.ShouldRecordOperationFast(elapsedMs, PerformanceHitchRecorder.ItemCheckWriterResolveThresholdMs))
            {
                return;
            }

            context = context ?? new ItemCheckWriterArbiterContext();
            var ownerName = decision == null ? string.Empty : decision.OwnerName;
            var reason = decision == null ? "unknown" : decision.Reason;
            var metadata =
                "bridgeStart=" + context.BridgePendingAtStart +
                ";bridgeNow=" + context.BridgePendingNow +
                ";pulse=" + context.UseItemPulseActive +
                ";autoCapture=" + context.AutoCaptureCritterActive +
                ";autoHarvest=" + context.AutoHarvestActive +
                ";blocked=" + (decision == null ? string.Empty : decision.BlockedCandidatesSummary);

            PerformanceHitchRecorder.RecordOperationIfNeeded(
                "Performance.ItemCheckWriter.Resolve",
                elapsedMs,
                PerformanceHitchRecorder.ItemCheckWriterResolveThresholdMs,
                TrimSummary(reason, 256),
                TrimSummary(ownerName, 128),
                TrimSummary(metadata, 512));
        }

        public static bool IsBlockedByActiveOwner(
            ItemCheckWriterKind candidate,
            ItemCheckWriterArbiterContext context,
            out ItemCheckWriterDecision decision)
        {
            decision = ResolveOwner(context);
            return decision != null &&
                   decision.Owner != ItemCheckWriterKind.None &&
                   decision.Owner != candidate;
        }

        public static void RecordApplied(
            ItemCheckWriterKind owner,
            Guid requestId,
            string phase,
            string reason)
        {
            RecordApplied(owner, requestId, phase, reason, string.Empty);
        }

        public static void RecordApplied(
            ItemCheckWriterKind owner,
            Guid requestId,
            string phase,
            string reason,
            string blockedCandidatesSummary)
        {
            var decision = BuildDecision(
                owner,
                requestId,
                phase ?? string.Empty,
                string.IsNullOrWhiteSpace(reason) ? "applied" : reason,
                blockedCandidatesSummary ?? string.Empty);
            RecordDecision(decision);
        }

        public static void RecordSkipped(ItemCheckWriterKind blockedOwner, string reason)
        {
            var decision = BuildDecision(
                blockedOwner,
                Guid.Empty,
                "skip",
                string.IsNullOrWhiteSpace(reason) ? "skipped" : reason,
                BuildBlockedCandidates(blockedOwner));
            RecordDecision(decision);
        }

        private static ItemCheckWriterDecision BuildDecision(
            ItemCheckWriterKind owner,
            Guid ownerRequestId,
            string phase,
            string reason,
            string blockedCandidatesSummary)
        {
            return new ItemCheckWriterDecision
            {
                Owner = owner,
                OwnerRequestId = ownerRequestId,
                Phase = phase ?? string.Empty,
                Reason = reason ?? string.Empty,
                BlockedCandidatesSummary = TrimSummary(blockedCandidatesSummary, 512),
                DecidedUtc = DateTime.UtcNow
            };
        }

        private static string BuildBlockedCandidates(ItemCheckWriterKind owner)
        {
            switch (owner)
            {
                case ItemCheckWriterKind.ItemUseBridge:
                    return "UseItemPulseBridge:blockedByItemUseBridge; AutoCaptureCritterSustainedUse:blockedByItemUseBridge; AutoHarvestSustainedUse:blockedByItemUseBridge; CombatPerfectRevolver:blockedByItemUseBridge; CombatFlailCombo:blockedByItemUseBridge; CombatItemCheckAutoClicker:blockedByItemUseBridge; CombatAim:blockedByItemUseBridge";

                case ItemCheckWriterKind.AutoCaptureCritterSustainedUse:
                    return "UseItemPulseBridge:blockedByAutoCapture; AutoHarvestSustainedUse:blockedByAutoCapture; CombatPerfectRevolver:blockedByAutoCapture; CombatFlailCombo:blockedByAutoCapture; CombatItemCheckAutoClicker:blockedByAutoCapture; CombatAim:blockedByAutoCapture";

                case ItemCheckWriterKind.AutoHarvestSustainedUse:
                    return "UseItemPulseBridge:blockedByAutoHarvest; AutoCaptureCritterSustainedUse:blockedByAutoHarvest; CombatPerfectRevolver:blockedByAutoHarvest; CombatFlailCombo:blockedByAutoHarvest; CombatItemCheckAutoClicker:blockedByAutoHarvest; CombatAim:blockedByAutoHarvest";

                case ItemCheckWriterKind.UseItemPulseBridge:
                    return "CombatPerfectRevolver:blockedByUseItemPulse; CombatFlailCombo:blockedByUseItemPulse; CombatItemCheckAutoClicker:blockedByUseItemPulse";

                case ItemCheckWriterKind.CombatPerfectRevolver:
                    return "CombatFlailCombo:blockedByPerfectRevolver; CombatItemCheckAutoClicker:blockedByPerfectRevolver";

                case ItemCheckWriterKind.CombatFlailCombo:
                    return "CombatItemCheckAutoClicker:blockedByFlailCombo";

                case ItemCheckWriterKind.CombatItemCheckAutoClicker:
                    return string.Empty;

                default:
                    return string.Empty;
            }
        }

        private static void RecordDecision(ItemCheckWriterDecision decision)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision == null ? ItemCheckWriterDecision.None : decision.Clone();
            }
        }

        private static string TrimSummary(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0 || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength) + "...";
        }
    }
}
