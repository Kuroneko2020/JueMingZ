using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class TerrariaTeleportRodCompat
    {
        // Teleport corrections only adjust temporary mouse targets; restore
        // captured mouse/tile state on every failure path.
        private const int RodOfDiscordItemId = 1326;
        private const int RodOfHarmonyItemId = 5335;
        private const int TempleWallId = 87;
        private const int LihzahrdBrickTileId = 350;
        private const int DefaultSearchRadiusPixels = 208;
        private const int SearchStepPixels = 8;
        private const float WorldBoundaryMarginPixels = 50f;

        private static readonly object SyncRoot = new object();
        private static bool _resolved;
        private static MethodInfo _limitPointMethod;
        private static MethodInfo _solidCollisionMethod;
        private static MethodInfo _anyWallOfTypeOnLineMethod;
        private static MethodInfo _getTileSafelyMethod;
        private static Type _collisionType;
        private static Type _framingType;
        private static Type _npcType;
        private static ConstructorInfo _vector2Constructor;
        private static string _lastError = string.Empty;

        public static string LastError { get { lock (SyncRoot) { return _lastError; } } }
        public static int SearchRadiusPixels { get { return DefaultSearchRadiusPixels; } }
        public static int SearchRadiusTiles { get { return DefaultSearchRadiusPixels / 16; } }

        public static bool IsTeleportRodItem(int itemType)
        {
            return itemType == RodOfDiscordItemId || itemType == RodOfHarmonyItemId;
        }

        public static bool IsRodOfHarmonyItem(int itemType)
        {
            return itemType == RodOfHarmonyItemId;
        }

        public static bool IsRodOfDiscordItem(int itemType)
        {
            return itemType == RodOfDiscordItemId;
        }

        public static int GetTeleportRodPriority(int itemType, string itemName)
        {
            if (IsRodOfHarmonyItem(itemType))
            {
                return 0;
            }

            if (IsRodOfDiscordItem(itemType))
            {
                return 1;
            }

            return IsTeleportRodItem(itemType) ? 2 : int.MaxValue;
        }

        public static bool TryFindItemArgument(object[] args, out object item)
        {
            item = null;
            if (args == null || args.Length == 0)
            {
                SetLastError("Teleport rod hook received no arguments.");
                return false;
            }

            for (var index = 0; index < args.Length; index++)
            {
                var candidate = args[index];
                int itemType;
                string itemName;
                if (TryReadItemInfo(candidate, out itemType, out itemName))
                {
                    item = candidate;
                    return true;
                }
            }

            SetLastError("Teleport rod hook did not receive a Terraria.Item argument.");
            return false;
        }

        public static bool TryReadItemInfo(object item, out int itemType, out string itemName)
        {
            itemType = 0;
            itemName = string.Empty;
            if (item == null)
            {
                return false;
            }

            if (!TryGetInt(item, "type", out itemType))
            {
                return false;
            }

            object rawName;
            if (TryGetMember(item, "Name", out rawName) ||
                TryGetMember(item, "HoverName", out rawName) ||
                TryGetMember(item, "AffixName", out rawName))
            {
                itemName = rawName == null ? string.Empty : rawName.ToString();
            }

            return true;
        }

        public static bool TryBuildCorrectionPlan(object player, object item, out TeleportRodCorrectionPlan plan)
        {
            plan = new TeleportRodCorrectionPlan();
            int itemType;
            string itemName;
            if (!TryReadItemInfo(item, out itemType, out itemName))
            {
                plan.SkipReason = "itemUnavailable";
                plan.CompatError = LastError;
                return false;
            }

            plan.ItemType = itemType;
            plan.ItemName = itemName;
            if (!IsTeleportRodItem(itemType))
            {
                plan.SkipReason = "notTeleportRod";
                return false;
            }

            if (player == null)
            {
                plan.SkipReason = "playerUnavailable";
                plan.CompatError = "Player unavailable.";
                return false;
            }

            if (!EnsureResolved(player))
            {
                plan.SkipReason = "compatUnavailable";
                plan.CompatError = LastError;
                return false;
            }

            TeleportRodContext context;
            if (!TryReadContext(player, plan, out context))
            {
                plan.SkipReason = "contextUnavailable";
                plan.CompatError = LastError;
                return false;
            }

            float limitedX;
            float limitedY;
            if (!TryLimitPoint(player, plan.RawTopLeftX, plan.RawTopLeftY, out limitedX, out limitedY))
            {
                plan.SkipReason = "limitPointUnavailable";
                plan.CompatError = LastError;
                return false;
            }

            plan.OriginalTopLeftX = limitedX;
            plan.OriginalTopLeftY = limitedY;
            plan.SearchRadiusPixels = DefaultSearchRadiusPixels;
            plan.SearchStepPixels = SearchStepPixels;

            string unsafeReason;
            if (IsSafeTeleportTopLeft(player, context, limitedX, limitedY, out unsafeReason))
            {
                plan.OriginalSafe = true;
                plan.SkipReason = "originalSafe";
                plan.Message = "Original teleport rod target is already safe; vanilla method will continue unchanged.";
                return true;
            }

            plan.OriginalSafe = false;
            plan.OriginalUnsafeReason = unsafeReason;

            float safeX;
            float safeY;
            if (!TryFindNearestSafeTeleportTopLeft(player, context, limitedX, limitedY, plan, out safeX, out safeY))
            {
                plan.SkipReason = "noCandidate";
                plan.Message = "No safe teleport top-left was found near the original target; vanilla method will continue unchanged.";
                return false;
            }

            plan.CorrectedTopLeftX = safeX;
            plan.CorrectedTopLeftY = safeY;
            if (!TryBuildTeleportRodMouseTarget(context, safeX, safeY, plan))
            {
                plan.SkipReason = "mouseTargetUnavailable";
                plan.CompatError = LastError;
                return false;
            }

            plan.HasCorrection = true;
            plan.Message = "Teleport rod mouse target corrected; vanilla method will continue.";
            return true;
        }

        public static bool TryApplyCorrectedMouseTarget(object player, TeleportRodCorrectionPlan plan, out MouseTargetInputState restoreState)
        {
            restoreState = null;
            if (plan == null || !plan.HasCorrection)
            {
                SetLastError("Teleport correction plan has no corrected target.");
                return false;
            }

            MouseTargetInputState captured;
            if (!TerrariaInputCompat.TryCaptureMouseTargetState(player, out captured))
            {
                plan.MouseCaptureSucceeded = false;
                plan.CompatError = TerrariaInputCompat.LastInputCompatError;
                SetLastError(plan.CompatError);
                return false;
            }

            plan.MouseCaptureSucceeded = true;
            if (!TerrariaInputCompat.TrySetMouseScreenPosition(plan.CorrectedMouseScreenX, plan.CorrectedMouseScreenY))
            {
                plan.MouseApplySucceeded = false;
                plan.CompatError = TerrariaInputCompat.LastInputCompatError;
                TerrariaInputCompat.TryRestoreMouseTargetState(captured);
                SetLastError(plan.CompatError);
                return false;
            }

            plan.MouseApplySucceeded = true;
            restoreState = captured;
            return true;
        }

        public static bool TryRestoreMouseTarget(MouseTargetInputState restoreState, TeleportRodCorrectionPlan plan)
        {
            if (restoreState == null)
            {
                if (plan != null)
                {
                    plan.MouseRestoreSucceeded = false;
                    plan.CompatError = "Mouse restore state is missing.";
                }

                SetLastError("Mouse restore state is missing.");
                return false;
            }

            var restored = TerrariaInputCompat.TryRestoreMouseTargetState(restoreState);
            if (plan != null)
            {
                plan.MouseRestoreSucceeded = restored;
                if (!restored)
                {
                    plan.CompatError = TerrariaInputCompat.LastInputCompatError;
                }
            }

            if (!restored)
            {
                SetLastError(TerrariaInputCompat.LastInputCompatError);
            }

            return restored;
        }

        private static bool TryReadContext(object player, TeleportRodCorrectionPlan plan, out TeleportRodContext context)
        {
            context = new TeleportRodContext();
            int width;
            int height;
            if (!TryGetInt(player, "width", out width) || !TryGetInt(player, "height", out height))
            {
                return Fail("Cannot read player width/height.");
            }

            float gravDir;
            if (!TryGetFloat(player, "gravDir", out gravDir) || Math.Abs(gravDir) < 0.001f)
            {
                gravDir = 1f;
            }

            float screenX;
            float screenY;
            if (!TerrariaInputCompat.TryGetScreenPosition(out screenX, out screenY))
            {
                return Fail("Cannot read Main.screenPosition: " + TerrariaInputCompat.LastInputCompatError);
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            var mouseX = GetStaticInt(mainType, "mouseX", 0);
            var mouseY = GetStaticInt(mainType, "mouseY", 0);
            var screenHeight = GetStaticInt(mainType, "screenHeight", 0);
            var maxTilesX = GetStaticInt(mainType, "maxTilesX", 0);
            var maxTilesY = GetStaticInt(mainType, "maxTilesY", 0);
            if (screenHeight <= 0 || maxTilesX <= 0 || maxTilesY <= 0)
            {
                return Fail("Cannot read screen/world dimensions for teleport rod correction.");
            }

            context.PlayerWidth = width;
            context.PlayerHeight = height;
            context.GravityDirection = gravDir;
            context.ScreenX = screenX;
            context.ScreenY = screenY;
            context.ScreenHeight = screenHeight;
            context.MaxTilesX = maxTilesX;
            context.MaxTilesY = maxTilesY;

            plan.OriginalMouseScreenX = mouseX;
            plan.OriginalMouseScreenY = mouseY;
            plan.OriginalMouseWorldX = screenX + mouseX;
            plan.OriginalMouseWorldY = screenY + mouseY;
            plan.RawTopLeftX = screenX + mouseX - width / 2f;
            plan.RawTopLeftY = gravDir < 0f
                ? screenY + screenHeight - mouseY
                : screenY + mouseY - height;

            return true;
        }

        private static bool TryFindNearestSafeTeleportTopLeft(
            object player,
            TeleportRodContext context,
            float originX,
            float originY,
            TeleportRodCorrectionPlan plan,
            out float safeX,
            out float safeY)
        {
            safeX = 0f;
            safeY = 0f;
            var bestDistanceSquared = int.MaxValue;
            var found = false;
            var radius = DefaultSearchRadiusPixels;
            var radiusSquared = radius * radius;

            for (var dy = -radius; dy <= radius; dy += SearchStepPixels)
            {
                for (var dx = -radius; dx <= radius; dx += SearchStepPixels)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    var distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared > radiusSquared)
                    {
                        continue;
                    }

                    plan.CandidateCount++;
                    if (distanceSquared >= bestDistanceSquared)
                    {
                        continue;
                    }

                    var candidateX = originX + dx;
                    var candidateY = originY + dy;
                    string unsafeReason;
                    if (!IsSafeTeleportTopLeft(player, context, candidateX, candidateY, out unsafeReason))
                    {
                        continue;
                    }

                    plan.ValidCandidateCount++;
                    bestDistanceSquared = distanceSquared;
                    safeX = candidateX;
                    safeY = candidateY;
                    found = true;
                }
            }

            if (found)
            {
                plan.NearestCandidateDistance = (float)Math.Sqrt(bestDistanceSquared);
            }

            return found;
        }

        private static bool IsSafeTeleportTopLeft(
            object player,
            TeleportRodContext context,
            float topLeftX,
            float topLeftY,
            out string unsafeReason)
        {
            unsafeReason = string.Empty;
            if (!IsFinite(topLeftX) || !IsFinite(topLeftY))
            {
                unsafeReason = "nonFinite";
                return false;
            }

            if (!IsWorldPointInBounds(context, topLeftX, topLeftY))
            {
                unsafeReason = "worldBounds";
                return false;
            }

            float reachableX;
            float reachableY;
            if (!TryLimitPoint(player, topLeftX, topLeftY, out reachableX, out reachableY))
            {
                unsafeReason = "limitPointUnavailable";
                return false;
            }

            if (DistanceSquared(reachableX, reachableY, topLeftX, topLeftY) > 0.001f)
            {
                unsafeReason = "outsideReachableArea";
                return false;
            }

            bool templeWallOnLine;
            if (!TryAnyWallOfTypeOnLine(player, context, topLeftX, topLeftY, LihzahrdBrickTileId, out templeWallOnLine))
            {
                unsafeReason = "templeLineCheckUnavailable";
                return false;
            }

            if (templeWallOnLine)
            {
                unsafeReason = "lihzahrdWallLine";
                return false;
            }

            bool templeWallBlocked;
            if (!TryIsTempleWallBlocked(context, topLeftX, topLeftY, out templeWallBlocked))
            {
                unsafeReason = "templeWallCheckUnavailable";
                return false;
            }

            if (templeWallBlocked)
            {
                unsafeReason = "templeWallPrePlantera";
                return false;
            }

            bool solidCollision;
            if (!TrySolidCollision(topLeftX, topLeftY, context.PlayerWidth, context.PlayerHeight, out solidCollision))
            {
                unsafeReason = "solidCollisionCheckUnavailable";
                return false;
            }

            if (solidCollision)
            {
                unsafeReason = "solidCollision";
                return false;
            }

            return true;
        }

        private static bool TryBuildTeleportRodMouseTarget(TeleportRodContext context, float topLeftX, float topLeftY, TeleportRodCorrectionPlan plan)
        {
            var mouseWorldX = topLeftX + context.PlayerWidth / 2f;
            int screenX;
            int screenY;
            float mouseWorldY;
            if (context.GravityDirection < 0f)
            {
                screenX = (int)Math.Round(mouseWorldX - context.ScreenX);
                screenY = (int)Math.Round(context.ScreenHeight - (topLeftY - context.ScreenY));
                mouseWorldY = context.ScreenY + screenY;
            }
            else
            {
                mouseWorldY = topLeftY + context.PlayerHeight;
                screenX = (int)Math.Round(mouseWorldX - context.ScreenX);
                screenY = (int)Math.Round(mouseWorldY - context.ScreenY);
            }

            plan.CorrectedMouseWorldX = mouseWorldX;
            plan.CorrectedMouseWorldY = mouseWorldY;
            plan.CorrectedMouseScreenX = screenX;
            plan.CorrectedMouseScreenY = screenY;
            return true;
        }

        private static bool TryLimitPoint(object player, float x, float y, out float limitedX, out float limitedY)
        {
            limitedX = x;
            limitedY = y;
            if (_limitPointMethod == null)
            {
                return Fail("Player.LimitPointToPlayerReachableArea is unavailable.");
            }

            try
            {
                var vector = CreateVector2(x, y);
                if (vector == null)
                {
                    return Fail("Cannot create Vector2 for reachable-area check.");
                }

                var args = new[] { vector };
                _limitPointMethod.Invoke(player, args);
                return TryReadVector2(args[0], out limitedX, out limitedY)
                    ? ClearError()
                    : Fail("Cannot read Vector2 returned by LimitPointToPlayerReachableArea.");
            }
            catch (Exception error)
            {
                return Fail("LimitPointToPlayerReachableArea failed: " + error.Message);
            }
        }

        private static bool TrySolidCollision(float x, float y, int width, int height, out bool collision)
        {
            collision = true;
            if (_solidCollisionMethod == null)
            {
                return Fail("Collision.SolidCollision is unavailable.");
            }

            try
            {
                var vector = CreateVector2(x, y);
                if (vector == null)
                {
                    return Fail("Cannot create Vector2 for SolidCollision.");
                }

                object[] args;
                if (_solidCollisionMethod.GetParameters().Length == 4)
                {
                    args = new[] { vector, (object)width, height, false };
                }
                else
                {
                    args = new[] { vector, (object)width, height };
                }

                collision = Convert.ToBoolean(_solidCollisionMethod.Invoke(null, args), CultureInfo.InvariantCulture);
                return ClearError();
            }
            catch (Exception error)
            {
                return Fail("Collision.SolidCollision failed: " + error.Message);
            }
        }

        private static bool TryAnyWallOfTypeOnLine(
            object player,
            TeleportRodContext context,
            float topLeftX,
            float topLeftY,
            int wallType,
            out bool blocked)
        {
            blocked = true;
            if (_anyWallOfTypeOnLineMethod == null)
            {
                return Fail("Collision.AnyWallOfTypeOnLine is unavailable.");
            }

            float centerX;
            float centerY;
            if (!TryReadPlayerCenter(player, context, out centerX, out centerY))
            {
                return Fail("Cannot read player center for temple wall line check.");
            }

            var targetCenterX = topLeftX + context.PlayerWidth / 2f;
            var targetCenterY = topLeftY + context.PlayerHeight / 2f;
            var fromTileX = Clamp((int)(centerX / 16f), 0, context.MaxTilesX - 1);
            var fromTileY = Clamp((int)(centerY / 16f), 0, context.MaxTilesY - 1);
            var toTileX = Clamp((int)(targetCenterX / 16f), 0, context.MaxTilesX - 1);
            var toTileY = Clamp((int)(targetCenterY / 16f), 0, context.MaxTilesY - 1);

            try
            {
                var parameters = _anyWallOfTypeOnLineMethod.GetParameters();
                var args = new object[]
                {
                    ConvertArgument(fromTileX, parameters[0].ParameterType),
                    ConvertArgument(fromTileY, parameters[1].ParameterType),
                    ConvertArgument(toTileX, parameters[2].ParameterType),
                    ConvertArgument(toTileY, parameters[3].ParameterType),
                    ConvertArgument(wallType, parameters[4].ParameterType)
                };
                blocked = Convert.ToBoolean(_anyWallOfTypeOnLineMethod.Invoke(null, args), CultureInfo.InvariantCulture);
                return ClearError();
            }
            catch (Exception error)
            {
                return Fail("Collision.AnyWallOfTypeOnLine failed: " + error.Message);
            }
        }

        private static bool TryIsTempleWallBlocked(TeleportRodContext context, float topLeftX, float topLeftY, out bool blocked)
        {
            blocked = true;
            if (_getTileSafelyMethod == null)
            {
                return Fail("Framing.GetTileSafely is unavailable.");
            }

            var tileX = (int)(topLeftX / 16f);
            var tileY = (int)(topLeftY / 16f);
            if (tileX < 0 || tileX >= context.MaxTilesX || tileY < 0 || tileY >= context.MaxTilesY)
            {
                blocked = true;
                return ClearError();
            }

            try
            {
                var tile = _getTileSafelyMethod.Invoke(null, new object[] { tileX, tileY });
                int wall;
                if (!TryReadTileWall(tile, out wall))
                {
                    return Fail("Cannot read tile wall for temple wall check.");
                }

                if (wall != TempleWallId)
                {
                    blocked = false;
                    return ClearError();
                }

                bool downedPlantBoss;
                if (!TryGetStaticBool(_npcType, "downedPlantBoss", out downedPlantBoss))
                {
                    return Fail("Cannot read NPC.downedPlantBoss for temple wall check.");
                }

                if (downedPlantBoss)
                {
                    blocked = false;
                    return ClearError();
                }

                bool remixWorld;
                TryGetStaticBool(TerrariaRuntimeTypes.MainType, "remixWorld", out remixWorld);
                double worldSurface;
                TryGetStaticDouble(TerrariaRuntimeTypes.MainType, "worldSurface", out worldSurface);
                blocked = remixWorld || tileY > worldSurface;
                return ClearError();
            }
            catch (Exception error)
            {
                return Fail("Temple wall check failed: " + error.Message);
            }
        }

        private static bool TryReadPlayerCenter(object player, TeleportRodContext context, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            object center;
            if (TryGetMember(player, "Center", out center) && TryReadVector2(center, out x, out y))
            {
                return true;
            }

            object position;
            float positionX;
            float positionY;
            if (TryGetMember(player, "position", out position) && TryReadVector2(position, out positionX, out positionY))
            {
                x = positionX + context.PlayerWidth / 2f;
                y = positionY + context.PlayerHeight / 2f;
                return true;
            }

            return false;
        }

        private static bool TryReadTileWall(object tile, out int wall)
        {
            wall = 0;
            if (tile == null)
            {
                return false;
            }

            if (TryGetInt(tile, "wall", out wall) ||
                TryGetInt(tile, "WallType", out wall))
            {
                return true;
            }

            return false;
        }

        private static bool IsWorldPointInBounds(TeleportRodContext context, float x, float y)
        {
            return x > WorldBoundaryMarginPixels &&
                   x < context.MaxTilesX * 16f - WorldBoundaryMarginPixels &&
                   y > WorldBoundaryMarginPixels &&
                   y < context.MaxTilesY * 16f - WorldBoundaryMarginPixels;
        }

        private static bool EnsureResolved(object player)
        {
            lock (SyncRoot)
            {
                if (_resolved)
                {
                    return HasRequiredMembers();
                }

                _resolved = true;
                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    _lastError = TerrariaRuntimeTypes.LastError;
                    return false;
                }

                var vector2Type = TerrariaRuntimeTypes.Vector2Type;
                var playerType = player == null ? TerrariaRuntimeTypes.PlayerType : player.GetType();
                if (vector2Type == null || playerType == null)
                {
                    _lastError = "Vector2 or Player type unavailable.";
                    return false;
                }

                _vector2Constructor = vector2Type.GetConstructor(new[] { typeof(float), typeof(float) });
                _limitPointMethod = playerType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(method =>
                    {
                        if (!string.Equals(method.Name, "LimitPointToPlayerReachableArea", StringComparison.Ordinal))
                        {
                            return false;
                        }

                        var parameters = method.GetParameters();
                        return parameters.Length == 1 &&
                               parameters[0].ParameterType.IsByRef &&
                               parameters[0].ParameterType.GetElementType() == vector2Type;
                    });

                _collisionType = FindType("Terraria.Collision");
                _framingType = FindType("Terraria.Framing");
                _npcType = FindType("Terraria.NPC");
                _solidCollisionMethod = FindSolidCollisionMethod(vector2Type);
                _anyWallOfTypeOnLineMethod = FindAnyWallOfTypeOnLineMethod();
                _getTileSafelyMethod = FindGetTileSafelyMethod();

                if (!HasRequiredMembers())
                {
                    _lastError = "Teleport rod correction missing required Terraria members: " +
                                 "LimitPoint=" + BoolText(_limitPointMethod != null) +
                                 ", SolidCollision=" + BoolText(_solidCollisionMethod != null) +
                                 ", AnyWallOfTypeOnLine=" + BoolText(_anyWallOfTypeOnLineMethod != null) +
                                 ", GetTileSafely=" + BoolText(_getTileSafelyMethod != null) +
                                 ", NPC=" + BoolText(_npcType != null) +
                                 ", Vector2Ctor=" + BoolText(_vector2Constructor != null) + ".";
                    return false;
                }

                _lastError = string.Empty;
                return true;
            }
        }

        private static bool HasRequiredMembers()
        {
            return _limitPointMethod != null &&
                   _solidCollisionMethod != null &&
                   _anyWallOfTypeOnLineMethod != null &&
                   _getTileSafelyMethod != null &&
                   _npcType != null &&
                   _vector2Constructor != null;
        }

        private static MethodInfo FindSolidCollisionMethod(Type vector2Type)
        {
            if (_collisionType == null || vector2Type == null)
            {
                return null;
            }

            var candidates = _collisionType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method =>
                {
                    if (!string.Equals(method.Name, "SolidCollision", StringComparison.Ordinal) || method.ReturnType != typeof(bool))
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != 3 && parameters.Length != 4)
                    {
                        return false;
                    }

                    return parameters[0].ParameterType == vector2Type &&
                           IsIntegerType(parameters[1].ParameterType) &&
                           IsIntegerType(parameters[2].ParameterType) &&
                           (parameters.Length == 3 || parameters[3].ParameterType == typeof(bool));
                })
                .OrderBy(method => method.GetParameters().Length)
                .ToArray();
            return candidates.Length == 0 ? null : candidates[0];
        }

        private static MethodInfo FindAnyWallOfTypeOnLineMethod()
        {
            if (_collisionType == null)
            {
                return null;
            }

            var candidates = _collisionType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method =>
                {
                    if (!string.Equals(method.Name, "AnyWallOfTypeOnLine", StringComparison.Ordinal) || method.ReturnType != typeof(bool))
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    return parameters.Length == 5 &&
                           IsNumericType(parameters[0].ParameterType) &&
                           IsNumericType(parameters[1].ParameterType) &&
                           IsNumericType(parameters[2].ParameterType) &&
                           IsNumericType(parameters[3].ParameterType) &&
                           IsIntegerType(parameters[4].ParameterType);
                })
                .ToArray();
            return candidates.Length == 0 ? null : candidates[0];
        }

        private static MethodInfo FindGetTileSafelyMethod()
        {
            if (_framingType == null)
            {
                return null;
            }

            return _framingType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (!string.Equals(method.Name, "GetTileSafely", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    return parameters.Length == 2 &&
                           IsIntegerType(parameters[0].ParameterType) &&
                           IsIntegerType(parameters[1].ParameterType);
                });
        }

        private static object CreateVector2(float x, float y)
        {
            try
            {
                return _vector2Constructor == null ? null : _vector2Constructor.Invoke(new object[] { x, y });
            }
            catch (Exception error)
            {
                SetLastError("Create Vector2 failed: " + error.Message);
                return null;
            }
        }

        private static bool TryReadVector2(object vector, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (vector == null)
            {
                return false;
            }

            return TryGetFloat(vector, "X", out x) && TryGetFloat(vector, "Y", out y);
        }

        private static object ConvertArgument(object value, Type targetType)
        {
            if (targetType == typeof(float))
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(double))
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(short))
            {
                return Convert.ToInt16(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(ushort))
            {
                return Convert.ToUInt16(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(byte))
            {
                return Convert.ToByte(value, CultureInfo.InvariantCulture);
            }

            return value;
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

        private static object GetStatic(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    return field.GetValue(null);
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property))
                {
                    return property.GetValue(null, null);
                }
            }
            catch (Exception error)
            {
                SetLastError("Read static member failed: " + name + ": " + error.Message);
            }

            return null;
        }

        private static bool TryGetMember(object instance, string name, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    value = field.GetValue(instance);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property))
                {
                    value = property.GetValue(instance, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                SetLastError("Read member failed: " + name + ": " + error.Message);
            }

            return false;
        }

        private static int GetStaticInt(Type type, string name, int fallback)
        {
            object value = GetStatic(type, name);
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool TryGetStaticBool(Type type, string name, out bool value)
        {
            value = false;
            object raw = GetStatic(type, name);
            if (raw == null)
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

        private static bool TryGetStaticDouble(Type type, string name, out double value)
        {
            value = 0d;
            object raw = GetStatic(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetInt(object instance, string name, out int value)
        {
            value = 0;
            object raw;
            if (!TryGetMember(instance, name, out raw) || raw == null)
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

        private static bool TryGetFloat(object instance, string name, out float value)
        {
            value = 0f;
            object raw;
            if (!TryGetMember(instance, name, out raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool Fail(string message)
        {
            SetLastError(message);
            return false;
        }

        private static bool ClearError()
        {
            SetLastError(string.Empty);
            return true;
        }

        private static void SetLastError(string message)
        {
            lock (SyncRoot)
            {
                _lastError = message ?? string.Empty;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float DistanceSquared(float ax, float ay, float bx, float by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return dx * dx + dy * dy;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static bool IsIntegerType(Type type)
        {
            return type == typeof(int) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(byte) ||
                   type == typeof(long);
        }

        private static bool IsNumericType(Type type)
        {
            return IsIntegerType(type) || type == typeof(float) || type == typeof(double);
        }

        private static string BoolText(bool value)
        {
            return value ? "yes" : "no";
        }

        private sealed class TeleportRodContext
        {
            public int PlayerWidth;
            public int PlayerHeight;
            public float GravityDirection;
            public float ScreenX;
            public float ScreenY;
            public int ScreenHeight;
            public int MaxTilesX;
            public int MaxTilesY;
        }
    }
}
