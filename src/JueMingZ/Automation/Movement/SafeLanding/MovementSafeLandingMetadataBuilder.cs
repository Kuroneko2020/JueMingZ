using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Movement
{
    internal static class MovementSafeLandingMetadataBuilder
    {
        public static readonly string[] PreservedKeys =
        {
            "SafeLandingStrategy",
            "SafeLandingActionType",
            "SafeLandingPriority",
            "SafeLandingImpactTicks",
            "SafeLandingImpactDistancePixels",
            "SafeLandingImpactWorldX",
            "SafeLandingImpactWorldY",
            "SafeLandingFallingSpeed",
            "SafeLandingVelocityX",
            "SafeLandingCapabilitySummary",
            "SafeLandingSuppressDown",
            "SafeLandingGrappleTargetWorldX",
            "SafeLandingGrappleTargetWorldY",
            "SafeLandingTeleportRodItemType",
            "SafeLandingTeleportRodItemName",
            "SafeLandingTeleportRodInventorySlot",
            "SafeLandingTeleportTargetTileX",
            "SafeLandingTeleportTargetTileY",
            "SafeLandingTeleportTargetWorldX",
            "SafeLandingTeleportTargetWorldY",
            "SafeLandingBlockItemType",
            "SafeLandingBlockItemName",
            "SafeLandingBlockCreateTile",
            "SafeLandingBlockPlaceStyle",
            "SafeLandingBlockInventorySlot",
            "SafeLandingBlockHotbarSlot",
            "SafeLandingBlockTargetTileX",
            "SafeLandingBlockTargetTileY",
            "SafeLandingBlockTargetWorldX",
            "SafeLandingBlockTargetWorldY",
            "SafeLandingGravityOriginalDirection",
            "SafeLandingRescueMode",
            "SafeLandingEquipmentCategory",
            "SafeLandingEquipmentSourceKind",
            "SafeLandingEquipmentSourceSlot",
            "SafeLandingEquipmentTargetKind",
            "SafeLandingEquipmentTargetSlot",
            "SafeLandingEquipmentItemType",
            "SafeLandingEquipmentMountType",
            "SafeLandingLandingSurfaceKnown",
            "SafeLandingLandingContactWorldX",
            "SafeLandingLandingContactWorldY",
            "SafeLandingLandingContactTileX",
            "SafeLandingLandingContactTileY",
            "SafeLandingLandingSurfaceKind",
            "SafeLandingLandingSlopeType",
            "SafeLandingLandingSlopeDirection",
            "SafeLandingLandingContactSample",
            "SafeLandingLandingMovingIntoSlope",
            "SafeLandingLandingMovingWithSlope",
            "SafeLandingLandingProjectedPlayerLeftX",
            "SafeLandingLandingProjectedPlayerRightX",
            "SafeLandingLandingProjectedPlayerBottomY",
            "SafeLandingLandingSurfaceSummary",
            "SafeLandingGrappleHookSpeed",
            "SafeLandingGrappleTargetSource",
            "SafeLandingGrappleTargetFromLandingSurface",
            "SafeLandingGrappleTargetDistancePixels",
            "SafeLandingGrappleHookVerticalSpeed",
            "SafeLandingGrappleRelativeDownSpeed",
            "SafeLandingGrappleRequiredLeadTicks",
            "SafeLandingGrappleRequiredLeadPixels",
            "SafeLandingGrappleEstimatedTicksToTarget",
            "SafeLandingGrappleTooEarly",
            "SafeLandingGrappleTooLate",
            "SafeLandingGrappleTooSlowForDownwardSurface",
            "SafeLandingGrappleTimingSummary"
        };

        public static void AddBaseMetadata(IDictionary<string, string> metadata, MovementSafeLandingAnalysis analysis, MovementSafeLandingRescuePlan plan)
        {
            if (metadata == null)
            {
                return;
            }

            metadata[ActionMetadataKeys.Scenario] = ScenarioNames.MovementSafeLanding;
            metadata[ActionMetadataKeys.SourceKind] = "Automation";
            EnsurePreservedKeys(metadata);
            metadata["SafeLandingStrategy"] = plan == null ? string.Empty : plan.StrategyId ?? string.Empty;
            metadata["SafeLandingActionType"] = plan == null ? string.Empty : plan.ActionType ?? string.Empty;
            metadata["SafeLandingPriority"] = plan == null ? "-1" : plan.Priority.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingImpactTicks"] = analysis == null ? string.Empty : analysis.ImpactTicks.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingImpactDistancePixels"] = analysis == null ? string.Empty : analysis.ImpactDistancePixels.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingImpactWorldX"] = analysis == null ? string.Empty : analysis.ImpactWorldX.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingImpactWorldY"] = analysis == null ? string.Empty : analysis.ImpactWorldY.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingFallingSpeed"] = analysis == null ? string.Empty : analysis.FallingSpeed.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingVelocityX"] = analysis == null ? string.Empty : analysis.VelocityX.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingCapabilitySummary"] = analysis == null ? string.Empty : analysis.ActiveCapabilitySummary ?? string.Empty;
            metadata["SafeLandingSuppressDown"] = analysis != null && analysis.ControlDown ? "true" : "false";
            metadata["SafeLandingGrappleTargetWorldX"] = analysis == null ? string.Empty : analysis.GrappleTargetWorldX.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingGrappleTargetWorldY"] = analysis == null ? string.Empty : analysis.GrappleTargetWorldY.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingTeleportRodItemType"] = analysis == null ? string.Empty : analysis.TeleportRodItemType.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingTeleportRodItemName"] = analysis == null ? string.Empty : analysis.TeleportRodItemName ?? string.Empty;
            metadata["SafeLandingTeleportRodInventorySlot"] = analysis == null ? string.Empty : analysis.TeleportRodInventorySlot.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingTeleportTargetTileX"] = analysis == null ? string.Empty : analysis.TeleportTargetTileX.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingTeleportTargetTileY"] = analysis == null ? string.Empty : analysis.TeleportTargetTileY.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingTeleportTargetWorldX"] = analysis == null ? string.Empty : analysis.TeleportTargetWorldX.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingTeleportTargetWorldY"] = analysis == null ? string.Empty : analysis.TeleportTargetWorldY.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingBlockItemType"] = analysis == null ? string.Empty : analysis.CushionBlockItemType.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingBlockItemName"] = analysis == null ? string.Empty : analysis.CushionBlockItemName ?? string.Empty;
            metadata["SafeLandingBlockCreateTile"] = analysis == null ? string.Empty : analysis.CushionBlockCreateTile.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingBlockPlaceStyle"] = analysis == null ? string.Empty : analysis.CushionBlockPlaceStyle.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingBlockInventorySlot"] = analysis == null ? string.Empty : analysis.CushionBlockInventorySlot.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingBlockHotbarSlot"] = analysis == null ? string.Empty : analysis.CushionBlockHotbarSlot.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingBlockTargetTileX"] = analysis == null ? string.Empty : analysis.BlockPlacementTileX.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingBlockTargetTileY"] = analysis == null ? string.Empty : analysis.BlockPlacementTileY.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingBlockTargetWorldX"] = analysis == null ? string.Empty : analysis.BlockPlacementWorldX.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingBlockTargetWorldY"] = analysis == null ? string.Empty : analysis.BlockPlacementWorldY.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingGravityOriginalDirection"] = analysis == null ? string.Empty : analysis.GravityDirection.ToString("0.###", CultureInfo.InvariantCulture);

            // Landing surface metadata (grapple-surface-aim)
            metadata["SafeLandingLandingSurfaceKnown"] = BoolText(analysis != null && analysis.LandingSurfaceKnown);
            metadata["SafeLandingLandingContactWorldX"] = analysis == null ? string.Empty : analysis.LandingContactWorldX.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingLandingContactWorldY"] = analysis == null ? string.Empty : analysis.LandingContactWorldY.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingLandingContactTileX"] = analysis == null ? string.Empty : analysis.LandingContactTileX.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingLandingContactTileY"] = analysis == null ? string.Empty : analysis.LandingContactTileY.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingLandingSurfaceKind"] = analysis == null ? string.Empty : analysis.LandingSurfaceKind ?? string.Empty;
            metadata["SafeLandingLandingSlopeType"] = analysis == null ? string.Empty : analysis.LandingSlopeType.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingLandingSlopeDirection"] = analysis == null ? string.Empty : analysis.LandingSlopeDirection ?? string.Empty;
            metadata["SafeLandingLandingContactSample"] = analysis == null ? string.Empty : analysis.LandingContactSample ?? string.Empty;
            metadata["SafeLandingLandingMovingIntoSlope"] = BoolText(analysis != null && analysis.LandingMovingIntoSlope);
            metadata["SafeLandingLandingMovingWithSlope"] = BoolText(analysis != null && analysis.LandingMovingWithSlope);
            metadata["SafeLandingLandingProjectedPlayerLeftX"] = analysis == null ? string.Empty : analysis.LandingProjectedPlayerLeftX.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingLandingProjectedPlayerRightX"] = analysis == null ? string.Empty : analysis.LandingProjectedPlayerRightX.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingLandingProjectedPlayerBottomY"] = analysis == null ? string.Empty : analysis.LandingProjectedPlayerBottomY.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingLandingSurfaceSummary"] = analysis == null ? string.Empty : analysis.LandingSurfaceSummary ?? string.Empty;

            // Grapple diagnostic metadata (grapple-surface-aim)
            metadata["SafeLandingGrappleHookSpeed"] = analysis == null ? string.Empty : analysis.GrappleHookSpeed.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingGrappleTargetSource"] = analysis == null ? string.Empty : analysis.GrappleTargetSource ?? string.Empty;
            metadata["SafeLandingGrappleTargetFromLandingSurface"] = BoolText(analysis != null && analysis.GrappleTargetFromLandingSurface);
            metadata["SafeLandingGrappleTargetDistancePixels"] = analysis == null ? string.Empty : analysis.GrappleTargetDistancePixels.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingGrappleHookVerticalSpeed"] = analysis == null ? string.Empty : analysis.GrappleHookVerticalSpeed.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingGrappleRelativeDownSpeed"] = analysis == null ? string.Empty : analysis.GrappleRelativeDownSpeed.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingGrappleRequiredLeadTicks"] = analysis == null ? string.Empty : analysis.GrappleRequiredLeadTicks.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingGrappleRequiredLeadPixels"] = analysis == null ? string.Empty : analysis.GrappleRequiredLeadPixels.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingGrappleEstimatedTicksToTarget"] = analysis == null ? string.Empty : analysis.GrappleEstimatedTicksToTarget.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["SafeLandingGrappleTooEarly"] = BoolText(analysis != null && analysis.GrappleTooEarly);
            metadata["SafeLandingGrappleTooLate"] = BoolText(analysis != null && analysis.GrappleTooLate);
            metadata["SafeLandingGrappleTooSlowForDownwardSurface"] = BoolText(analysis != null && analysis.GrappleTooSlowForDownwardSurface);
            metadata["SafeLandingGrappleTimingSummary"] = analysis == null ? string.Empty : analysis.GrappleTimingSummary ?? string.Empty;

            metadata["SafeLandingStrategyCatalogVersion"] = MovementSafeLandingStrategyCatalog.Version;
            metadata["SafeLandingSelectedPlanSummary"] = analysis == null ? string.Empty : analysis.SelectedPlanSummary ?? string.Empty;
        }

        public static void AddEquipmentMetadata(IDictionary<string, string> metadata, MovementSafeLandingEquipmentPlan plan)
        {
            if (metadata == null)
            {
                return;
            }

            EnsurePreservedKeys(metadata);
            if (plan == null)
            {
                return;
            }

            metadata["SafeLandingStrategy"] = plan.StrategyId ?? string.Empty;
            metadata["SafeLandingActionType"] = plan.ActionType ?? string.Empty;
            metadata["SafeLandingPriority"] = plan.SelectedPriority.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingEquipmentCategory"] = plan.EquipmentCategory ?? string.Empty;
            metadata["SafeLandingEquipmentSourceKind"] = MovementSafeLandingEquipmentCompat.ContainerKindName(plan.SourceContainerKind);
            metadata["SafeLandingEquipmentSourceSlot"] = plan.SourceSlot.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingEquipmentTargetKind"] = MovementSafeLandingEquipmentCompat.ContainerKindName(plan.TargetContainerKind);
            metadata["SafeLandingEquipmentTargetSlot"] = plan.TargetSlot.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingEquipmentItemType"] = plan.CandidateItemType.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingEquipmentMountType"] = plan.CandidateMountType.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingEquipmentItemName"] = plan.CandidateSignature == null ? string.Empty : plan.CandidateSignature.Name ?? string.Empty;
            metadata["SafeLandingApplyRocketRelease"] = plan.ApplyRocketRelease ? "true" : "false";
            metadata["SafeLandingHoldTicks"] = plan.HoldTicks.ToString(CultureInfo.InvariantCulture);
        }

        public static void AddEquipmentRecordMetadata(IDictionary<string, string> metadata, MovementSafeLandingEquipmentMoveRecord record)
        {
            if (metadata == null)
            {
                return;
            }

            EnsurePreservedKeys(metadata);
            if (record == null)
            {
                metadata["SafeLandingPriority"] = "-1";
                return;
            }

            metadata["SafeLandingStrategy"] = record.StrategyId ?? string.Empty;
            metadata["SafeLandingEquipmentCategory"] = record.EquipmentCategory ?? string.Empty;
            metadata["SafeLandingActionType"] = record.ActionType ?? string.Empty;
            metadata["SafeLandingPriority"] = record.SelectedPriority.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingEquipmentSourceKind"] = MovementSafeLandingEquipmentCompat.ContainerKindName(record.SourceContainerKind);
            metadata["SafeLandingEquipmentSourceSlot"] = record.SourceSlot.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingEquipmentTargetKind"] = MovementSafeLandingEquipmentCompat.ContainerKindName(record.TargetContainerKind);
            metadata["SafeLandingEquipmentTargetSlot"] = record.TargetSlot.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingEquipmentItemType"] = record.CandidateItemType.ToString(CultureInfo.InvariantCulture);
            metadata["SafeLandingEquipmentMountType"] = record.CandidateMountType.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolText(bool value)
        {
            return value ? "true" : "false";
        }

        public static void EnsurePreservedKeys(IDictionary<string, string> metadata)
        {
            if (metadata == null)
            {
                return;
            }

            for (var index = 0; index < PreservedKeys.Length; index++)
            {
                var key = PreservedKeys[index];
                if (!metadata.ContainsKey(key))
                {
                    metadata[key] = string.Empty;
                }
            }
        }
    }
}
