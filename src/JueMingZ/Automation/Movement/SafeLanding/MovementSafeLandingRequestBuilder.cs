using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Movement
{
    internal static class MovementSafeLandingRequestBuilder
    {
        // These builders describe ActionQueue work only. SafeLanding rescue and
        // restore must stay inside controlled input/equipment executors.
        public static InputActionRequest BuildJumpRequest(MovementSafeLandingRescuePlan plan, MovementSafeLandingAnalysis analysis, string description, InputActionPriority priority)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                Priority = priority,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.MovementSafeLanding,
                Description = description ?? "Movement safe landing uses a vanilla control input",
                Timeout = plan == null || plan.Timeout == TimeSpan.Zero ? TimeSpan.FromMilliseconds(450) : plan.Timeout,
                QueueTimeout = plan == null || plan.QueueTimeout == TimeSpan.Zero ? TimeSpan.FromMilliseconds(250) : plan.QueueTimeout,
                AdmissionKey = BuildAdmissionKey(analysis, plan, "jump"),
                IsExclusive = true
            };
            MovementSafeLandingMetadataBuilder.AddBaseMetadata(request.Metadata, analysis, plan);
            request.Metadata["JumpMode"] = "SafeLandingTakeover";
            return request;
        }

        public static InputActionRequest BuildRecoveryJumpRequest(string strategyId, string actionType, string priority, string capabilitySummary, float originalGravityDirection, string description)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                Priority = InputActionPriority.Normal,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.MovementSafeLanding,
                Description = description ?? "Movement safe landing recovery input",
                Timeout = TimeSpan.FromMilliseconds(450),
                QueueTimeout = TimeSpan.FromMilliseconds(500),
                AdmissionKey = FeatureIds.MovementSafeLanding + "|recovery|" + (actionType ?? string.Empty) + "|" + (capabilitySummary ?? string.Empty),
                IsExclusive = true
            };
            MovementSafeLandingMetadataBuilder.EnsurePreservedKeys(request.Metadata);
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.MovementSafeLanding;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["JumpMode"] = "SafeLandingTakeover";
            request.Metadata["SafeLandingStrategy"] = strategyId ?? string.Empty;
            request.Metadata["SafeLandingActionType"] = actionType ?? string.Empty;
            request.Metadata["SafeLandingPriority"] = priority ?? "1";
            request.Metadata["SafeLandingCapabilitySummary"] = capabilitySummary ?? string.Empty;
            request.Metadata["SafeLandingSuppressDown"] = "false";
            request.Metadata["SafeLandingGravityOriginalDirection"] = originalGravityDirection.ToString("0.###", CultureInfo.InvariantCulture);
            request.Metadata["SafeLandingStrategyCatalogVersion"] = MovementSafeLandingStrategyCatalog.Version;
            return request;
        }

        public static InputActionRequest BuildTemporaryEquipmentApplyRequest(MovementSafeLandingEquipmentPlan plan, MovementSafeLandingAnalysis analysis)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.InventorySlot,
                Priority = InputActionPriority.High,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.MovementSafeLanding,
                Description = "Movement safe landing temporary equipment",
                Timeout = TimeSpan.FromMilliseconds(650),
                QueueTimeout = TimeSpan.FromMilliseconds(300),
                AdmissionKey = BuildAdmissionKey(analysis, PlanFromEquipment(plan), "temporaryEquipmentApply"),
                IsExclusive = true
            };
            MovementSafeLandingMetadataBuilder.AddBaseMetadata(request.Metadata, analysis, PlanFromEquipment(plan));
            request.Metadata["SafeLandingRescueMode"] = "TemporaryEquipmentApply";
            MovementSafeLandingMetadataBuilder.AddEquipmentMetadata(request.Metadata, plan);
            request.Metadata["SafeLandingImpactTicks"] = plan == null ? string.Empty : plan.ImpactTicks.ToString("0.###", CultureInfo.InvariantCulture);
            request.Metadata["SafeLandingImpactDistancePixels"] = plan == null ? string.Empty : plan.ImpactDistancePixels.ToString(CultureInfo.InvariantCulture);
            request.Metadata["SafeLandingFallingSpeed"] = plan == null ? string.Empty : plan.FallingSpeed.ToString("0.###", CultureInfo.InvariantCulture);
            request.Metadata["SafeLandingCapabilitySummary"] = plan == null ? string.Empty : plan.CapabilitySummary ?? string.Empty;
            request.Metadata["SafeLandingSuppressDown"] = plan != null && plan.SuppressDown ? "true" : "false";
            return request;
        }

        public static InputActionRequest BuildTeleportRodRequest(MovementSafeLandingRescuePlan plan, MovementSafeLandingAnalysis analysis)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.UseHotbarItem,
                Priority = plan == null ? InputActionPriority.High : plan.RequestPriority,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.MovementSafeLanding,
                Description = "Movement safe landing uses teleport rod toward the projected landing tile",
                Timeout = plan == null || plan.Timeout == TimeSpan.Zero ? TimeSpan.FromMilliseconds(650) : plan.Timeout,
                QueueTimeout = plan == null || plan.QueueTimeout == TimeSpan.Zero ? TimeSpan.FromMilliseconds(250) : plan.QueueTimeout,
                AdmissionKey = BuildAdmissionKey(analysis, plan, "teleportRod"),
                IsExclusive = true
            };

            MovementSafeLandingMetadataBuilder.AddBaseMetadata(request.Metadata, analysis, plan);
            var slot = ResolveTeleportRodUseSlot(analysis);
            request.Metadata["Slot"] = slot.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.TargetSlot] = slot.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldX] = analysis == null ? string.Empty : analysis.TeleportTargetWorldX.ToString("0.###", CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldY] = analysis == null ? string.Empty : analysis.TeleportTargetWorldY.ToString("0.###", CultureInfo.InvariantCulture);
            request.Metadata["ApplyMainMouseLeftForItemCheck"] = "true";
            request.Metadata["SafeLandingRescueMode"] = "TeleportRod";
            return request;
        }

        public static InputActionRequest BuildTemporaryEquipmentActivationRequest(
            MovementSafeLandingEquipmentMoveRecord record,
            MovementSafeLandingAnalysis analysis,
            int pendingRestoreCount,
            string activationSource)
        {
            var selectedPriority = record == null ? analysis == null ? -1 : analysis.SelectedPriority : record.SelectedPriority;
            var selectedStrategyId = record == null ? analysis == null ? string.Empty : analysis.SelectedStrategyId : record.StrategyId;
            var selectedActionType = record == null ? analysis == null ? string.Empty : analysis.SelectedActionType : record.ActionType;

            var plan = new MovementSafeLandingRescuePlan
            {
                Priority = selectedPriority,
                StrategyId = selectedStrategyId,
                ActionType = selectedActionType,
                RequestKind = InputActionKind.Jump,
                Timeout = TimeSpan.FromMilliseconds(450)
            };
            var request = BuildJumpRequest(plan, analysis, "Movement safe landing activates temporary equipment", InputActionPriority.High);
            request.Metadata["SafeLandingTemporaryEquipmentActivation"] = "true";
            request.Metadata["SafeLandingTemporaryEquipmentPendingRestoreCount"] = pendingRestoreCount.ToString(CultureInfo.InvariantCulture);
            request.Metadata["SafeLandingTemporaryEquipmentActivationSource"] = activationSource ?? string.Empty;
            MovementSafeLandingMetadataBuilder.AddEquipmentRecordMetadata(request.Metadata, record);
            return request;
        }

        public static InputActionRequest BuildTemporaryEquipmentRestoreRequest(IList<MovementSafeLandingEquipmentMoveRecord> records, string reason)
        {
            var first = records == null || records.Count == 0 ? null : records[0];
            // Restore uses recorded move signatures; request submission alone does
            // not clear pending equipment records.
            var request = new InputActionRequest
            {
                Kind = InputActionKind.InventorySlot,
                Priority = InputActionPriority.Normal,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.MovementSafeLanding,
                Description = "Movement safe landing restore temporary equipment",
                Timeout = TimeSpan.FromSeconds(3),
                QueueTimeout = TimeSpan.FromSeconds(2),
                AdmissionKey = FeatureIds.MovementSafeLanding + "|temporaryEquipmentRestore",
                IsExclusive = true
            };
            MovementSafeLandingMetadataBuilder.EnsurePreservedKeys(request.Metadata);
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.MovementSafeLanding;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["SafeLandingRescueMode"] = "TemporaryEquipmentRestore";
            MovementSafeLandingMetadataBuilder.AddEquipmentRecordMetadata(request.Metadata, first);
            request.Metadata["PendingRestoreCount"] = records == null ? "0" : records.Count.ToString(CultureInfo.InvariantCulture);
            request.Metadata["Reason"] = reason ?? string.Empty;
            request.Metadata["SafeLandingStrategyCatalogVersion"] = MovementSafeLandingStrategyCatalog.Version;
            return request;
        }

        public static bool PreservesRequiredMetadataKeys(InputActionRequest request, out string missingKey)
        {
            missingKey = string.Empty;
            if (request == null || request.Metadata == null)
            {
                missingKey = "metadataUnavailable";
                return false;
            }

            for (var index = 0; index < MovementSafeLandingMetadataBuilder.PreservedKeys.Length; index++)
            {
                var key = MovementSafeLandingMetadataBuilder.PreservedKeys[index];
                if (!request.Metadata.ContainsKey(key))
                {
                    missingKey = key;
                    return false;
                }
            }

            return true;
        }

        private static MovementSafeLandingRescuePlan PlanFromEquipment(MovementSafeLandingEquipmentPlan plan)
        {
            if (plan == null)
            {
                return null;
            }

            return new MovementSafeLandingRescuePlan
            {
                Priority = plan.SelectedPriority,
                StrategyId = plan.StrategyId,
                ActionType = plan.ActionType,
                RequestKind = InputActionKind.InventorySlot,
                Timeout = TimeSpan.FromMilliseconds(650),
                RequiresTemporaryEquipment = true,
                RequiresRestore = true
            };
        }

        private static string BuildAdmissionKey(MovementSafeLandingAnalysis analysis, MovementSafeLandingRescuePlan plan, string suffix)
        {
            return FeatureIds.MovementSafeLanding +
                   "|" + (suffix ?? string.Empty) +
                   "|" + (plan == null ? string.Empty : plan.StrategyId ?? string.Empty) +
                   "|" + (plan == null ? string.Empty : plan.ActionType ?? string.Empty) +
                   "|" + (analysis == null ? string.Empty : analysis.SelectedPriority.ToString(CultureInfo.InvariantCulture));
        }

        private static int ResolveTeleportRodUseSlot(MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null)
            {
                return -1;
            }

            return analysis.TeleportRodInventorySlot;
        }
    }
}
