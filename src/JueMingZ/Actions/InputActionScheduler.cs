using System.Collections.Generic;
using System;
using JueMingZ.Common;

namespace JueMingZ.Actions
{
    public enum InputActionSchedulerBucket
    {
        UserExplicitCommand = 20,
        SafetyOrRecoveryAutomation = 30,
        ForegroundSessionContinuation = 40,
        TimeSensitiveWorldAutomation = 50,
        BackgroundInventoryTransaction = 60,
        BackgroundAutomation = 70,
        Unknown = 80
    }

    public static class InputActionScheduler
    {
        public static InputActionRequest SelectNext(IEnumerable<InputActionRequest> pending)
        {
            if (pending == null)
            {
                return null;
            }

            InputActionRequest best = null;
            foreach (var request in pending)
            {
                if (request == null)
                {
                    continue;
                }

                if (best == null ||
                    request.Priority > best.Priority ||
                    (request.Priority == best.Priority && CompareSchedulerBucket(request, best) < 0) ||
                    (request.Priority == best.Priority &&
                     CompareSchedulerBucket(request, best) == 0 &&
                     request.CreatedUtc < best.CreatedUtc))
                {
                    best = request;
                }
            }

            return best;
        }

        public static int ComparePriorityThenCreated(InputActionRequest left, InputActionRequest right)
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

            if (left.Priority != right.Priority)
            {
                return right.Priority.CompareTo(left.Priority);
            }

            var bucketCompare = CompareSchedulerBucket(left, right);
            if (bucketCompare != 0)
            {
                return bucketCompare;
            }

            return left.CreatedUtc.CompareTo(right.CreatedUtc);
        }

        public static InputActionSchedulerBucket ResolveBucket(InputActionRequest request)
        {
            if (request == null)
            {
                return InputActionSchedulerBucket.Unknown;
            }

            var sourceKind = GetMetadata(request, ActionMetadataKeys.SourceKind);
            if (string.Equals(sourceKind, "Hotkey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceKind, "User", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceKind, "Ui", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceKind, "Diagnostic", StringComparison.OrdinalIgnoreCase))
            {
                return InputActionSchedulerBucket.UserExplicitCommand;
            }

            var source = request.SourceFeatureId ?? string.Empty;
            if (string.Equals(source, FeatureIds.MovementSafeLanding, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "buff.auto_heal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "buff.auto_mana", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "buff.nurse_auto_heal", StringComparison.OrdinalIgnoreCase))
            {
                return InputActionSchedulerBucket.SafetyOrRecoveryAutomation;
            }

            if (string.Equals(source, FeatureIds.WorldAutomationAutoCaptureCritter, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, FeatureIds.WorldAutomationAutoHarvest, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, FeatureIds.WorldAutomationAutoMining, StringComparison.OrdinalIgnoreCase))
            {
                return InputActionSchedulerBucket.TimeSensitiveWorldAutomation;
            }

            if (string.Equals(source, FeatureIds.InventoryAutoStack, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, FeatureIds.InventoryAutoSell, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, FeatureIds.InventoryAutoDiscard, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, FeatureIds.InventoryAutoDepositCoins, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, FeatureIds.InventoryKeepFavorited, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, FeatureIds.FishingAutoStoreQuestFish, StringComparison.OrdinalIgnoreCase))
            {
                return InputActionSchedulerBucket.BackgroundInventoryTransaction;
            }

            if (string.Equals(sourceKind, "Automation", StringComparison.OrdinalIgnoreCase))
            {
                return InputActionSchedulerBucket.BackgroundAutomation;
            }

            return InputActionSchedulerBucket.Unknown;
        }

        public static string ResolveBucketName(InputActionRequest request)
        {
            var bucket = ResolveBucket(request);
            return "P" + ((int)bucket / 10).ToString() + ":" + bucket;
        }

        public static bool IsUserExplicitCommand(InputActionRequest request)
        {
            return ResolveBucket(request) == InputActionSchedulerBucket.UserExplicitCommand;
        }

        public static bool IsBackgroundAutomation(InputActionRequest request)
        {
            var bucket = ResolveBucket(request);
            return bucket == InputActionSchedulerBucket.TimeSensitiveWorldAutomation ||
                   bucket == InputActionSchedulerBucket.BackgroundInventoryTransaction ||
                   bucket == InputActionSchedulerBucket.BackgroundAutomation;
        }

        private static int CompareSchedulerBucket(InputActionRequest left, InputActionRequest right)
        {
            return ((int)ResolveBucket(left)).CompareTo((int)ResolveBucket(right));
        }

        private static string GetMetadata(InputActionRequest request, string key)
        {
            if (request == null ||
                request.Metadata == null ||
                string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string value;
            return request.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }
    }
}
