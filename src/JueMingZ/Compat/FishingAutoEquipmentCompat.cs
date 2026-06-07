using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Automation.Fishing;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    internal static class FishingAutoEquipmentCompat
    {
        // Equipment plans record every move for restore verification; failed
        // reads or stale signatures must skip rather than reshuffle gear.
        private sealed class SourceCandidate
        {
            public FishingEquipmentContainerKind Kind;
            public int Slot;
            public int Priority;
            public object Item;
            public FishingAutoEquipmentItemSignature Signature;
        }

        private sealed class AccessoryChoice
        {
            public SourceCandidate Source;
            public int EquippedSlot;
            public int SourcePriority;
            public object Item;
            public FishingAutoEquipmentItemSignature Signature;
            public FishingEquipmentProfile Profile;

            public bool IsEquipped
            {
                get { return EquippedSlot >= 0; }
            }
        }

        private sealed class ReplacementSlot
        {
            public int Slot;
            public int Priority;
            public int ExistingScore;
        }

        private sealed class RestoreRequest
        {
            public FishingAutoEquipmentSessionInfo Session;
            public List<FishingAutoEquipmentMoveRecord> Records;
            public string Reason;
        }

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Guid, FishingAutoEquipmentPlan> ApplyPlans = new Dictionary<Guid, FishingAutoEquipmentPlan>();
        private static readonly Dictionary<Guid, FishingAutoEquipmentActionResult> ApplyResults = new Dictionary<Guid, FishingAutoEquipmentActionResult>();
        private static readonly Dictionary<Guid, RestoreRequest> RestoreRequests = new Dictionary<Guid, RestoreRequest>();
        private static readonly Dictionary<Guid, FishingAutoEquipmentActionResult> RestoreResults = new Dictionary<Guid, FishingAutoEquipmentActionResult>();

        public static bool TryCaptureSessionInfo(object player, out FishingAutoEquipmentSessionInfo session, out string message)
        {
            session = null;
            message = string.Empty;
            if (player == null)
            {
                message = "playerUnavailable";
                return false;
            }

            int selected;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selected))
            {
                message = "selectedItemUnavailable";
                return false;
            }

            object rod;
            if (!TryGetInventoryItem(player, selected, out rod) || rod == null)
            {
                message = "selectedItemSlotUnavailable";
                return false;
            }

            int fishingPole;
            TryReadItemInt(rod, "fishingPole", out fishingPole);
            var signature = CreateSignature(rod);
            if (signature.IsAir || fishingPole <= 0)
            {
                message = "selectedItemNotFishingPole";
                return false;
            }

            int loadout;
            if (!FishingLoadoutCompat.TryGetCurrentLoadoutIndex(player, out loadout))
            {
                loadout = -1;
            }

            session = new FishingAutoEquipmentSessionInfo
            {
                OriginalSelectedItemIndex = selected,
                OriginalRodSignature = signature,
                OriginalLoadoutIndex = loadout
            };
            return true;
        }

        public static bool TryBuildApplyPlan(object player, FishingAutoEquipmentSessionInfo session, FishingLiquidKind liquidKind, out FishingAutoEquipmentPlan plan, out string message)
        {
            plan = new FishingAutoEquipmentPlan();
            message = string.Empty;
            if (player == null)
            {
                message = "playerUnavailable";
                plan.SkipReason = message;
                return false;
            }

            if (session == null)
            {
                message = "sessionUnavailable";
                plan.SkipReason = message;
                return false;
            }

            plan.Session = session;
            IList armor;
            if (!TryGetArmorItems(player, out armor) || armor == null)
            {
                message = "armorUnavailable";
                plan.SkipReason = message;
                return false;
            }

            bool expertOrMaster = TryReadExpertOrMasterMode();
            var sources = ScanSources(player, armor);
            plan.CandidateCount = sources.Count;

            var usedSources = new HashSet<string>(StringComparer.Ordinal);
            var moveId = 1;
            BuildClothingMoves(player, armor, sources, usedSources, expertOrMaster, plan, ref moveId);
            BuildAccessoryMoves(player, armor, sources, usedSources, expertOrMaster, liquidKind, plan, ref moveId);

            if (plan.Moves.Count == 0)
            {
                message = sources.Count == 0 ? "noCandidates" : "noBetterCandidate";
                plan.SkipReason = message;
                return true;
            }

            message = "planReady";
            return true;
        }

        private static void BuildClothingMoves(
            object player,
            IList armor,
            IList<SourceCandidate> sources,
            HashSet<string> usedSources,
            bool expertOrMaster,
            FishingAutoEquipmentPlan plan,
            ref int moveId)
        {
            var maxTargetSlot = Math.Min(3, GetCollectionCount(armor));
            for (var targetSlot = 0; targetSlot < maxTargetSlot; targetSlot++)
            {
                bool targetUsable;
                if (!TryIsTargetSlotUsable(player, targetSlot, out targetUsable) || !targetUsable)
                {
                    continue;
                }

                var currentItem = GetIndexed(armor, targetSlot);
                var currentScore = FishingAutoEquipmentScorer.ScoreEquipped(player, currentItem, targetSlot, expertOrMaster, FishingLiquidKind.Unknown);
                SourceCandidate best = null;
                var bestScore = 0;
                var bestGroup = string.Empty;
                var bestReason = string.Empty;
                for (var index = 0; index < sources.Count; index++)
                {
                    var source = sources[index];
                    if (source == null || usedSources.Contains(BuildSourceKey(source.Kind, source.Slot)))
                    {
                        continue;
                    }

                    int candidateScore;
                    string effectGroup;
                    string reason;
                    if (!FishingAutoEquipmentScorer.TryScore(player, source.Item, targetSlot, expertOrMaster, FishingLiquidKind.Unknown, out candidateScore, out effectGroup, out reason) ||
                        candidateScore <= currentScore)
                    {
                        continue;
                    }

                    if (best == null ||
                        candidateScore > bestScore ||
                        candidateScore == bestScore && source.Priority < best.Priority ||
                        candidateScore == bestScore && source.Priority == best.Priority && source.Slot < best.Slot)
                    {
                        best = source;
                        bestScore = candidateScore;
                        bestGroup = effectGroup;
                        bestReason = reason;
                    }
                }

                if (best == null)
                {
                    continue;
                }

                usedSources.Add(BuildSourceKey(best.Kind, best.Slot));
                plan.Moves.Add(new FishingAutoEquipmentMovePlan
                {
                    MoveId = moveId++,
                    TargetEquipmentSlot = targetSlot,
                    SourceContainerKind = best.Kind,
                    SourceSlot = best.Slot,
                    CandidateSignature = best.Signature,
                    TargetSignatureAtPlan = CreateSignature(currentItem),
                    CandidateScore = bestScore,
                    ExistingScore = currentScore,
                    EffectGroup = bestGroup,
                    Reason = bestReason
                });
            }
        }

        private static void BuildAccessoryMoves(
            object player,
            IList armor,
            IList<SourceCandidate> sources,
            HashSet<string> usedSources,
            bool expertOrMaster,
            FishingLiquidKind liquidKind,
            FishingAutoEquipmentPlan plan,
            ref int moveId)
        {
            var accessorySlots = GetUsableAccessorySlots(player, armor);
            if (accessorySlots.Count == 0)
            {
                return;
            }

            var choices = BuildAccessoryChoices(player, armor, sources, usedSources, expertOrMaster, liquidKind, accessorySlots);
            if (choices.Count == 0)
            {
                return;
            }

            var selected = SelectAccessoryChoices(choices, accessorySlots.Count);
            if (selected.Count == 0)
            {
                return;
            }

            var keptSlots = new HashSet<int>();
            var sourceChoices = new List<AccessoryChoice>();
            for (var index = 0; index < selected.Count; index++)
            {
                var choice = selected[index];
                if (choice == null)
                {
                    continue;
                }

                if (choice.IsEquipped)
                {
                    keptSlots.Add(choice.EquippedSlot);
                }
                else
                {
                    sourceChoices.Add(choice);
                }
            }

            if (sourceChoices.Count == 0)
            {
                return;
            }

            sourceChoices.Sort(CompareChoiceDescending);
            var replacements = BuildReplacementSlots(player, armor, accessorySlots, keptSlots, expertOrMaster, liquidKind);
            var replaceCount = Math.Min(sourceChoices.Count, replacements.Count);
            for (var index = 0; index < replaceCount; index++)
            {
                var choice = sourceChoices[index];
                var replacement = replacements[index];
                if (choice == null || choice.Source == null || replacement == null)
                {
                    continue;
                }

                usedSources.Add(BuildSourceKey(choice.Source.Kind, choice.Source.Slot));
                var currentItem = GetIndexed(armor, replacement.Slot);
                plan.Moves.Add(new FishingAutoEquipmentMovePlan
                {
                    MoveId = moveId++,
                    TargetEquipmentSlot = replacement.Slot,
                    SourceContainerKind = choice.Source.Kind,
                    SourceSlot = choice.Source.Slot,
                    CandidateSignature = choice.Signature,
                    TargetSignatureAtPlan = CreateSignature(currentItem),
                    CandidateScore = choice.Profile == null ? 0 : choice.Profile.Score,
                    ExistingScore = replacement.ExistingScore,
                    EffectGroup = choice.Profile == null ? string.Empty : choice.Profile.EffectGroup,
                    Reason = choice.Profile == null ? string.Empty : choice.Profile.Reason
                });
            }
        }

        private static List<int> GetUsableAccessorySlots(object player, IList armor)
        {
            var result = new List<int>();
            var maxTargetSlot = Math.Min(10, GetCollectionCount(armor));
            for (var slot = 3; slot < maxTargetSlot; slot++)
            {
                bool usable;
                if (TryIsTargetSlotUsable(player, slot, out usable) && usable)
                {
                    result.Add(slot);
                }
            }

            return result;
        }

        private static List<AccessoryChoice> BuildAccessoryChoices(
            object player,
            IList armor,
            IList<SourceCandidate> sources,
            HashSet<string> usedSources,
            bool expertOrMaster,
            FishingLiquidKind liquidKind,
            IList<int> accessorySlots)
        {
            var choices = new List<AccessoryChoice>();
            for (var index = 0; index < accessorySlots.Count; index++)
            {
                var slot = accessorySlots[index];
                FishingEquipmentProfile profile;
                var item = GetIndexed(armor, slot);
                if (FishingEquipmentCatalog.TryBuildProfile(player, item, slot, expertOrMaster, liquidKind, out profile) &&
                    profile != null &&
                    profile.IsAccessory)
                {
                    choices.Add(new AccessoryChoice
                    {
                        EquippedSlot = slot,
                        SourcePriority = 0,
                        Item = item,
                        Signature = CreateSignature(item),
                        Profile = profile
                    });
                }
            }

            var targetSlot = accessorySlots[0];
            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (source == null || usedSources.Contains(BuildSourceKey(source.Kind, source.Slot)))
                {
                    continue;
                }

                FishingEquipmentProfile profile;
                if (FishingEquipmentCatalog.TryBuildProfile(player, source.Item, targetSlot, expertOrMaster, liquidKind, out profile) &&
                    profile != null &&
                    profile.IsAccessory)
                {
                    choices.Add(new AccessoryChoice
                    {
                        Source = source,
                        EquippedSlot = -1,
                        SourcePriority = source.Priority,
                        Item = source.Item,
                        Signature = source.Signature,
                        Profile = profile
                    });
                }
            }

            return choices;
        }

        private static List<AccessoryChoice> SelectAccessoryChoices(IList<AccessoryChoice> choices, int maxCount)
        {
            var bestByGroup = new Dictionary<string, AccessoryChoice>(StringComparer.Ordinal);
            for (var index = 0; index < choices.Count; index++)
            {
                var choice = choices[index];
                if (choice == null || choice.Profile == null || string.IsNullOrWhiteSpace(choice.Profile.EffectGroup))
                {
                    continue;
                }

                AccessoryChoice existing;
                if (!bestByGroup.TryGetValue(choice.Profile.EffectGroup, out existing) ||
                    CompareChoiceDescending(choice, existing) < 0)
                {
                    bestByGroup[choice.Profile.EffectGroup] = choice;
                }
            }

            var deduped = new List<AccessoryChoice>();
            foreach (var pair in bestByGroup)
            {
                if (pair.Value != null)
                {
                    deduped.Add(pair.Value);
                }
            }

            var hasTackleBag = false;
            var hasLavaproofTackleBag = false;
            for (var index = 0; index < deduped.Count; index++)
            {
                var profile = deduped[index].Profile;
                if (profile == null)
                {
                    continue;
                }

                hasTackleBag |= profile.IsTackleBag;
                hasLavaproofTackleBag |= profile.IsLavaproofTackleBag;
            }

            var selected = new List<AccessoryChoice>();
            for (var index = 0; index < deduped.Count; index++)
            {
                var choice = deduped[index];
                var profile = choice == null ? null : choice.Profile;
                if (profile == null)
                {
                    continue;
                }

                if (hasTackleBag && profile.CoveredByAnyTackleBag)
                {
                    continue;
                }

                if (hasLavaproofTackleBag && profile.CoveredByLavaproofTackleBag)
                {
                    continue;
                }

                selected.Add(choice);
            }

            selected.Sort(CompareChoiceDescending);
            if (selected.Count > maxCount)
            {
                selected.RemoveRange(maxCount, selected.Count - maxCount);
            }

            return selected;
        }

        private static List<ReplacementSlot> BuildReplacementSlots(
            object player,
            IList armor,
            IList<int> accessorySlots,
            HashSet<int> keptSlots,
            bool expertOrMaster,
            FishingLiquidKind liquidKind)
        {
            var result = new List<ReplacementSlot>();
            for (var index = 0; index < accessorySlots.Count; index++)
            {
                var slot = accessorySlots[index];
                if (keptSlots.Contains(slot))
                {
                    continue;
                }

                var item = GetIndexed(armor, slot);
                result.Add(new ReplacementSlot
                {
                    Slot = slot,
                    Priority = ReplacementPriority(player, item, slot, expertOrMaster, liquidKind),
                    ExistingScore = FishingAutoEquipmentScorer.ScoreEquipped(player, item, slot, expertOrMaster, liquidKind)
                });
            }

            result.Sort(CompareReplacementSlots);
            return result;
        }

        private static int ReplacementPriority(object player, object item, int slot, bool expertOrMaster, FishingLiquidKind liquidKind)
        {
            var signature = CreateSignature(item);
            if (signature.IsAir)
            {
                return 1;
            }

            FishingEquipmentProfile profile;
            if (FishingEquipmentCatalog.TryBuildProfile(player, item, slot, expertOrMaster, liquidKind, out profile) ||
                FishingEquipmentCatalog.TryBuildProfile(player, item, slot, expertOrMaster, FishingLiquidKind.Lava, out profile))
            {
                return 0;
            }

            return 2;
        }

        private static int CompareChoiceDescending(AccessoryChoice left, AccessoryChoice right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var leftScore = left.Profile == null ? 0 : left.Profile.Score;
            var rightScore = right.Profile == null ? 0 : right.Profile.Score;
            var compare = rightScore.CompareTo(leftScore);
            if (compare != 0)
            {
                return compare;
            }

            if (left.IsEquipped != right.IsEquipped)
            {
                return left.IsEquipped ? -1 : 1;
            }

            compare = left.SourcePriority.CompareTo(right.SourcePriority);
            if (compare != 0)
            {
                return compare;
            }

            var leftSlot = left.IsEquipped ? left.EquippedSlot : left.Source == null ? int.MaxValue : left.Source.Slot;
            var rightSlot = right.IsEquipped ? right.EquippedSlot : right.Source == null ? int.MaxValue : right.Source.Slot;
            compare = leftSlot.CompareTo(rightSlot);
            if (compare != 0)
            {
                return compare;
            }

            var leftType = left.Signature == null ? 0 : left.Signature.Type;
            var rightType = right.Signature == null ? 0 : right.Signature.Type;
            return leftType.CompareTo(rightType);
        }

        private static int CompareReplacementSlots(ReplacementSlot left, ReplacementSlot right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var compare = left.Priority.CompareTo(right.Priority);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.ExistingScore.CompareTo(right.ExistingScore);
            return compare != 0 ? compare : left.Slot.CompareTo(right.Slot);
        }

        public static void RegisterApplyPlan(Guid requestId, FishingAutoEquipmentPlan plan)
        {
            if (requestId == Guid.Empty || plan == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                ApplyPlans[requestId] = plan;
            }
        }

        public static void RegisterRestoreRequest(Guid requestId, FishingAutoEquipmentSessionInfo session, IList<FishingAutoEquipmentMoveRecord> records, string reason)
        {
            if (requestId == Guid.Empty || session == null || records == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                RestoreRequests[requestId] = new RestoreRequest
                {
                    Session = session,
                    Records = CopyRecords(records),
                    Reason = reason ?? string.Empty
                };
            }
        }

        public static bool TryTakeApplyResult(Guid requestId, out FishingAutoEquipmentActionResult result)
        {
            result = null;
            if (requestId == Guid.Empty)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (!ApplyResults.TryGetValue(requestId, out result))
                {
                    return false;
                }

                ApplyResults.Remove(requestId);
                return true;
            }
        }

        public static bool TryTakeRestoreResult(Guid requestId, out FishingAutoEquipmentActionResult result)
        {
            result = null;
            if (requestId == Guid.Empty)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (!RestoreResults.TryGetValue(requestId, out result))
                {
                    return false;
                }

                RestoreResults.Remove(requestId);
                return true;
            }
        }

        public static bool TryApplyRegisteredPlan(Guid requestId, out FishingAutoEquipmentActionResult result)
        {
            result = null;
            FishingAutoEquipmentPlan plan;
            lock (SyncRoot)
            {
                if (!ApplyPlans.TryGetValue(requestId, out plan))
                {
                    result = BuildResult("applySkipped", "applyPlanUnavailable", "Fishing auto equipment apply plan unavailable.");
                    ApplyResults[requestId] = result;
                    return false;
                }

                ApplyPlans.Remove(requestId);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                result = BuildResult("applySkipped", "playerUnavailable", "Local player unavailable for fishing auto equipment apply.");
                StoreApplyResult(requestId, result);
                return false;
            }

            if (TryIsMouseItemPresent())
            {
                result = BuildResult("applyBlocked", "blockedByMouseItem", "Fishing auto equipment apply blocked because Main.mouseItem is not empty.");
                result.BlockedByMouseItem = true;
                StoreApplyResult(requestId, result);
                return false;
            }

            result = ApplyPlan(player, plan);
            StoreApplyResult(requestId, result);
            return result.AppliedMoveCount > 0;
        }

        public static bool TryRestoreRegisteredRecords(Guid requestId, out FishingAutoEquipmentActionResult result)
        {
            result = null;
            RestoreRequest request;
            lock (SyncRoot)
            {
                if (!RestoreRequests.TryGetValue(requestId, out request))
                {
                    result = BuildResult("restoreSkipped", "restoreRequestUnavailable", "Fishing auto equipment restore request unavailable.");
                    RestoreResults[requestId] = result;
                    return false;
                }

                RestoreRequests.Remove(requestId);
            }

            // Restore is as important as apply: stale loadout or mouse item
            // state keeps records pending instead of forcing gear back.
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                result = BuildResult("restoreSkipped", "playerUnavailable", "Local player unavailable for fishing auto equipment restore.");
                ApplyRestoreReasonFlags(result, request.Reason);
                result.Records.AddRange(CopyRecords(request.Records));
                result.PendingRestoreCount = result.Records.Count;
                StoreRestoreResult(requestId, result);
                return false;
            }

            if (TryIsMouseItemPresent())
            {
                result = BuildResult("restoreBlocked", "blockedByMouseItem", "Fishing auto equipment restore blocked because Main.mouseItem is not empty.");
                result.BlockedByMouseItem = true;
                ApplyRestoreReasonFlags(result, request.Reason);
                result.Records.AddRange(CopyRecords(request.Records));
                result.PendingRestoreCount = result.Records.Count;
                StoreRestoreResult(requestId, result);
                return false;
            }

            int currentLoadout;
            if (request.Session != null &&
                request.Session.OriginalLoadoutIndex >= 0 &&
                FishingLoadoutCompat.TryGetCurrentLoadoutIndex(player, out currentLoadout) &&
                currentLoadout != request.Session.OriginalLoadoutIndex)
            {
                result = BuildResult("restorePaused", "loadoutChangedDuringAutoEquipment", "Fishing auto equipment restore paused because current loadout changed.");
                result.LoadoutChangedDuringAutoEquipment = true;
                ApplyRestoreReasonFlags(result, request.Reason);
                result.Records.AddRange(CopyRecords(request.Records));
                result.PendingRestoreCount = result.Records.Count;
                StoreRestoreResult(requestId, result);
                return false;
            }

            result = RestoreRecords(player, request);
            ApplyRestoreReasonFlags(result, request.Reason);
            StoreRestoreResult(requestId, result);
            return result.PendingRestoreCount == 0;
        }

        public static bool TryIsMouseItemPresent()
        {
            object mouseItem;
            if (!TryGetStaticMember(TerrariaRuntimeTypes.MainType, "mouseItem", out mouseItem) || mouseItem == null)
            {
                return false;
            }

            return !CreateSignature(mouseItem).IsAir;
        }

        public static bool TryIsStillHoldingOriginalRod(object player, FishingAutoEquipmentSessionInfo session, out bool stillHolding, out string reason)
        {
            stillHolding = false;
            reason = string.Empty;
            if (player == null || session == null)
            {
                reason = "sessionOrPlayerUnavailable";
                return false;
            }

            int selected;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selected))
            {
                reason = "selectedItemUnavailable";
                return false;
            }

            if (selected != session.OriginalSelectedItemIndex)
            {
                reason = "selectedItemChanged";
                return true;
            }

            object item;
            if (!TryGetInventoryItem(player, session.OriginalSelectedItemIndex, out item) || item == null)
            {
                reason = "originalRodSlotUnavailable";
                return false;
            }

            var signature = CreateSignature(item);
            stillHolding = session.OriginalRodSignature != null && session.OriginalRodSignature.Matches(signature);
            reason = stillHolding ? "stillHoldingOriginalRod" : "originalRodSlotChanged";
            return true;
        }

        public static FishingAutoEquipmentItemSignature CreateSignature(object item)
        {
            var signature = new FishingAutoEquipmentItemSignature();
            if (item == null)
            {
                return signature;
            }

            TryReadItemInt(item, "type", out var type);
            TryReadItemInt(item, "stack", out var stack);
            TryReadItemInt(item, "prefix", out var prefix);
            signature.Type = type;
            signature.Stack = stack;
            signature.Prefix = prefix;
            var rawName = GetMember(item, "Name") ?? GetMember(item, "name");
            signature.Name = rawName == null ? string.Empty : rawName.ToString();
            return signature;
        }

        public static string ContainerKindName(FishingEquipmentContainerKind kind)
        {
            switch (kind)
            {
                case FishingEquipmentContainerKind.Inventory:
                    return "Inventory";
                case FishingEquipmentContainerKind.VoidBag:
                    return "VoidBag";
                case FishingEquipmentContainerKind.Social:
                    return "Social";
                default:
                    return "Unknown";
            }
        }

        private static FishingAutoEquipmentActionResult ApplyPlan(object player, FishingAutoEquipmentPlan plan)
        {
            var result = BuildResult("applyAttempted", string.Empty, "Fishing auto equipment apply attempted.");
            result.Invoked = true;
            if (plan == null || plan.Moves == null || plan.Moves.Count == 0)
            {
                result.Decision = "applySkipped";
                result.SkipReason = plan == null ? "planUnavailable" : plan.SkipReason;
                result.Message = "No fishing auto equipment move was planned.";
                return result;
            }

            IList armor;
            if (!TryGetArmorItems(player, out armor) || armor == null)
            {
                result.Decision = "applySkipped";
                result.SkipReason = "armorUnavailable";
                result.Message = "Player armor array unavailable.";
                return result;
            }

            for (var index = 0; index < plan.Moves.Count; index++)
            {
                var move = plan.Moves[index];
                string skipReason;
                FishingAutoEquipmentMoveRecord record;
                if (TryApplyMove(player, armor, move, out record, out skipReason))
                {
                    result.AppliedMoveCount++;
                    result.Records.Add(record);
                    continue;
                }

                result.SkippedMoveCount++;
                result.SkipReason = string.IsNullOrWhiteSpace(result.SkipReason) ? skipReason : result.SkipReason + "," + skipReason;
            }

            result.PendingRestoreCount = result.Records.Count;
            result.Decision = result.AppliedMoveCount > 0 ? "applySucceeded" : "applySkipped";
            result.Message = "Fishing auto equipment apply completed. appliedMoveCount=" +
                             result.AppliedMoveCount.ToString(CultureInfo.InvariantCulture) +
                             ", skippedMoveCount=" +
                             result.SkippedMoveCount.ToString(CultureInfo.InvariantCulture) + ".";
            return result;
        }

        private static bool TryApplyMove(object player, IList armor, FishingAutoEquipmentMovePlan move, out FishingAutoEquipmentMoveRecord record, out string skipReason)
        {
            record = null;
            skipReason = string.Empty;
            if (move == null)
            {
                skipReason = "movePlanNull";
                return false;
            }

            bool usable;
            if (!TryIsTargetSlotUsable(player, move.TargetEquipmentSlot, out usable) || !usable)
            {
                skipReason = "targetSlotUnavailable";
                return false;
            }

            object sourceItem;
            if (!TryGetContainerItem(player, move.SourceContainerKind, move.SourceSlot, out sourceItem) ||
                !SignatureMatches(sourceItem, move.CandidateSignature))
            {
                skipReason = "sourceItemChanged";
                return false;
            }

            var targetItem = GetIndexed(armor, move.TargetEquipmentSlot);
            if (!SignatureMatches(targetItem, move.TargetSignatureAtPlan))
            {
                skipReason = "targetItemChanged";
                return false;
            }

            var targetSignature = CreateSignature(targetItem);
            var replacementForSource = targetItem ?? CreateAirLike(sourceItem);
            if (!SetIndexed(armor, move.TargetEquipmentSlot, sourceItem) ||
                !SetContainerItem(player, move.SourceContainerKind, move.SourceSlot, replacementForSource))
            {
                skipReason = "swapWriteFailed";
                return false;
            }

            if (!SignatureMatches(GetIndexed(armor, move.TargetEquipmentSlot), move.CandidateSignature))
            {
                skipReason = "targetVerificationFailed";
                return false;
            }

            record = new FishingAutoEquipmentMoveRecord
            {
                MoveId = move.MoveId,
                TargetEquipmentSlot = move.TargetEquipmentSlot,
                SourceContainerKind = move.SourceContainerKind,
                SourceSlot = move.SourceSlot,
                FishingItemSignature = move.CandidateSignature,
                OriginalTargetWasAir = targetSignature.IsAir,
                OriginalTargetItemSignature = targetSignature,
                OriginalTargetHoldingContainerKind = move.SourceContainerKind,
                OriginalTargetHoldingSlot = move.SourceSlot,
                ApplyStatus = "applied",
                RestoreStatus = "pending"
            };
            return true;
        }

        private static FishingAutoEquipmentActionResult RestoreRecords(object player, RestoreRequest request)
        {
            var result = BuildResult("restoreAttempted", string.Empty, "Fishing auto equipment restore attempted.");
            result.Invoked = true;
            if (request == null || request.Records == null || request.Records.Count == 0)
            {
                result.Decision = "restoreSkipped";
                result.SkipReason = "noRecords";
                result.Message = "No fishing auto equipment records to restore.";
                return result;
            }

            IList armor;
            if (!TryGetArmorItems(player, out armor) || armor == null)
            {
                result.Decision = "restoreSkipped";
                result.SkipReason = "armorUnavailable";
                result.Records.AddRange(CopyRecords(request.Records));
                result.PendingRestoreCount = result.Records.Count;
                result.Message = "Player armor array unavailable.";
                return result;
            }

            for (var index = 0; index < request.Records.Count; index++)
            {
                var record = request.Records[index];
                if (record == null)
                {
                    continue;
                }

                var targetItem = GetIndexed(armor, record.TargetEquipmentSlot);
                if (!SignatureMatches(targetItem, record.FishingItemSignature))
                {
                    if (SignatureMatches(targetItem, record.OriginalTargetItemSignature))
                    {
                        result.RestoredMoveCount++;
                        record.RestoreStatus = "originalAlreadyRestored";
                        continue;
                    }

                    result.UserChangedManagedSlotCount++;
                    record.RestoreStatus = "userChangedManagedSlot";
                    result.Records.Add(CloneRecord(record));
                    continue;
                }

                if (record.OriginalTargetWasAir)
                {
                    FishingEquipmentContainerKind destinationKind;
                    int destinationSlot;
                    if (!TryFindRestoreDestination(player, record, out destinationKind, out destinationSlot))
                    {
                        record.RestoreStatus = "pendingRestoreNoSpace";
                        result.PendingRestoreNoSpaceCount++;
                        result.Records.Add(CloneRecord(record));
                        continue;
                    }

                    var air = CreateAirLike(targetItem);
                    if (SetContainerItem(player, destinationKind, destinationSlot, targetItem) &&
                        SetIndexed(armor, record.TargetEquipmentSlot, air))
                    {
                        result.RestoredMoveCount++;
                        record.RestoreStatus = "restoredToEmptyTarget";
                        continue;
                    }

                    record.RestoreStatus = "pendingRestoreWriteFailed";
                    result.Records.Add(CloneRecord(record));
                    continue;
                }

                object originalItem;
                if (!TryGetContainerItem(player, record.OriginalTargetHoldingContainerKind, record.OriginalTargetHoldingSlot, out originalItem) ||
                    !SignatureMatches(originalItem, record.OriginalTargetItemSignature))
                {
                    FishingEquipmentContainerKind relocatedKind;
                    int relocatedSlot;
                    if (TryFindRestoreOriginalItem(player, record, out relocatedKind, out relocatedSlot, out originalItem) &&
                        SetIndexed(armor, record.TargetEquipmentSlot, originalItem) &&
                        SetContainerItem(player, relocatedKind, relocatedSlot, targetItem))
                    {
                        result.RestoredMoveCount++;
                        result.OriginalRelocatedByUserCount++;
                        record.RestoreStatus = "restoredRelocatedOriginal";
                        continue;
                    }

                    result.OriginalMovedByUserCount++;
                    record.RestoreStatus = "originalMovedByUser";
                    result.Records.Add(CloneRecord(record));
                    continue;
                }

                if (SetIndexed(armor, record.TargetEquipmentSlot, originalItem) &&
                    SetContainerItem(player, record.OriginalTargetHoldingContainerKind, record.OriginalTargetHoldingSlot, targetItem))
                {
                    result.RestoredMoveCount++;
                    record.RestoreStatus = "restoredSwap";
                    continue;
                }

                record.RestoreStatus = "pendingRestoreWriteFailed";
                result.Records.Add(CloneRecord(record));
            }

            result.PendingRestoreCount = result.Records.Count;
            result.Decision = result.PendingRestoreCount > 0 ? "restorePending" : "restoreCompleted";
            result.Message = "Fishing auto equipment restore completed. restoredMoveCount=" +
                             result.RestoredMoveCount.ToString(CultureInfo.InvariantCulture) +
                             ", pendingRestoreCount=" +
                             result.PendingRestoreCount.ToString(CultureInfo.InvariantCulture) + ".";
            if (result.UserChangedManagedSlotCount > 0)
            {
                result.SkipReason = AppendReason(result.SkipReason, "userChangedManagedSlot");
            }

            if (result.OriginalMovedByUserCount > 0)
            {
                result.SkipReason = AppendReason(result.SkipReason, "originalMovedByUser");
            }

            if (result.OriginalRelocatedByUserCount > 0)
            {
                result.SkipReason = AppendReason(result.SkipReason, "originalRelocatedByUser");
            }

            if (result.PendingRestoreNoSpaceCount > 0)
            {
                result.SkipReason = AppendReason(result.SkipReason, "pendingRestoreNoSpace");
            }

            return result;
        }

        private static bool TryFindRestoreOriginalItem(
            object player,
            FishingAutoEquipmentMoveRecord record,
            out FishingEquipmentContainerKind kind,
            out int slot,
            out object item)
        {
            kind = FishingEquipmentContainerKind.Unknown;
            slot = -1;
            item = null;
            if (player == null || record == null || record.OriginalTargetItemSignature == null ||
                record.OriginalTargetItemSignature.IsAir)
            {
                return false;
            }

            IList inventory;
            if (TryGetInventoryItems(player, out inventory) &&
                TryFindMatchingItem(inventory, 0, Math.Min(50, GetCollectionCount(inventory)), record.OriginalTargetItemSignature, out slot, out item))
            {
                kind = FishingEquipmentContainerKind.Inventory;
                return true;
            }

            IList voidBag;
            if (TryGetVoidBagItems(player, out voidBag) &&
                TryFindMatchingItem(voidBag, 0, GetCollectionCount(voidBag), record.OriginalTargetItemSignature, out slot, out item))
            {
                kind = FishingEquipmentContainerKind.VoidBag;
                return true;
            }

            IList armor;
            if (TryGetArmorItems(player, out armor) &&
                TryFindMatchingItem(armor, 10, GetCollectionCount(armor), record.OriginalTargetItemSignature, out slot, out item))
            {
                kind = FishingEquipmentContainerKind.Social;
                return true;
            }

            return false;
        }

        private static bool TryFindMatchingItem(
            IList items,
            int startSlot,
            int endSlotExclusive,
            FishingAutoEquipmentItemSignature signature,
            out int slot,
            out object item)
        {
            slot = -1;
            item = null;
            if (items == null || signature == null)
            {
                return false;
            }

            var count = GetCollectionCount(items);
            var start = Math.Max(0, startSlot);
            var end = Math.Min(Math.Max(start, endSlotExclusive), count);
            for (var index = start; index < end; index++)
            {
                var candidate = GetIndexed(items, index);
                if (!SignatureMatches(candidate, signature))
                {
                    continue;
                }

                slot = index;
                item = candidate;
                return true;
            }

            return false;
        }

        private static bool TryFindRestoreDestination(object player, FishingAutoEquipmentMoveRecord record, out FishingEquipmentContainerKind kind, out int slot)
        {
            kind = FishingEquipmentContainerKind.Unknown;
            slot = -1;
            if (record == null)
            {
                return false;
            }

            if (TryIsContainerSlotEmpty(player, record.SourceContainerKind, record.SourceSlot))
            {
                kind = record.SourceContainerKind;
                slot = record.SourceSlot;
                return true;
            }

            if (record.SourceContainerKind == FishingEquipmentContainerKind.Inventory)
            {
                if (TryFindEmptyInventorySlot(player, out slot))
                {
                    kind = FishingEquipmentContainerKind.Inventory;
                    return true;
                }
            }
            else if (record.SourceContainerKind == FishingEquipmentContainerKind.VoidBag)
            {
                if (TryFindEmptyVoidBagSlot(player, out slot))
                {
                    kind = FishingEquipmentContainerKind.VoidBag;
                    return true;
                }

                if (TryFindEmptyInventorySlot(player, out slot))
                {
                    kind = FishingEquipmentContainerKind.Inventory;
                    return true;
                }
            }
            else if (record.SourceContainerKind == FishingEquipmentContainerKind.Social)
            {
                if (TryFindEmptyInventorySlot(player, out slot))
                {
                    kind = FishingEquipmentContainerKind.Inventory;
                    return true;
                }
            }

            if (TryFindEmptyInventorySlot(player, out slot))
            {
                kind = FishingEquipmentContainerKind.Inventory;
                return true;
            }

            return false;
        }

        private static List<SourceCandidate> ScanSources(object player, IList armor)
        {
            var result = new List<SourceCandidate>();
            IList inventory;
            if (TryGetInventoryItems(player, out inventory) && inventory != null)
            {
                var count = Math.Min(50, GetCollectionCount(inventory));
                for (var index = 0; index < count; index++)
                {
                    AddSourceCandidate(result, FishingEquipmentContainerKind.Inventory, index, 2, GetIndexed(inventory, index));
                }
            }

            IList voidBag;
            if (TryGetVoidBagItems(player, out voidBag) && voidBag != null)
            {
                var count = GetCollectionCount(voidBag);
                for (var index = 0; index < count; index++)
                {
                    AddSourceCandidate(result, FishingEquipmentContainerKind.VoidBag, index, 3, GetIndexed(voidBag, index));
                }
            }

            if (armor != null)
            {
                var count = GetCollectionCount(armor);
                for (var index = 10; index < count; index++)
                {
                    AddSourceCandidate(result, FishingEquipmentContainerKind.Social, index, 1, GetIndexed(armor, index));
                }
            }

            return result;
        }

        private static void AddSourceCandidate(List<SourceCandidate> result, FishingEquipmentContainerKind kind, int slot, int priority, object item)
        {
            if (result == null || item == null)
            {
                return;
            }

            var signature = CreateSignature(item);
            if (signature.IsAir)
            {
                return;
            }

            result.Add(new SourceCandidate
            {
                Kind = kind,
                Slot = slot,
                Priority = priority,
                Item = item,
                Signature = signature
            });
        }

        private static bool TryIsTargetSlotUsable(object player, int slot, out bool usable)
        {
            usable = false;
            if (slot < 0 || slot > 9)
            {
                return false;
            }

            if (slot < 3)
            {
                usable = true;
                return true;
            }

            return FishingLoadoutCompat.TryIsItemSlotUnlockedAndUsable(player, slot, out usable);
        }

        private static bool TryReadExpertOrMasterMode()
        {
            bool expert;
            bool master;
            var hasExpert = TryReadStaticBool(TerrariaRuntimeTypes.MainType, "expertMode", out expert);
            var hasMaster = TryReadStaticBool(TerrariaRuntimeTypes.MainType, "masterMode", out master);
            return (hasExpert && expert) || (hasMaster && master);
        }

        private static bool TryIsContainerSlotEmpty(object player, FishingEquipmentContainerKind kind, int slot)
        {
            object item;
            if (!TryGetContainerItem(player, kind, slot, out item))
            {
                return false;
            }

            return item == null || CreateSignature(item).IsAir;
        }

        private static bool TryFindEmptyInventorySlot(object player, out int slot)
        {
            slot = -1;
            IList inventory;
            if (!TryGetInventoryItems(player, out inventory) || inventory == null)
            {
                return false;
            }

            var count = Math.Min(50, GetCollectionCount(inventory));
            for (var index = 0; index < count; index++)
            {
                var item = GetIndexed(inventory, index);
                if (item == null || CreateSignature(item).IsAir)
                {
                    slot = index;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindEmptyVoidBagSlot(object player, out int slot)
        {
            slot = -1;
            IList items;
            if (!TryGetVoidBagItems(player, out items) || items == null)
            {
                return false;
            }

            var count = GetCollectionCount(items);
            for (var index = 0; index < count; index++)
            {
                var item = GetIndexed(items, index);
                if (item == null || CreateSignature(item).IsAir)
                {
                    slot = index;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetContainerItem(object player, FishingEquipmentContainerKind kind, int slot, out object item)
        {
            item = null;
            IList items;
            if (!TryGetContainerItems(player, kind, out items) || items == null || slot < 0 || slot >= GetCollectionCount(items))
            {
                return false;
            }

            item = GetIndexed(items, slot);
            return true;
        }

        private static bool SetContainerItem(object player, FishingEquipmentContainerKind kind, int slot, object value)
        {
            IList items;
            return TryGetContainerItems(player, kind, out items) &&
                   SetIndexed(items, slot, value ?? CreateAirLike(null));
        }

        private static bool TryGetContainerItems(object player, FishingEquipmentContainerKind kind, out IList items)
        {
            items = null;
            if (player == null)
            {
                return false;
            }

            if (kind == FishingEquipmentContainerKind.Inventory)
            {
                return TryGetInventoryItems(player, out items);
            }

            if (kind == FishingEquipmentContainerKind.VoidBag)
            {
                return TryGetVoidBagItems(player, out items);
            }

            if (kind == FishingEquipmentContainerKind.Social)
            {
                return TryGetArmorItems(player, out items);
            }

            return false;
        }

        private static bool TryGetInventoryItem(object player, int slot, out object item)
        {
            item = null;
            IList inventory;
            if (!TryGetInventoryItems(player, out inventory) || inventory == null || slot < 0 || slot >= GetCollectionCount(inventory))
            {
                return false;
            }

            item = GetIndexed(inventory, slot);
            return true;
        }

        private static bool TryGetInventoryItems(object player, out IList items)
        {
            items = GetMember(player, "inventory") as IList;
            return items != null;
        }

        private static bool TryGetVoidBagItems(object player, out IList items)
        {
            items = null;
            var bank4 = GetMember(player, "bank4");
            if (bank4 == null)
            {
                return false;
            }

            items = GetMember(bank4, "item") as IList;
            return items != null;
        }

        private static bool TryGetArmorItems(object player, out IList items)
        {
            items = GetMember(player, "armor") as IList;
            return items != null;
        }

        private static bool SignatureMatches(object item, FishingAutoEquipmentItemSignature expected)
        {
            if (expected == null)
            {
                return false;
            }

            return expected.Matches(CreateSignature(item));
        }

        private static object CreateAirLike(object item)
        {
            Type itemType = item == null ? null : item.GetType();
            if (itemType == null)
            {
                itemType = FindType("Terraria.Item");
            }

            if (itemType == null)
            {
                return null;
            }

            try
            {
                var empty = Activator.CreateInstance(itemType);
                TryTurnToAir(empty);
                return empty;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("FishingAutoEquipmentCompat.CreateAirLike", error);
                return null;
            }
        }

        private static bool TryTurnToAir(object item)
        {
            if (item == null)
            {
                return false;
            }

            var methods = item.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "TurnToAir", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                try
                {
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
                    {
                        method.Invoke(item, new object[] { false });
                        return true;
                    }

                    if (parameters.Length == 0)
                    {
                        method.Invoke(item, new object[0]);
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
            {
                return field.GetValue(instance);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property) && property.CanRead
                ? property.GetValue(instance, null)
                : null;
        }

        private static bool TryGetStaticMember(Type type, string name, out object value)
        {
            value = null;
            if (type == null)
            {
                return false;
            }

            if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
            {
                try
                {
                    value = field.GetValue(null);
                    return true;
                }
                catch (Exception error)
                {
                    RuntimeDiagnostics.RecordError("FishingAutoEquipmentCompat.TryGetStaticMember." + name, error);
                    return false;
                }
            }

            if (TerrariaMemberCache.TryGetProperty(type, name, true, out var property) && property.CanRead)
            {
                try
                {
                    value = property.GetValue(null, null);
                    return true;
                }
                catch (Exception error)
                {
                    RuntimeDiagnostics.RecordError("FishingAutoEquipmentCompat.TryGetStaticMember." + name, error);
                    return false;
                }
            }

            return false;
        }

        private static bool TryReadItemInt(object item, string name, out int value)
        {
            value = 0;
            var raw = GetMember(item, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadStaticBool(Type type, string name, out bool value)
        {
            value = false;
            object raw;
            if (!TryGetStaticMember(type, name, out raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object GetIndexed(object source, int index)
        {
            if (source == null || index < 0)
            {
                return null;
            }

            var list = source as IList;
            if (list != null)
            {
                return index < list.Count ? list[index] : null;
            }

            var array = source as Array;
            return array != null && array.Rank == 1 && index < array.GetLength(0)
                ? array.GetValue(index)
                : null;
        }

        private static bool SetIndexed(object source, int index, object value)
        {
            if (source == null || index < 0 || value == null)
            {
                return false;
            }

            try
            {
                var list = source as IList;
                if (list != null)
                {
                    if (index >= list.Count)
                    {
                        return false;
                    }

                    list[index] = value;
                    return true;
                }

                var array = source as Array;
                if (array != null && array.Rank == 1 && index < array.GetLength(0))
                {
                    array.SetValue(value, index);
                    return true;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("FishingAutoEquipmentCompat.SetIndexed", error);
                return false;
            }

            return false;
        }

        private static int GetCollectionCount(object source)
        {
            var list = source as IList;
            if (list != null)
            {
                return list.Count;
            }

            var array = source as Array;
            return array == null || array.Rank != 1 ? 0 : array.GetLength(0);
        }

        private static string BuildSourceKey(FishingEquipmentContainerKind kind, int slot)
        {
            return ((int)kind).ToString(CultureInfo.InvariantCulture) + ":" + slot.ToString(CultureInfo.InvariantCulture);
        }

        private static FishingAutoEquipmentActionResult BuildResult(string decision, string skipReason, string message)
        {
            return new FishingAutoEquipmentActionResult
            {
                Decision = decision ?? string.Empty,
                SkipReason = skipReason ?? string.Empty,
                Message = message ?? string.Empty
            };
        }

        private static void ApplyRestoreReasonFlags(FishingAutoEquipmentActionResult result, string reason)
        {
            if (result == null)
            {
                return;
            }

            result.LeftOriginalRod = string.Equals(reason, "leftOriginalRod", StringComparison.OrdinalIgnoreCase);
            result.StillHoldingOriginalRod = string.Equals(reason, "stillHoldingOriginalRod", StringComparison.OrdinalIgnoreCase);
        }

        private static void StoreApplyResult(Guid requestId, FishingAutoEquipmentActionResult result)
        {
            lock (SyncRoot)
            {
                ApplyResults[requestId] = result ?? new FishingAutoEquipmentActionResult();
            }
        }

        private static void StoreRestoreResult(Guid requestId, FishingAutoEquipmentActionResult result)
        {
            lock (SyncRoot)
            {
                RestoreResults[requestId] = result ?? new FishingAutoEquipmentActionResult();
            }
        }

        private static List<FishingAutoEquipmentMoveRecord> CopyRecords(IList<FishingAutoEquipmentMoveRecord> records)
        {
            var result = new List<FishingAutoEquipmentMoveRecord>();
            if (records == null)
            {
                return result;
            }

            for (var index = 0; index < records.Count; index++)
            {
                result.Add(CloneRecord(records[index]));
            }

            return result;
        }

        private static FishingAutoEquipmentMoveRecord CloneRecord(FishingAutoEquipmentMoveRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new FishingAutoEquipmentMoveRecord
            {
                MoveId = record.MoveId,
                TargetEquipmentSlot = record.TargetEquipmentSlot,
                SourceContainerKind = record.SourceContainerKind,
                SourceSlot = record.SourceSlot,
                FishingItemSignature = CloneSignature(record.FishingItemSignature),
                OriginalTargetWasAir = record.OriginalTargetWasAir,
                OriginalTargetItemSignature = CloneSignature(record.OriginalTargetItemSignature),
                OriginalTargetHoldingContainerKind = record.OriginalTargetHoldingContainerKind,
                OriginalTargetHoldingSlot = record.OriginalTargetHoldingSlot,
                ApplyStatus = record.ApplyStatus,
                RestoreStatus = record.RestoreStatus
            };
        }

        private static FishingAutoEquipmentItemSignature CloneSignature(FishingAutoEquipmentItemSignature signature)
        {
            if (signature == null)
            {
                return new FishingAutoEquipmentItemSignature();
            }

            return new FishingAutoEquipmentItemSignature
            {
                Type = signature.Type,
                Stack = signature.Stack,
                Prefix = signature.Prefix,
                Name = signature.Name ?? string.Empty
            };
        }

        private static string AppendReason(string current, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return current ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                return reason;
            }

            return current + "," + reason;
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
