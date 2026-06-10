using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.Movement;

namespace JueMingZ.Compat
{
    public static partial class MovementSafeLandingCompat
    {
        private static bool TryResolveAlreadySafe(object player, JumpInputProfile jump, out string reason)
        {
            return TryResolveAlreadySafe(player, jump, null, out reason);
        }

        private static bool TryResolveAlreadySafe(object player, JumpInputProfile jump, MovementSafeLandingAnalysis analysis, out string reason)
        {
            if (analysis != null)
            {
                var activeGrapCount = analysis.FallingSpeed >= MinimumDangerFallingSpeed
                    ? 0
                    : analysis.RawGrapCount;
                return TryResolveAlreadySafeCore(
                    analysis.RawCreativeGodMode,
                    analysis.RawNoFallDmg,
                    analysis.RawSlowFall,
                    analysis.RawWet,
                    analysis.RawHoneyWet,
                    analysis.RawShimmering,
                    analysis.RawWebbed,
                    analysis.RawStoned,
                    activeGrapCount,
                    analysis.RawEquippedWingCount,
                    analysis.HasWingFlight,
                    analysis.RawMountNoFallDamage,
                    out reason);
            }

            var creativeGodMode = TryReadBool(player, "creativeGodMode", false);
            var noFallDmg = TryReadBool(player, "noFallDmg", false);
            var slowFall = TryReadBool(player, "slowFall", false);
            var wet = TryReadBool(player, "wet", false);
            var honeyWet = TryReadBool(player, "honeyWet", false);
            var shimmering = TryReadBool(player, "shimmering", false);
            var webbed = TryReadBool(player, "webbed", false);
            var stoned = TryReadBool(player, "stoned", false);
            var grapCount = TryReadInt(player, "grapCount", 0);
            var equippedWingCount = TryCountEquippedFallDamageWings(player);
            var wingFlightState = jump != null && jump.HasWingFlight;
            var mountNoFallDamage = TryReadMountNoFallDamage(player);

            return TryResolveAlreadySafeCore(
                creativeGodMode,
                noFallDmg,
                slowFall,
                wet,
                honeyWet,
                shimmering,
                webbed,
                stoned,
                grapCount,
                equippedWingCount,
                wingFlightState,
                mountNoFallDamage,
                out reason);
        }

        private static void PopulateAlreadySafeRawState(object player, MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null)
            {
                return;
            }

            analysis.RawCreativeGodMode = TryReadBool(player, "creativeGodMode", false);
            analysis.RawNoFallDmg = TryReadBool(player, "noFallDmg", false);
            analysis.RawSlowFall = TryReadBool(player, "slowFall", false);
            analysis.RawWet = TryReadBool(player, "wet", false);
            analysis.RawHoneyWet = TryReadBool(player, "honeyWet", false);
            analysis.RawShimmering = TryReadBool(player, "shimmering", false);
            analysis.RawWebbed = TryReadBool(player, "webbed", false);
            analysis.RawStoned = TryReadBool(player, "stoned", false);
            analysis.RawGrapCount = TryReadInt(player, "grapCount", 0);
            analysis.RawEquippedWingCount = TryCountEquippedFallDamageWings(player);
            analysis.RawMountNoFallDamage = TryReadMountNoFallDamage(player);
        }

        private static bool TryResolveAlreadySafeCore(
            bool creativeGodMode,
            bool noFallDmg,
            bool slowFall,
            bool wet,
            bool honeyWet,
            bool shimmering,
            bool webbed,
            bool stoned,
            int grapCount,
            int equippedWingCount,
            bool wingFlightState,
            bool mountNoFallDamage,
            out string reason)
        {
            reason = string.Empty;
            if (creativeGodMode)
            {
                reason = "creativeGodMode";
                return true;
            }

            if (!stoned && noFallDmg)
            {
                reason = "noFallDmg";
                return true;
            }

            if (slowFall)
            {
                reason = "slowFall";
                return true;
            }

            if (wet || honeyWet || shimmering)
            {
                reason = "liquid";
                return true;
            }

            if (webbed)
            {
                reason = "webbed";
                return true;
            }

            if (!stoned && grapCount > 0)
            {
                reason = "grappled";
                return true;
            }

            if (!stoned && equippedWingCount > 0)
            {
                reason = "wingsEquipped";
                return true;
            }

            if (!stoned && wingFlightState)
            {
                reason = "wingFlightState";
                return true;
            }

            if (!stoned && mountNoFallDamage)
            {
                reason = "safeMount";
                return true;
            }

            return false;
        }

