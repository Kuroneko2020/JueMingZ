using System;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.Movement;
using JueMingZ.Diagnostics;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace JueMingZ.Compat
{
    public static partial class MovementSafeLandingCompat
    {
        // Landing probes are the hot SafeLanding analysis path. Keep scan ranges,
        // cached reflection delegates and fixed buffers stable unless tests cover the cost.
        public static bool TryIsGravityRestoreSafe(object player, float originalGravityDirection, out bool safeToRestore, out string reason)
        {
            safeToRestore = false;
            reason = string.Empty;
            if (player == null)
            {
                reason = "playerUnavailable";
                return false;
            }

            var originalDirection = originalGravityDirection >= 0f ? 1f : -1f;
            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.PlayerControllable)
            {
                reason = "playerNotControllable";
                return true;
            }

            var currentDirection = profile.GravityDirection >= 0f ? 1f : -1f;
            if (Math.Abs(currentDirection - originalDirection) < 0.01f)
            {
                reason = "alreadyOriginalGravity";
                return true;
            }

            if (!profile.HasGravityGlobe)
            {
                reason = "gravityGlobeUnavailable";
                return true;
            }

            var originalDirectionSpeed = profile.VelocityY * originalDirection;
            if (originalDirectionSpeed > 1f)
            {
                reason = "restoreWouldContinueFalling";
                return true;
            }

            float positionX;
            float positionY;
            if (!TryReadVectorMember(player, "position", out positionX, out positionY))
            {
                reason = "positionUnavailable";
                return false;
            }

            var restoreProbe = new MovementSafeLandingAnalysis
            {
                PositionX = positionX,
                PositionY = positionY,
                Width = TryReadInt(player, "width", 20),
                Height = TryReadInt(player, "height", 42),
                GravityDirection = originalDirection,
                FallingSpeed = Math.Max(MinimumDangerFallingSpeed, Math.Abs(originalDirectionSpeed) + 1f)
            };

            int impactDistance;
            if (!TryFindImpactDistance(restoreProbe, out impactDistance))
            {
                reason = "originalGravityNoNearbySurface";
                return true;
            }

            if (impactDistance > GravityRestoreProbePixels)
            {
                reason = "originalGravitySurfaceTooFar:" + impactDistance.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            safeToRestore = true;
            reason = "originalGravitySurfaceNear:" + impactDistance.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        public static bool TryProbeLandingCollision(float x, float y, int width, int height, float gravityDirection, float fallingSpeed, out bool solid)
        {
            solid = false;
            var solidResolved = TrySolidOrTopSurfaceCollision(x, y, width, height, out var solidHit);
            if (solidResolved && solidHit)
            {
                solid = true;
                return true;
            }

            var direction = gravityDirection >= 0f ? 1f : -1f;
            var slopeProbeY = y + 4f * direction;
            if (TrySolidOrTopSurfaceCollision(x, slopeProbeY, width, height, out var slopeSolidHit) && slopeSolidHit)
            {
                solid = true;
                return true;
            }

            var slopeProbeDeepY = y + 8f * direction;
            if (TrySolidOrTopSurfaceCollision(x, slopeProbeDeepY, width, height, out var slopeDeepSolidHit) && slopeDeepSolidHit)
            {
                solid = true;
                return true;
            }

            var tileCollisionResolved = TryTileCollisionImpact(x, y, width, height, gravityDirection, fallingSpeed, out var tileCollisionHit);
            if (tileCollisionResolved && tileCollisionHit)
            {
                solid = true;
                return true;
            }

            if (TryManualLandingSurfaceImpact(x, y, width, height, gravityDirection, fallingSpeed, out var manualSurfaceHit))
            {
                solid = manualSurfaceHit;
                if (solid)
                {
                    MarkCollisionFastPath(CollisionPathManualSurface);
                    ClearError();
                }

                return true;
            }

            if (solidResolved || tileCollisionResolved)
            {
                solid = false;
                return true;
            }

            return false;
        }

        // Kept for backward compat; delegates to the surface-hit overload.
        private static bool TryManualLandingSurfaceImpact(float x, float y, int width, int height, float gravityDirection, float fallingSpeed, out bool solid)
        {
            var hit = TryManualLandingSurfaceImpact(x, y, width, height, gravityDirection, fallingSpeed);
            solid = hit != null && hit.Found;
            return true;
        }

        private static MovementLandingSurfaceHit TryManualLandingSurfaceImpact(float x, float y, int width, int height, float gravityDirection, float fallingSpeed, float velocityX = 0f)
        {
            if (gravityDirection < 0f || fallingSpeed <= 0.05f)
            {
                return new MovementLandingSurfaceHit();
            }

            Array tiles;
            if (!TryGetMainTileArray(out tiles))
            {
                return new MovementLandingSurfaceHit();
            }

            Array solidTiles;
            Array solidTopTiles;
            Array platformTiles;
            TryGetTileStaticTables(out solidTiles, out solidTopTiles, out platformTiles);
            var bottomY = y + height;
            var topY = y;
            var tolerance = Math.Max(8f, Math.Min(24f, Math.Abs(fallingSpeed) + 6f));
            var leftTile = (int)Math.Floor((x + 1f) / 16f);
            var rightTile = (int)Math.Floor((x + Math.Max(1, width - 1)) / 16f);
            var topTile = (int)Math.Floor((bottomY - tolerance) / 16f) - 1;
            var bottomTile = (int)Math.Floor((bottomY + tolerance) / 16f) + 1;
            var left = x + 1f;
            var right = x + Math.Max(1, width) - 1f;
            var center = (left + right) / 2f;
            var samples = BuildLandingSurfaceSamples(x, width, velocityX);
            MovementLandingSurfaceHit bestHit = null;

            for (var tileY = topTile; tileY <= bottomTile; tileY++)
            {
                for (var tileX = leftTile; tileX <= rightTile; tileX++)
                {
                    var tile = GetTile(tiles, tileX, tileY);
                    if (!IsLandingSurfaceTile(tile, solidTiles, solidTopTiles, platformTiles))
                    {
                        continue;
                    }

                    for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
                    {
                        var sample = samples.Get(sampleIndex);
                        var sampleX = sample.X;
                        var tileLeft = tileX * 16f;
                        if (sampleX < tileLeft - 0.25f || sampleX > tileLeft + 16.25f)
                        {
                            continue;
                        }

                        float surfaceY;
                        if (!TryResolveTileSurfaceY(tileX, tileY, tile, sampleX, out surfaceY))
                        {
                            continue;
                        }

                        if (!(bottomY >= surfaceY - 1f && bottomY <= surfaceY + tolerance && topY < surfaceY + 16f))
                        {
                            continue;
                        }

                        var slope = ReadTileSlope(tile);
                        var surfaceKind = ResolveLandingSurfaceKind(tile, solidTopTiles, platformTiles);
                        var slopeDirection = ResolveSlopeDirection(slope);
                        var movingIntoSlope = IsMovingIntoSlope(slopeDirection, velocityX);
                        var movingWithSlope = IsMovingWithSlope(slopeDirection, velocityX);
                        var candidate = new MovementLandingSurfaceHit
                        {
                            Found = true,
                            ImpactDistancePixels = 0,
                            ImpactTicks = fallingSpeed > 0.001f ? 0f : -1f,
                            ProjectedPlayerLeftX = x,
                            ProjectedPlayerRightX = x + width,
                            ProjectedPlayerBottomY = bottomY,
                            ContactWorldX = sampleX,
                            ContactWorldY = surfaceY,
                            ContactTileX = tileX,
                            ContactTileY = tileY,
                            SurfaceKind = surfaceKind,
                            SlopeType = slope,
                            SlopeDirection = slopeDirection,
                            ContactSample = sample.Label,
                            MovingIntoSlope = movingIntoSlope,
                            MovingWithSlope = movingWithSlope
                        };

                        if (IsBetterManualSurfaceHit(candidate, bestHit, velocityX, center, gravityDirection))
                        {
                            bestHit = candidate;
                        }
                    }
                }
            }

            return bestHit ?? new MovementLandingSurfaceHit();
        }

        private static bool TryGetTileStaticTables(out Array solidTiles, out Array solidTopTiles, out Array platformTiles)
        {
            var cache = _tileStaticTableCache;
            if (cache != null)
            {
                solidTiles = cache.SolidTiles;
                solidTopTiles = cache.SolidTopTiles;
                platformTiles = cache.PlatformTiles;
                return solidTiles != null || solidTopTiles != null || platformTiles != null;
            }

            lock (CollisionCacheSyncRoot)
            {
                cache = _tileStaticTableCache;
                if (cache == null)
                {
                    var mainType = ResolveMainType();
                    var setsType = FindType("Terraria.ID.TileID+Sets");
                    cache = new TileStaticTableCache
                    {
                        MainType = mainType,
                        SetsType = setsType,
                        SolidTiles = GetStaticMember(mainType, "tileSolid") as Array,
                        SolidTopTiles = GetStaticMember(mainType, "tileSolidTop") as Array,
                        PlatformTiles = GetStaticMember(setsType, "Platforms") as Array
                    };
                    _tileStaticTableCache = cache;
                }

                solidTiles = cache.SolidTiles;
                solidTopTiles = cache.SolidTopTiles;
                platformTiles = cache.PlatformTiles;
                return solidTiles != null || solidTopTiles != null || platformTiles != null;
            }
        }

        private sealed class TileStaticTableCache
        {
            public Type MainType;
            public Type SetsType;
            public Array SolidTiles;
            public Array SolidTopTiles;
            public Array PlatformTiles;
        }

        private const int LandingSurfaceSampleCapacity = 16;
        private const int LandingSurfaceSampleLeftFoot = 1;
        private const int LandingSurfaceSampleCenterFoot = 2;
        private const int LandingSurfaceSampleRightFoot = 3;
        private const int LandingSurfaceSampleLeadingFoot = 4;
        private const int LandingSurfaceSampleTileSegment = 5;

        private struct LandingSurfaceSample
        {
            public LandingSurfaceSample(float x, int labelKind, int priority)
            {
                X = x;
                LabelKind = labelKind;
                Priority = priority;
            }

            public float X;
            public int LabelKind;
            public int Priority;

            public string Label
            {
                get { return LandingSurfaceSampleLabelToString(LabelKind); }
            }
        }

        private struct LandingSurfaceSampleBuffer
        {
            private LandingSurfaceSample _sample0;
            private LandingSurfaceSample _sample1;
            private LandingSurfaceSample _sample2;
            private LandingSurfaceSample _sample3;
            private LandingSurfaceSample _sample4;
            private LandingSurfaceSample _sample5;
            private LandingSurfaceSample _sample6;
            private LandingSurfaceSample _sample7;
            private LandingSurfaceSample _sample8;
            private LandingSurfaceSample _sample9;
            private LandingSurfaceSample _sample10;
            private LandingSurfaceSample _sample11;
            private LandingSurfaceSample _sample12;
            private LandingSurfaceSample _sample13;
            private LandingSurfaceSample _sample14;
            private LandingSurfaceSample _sample15;

            public int Count { get; private set; }

            public LandingSurfaceSample Get(int index)
            {
                switch (index)
                {
                    case 0: return _sample0;
                    case 1: return _sample1;
                    case 2: return _sample2;
                    case 3: return _sample3;
                    case 4: return _sample4;
                    case 5: return _sample5;
                    case 6: return _sample6;
                    case 7: return _sample7;
                    case 8: return _sample8;
                    case 9: return _sample9;
                    case 10: return _sample10;
                    case 11: return _sample11;
                    case 12: return _sample12;
                    case 13: return _sample13;
                    case 14: return _sample14;
                    case 15: return _sample15;
                    default: return default(LandingSurfaceSample);
                }
            }

            public void Set(int index, LandingSurfaceSample sample)
            {
                switch (index)
                {
                    case 0: _sample0 = sample; break;
                    case 1: _sample1 = sample; break;
                    case 2: _sample2 = sample; break;
                    case 3: _sample3 = sample; break;
                    case 4: _sample4 = sample; break;
                    case 5: _sample5 = sample; break;
                    case 6: _sample6 = sample; break;
                    case 7: _sample7 = sample; break;
                    case 8: _sample8 = sample; break;
                    case 9: _sample9 = sample; break;
                    case 10: _sample10 = sample; break;
                    case 11: _sample11 = sample; break;
                    case 12: _sample12 = sample; break;
                    case 13: _sample13 = sample; break;
                    case 14: _sample14 = sample; break;
                    case 15: _sample15 = sample; break;
                }
            }

            public bool Add(float x, int labelKind, int priority)
            {
                for (var index = 0; index < Count; index++)
                {
                    var existing = Get(index);
                    if (Math.Abs(existing.X - x) < 0.25f)
                    {
                        if (priority > existing.Priority)
                        {
                            Set(index, new LandingSurfaceSample(existing.X, labelKind, priority));
                        }

                        return true;
                    }
                }

                if (Count >= LandingSurfaceSampleCapacity)
                {
                    return false;
                }

                Set(Count, new LandingSurfaceSample(x, labelKind, priority));
                Count++;
                return true;
            }
        }

        private static LandingSurfaceSampleBuffer BuildLandingSurfaceSamples(float x, int width, float velocityX)
        {
            var samples = new LandingSurfaceSampleBuffer();
            var left = x + 1f;
            var right = x + Math.Max(1, width) - 1f;
            var center = (left + right) / 2f;

            AddLandingSurfaceSample(ref samples, left, LandingSurfaceSampleLeftFoot, 2);
            AddLandingSurfaceSample(ref samples, center, LandingSurfaceSampleCenterFoot, 2);
            AddLandingSurfaceSample(ref samples, right, LandingSurfaceSampleRightFoot, 2);

            if (velocityX < -0.01f)
            {
                AddLandingSurfaceSample(ref samples, left, LandingSurfaceSampleLeadingFoot, 4);
            }
            else if (velocityX > 0.01f)
            {
                AddLandingSurfaceSample(ref samples, right, LandingSurfaceSampleLeadingFoot, 4);
            }

            var footLeftTile = (int)Math.Floor((x + 0.5f) / 16f);
            var footRightTile = (int)Math.Floor((x + Math.Max(1, width) - 0.5f) / 16f);
            for (var tileCol = footLeftTile; tileCol <= footRightTile; tileCol++)
            {
                AddLandingSurfaceSample(ref samples, ClampFloat(tileCol * 16f + 8f, left, right), LandingSurfaceSampleTileSegment, 1);
            }

            return samples;
        }

        private static void AddLandingSurfaceSample(ref LandingSurfaceSampleBuffer samples, float x, int labelKind, int priority)
        {
            samples.Add(x, labelKind, priority);
        }

        private static string LandingSurfaceSampleLabelToString(int labelKind)
        {
            switch (labelKind)
            {
                case LandingSurfaceSampleLeftFoot:
                    return "left_foot";
                case LandingSurfaceSampleCenterFoot:
                    return "center_foot";
                case LandingSurfaceSampleRightFoot:
                    return "right_foot";
                case LandingSurfaceSampleLeadingFoot:
                    return "leading_foot";
                case LandingSurfaceSampleTileSegment:
                    return "tile_segment";
                default:
                    return string.Empty;
            }
        }

        private static bool IsBetterManualSurfaceHit(
            MovementLandingSurfaceHit candidate,
            MovementLandingSurfaceHit current,
            float velocityX,
            float playerCenterX,
            float gravityDirection)
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
                return gravityDirection >= 0f
                    ? candidate.ContactWorldY < current.ContactWorldY
                    : candidate.ContactWorldY > current.ContactWorldY;
            }

            var candidateLeading = string.Equals(candidate.ContactSample, "leading_foot", StringComparison.Ordinal);
            var currentLeading = string.Equals(current.ContactSample, "leading_foot", StringComparison.Ordinal);
            if (candidateLeading != currentLeading)
            {
                return candidateLeading;
            }

            var candidateCenterDistance = Math.Abs(candidate.ContactWorldX - playerCenterX);
            var currentCenterDistance = Math.Abs(current.ContactWorldX - playerCenterX);
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

        private static string ResolveLandingSurfaceKind(object tile, Array solidTopTiles, Array platformTiles)
        {
            if (tile == null)
            {
                return "unknown";
            }

            if (ReadTileHalfBrick(tile))
            {
                return "half_brick";
            }

            var slope = ReadTileSlope(tile);
            if (slope >= 1 && slope <= 4)
            {
                return "slope";
            }

            var tileType = ReadTileType(tile);
            if (tileType == PlatformTileType || ReadStaticBoolArray(platformTiles, tileType))
            {
                return "platform";
            }

            if (ReadStaticBoolArray(solidTopTiles, tileType))
            {
                return "solid_top";
            }

            return "full_block";
        }

        private static string ResolveSlopeDirection(int slope)
        {
            if (slope == 1 || slope == 3)
            {
                return "left_high_right_low";
            }

            if (slope == 2 || slope == 4)
            {
                return "left_low_right_high";
            }

            return "none";
        }

        private static bool IsMovingIntoSlope(string slopeDirection, float velocityX)
        {
            if (string.Equals(slopeDirection, "left_high_right_low", StringComparison.Ordinal))
            {
                return velocityX < -0.01f;
            }

            if (string.Equals(slopeDirection, "left_low_right_high", StringComparison.Ordinal))
            {
                return velocityX > 0.01f;
            }

            return false;
        }

        private static bool IsMovingWithSlope(string slopeDirection, float velocityX)
        {
            if (string.Equals(slopeDirection, "left_high_right_low", StringComparison.Ordinal))
            {
                return velocityX > 0.01f;
            }

            if (string.Equals(slopeDirection, "left_low_right_high", StringComparison.Ordinal))
            {
                return velocityX < -0.01f;
            }

            return false;
        }

        // Extracted helper: resolve tile surface Y for a given sample X.
        private static bool TryResolveTileSurfaceY(int tileX, int tileY, object tile, float sampleX, out float surfaceY)
        {
            surfaceY = 0f;
            if (tile == null || !IsTileActive(tile) || IsTileInactive(tile))
            {
                return false;
            }

            var tileTop = tileY * 16f;
            var slope = ReadTileSlope(tile);

            if (ReadTileHalfBrick(tile))
            {
                surfaceY = tileTop + 8f;
                return true;
            }

            if (slope == 0)
            {
                surfaceY = tileTop;
                return true;
            }

            if (slope == 1 || slope == 3)
            {
                surfaceY = tileTop + ClampFloat(sampleX - tileX * 16f, 0f, 16f);
                return true;
            }

            if (slope == 2 || slope == 4)
            {
                surfaceY = tileTop + 16f - ClampFloat(sampleX - tileX * 16f, 0f, 16f);
                return true;
            }

            MarkCollisionFastPath(CollisionPathUnavailable);
            return false;
        }

        private static float ResolveGrappleHookSpeed(MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null)
            {
                return 0f;
            }

            if (analysis.HasEquippedGrapple)
            {
                if (analysis.EquippedGrappleShootSpeed > 0f)
                {
                    return analysis.EquippedGrappleShootSpeed;
                }

                if (analysis.EquippedGrappleItemType > 0 &&
                    GrappleHookSpeedTable.TryGetValue(analysis.EquippedGrappleItemType, out var equippedSpeed))
                {
                    return equippedSpeed;
                }

                return 13f;
            }

            if (analysis.HasInventoryGrapple)
            {
                if (analysis.InventoryGrappleShootSpeed > 0f)
                {
                    return analysis.InventoryGrappleShootSpeed;
                }

                if (analysis.InventoryGrappleItemType > 0 &&
                    GrappleHookSpeedTable.TryGetValue(analysis.InventoryGrappleItemType, out var inventorySpeed))
                {
                    return inventorySpeed;
                }

                return 13f;
            }

            return 13f;
        }

        private static bool IsFallingAcrossTileSurface(float sampleX, float topY, float bottomY, int tileX, int tileY, object tile, float tolerance)
        {
            var tileLeft = tileX * 16f;
            var tileTop = tileY * 16f;
            var slope = ReadTileSlope(tile);
            var surfaceY = tileTop;
            if (ReadTileHalfBrick(tile))
            {
                surfaceY = tileTop + 8f;
            }
            else if (slope == 1)
            {
                surfaceY = tileTop + ClampFloat(sampleX - tileLeft, 0f, 16f);
            }
            else if (slope == 2)
            {
                surfaceY = tileTop + 16f - ClampFloat(sampleX - tileLeft, 0f, 16f);
            }
            else if (slope > 2)
            {
                return false;
            }

            return bottomY >= surfaceY - 1f &&
                   bottomY <= surfaceY + tolerance &&
                   topY < surfaceY + 16f;
        }

        private static bool IsLandingSurfaceTile(object tile, Array solidTiles, Array solidTopTiles, Array platformTiles)
        {
            if (tile == null || !IsTileActive(tile) || IsTileInactive(tile))
            {
                return false;
            }

            var tileType = ReadTileType(tile);
            return ReadStaticBoolArray(solidTiles, tileType) ||
                   ReadStaticBoolArray(solidTopTiles, tileType) ||
                   tileType == PlatformTileType ||
                   ReadStaticBoolArray(platformTiles, tileType);
        }

        private static bool ReadStaticBoolArray(Array values, int index)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
                return false;
            }

            try
            {
                var raw = values.GetValue(index);
                return raw is bool && (bool)raw;
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySolidOrTopSurfaceCollision(float x, float y, int width, int height, out bool solid)
        {
            solid = false;
            if (TryTypedSolidOrTopSurfaceCollision(x, y, width, height, out solid))
            {
                return true;
            }

            if (!EnsureSolidCollisionTopSurfaceMethod())
            {
                return TrySolidCollision(x, y, width, height, out solid);
            }

            try
            {
                if (_solidCollisionTopSurfaceDelegate != null)
                {
                    solid = _solidCollisionTopSurfaceDelegate(new XnaVector2(x, y), width, height, true);
                    MarkCollisionFastPath(CollisionPathDelegateSolidTop);
                    return ClearError();
                }

                var vector = CreateCollisionVector2(x, y);
                if (vector == null)
                {
                    return Fail("Microsoft.Xna.Framework.Vector2 type unavailable.");
                }

                var result = _solidCollisionTopSurfaceMethod.Invoke(null, new object[] { vector, width, height, true });
                if (result is bool)
                {
                    solid = (bool)result;
                    MarkCollisionFastPath(CollisionPathReflectionSolidTop);
                    return ClearError();
                }

                return Fail("Collision.SolidCollision(Vector2,int,int,bool) returned a non-bool result.");
            }
            catch (Exception error)
            {
                return Fail("Collision.SolidCollision(Vector2,int,int,bool) failed: " + error.Message);
            }
        }

        private static bool TryTileCollisionImpact(float x, float y, int width, int height, float gravityDirection, float fallingSpeed, out bool collided)
        {
            collided = false;
            var direction = gravityDirection >= 0f ? 1 : -1;
            var requestedSpeed = Math.Max(
                ImpactCollisionProbeVelocity,
                Math.Min(24f, Math.Abs(fallingSpeed) + 2f));
            var requestedVelocityY = requestedSpeed * direction;

            if (TryTypedTileCollisionImpact(x, y, width, height, direction, requestedVelocityY, out collided))
            {
                return true;
            }

            if (!EnsureTileCollisionMethod())
            {
                MarkCollisionFastPath(CollisionPathUnavailable);
                return false;
            }

            try
            {
                if (_tileCollisionDelegate != null)
                {
                    var typedVelocity = _tileCollisionDelegate(
                        new XnaVector2(x, y),
                        new XnaVector2(0f, requestedVelocityY),
                        width,
                        height,
                        false,
                        false,
                        direction,
                        false,
                        false,
                        false);
                    collided = IsTileCollisionVelocityChanged(typedVelocity.X, typedVelocity.Y, direction, requestedVelocityY);
                    MarkCollisionFastPath(CollisionPathDelegateTile);
                    return ClearError();
                }

                var position = CreateCollisionVector2(x, y);
                var velocity = CreateCollisionVector2(0f, requestedVelocityY);
                if (position == null || velocity == null)
                {
                    return Fail("Microsoft.Xna.Framework.Vector2 type unavailable.");
                }

                var result = _tileCollisionMethod.Invoke(null, new object[]
                {
                    position,
                    velocity,
                    width,
                    height,
                    false,
                    false,
                    direction,
                    false,
                    false,
                    false
                });
                if (result == null || !TryReadVector2(result, out var returnedX, out var returnedY))
                {
                    return Fail("Collision.TileCollision returned an unreadable Vector2 result.");
                }

                collided = IsTileCollisionVelocityChanged(returnedX, returnedY, direction, requestedVelocityY);
                MarkCollisionFastPath(CollisionPathReflectionTile);
                return ClearError();
            }
            catch (Exception error)
            {
                return Fail("Collision.TileCollision failed: " + error.Message);
            }
        }

        private static bool TrySolidCollision(float x, float y, int width, int height, out bool solid)
        {
            solid = false;
            if (TryTypedSolidCollision(x, y, width, height, out solid))
            {
                return true;
            }

            if (!EnsureSolidCollisionMethod())
            {
                MarkCollisionFastPath(CollisionPathUnavailable);
                return false;
            }

            try
            {
                if (_solidCollisionDelegate != null)
                {
                    solid = _solidCollisionDelegate(new XnaVector2(x, y), width, height);
                    MarkCollisionFastPath(CollisionPathDelegateSolid);
                    return ClearError();
                }

                var vector = CreateCollisionVector2(x, y);
                if (vector == null)
                {
                    return Fail("Microsoft.Xna.Framework.Vector2 type unavailable.");
                }

                var result = _solidCollisionMethod.Invoke(null, new object[] { vector, width, height });
                if (result is bool)
                {
                    solid = (bool)result;
                    MarkCollisionFastPath(CollisionPathReflectionSolid);
                    return ClearError();
                }

                return Fail("Collision.SolidCollision returned a non-bool result.");
            }
            catch (Exception error)
            {
                return Fail("Collision.SolidCollision failed: " + error.Message);
            }
        }

        private static bool EnsureSolidCollisionMethod()
        {
            if (_collisionMethodResolved)
            {
                return _solidCollisionMethod != null;
            }

            _collisionMethodResolved = true;
            var collisionType = FindType("Terraria.Collision");
            var vectorType = TerrariaRuntimeTypes.Vector2Type;
            if (collisionType == null || vectorType == null)
            {
                _lastError = "Terraria.Collision or Vector2 type unavailable.";
                return false;
            }

            _solidCollisionMethod = collisionType.GetMethod(
                "SolidCollision",
                StaticFlags,
                null,
                new[] { vectorType, typeof(int), typeof(int) },
                null);
            if (_solidCollisionMethod == null)
            {
                _lastError = "Terraria.Collision.SolidCollision(Vector2,int,int) not found.";
                return false;
            }

            _solidCollisionDelegate = CreateDelegate<SolidCollisionDelegate>(_solidCollisionMethod);
            return true;
        }

        private static bool EnsureTileCollisionMethod()
        {
            if (_tileCollisionMethodResolved)
            {
                return _tileCollisionMethod != null;
            }

            _tileCollisionMethodResolved = true;
            var collisionType = FindType("Terraria.Collision");
            var vectorType = TerrariaRuntimeTypes.Vector2Type;
            if (collisionType == null || vectorType == null)
            {
                _lastError = "Terraria.Collision or Vector2 type unavailable.";
                return false;
            }

            _tileCollisionMethod = collisionType.GetMethod(
                "TileCollision",
                StaticFlags,
                null,
                new[]
                {
                    vectorType,
                    vectorType,
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool),
                    typeof(int),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)
                },
                null);
            _tileCollisionDelegate = _tileCollisionMethod == null
                ? null
                : CreateDelegate<TileCollisionDelegate>(_tileCollisionMethod);
            return _tileCollisionMethod != null;
        }

        private static bool EnsureSolidCollisionTopSurfaceMethod()
        {
            if (_solidCollisionTopSurfaceMethodResolved)
            {
                return _solidCollisionTopSurfaceMethod != null;
            }

            _solidCollisionTopSurfaceMethodResolved = true;
            var collisionType = FindType("Terraria.Collision");
            var vectorType = TerrariaRuntimeTypes.Vector2Type;
            if (collisionType == null || vectorType == null)
            {
                _lastError = "Terraria.Collision or Vector2 type unavailable.";
                return false;
            }

            _solidCollisionTopSurfaceMethod = collisionType.GetMethod(
                "SolidCollision",
                StaticFlags,
                null,
                new[] { vectorType, typeof(int), typeof(int), typeof(bool) },
                null);
            _solidCollisionTopSurfaceDelegate = _solidCollisionTopSurfaceMethod == null
                ? null
                : CreateDelegate<SolidCollisionTopSurfaceDelegate>(_solidCollisionTopSurfaceMethod);
            return _solidCollisionTopSurfaceMethod != null;
        }

        private static bool TryTypedSolidOrTopSurfaceCollision(float x, float y, int width, int height, out bool solid)
        {
            solid = false;
            if (_typedSolidTopSurfaceCollisionDisabled)
            {
                return false;
            }

            try
            {
                solid = global::Terraria.Collision.SolidCollision(new XnaVector2(x, y), width, height, true);
                MarkCollisionFastPath(CollisionPathTypedSolidTop);
                return ClearError();
            }
            catch (Exception error)
            {
                DisableTypedCollisionPath(
                    ref _typedSolidTopSurfaceCollisionDisabled,
                    "safe-landing-typed-solid-top-disabled",
                    "Collision.SolidCollision(Vector2,int,int,bool)",
                    error);
                return false;
            }
        }

        private static bool TryTypedSolidCollision(float x, float y, int width, int height, out bool solid)
        {
            solid = false;
            if (_typedSolidCollisionDisabled)
            {
                return false;
            }

            try
            {
                solid = global::Terraria.Collision.SolidCollision(new XnaVector2(x, y), width, height);
                MarkCollisionFastPath(CollisionPathTypedSolid);
                return ClearError();
            }
            catch (Exception error)
            {
                DisableTypedCollisionPath(
                    ref _typedSolidCollisionDisabled,
                    "safe-landing-typed-solid-disabled",
                    "Collision.SolidCollision(Vector2,int,int)",
                    error);
                return false;
            }
        }

        private static bool TryTypedTileCollisionImpact(
            float x,
            float y,
            int width,
            int height,
            int direction,
            float requestedVelocityY,
            out bool collided)
        {
            collided = false;
            if (_typedTileCollisionDisabled)
            {
                return false;
            }

            try
            {
                var returnedVelocity = global::Terraria.Collision.TileCollision(
                    new XnaVector2(x, y),
                    new XnaVector2(0f, requestedVelocityY),
                    width,
                    height,
                    false,
                    false,
                    direction,
                    false,
                    false,
                    false);
                collided = IsTileCollisionVelocityChanged(returnedVelocity.X, returnedVelocity.Y, direction, requestedVelocityY);
                MarkCollisionFastPath(CollisionPathTypedTile);
                return ClearError();
            }
            catch (Exception error)
            {
                DisableTypedCollisionPath(
                    ref _typedTileCollisionDisabled,
                    "safe-landing-typed-tile-collision-disabled",
                    "Collision.TileCollision(Vector2,Vector2,int,int,bool,bool,int,bool,bool,bool)",
                    error);
                return false;
            }
        }

        private static bool IsTileCollisionVelocityChanged(float returnedX, float returnedY, int direction, float requestedVelocityY)
        {
            return returnedY * direction < requestedVelocityY * direction - 0.01f || Math.Abs(returnedX) > 0.01f;
        }

        private static object CreateCollisionVector2(float x, float y)
        {
            var vectorType = TerrariaRuntimeTypes.Vector2Type;
            if (vectorType == typeof(XnaVector2))
            {
                return new XnaVector2(x, y);
            }

            if (vectorType == null)
            {
                return null;
            }

            try
            {
                return Activator.CreateInstance(vectorType, new object[] { x, y });
            }
            catch
            {
                return null;
            }
        }

        private static TDelegate CreateDelegate<TDelegate>(MethodInfo method) where TDelegate : class
        {
            if (method == null)
            {
                return null;
            }

            try
            {
                return Delegate.CreateDelegate(typeof(TDelegate), method, false) as TDelegate;
            }
            catch
            {
                return null;
            }
        }

        private static void DisableTypedCollisionPath(ref bool disabled, string throttleKey, string path, Exception error)
        {
            disabled = true;
            MarkCollisionFastPath(CollisionPathUnavailable);
            var message = path + " typed fast path failed; using delegate/reflection/manual fallback: " +
                          (error == null ? string.Empty : error.GetType().Name + ": " + error.Message);
            _lastError = message;
            LogThrottle.WarnThrottled(
                throttleKey,
                TimeSpan.FromSeconds(30),
                "MovementSafeLandingCompat",
                message);
        }

        private static void MarkCollisionFastPath(int path)
        {
            _lastCollisionFastPath = path;
        }

        private static string CollisionFastPathToString(int path)
        {
            switch (path)
            {
                case CollisionPathTypedSolidTop:
                    return "typed_solid_top";
                case CollisionPathTypedSolid:
                    return "typed_solid";
                case CollisionPathTypedTile:
                    return "typed_tile_collision";
                case CollisionPathDelegateSolidTop:
                    return "delegate_solid_top";
                case CollisionPathDelegateSolid:
                    return "delegate_solid";
                case CollisionPathDelegateTile:
                    return "delegate_tile_collision";
                case CollisionPathReflectionSolidTop:
                    return "reflection_solid_top";
                case CollisionPathReflectionSolid:
                    return "reflection_solid";
                case CollisionPathReflectionTile:
                    return "reflection_tile_collision";
                case CollisionPathManualSurface:
                    return "manual_surface";
                case CollisionPathUnavailable:
                    return "unavailable";
                default:
                    return "none";
            }
        }
    }
}