        private static int TryCountEquippedFallDamageWings(object player)
        {
            var armor = GetMember(player, "armor") as IList;
            if (armor == null || armor.Count <= 3)
            {
                return 0;
            }

            var count = 0;
            var end = Math.Min(10, armor.Count);
            for (var slot = 3; slot < end; slot++)
            {
                if (!TryIsAccessorySlotUsable(player, slot))
                {
                    continue;
                }

                var item = armor[slot];
                if (item == null)
                {
                    continue;
                }

                int stack;
                int wingSlot;
                if (TryReadInt(item, "stack", out stack) &&
                    stack > 0 &&
                    TryReadInt(item, "wingSlot", out wingSlot) &&
                    wingSlot > -1)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryIsAccessorySlotUsable(object player, int slot)
        {
            if (player == null || slot < 3 || slot > 9)
            {
                return false;
            }

            try
            {
                var slotUsability = ResolveSlotUsabilityMethod(player.GetType());
                if (slotUsability != null)
                {
                    return Convert.ToBoolean(slotUsability.Invoke(player, new object[] { slot }));
                }
            }
            catch
            {
                return false;
            }

            return slot <= 7;
        }

        private static MethodInfo ResolveSlotUsabilityMethod(Type playerType)
        {
            if (_slotUsabilityMethodResolved)
            {
                return _slotUsabilityMethod;
            }

            _slotUsabilityMethodResolved = true;
            if (playerType == null)
            {
                _slotUsabilityMethod = null;
                return null;
            }

            _slotUsabilityMethod = playerType.GetMethod(
                "IsItemSlotUnlockedAndUsable",
                InstanceFlags,
                null,
                new[] { typeof(int) },
                null);
            return _slotUsabilityMethod;
        }

        private static bool TryResolveProjectedSafeLanding(MovementSafeLandingAnalysis analysis, int impactDistancePixels, out string reason)
        {
            reason = string.Empty;
            if (analysis == null || analysis.FallingSpeed < MinimumDangerFallingSpeed)
            {
                return false;
            }

            Array tiles;
            if (!TryGetMainTileArray(out tiles))
            {
                return false;
            }

            var maxDistance = impactDistancePixels >= 0
                ? Math.Max(16, Math.Min(impactDistancePixels, 768))
                : Math.Min(768, Math.Max(128, (int)(analysis.FallingSpeed * 24f) + 96));
            var gravityDirection = analysis.GravityDirection >= 0f ? 1 : -1;
            var scanOriginY = analysis.GravityDirection >= 0f
                ? analysis.PositionY + analysis.Height
                : analysis.PositionY;
            for (var offset = 0; offset <= maxDistance; offset += 16)
            {
                var tileY = (int)Math.Floor((scanOriginY + offset * gravityDirection) / 16f);
                if (ScanProjectedSafeLandingTiles(tiles, analysis, tileY, analysis.PositionX, out reason))
                {
                    return true;
                }

                var projectedX = ResolveProjectedImpactProbeX(analysis.PositionX, analysis.VelocityX, analysis.FallingSpeed, offset);
                if (!NearlyEqual(projectedX, analysis.PositionX) &&
                    ScanProjectedSafeLandingTiles(tiles, analysis, tileY, projectedX, out reason))
                {
                    return true;
                }

                var middleX = (analysis.PositionX + projectedX) / 2f;
                if (!NearlyEqual(middleX, analysis.PositionX) &&
                    !NearlyEqual(middleX, projectedX) &&
                    ScanProjectedSafeLandingTiles(tiles, analysis, tileY, middleX, out reason))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ScanProjectedSafeLandingTiles(Array tiles, MovementSafeLandingAnalysis analysis, int tileY, float positionX, out string reason)
        {
            reason = string.Empty;
            if (tiles == null || analysis == null)
            {
                return false;
            }

            var leftTile = (int)Math.Floor((positionX + 2f) / 16f);
            var rightTile = (int)Math.Floor((positionX + Math.Max(2, analysis.Width - 2)) / 16f);
            if (rightTile < leftTile)
            {
                var temp = leftTile;
                leftTile = rightTile;
                rightTile = temp;
            }

            for (var tileX = leftTile; tileX <= rightTile; tileX++)
            {
                var tile = GetTile(tiles, tileX, tileY);
                if (IsProjectedSafeTile(tile, out reason))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindImpactDistance(MovementSafeLandingAnalysis analysis, out int distance)
        {
            float impactPositionX;
            return TryFindImpact(analysis, out distance, out impactPositionX);
        }

        private static bool TryFindImpact(MovementSafeLandingAnalysis analysis, out int distance, out float impactPositionX)
        {
            distance = -1;
            impactPositionX = analysis == null ? 0f : analysis.PositionX;
            if (analysis == null)
            {
                return false;
            }

            MovementLandingSurfaceHit hit;
            if (!TryFindLandingSurface(analysis, out hit) || hit == null || !hit.Found)
            {
                return false;
            }

            distance = hit.ImpactDistancePixels;
            impactPositionX = hit.ProjectedPlayerLeftX;
            return true;
        }

        private static bool TryFindLandingSurface(MovementSafeLandingAnalysis analysis, out MovementLandingSurfaceHit hit)
        {
            hit = new MovementLandingSurfaceHit();
            if (analysis == null)
            {
                return false;
            }

            Interlocked.Increment(ref _landingProbeCount);
            var probe = Math.Min(768, Math.Max(128, (int)(analysis.FallingSpeed * 24f) + 96));
            const int coarseStep = 16;
            var previous = 0;
            for (var offset = 8; offset <= probe; offset += coarseStep)
            {
                MovementLandingSurfaceHit coarseHit;
                if (TryProbeProjectedLandingSurface(analysis, offset, out coarseHit) && coarseHit != null && coarseHit.Found)
                {
                    var low = Math.Max(0, previous - 4);
                    var high = offset;
                    var bestHit = coarseHit;
                    while (high - low > 4)
                    {
                        var middle = low + (high - low) / 2;
                        MovementLandingSurfaceHit middleHit;
                        if (TryProbeProjectedLandingSurface(analysis, middle, out middleHit) && middleHit != null && middleHit.Found)
                        {
                            high = middle;
                            bestHit = middleHit;
                        }
                        else
                        {
                            low = middle + 1;
                        }
                    }

                    MovementLandingSurfaceHit finalHit;
                    hit = TryProbeProjectedLandingSurface(analysis, high, out finalHit) && finalHit != null && finalHit.Found
                        ? finalHit
                        : bestHit;
                    return true;
                }

                previous = offset;
            }

            return false;
        }

        private static bool TryProbeProjectedLandingSurface(MovementSafeLandingAnalysis analysis, int offset, out MovementLandingSurfaceHit hit)
        {
            hit = new MovementLandingSurfaceHit();
            if (analysis == null)
            {
                return false;
            }

            var direction = analysis.GravityDirection >= 0f ? 1f : -1f;
            var y = analysis.PositionY + offset * direction;
            var projectedX = ResolveProjectedImpactProbeX(analysis.PositionX, analysis.VelocityX, analysis.FallingSpeed, offset);
            MovementLandingSurfaceHit bestHit = null;
            MovementLandingSurfaceHit candidate;

            if (TryProbeLandingSurfaceAtOffset(analysis, projectedX, y, offset, out candidate) && candidate != null && candidate.Found)
            {
                bestHit = candidate;
            }

            var middleX = (analysis.PositionX + projectedX) / 2f;
            if (!NearlyEqual(middleX, projectedX) &&
                !NearlyEqual(middleX, analysis.PositionX) &&
                TryProbeLandingSurfaceAtOffset(analysis, middleX, y, offset, out candidate) &&
                candidate != null &&
                candidate.Found &&
                IsBetterProbeHit(candidate, bestHit, analysis))
            {
                bestHit = candidate;
            }

            if (!NearlyEqual(analysis.PositionX, projectedX) &&
                TryProbeLandingSurfaceAtOffset(analysis, analysis.PositionX, y, offset, out candidate) &&
                candidate != null &&
                candidate.Found &&
                IsBetterProbeHit(candidate, bestHit, analysis))
            {
                bestHit = candidate;
            }

            if (bestHit == null)
            {
                return false;
            }

            hit = bestHit;
            return true;
        }

        private static bool TryProbeLandingSurfaceAtOffset(
            MovementSafeLandingAnalysis analysis,
            float x,
            float y,
            int offset,
            out MovementLandingSurfaceHit hit)
        {
            hit = TryManualLandingSurfaceImpact(
                x,
                y,
                analysis.Width,
                analysis.Height,
                analysis.GravityDirection,
                analysis.FallingSpeed,
                analysis.VelocityX);
            if (hit != null && hit.Found)
            {
                ApplyImpactDistanceToHit(analysis, hit, x, y, offset);
                return true;
            }

            bool solid;
            if (TryProbeLandingCollision(x, y, analysis.Width, analysis.Height, analysis.GravityDirection, analysis.FallingSpeed, out solid) && solid)
            {
                hit = CreateLegacyLandingSurfaceHit(analysis, x, y, offset);
                return true;
            }

            hit = new MovementLandingSurfaceHit();
            return false;
        }

        private static MovementLandingSurfaceHit CreateLegacyLandingSurfaceHit(MovementSafeLandingAnalysis analysis, float x, float y, int offset)
        {
            var bottomY = analysis.GravityDirection >= 0f ? y + analysis.Height : y;
            var centerX = x + analysis.Width / 2f;
            var hit = new MovementLandingSurfaceHit
            {
                Found = true,
                ContactWorldX = centerX,
                ContactWorldY = bottomY,
                ContactTileX = (int)Math.Floor(centerX / 16f),
                ContactTileY = (int)Math.Floor(bottomY / 16f),
                SurfaceKind = "unknown",
                SlopeDirection = "unknown",
                ContactSample = "center_foot",
                Summary = "legacy_collision contact=center_foot"
            };
            ApplyImpactDistanceToHit(analysis, hit, x, y, offset);
            return hit;
        }

        private static void ApplyImpactDistanceToHit(MovementSafeLandingAnalysis analysis, MovementLandingSurfaceHit hit, float x, float y, int offset)
        {
            if (analysis == null || hit == null)
            {
                return;
            }

            hit.ImpactDistancePixels = Math.Max(0, offset);
            hit.ImpactTicks = analysis.FallingSpeed > 0.001f
                ? hit.ImpactDistancePixels / analysis.FallingSpeed
                : -1f;
            hit.ProjectedPlayerLeftX = x;
            hit.ProjectedPlayerRightX = x + analysis.Width;
            hit.ProjectedPlayerBottomY = analysis.GravityDirection >= 0f
                ? y + analysis.Height
                : y;
        }

        private static bool IsBetterProbeHit(MovementLandingSurfaceHit candidate, MovementLandingSurfaceHit current, MovementSafeLandingAnalysis analysis)
        {
            if (candidate == null || !candidate.Found)
            {
                return false;
            }

            if (current == null || !current.Found)
            {
                return true;
            }

            if (candidate.MovingIntoSlope != current.MovingIntoSlope)
            {
                return candidate.MovingIntoSlope;
            }

            if (Math.Abs(candidate.ContactWorldY - current.ContactWorldY) > 0.25f)
            {
                return analysis == null || analysis.GravityDirection >= 0f
                    ? candidate.ContactWorldY < current.ContactWorldY
                    : candidate.ContactWorldY > current.ContactWorldY;
            }

            var candidateLeading = string.Equals(candidate.ContactSample, "leading_foot", StringComparison.Ordinal);
            var currentLeading = string.Equals(current.ContactSample, "leading_foot", StringComparison.Ordinal);
            if (candidateLeading != currentLeading)
            {
                return candidateLeading;
            }

            var velocityX = analysis == null ? 0f : analysis.VelocityX;
            if (Math.Abs(velocityX) > 0.01f)
            {
                var candidateProjectedCenter = (candidate.ProjectedPlayerLeftX + candidate.ProjectedPlayerRightX) / 2f;
                var currentProjectedCenter = (current.ProjectedPlayerLeftX + current.ProjectedPlayerRightX) / 2f;
                if (Math.Abs(candidateProjectedCenter - currentProjectedCenter) > 0.25f)
                {
                    return velocityX > 0f
                        ? candidateProjectedCenter > currentProjectedCenter
                        : candidateProjectedCenter < currentProjectedCenter;
                }
            }

            var candidateCenterX = (candidate.ProjectedPlayerLeftX + candidate.ProjectedPlayerRightX) / 2f;
            var currentCenterX = (current.ProjectedPlayerLeftX + current.ProjectedPlayerRightX) / 2f;
            if (candidate.ProjectedPlayerRightX <= candidate.ProjectedPlayerLeftX)
            {
                candidateCenterX = analysis == null ? candidate.ContactWorldX : analysis.PositionX + analysis.Width / 2f;
            }

            if (current.ProjectedPlayerRightX <= current.ProjectedPlayerLeftX)
            {
                currentCenterX = analysis == null ? current.ContactWorldX : analysis.PositionX + analysis.Width / 2f;
            }

            var candidateCenterDistance = Math.Abs(candidate.ContactWorldX - candidateCenterX);
            var currentCenterDistance = Math.Abs(current.ContactWorldX - currentCenterX);
            if (Math.Abs(candidateCenterDistance - currentCenterDistance) > 0.25f)
            {
                return candidateCenterDistance < currentCenterDistance;
            }

            if (velocityX < -0.01f && candidate.ContactTileX != current.ContactTileX)
            {
                return candidate.ContactTileX < current.ContactTileX;
            }

            if (velocityX > 0.01f && candidate.ContactTileX != current.ContactTileX)
            {
                return candidate.ContactTileX > current.ContactTileX;
            }

            return false;
        }

        private static void ApplyLandingSurfaceHit(MovementSafeLandingAnalysis analysis, MovementLandingSurfaceHit hit)
        {
            if (analysis == null)
            {
                return;
            }

            if (hit == null || !hit.Found)
            {
                analysis.LandingSurfaceKnown = false;
                return;
            }

            analysis.LandingSurfaceKnown = true;
            analysis.LandingContactWorldX = hit.ContactWorldX;
            analysis.LandingContactWorldY = hit.ContactWorldY;
            analysis.LandingContactTileX = hit.ContactTileX;
            analysis.LandingContactTileY = hit.ContactTileY;
            analysis.LandingSurfaceKind = hit.SurfaceKind ?? string.Empty;
            analysis.LandingSlopeType = hit.SlopeType;
            analysis.LandingSlopeDirection = hit.SlopeDirection ?? string.Empty;
            analysis.LandingContactSample = hit.ContactSample ?? string.Empty;
            analysis.LandingMovingIntoSlope = hit.MovingIntoSlope;
            analysis.LandingMovingWithSlope = hit.MovingWithSlope;
            analysis.LandingProjectedPlayerLeftX = hit.ProjectedPlayerLeftX;
            analysis.LandingProjectedPlayerRightX = hit.ProjectedPlayerRightX;
            analysis.LandingProjectedPlayerBottomY = hit.ProjectedPlayerBottomY;
            analysis.LandingSurfaceSummary = hit.Summary ?? string.Empty;
        }

        private static bool TryProbeProjectedLandingCollision(MovementSafeLandingAnalysis analysis, int offset, out bool solid, out float hitX)
        {
            solid = false;
            hitX = analysis == null ? 0f : analysis.PositionX;
            if (analysis == null)
            {
                return false;
            }

            var direction = analysis.GravityDirection >= 0f ? 1f : -1f;
            var y = analysis.PositionY + offset * direction;
            var projectedX = ResolveProjectedImpactProbeX(analysis.PositionX, analysis.VelocityX, analysis.FallingSpeed, offset);
            var resolved = false;

            if (TryProbeLandingCollision(projectedX, y, analysis.Width, analysis.Height, analysis.GravityDirection, analysis.FallingSpeed, out var projectedSolid))
            {
                resolved = true;
                if (projectedSolid)
                {
                    solid = true;
                    hitX = projectedX;
                    return true;
                }
            }

            var middleX = (analysis.PositionX + projectedX) / 2f;
            if (!NearlyEqual(middleX, projectedX) &&
                !NearlyEqual(middleX, analysis.PositionX) &&
                TryProbeLandingCollision(middleX, y, analysis.Width, analysis.Height, analysis.GravityDirection, analysis.FallingSpeed, out var middleSolid))
            {
                resolved = true;
                if (middleSolid)
                {
                    solid = true;
                    hitX = middleX;
                    return true;
                }
            }

            if (!NearlyEqual(analysis.PositionX, projectedX) &&
                TryProbeLandingCollision(analysis.PositionX, y, analysis.Width, analysis.Height, analysis.GravityDirection, analysis.FallingSpeed, out var currentSolid))
            {
                resolved = true;
                if (currentSolid)
                {
                    solid = true;
                    hitX = analysis.PositionX;
                    return true;
                }
            }

            return resolved;
        }

        private static float ResolveProjectedImpactProbeX(float positionX, float velocityX, float fallingSpeed, int offsetPixels)
        {
            if (offsetPixels <= 0 || Math.Abs(velocityX) < 0.01f || Math.Abs(fallingSpeed) < 0.001f)
            {
                return positionX;
            }

            var leadTicks = offsetPixels / Math.Abs(fallingSpeed);
            if (float.IsNaN(leadTicks) || float.IsInfinity(leadTicks) || leadTicks <= 0f)
            {
                return positionX;
            }

            if (leadTicks > ImpactHorizontalLeadMaxTicks)
            {
                leadTicks = ImpactHorizontalLeadMaxTicks;
            }

            return positionX + velocityX * leadTicks;
        }

        private static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) <= ImpactHorizontalSampleEpsilon;
        }

        private static bool TryGetMainTileArray(out Array tiles)
        {
            tiles = GetStaticMember(ResolveMainType(), "tile") as Array;
            return tiles != null && tiles.Rank >= 2;
        }

        private static Type ResolveMainType()
        {
            return _mainTypeOverrideForTesting ?? TerrariaRuntimeTypes.MainType ?? FindType("Terraria.Main");
        }

        private static object GetTile(Array tiles, int x, int y)
        {
            if (tiles == null || tiles.Rank < 2 || x < 0 || y < 0 || x >= tiles.GetLength(0) || y >= tiles.GetLength(1))
            {
                return null;
            }

            try
            {
                return tiles.GetValue(x, y);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsProjectedSafeTile(object tile, out string reason)
        {
            reason = string.Empty;
            if (tile == null)
            {
                return false;
            }

            if (ReadTileLiquidAmount(tile) > 0)
            {
                var liquidType = ReadTileLiquidType(tile);
                reason = liquidType == 2
                    ? "projectedHoney"
                    : liquidType == 3
                        ? "projectedShimmer"
                        : liquidType == LavaLiquidType
                            ? "projectedLavaNoFallDamage"
                            : "projectedWater";
                return true;
            }

            if (!IsTileActive(tile))
            {
                return false;
            }

            var tileType = ReadTileType(tile);
            if (IsFallDamageSafeLandingTile(tile, tileType, out reason))
            {
                return true;
            }

            return false;
        }

        private static bool IsFallDamageSafeLandingTile(object tile, int tileType, out string reason)
        {
            reason = string.Empty;
            switch (tileType)
            {
                case CobwebTileFallbackType:
                case CobwebReplicaTileType:
                    reason = "projectedCobweb";
                    return true;
                case CloudTileType:
                case RainCloudTileType:
                case SnowCloudTileType:
                case LavaCloudTileType:
                case StarCloudTileType:
                case RainbowCloudTileType:
                    reason = "projectedCloudBlock";
                    return true;
                case PinkSlimeBlockTileType:
                    reason = "projectedPinkSlimeBlock";
                    return true;
                case SillyBalloonPinkTileType:
                case SillyBalloonPurpleTileType:
                case SillyBalloonGreenTileType:
                    reason = "projectedSillyBalloonBlock";
                    return true;
                case PoopBlockTileType:
                    reason = "projectedPoopBlock";
                    return true;
                case PlatformTileType:
                    if (ReadPlatformStyle(tile) == CloudPlatformStyle)
                    {
                        reason = "projectedCloudPlatform";
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        private static void PopulateTeleportRodCandidate(object player, MovementSafeLandingAnalysis analysis)
        {
            if (player == null || analysis == null)
            {
                return;
            }

            var inventory = GetMember(player, "inventory") as IList;
            if (inventory == null || inventory.Count == 0)
            {
                return;
            }

            var selectedSlot = -1;
            TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot);
            var bestPriority = int.MaxValue;
            var found = false;
            var bestCandidate = default(TeleportRodCandidate);
            if (TryReadTeleportRod(inventory, selectedSlot, out var selectedCandidate))
            {
                bestCandidate = selectedCandidate;
                bestPriority = selectedCandidate.Priority;
                found = true;
            }

            var inventoryMax = Math.Min(59, inventory.Count);
            for (var slot = 0; slot < inventoryMax; slot++)
            {
                if (slot == selectedSlot ||
                    !TerrariaInputCompat.IsSupportedItemUseSlot(slot) ||
                    !TryReadTeleportRod(inventory, slot, out var candidate))
                {
                    continue;
                }

                if (!found ||
                    candidate.Priority < bestPriority ||
                    (candidate.Priority == bestPriority && candidate.Slot < bestCandidate.Slot))
                {
                    bestCandidate = candidate;
                    bestPriority = candidate.Priority;
                    found = true;
                }
            }

            if (found)
            {
                ApplyTeleportRodCandidate(analysis, bestCandidate);
            }
        }

        private static bool TryReadTeleportRod(IList inventory, int slot, out TeleportRodCandidate candidate)
        {
            candidate = default(TeleportRodCandidate);
            if (inventory == null || slot < 0 || slot >= inventory.Count)
            {
                return false;
            }

            if (!TerrariaInputCompat.IsSupportedItemUseSlot(slot))
            {
                return false;
            }

            var item = inventory[slot];
            if (item == null)
            {
                return false;
            }

            int itemType;
            int stack;
            if (!TryReadInt(item, "type", out itemType) ||
                !TryReadInt(item, "stack", out stack) ||
                itemType <= 0 ||
                stack <= 0 ||
                !TerrariaTeleportRodCompat.IsTeleportRodItem(itemType))
            {
                return false;
            }

            var name = Convert.ToString(GetMember(item, "Name") ?? GetMember(item, "name")) ?? string.Empty;
            candidate = new TeleportRodCandidate
            {
                Slot = slot,
                ItemType = itemType,
                Stack = stack,
                Name = name,
                Priority = TerrariaTeleportRodCompat.GetTeleportRodPriority(itemType, name)
            };
            return true;
        }

        private static bool IsPlaceableFallDamageSafeTile(int createTile, int placeStyle)
        {
            switch (createTile)
            {
                case CobwebTileFallbackType:
                case CobwebReplicaTileType:
                case CloudTileType:
                case RainCloudTileType:
                case SnowCloudTileType:
                case LavaCloudTileType:
                case StarCloudTileType:
                case RainbowCloudTileType:
                case PinkSlimeBlockTileType:
                case SillyBalloonPinkTileType:
                case SillyBalloonPurpleTileType:
                case SillyBalloonGreenTileType:
                case PoopBlockTileType:
                    return true;
                case PlatformTileType:
                    return placeStyle == CloudPlatformStyle;
                default:
                    return false;
            }
        }

        private static void ApplyTeleportRodCandidate(MovementSafeLandingAnalysis analysis, TeleportRodCandidate candidate)
        {
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = candidate.Slot;
            analysis.TeleportRodItemType = candidate.ItemType;
            analysis.TeleportRodItemName = candidate.Name ?? string.Empty;
        }

        private struct TeleportRodCandidate
        {
            public int Slot;
            public int ItemType;
            public int Stack;
            public string Name;
            public int Priority;
        }

        private static int ReadTileLiquidAmount(object tile)
        {
            int liquid;
            if (TryReadInt(tile, "liquid", out liquid) || TryReadInt(tile, "LiquidAmount", out liquid))
            {
                return liquid;
            }

            return 0;
        }

        private static int ReadTileLiquidType(object tile)
        {
            int liquidType;
            if (TryReadInt(tile, "LiquidType", out liquidType) ||
                TryReadInt(tile, "liquidType", out liquidType) ||
                TryInvokeInt(tile, "liquidType", out liquidType))
            {
                return liquidType;
            }

            bool liquidFlag;
            if (TryInvokeBool(tile, "lava", out liquidFlag) && liquidFlag)
            {
                return 1;
            }

            if (TryInvokeBool(tile, "honey", out liquidFlag) && liquidFlag)
            {
                return 2;
            }

            return TryInvokeBool(tile, "shimmer", out liquidFlag) && liquidFlag ? 3 : 0;
        }

        private static bool IsTileActive(object tile)
        {
            bool value;
            if (TryInvokeBool(tile, "active", out value))
            {
                return value;
            }

            return TryReadBool(tile, "active", false);
        }

        private static bool IsTileInactive(object tile)
        {
            bool value;
            if (TryInvokeBool(tile, "inActive", out value) ||
                TryInvokeBool(tile, "inactive", out value))
            {
                return value;
            }

            return TryReadBool(tile, "inActive", false) || TryReadBool(tile, "inactive", false);
        }

        private static int ReadTileType(object tile)
        {
            int type;
            return TryReadInt(tile, "type", out type) ? type : 0;
        }

        private static int ReadTileSlope(object tile)
        {
            int slope;
            if (TryInvokeInt(tile, "slope", out slope) ||
                TryReadInt(tile, "slope", out slope) ||
                TryReadInt(tile, "Slope", out slope))
            {
                return slope;
            }

            return 0;
        }

        private static bool ReadTileHalfBrick(object tile)
        {
            bool halfBrick;
            if (TryInvokeBool(tile, "halfBrick", out halfBrick) ||
                TryReadBool(tile, "halfBrick", out halfBrick) ||
                TryReadBool(tile, "HalfBrick", out halfBrick))
            {
                return halfBrick;
            }

            return false;
        }

        private static int ReadPlatformStyle(object tile)
        {
            var frameY = ReadTileFrameY(tile);
            if (frameY < 0)
            {
                return 0;
            }

            return frameY / PlatformFrameHeight;
        }

        private static int ReadTileFrameY(object tile)
        {
            int frameY;
            if (TryReadInt(tile, "frameY", out frameY) || TryReadInt(tile, "FrameY", out frameY))
            {
                return frameY;
            }

            return 0;
        }

        private static bool TryResolveCurrentGrappleTarget(object player, out float targetX, out float targetY)
        {
            targetX = 0f;
            targetY = 0f;
            if (player == null)
            {
                return false;
            }

            float positionX;
            float positionY;
            if (!TryReadVectorMember(player, "position", out positionX, out positionY))
            {
                return false;
            }

            var width = TryReadInt(player, "width", 20);
            var height = TryReadInt(player, "height", 42);
            var gravityDirection = TryReadFloat(player, "gravDir", 1f);
            if (Math.Abs(gravityDirection) < 0.001f)
            {
                gravityDirection = 1f;
            }

            float velocityX;
            float velocityY;
            if (!TryReadVectorMember(player, "velocity", out velocityX, out velocityY))
            {
                velocityX = 0f;
                velocityY = 0f;
            }

            var fallingSpeed = Math.Max(velocityY * gravityDirection, MinimumDangerFallingSpeed);
            var probeAnalysis = new MovementSafeLandingAnalysis
            {
                PositionX = positionX,
                PositionY = positionY,
                Width = width,
                Height = height,
                GravityDirection = gravityDirection,
                FallingSpeed = fallingSpeed
            };

            int impactDistance;
            if (!TryFindImpactDistance(probeAnalysis, out impactDistance))
            {
                impactDistance = Math.Max(96, (int)(fallingSpeed * 12f));
            }

            targetX = positionX + width / 2f + velocityX * 6f;
            targetY = gravityDirection >= 0f
                ? positionY + height + impactDistance + 12f
                : positionY - impactDistance - 12f;
            return true;
        }
    }
}

