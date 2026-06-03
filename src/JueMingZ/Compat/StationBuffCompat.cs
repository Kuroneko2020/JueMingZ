using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace JueMingZ.Compat
{
    public sealed class StationBuffTarget
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int TileType { get; set; }
        public int BuffType { get; set; }
        public int OriginX { get; set; }
        public int OriginY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int FrameX { get; set; }
        public int FrameY { get; set; }
        public float DistanceSquared { get; set; }
        public string Name { get; set; }

        public StationBuffTarget()
        {
            Name = string.Empty;
        }
    }

    public sealed class StationBuffScanDiagnostics
    {
        public long ScanCount { get; set; }
        public long CacheHitCount { get; set; }
        public long CacheMissCount { get; set; }
        public int TilesVisitedLast { get; set; }
        public double LastScanMs { get; set; }
        public string TileFastPathStatus { get; set; }
        public string LastDecision { get; set; }

        public StationBuffScanDiagnostics()
        {
            TileFastPathStatus = string.Empty;
            LastDecision = string.Empty;
        }
    }

    public static class StationBuffCompat
    {
        private const int ScanCacheTtlTicks = 30;
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly object DiagnosticsSyncRoot = new object();
        private static readonly object AccessorSyncRoot = new object();
        private static readonly StationBuffDefinition[] StationDefinitions =
        {
            new StationBuffDefinition(125, 29, "Crystal Ball", 2, 2, true),
            new StationBuffDefinition(287, 93, "Ammo Box", 2, 2, false),
            new StationBuffDefinition(354, 150, "Bewitching Table", 3, 2, false),
            new StationBuffDefinition(377, 159, "Sharpening Station", 3, 2, false),
            new StationBuffDefinition(464, 348, "War Table", 3, 2, false),
            new StationBuffDefinition(621, 192, "Slice of Cake", 2, 1, false),
            new StationBuffDefinition(699, 366, "Potion Station", 3, 2, true)
        };
        private static readonly Dictionary<Type, TileAccessor> TileAccessors = new Dictionary<Type, TileAccessor>();
        private static readonly Dictionary<Type, TileCollectionAccessor> TileCollectionAccessors = new Dictionary<Type, TileCollectionAccessor>();
        private static readonly int AllStationBuffMask = BuildAllStationBuffMask();
        private static StationBuffScanCacheEntry _lastScanCache;
        private static long _scanCount;
        private static long _scanCacheHitCount;
        private static long _scanCacheMissCount;
        private static int _tilesVisitedLast;
        private static double _lastScanMs;
        private static string _tileFastPathStatus = "not-used";
        private static string _lastDecision = string.Empty;
        private static bool _disableTileFastPathForTesting;
        private static object _tileCollectionOverrideForTesting;

        public static int AllKnownStationBuffMask
        {
            get { return AllStationBuffMask; }
        }

        public static bool TryFindMissingStationBuff(object player, out StationBuffTarget target, out string message)
        {
            target = null;
            List<StationBuffTarget> targets;
            if (!TryFindMissingStationBuffs(player, out targets, out message))
            {
                return false;
            }

            target = targets.Count > 0 ? targets[0] : null;
            if (target == null)
            {
                message = "No reachable missing station buff furniture found.";
                return false;
            }

            return true;
        }

        public static bool TryFindMissingStationBuffs(object player, out List<StationBuffTarget> targets, out string message)
        {
            return TryFindMissingStationBuffs(
                player,
                BuildMissingBuffMaskFromPlayer(player),
                AutoMiningCompat.ReadGameUpdateCount(),
                out targets,
                out message);
        }

        public static bool TryFindMissingStationBuffs(object player, int missingBuffMask, long tick, out List<StationBuffTarget> targets, out string message)
        {
            targets = new List<StationBuffTarget>();
            message = string.Empty;
            if (player == null)
            {
                message = "Local player unavailable.";
                RecordLastDecision(message);
                return false;
            }

            missingBuffMask &= AllStationBuffMask;
            if (missingBuffMask == 0)
            {
                message = "All known station buffs are active.";
                RecordLastDecision(message);
                return false;
            }

            int left;
            int top;
            int right;
            int bottom;
            var usingFallbackReach = false;
            if (!NurseServiceCompat.TryGetTileReachRegion(player, out left, out top, out right, out bottom))
            {
                BuildFallbackTileReachRegion(player, out left, out top, out right, out bottom);
                usingFallbackReach = true;
            }

            var tiles = GetTileCollectionForScan();
            if (tiles == null)
            {
                message = "Main.tile is unavailable.";
                RecordTileFastPathStatus("unavailable");
                RecordLastDecision(message);
                return false;
            }

            int maxTilesX;
            int maxTilesY;
            if (!TryGetTileCollectionBounds(tiles, out maxTilesX, out maxTilesY))
            {
                message = "Main.tile bounds are unavailable.";
                RecordTileFastPathStatus("unavailable");
                RecordLastDecision(message);
                return false;
            }

            var maxX = maxTilesX - 1;
            var maxY = maxTilesY - 1;
            left = Clamp(left, 0, maxX);
            right = Clamp(right, 0, maxX);
            top = Clamp(top, 0, maxY);
            bottom = Clamp(bottom, 0, maxY);

            var playerCenterX = ReadCenterX(player);
            var playerCenterY = ReadCenterY(player);
            var playerTileX = Clamp((int)Math.Floor(playerCenterX / 16f), 0, maxX);
            var playerTileY = Clamp((int)Math.Floor(playerCenterY / 16f), 0, maxY);
            var signature = new StationBuffScanSignature(
                RuntimeHelpersGetHashCode(tiles),
                maxTilesX,
                maxTilesY,
                left,
                top,
                right,
                bottom,
                playerTileX,
                playerTileY,
                missingBuffMask);
            if (TryReadScanCache(signature, tick, out targets, out message))
            {
                return targets.Count > 0;
            }

            targets = new List<StationBuffTarget>();
            RecordScanCacheMiss();
            var stopwatch = Stopwatch.StartNew();
            var tilesVisited = 0;
            var bestByStation = new StationBuffTarget[StationDefinitions.Length];
            var bestDistanceByStation = new float[StationDefinitions.Length];
            for (var index = 0; index < bestDistanceByStation.Length; index++)
            {
                bestDistanceByStation[index] = float.MaxValue;
            }

            for (var x = left; x <= right; x++)
            {
                for (var y = top; y <= bottom; y++)
                {
                    tilesVisited++;
                    var tile = GetTile(tiles, x, y);
                    bool active;
                    int tileType;
                    int frameX;
                    int frameY;
                    if (!TryReadActiveTypeAndFrame(tile, out active, out tileType, out frameX, out frameY) || !active)
                    {
                        continue;
                    }

                    int definitionIndex;
                    StationBuffDefinition definition;
                    if (!TryGetStationDefinition(tileType, out definitionIndex, out definition) ||
                        (missingBuffMask & definition.Mask) == 0)
                    {
                        continue;
                    }

                    int width;
                    int height;
                    TryGetStationDimensions(tileType, out width, out height);
                    var originX = x;
                    var originY = y;
                    NormalizeStationOrigin(x, y, tileType, frameX, frameY, out originX, out originY);
                    var interactionX = originX + Math.Max(0, width / 2);
                    var interactionY = originY + Math.Max(0, height - 1);
                    var dx = interactionX * 16f + 8f - playerCenterX;
                    var dy = interactionY * 16f + 8f - playerCenterY;
                    var distance = dx * dx + dy * dy;
                    if (distance >= bestDistanceByStation[definitionIndex])
                    {
                        continue;
                    }

                    bestDistanceByStation[definitionIndex] = distance;
                    bestByStation[definitionIndex] = new StationBuffTarget
                    {
                        TileX = x,
                        TileY = y,
                        TileType = tileType,
                        BuffType = definition.BuffType,
                        OriginX = originX,
                        OriginY = originY,
                        Width = width,
                        Height = height,
                        FrameX = frameX,
                        FrameY = frameY,
                        DistanceSquared = distance,
                        Name = definition.Name
                    };
                }
            }

            for (var index = 0; index < bestByStation.Length; index++)
            {
                if (bestByStation[index] != null)
                {
                    targets.Add(bestByStation[index]);
                }
            }

            targets.Sort(CompareTargetsByDistance);
            stopwatch.Stop();
            RecordScanCompleted(tilesVisited, stopwatch.Elapsed.TotalMilliseconds);
            if (targets.Count <= 0)
            {
                message = usingFallbackReach
                    ? "No reachable missing station buff furniture found in fallback tile reach region."
                    : "No reachable missing station buff furniture found.";
                WriteScanCache(signature, tick, false, targets, message);
                RecordLastDecision(message);
                return false;
            }

            message = usingFallbackReach
                ? "Selected station buff furniture batch by fallback tile reach region."
                : "Selected station buff furniture batch by Terraria tile reach region.";
            WriteScanCache(signature, tick, true, targets, message);
            RecordLastDecision(message);
            return true;
        }

        public static bool OpensInventoryPanel(int tileType)
        {
            int definitionIndex;
            StationBuffDefinition definition;
            return TryGetStationDefinition(tileType, out definitionIndex, out definition) && definition.OpensInventoryPanel;
        }

        public static List<Tuple<int, int>> BuildInteractionPoints(int tileX, int tileY, int tileType, out string message)
        {
            message = string.Empty;
            var result = new List<Tuple<int, int>>();
            AddUnique(result, tileX, tileY);

            var tiles = GetTileCollectionForScan();
            if (tiles == null)
            {
                message = "Main.tile is unavailable; using target tile only.";
                return result;
            }

            var tile = GetTile(tiles, tileX, tileY);
            if (tile == null)
            {
                message = "Target tile is unavailable; using target tile only.";
                return result;
            }

            int width;
            int height;
            TryGetStationDimensions(tileType, out width, out height);
            var frameX = ReadInt(tile, "frameX", 0);
            var frameY = ReadInt(tile, "frameY", 0);
            int originX;
            int originY;
            NormalizeStationOrigin(tileX, tileY, tileType, frameX, frameY, out originX, out originY);

            AddUnique(result, originX + Math.Max(0, width / 2), originY + Math.Max(0, height - 1));
            AddUnique(result, originX, originY);
            AddUnique(result, originX + Math.Max(0, width - 1), originY);
            AddUnique(result, originX, originY + Math.Max(0, height - 1));
            AddUnique(result, originX + Math.Max(0, width - 1), originY + Math.Max(0, height - 1));

            for (var y = originY; y < originY + height; y++)
            {
                for (var x = originX; x < originX + width; x++)
                {
                    var candidateTile = GetTile(tiles, x, y);
                    bool active;
                    int candidateType;
                    int candidateFrameX;
                    int candidateFrameY;
                    if (TryReadActiveTypeAndFrame(candidateTile, out active, out candidateType, out candidateFrameX, out candidateFrameY) && active && candidateType == tileType)
                    {
                        AddUnique(result, x, y);
                    }
                }
            }

            for (var y = tileY - 2; y <= tileY + 2; y++)
            {
                for (var x = tileX - 2; x <= tileX + 2; x++)
                {
                    var candidateTile = GetTile(tiles, x, y);
                    bool active;
                    int candidateType;
                    int candidateFrameX;
                    int candidateFrameY;
                    if (TryReadActiveTypeAndFrame(candidateTile, out active, out candidateType, out candidateFrameX, out candidateFrameY) && active && candidateType == tileType)
                    {
                        AddUnique(result, x, y);
                    }
                }
            }

            message = "Interaction points built from frame " + frameX + "," + frameY + " origin " + originX + "," + originY + " size " + width + "x" + height + ".";
            return result;
        }

        public static bool TryMapStation(int tileType, out int buffType, out string name)
        {
            int definitionIndex;
            StationBuffDefinition definition;
            if (TryGetStationDefinition(tileType, out definitionIndex, out definition))
            {
                buffType = definition.BuffType;
                name = definition.Name;
                return true;
            }

            buffType = 0;
            name = string.Empty;
            return false;
        }

        public static bool TryGetStationDimensions(int tileType, out int width, out int height)
        {
            int definitionIndex;
            StationBuffDefinition definition;
            if (TryGetStationDefinition(tileType, out definitionIndex, out definition))
            {
                width = definition.Width;
                height = definition.Height;
                return true;
            }

            width = 1;
            height = 1;
            return false;
        }

        public static int GetStationBuffMaskForBuffType(int buffType)
        {
            if (buffType <= 0)
            {
                return 0;
            }

            for (var index = 0; index < StationDefinitions.Length; index++)
            {
                if (StationDefinitions[index].BuffType == buffType)
                {
                    return StationDefinitions[index].Mask;
                }
            }

            return 0;
        }

        public static StationBuffScanDiagnostics GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                return new StationBuffScanDiagnostics
                {
                    ScanCount = _scanCount,
                    CacheHitCount = _scanCacheHitCount,
                    CacheMissCount = _scanCacheMissCount,
                    TilesVisitedLast = _tilesVisitedLast,
                    LastScanMs = _lastScanMs,
                    TileFastPathStatus = _tileFastPathStatus,
                    LastDecision = _lastDecision
                };
            }
        }

        internal static void ResetDiagnosticsForTesting()
        {
            lock (DiagnosticsSyncRoot)
            {
                _lastScanCache = null;
                _scanCount = 0;
                _scanCacheHitCount = 0;
                _scanCacheMissCount = 0;
                _tilesVisitedLast = 0;
                _lastScanMs = 0d;
                _tileFastPathStatus = "not-used";
                _lastDecision = string.Empty;
                _disableTileFastPathForTesting = false;
                _tileCollectionOverrideForTesting = null;
            }
        }

        internal static void SetTileFastPathDisabledForTesting(bool disabled)
        {
            _disableTileFastPathForTesting = disabled;
        }

        internal static void SetTileCollectionOverrideForTesting(object tiles)
        {
            lock (DiagnosticsSyncRoot)
            {
                _tileCollectionOverrideForTesting = tiles;
                _lastScanCache = null;
            }
        }

        private static void NormalizeStationOrigin(int tileX, int tileY, int tileType, int frameX, int frameY, out int originX, out int originY)
        {
            int width;
            int height;
            TryGetStationDimensions(tileType, out width, out height);
            var localX = PositiveModulo(frameX / 18, Math.Max(1, width));
            var localY = PositiveModulo(frameY / 18, Math.Max(1, height));
            originX = tileX - localX;
            originY = tileY - localY;
        }

        private static int PositiveModulo(int value, int modulo)
        {
            if (modulo <= 1)
            {
                return 0;
            }

            var result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static void AddUnique(List<Tuple<int, int>> points, int x, int y)
        {
            if (points == null || x < 0 || y < 0)
            {
                return;
            }

            for (var index = 0; index < points.Count; index++)
            {
                if (points[index].Item1 == x && points[index].Item2 == y)
                {
                    return;
                }
            }

            points.Add(Tuple.Create(x, y));
        }

        private static int CompareTargetsByDistance(StationBuffTarget left, StationBuffTarget right)
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

            var distanceCompare = left.DistanceSquared.CompareTo(right.DistanceSquared);
            return distanceCompare != 0 ? distanceCompare : left.TileType.CompareTo(right.TileType);
        }

        private static int BuildAllStationBuffMask()
        {
            var mask = 0;
            for (var index = 0; index < StationDefinitions.Length; index++)
            {
                StationDefinitions[index].Mask = 1 << index;
                mask |= StationDefinitions[index].Mask;
            }

            return mask;
        }

        private static object GetTileCollectionForScan()
        {
            var overrideTiles = _tileCollectionOverrideForTesting;
            return overrideTiles ?? GetStatic(TerrariaRuntimeTypes.MainType, "tile");
        }

        private static int BuildMissingBuffMaskFromPlayer(object player)
        {
            if (player == null)
            {
                return AllStationBuffMask;
            }

            var mask = 0;
            for (var index = 0; index < StationDefinitions.Length; index++)
            {
                var definition = StationDefinitions[index];
                if (!PlayerBuffCompat.HasActiveBuff(player, definition.BuffType))
                {
                    mask |= definition.Mask;
                }
            }

            return mask;
        }

        private static bool TryGetStationDefinition(int tileType, out int definitionIndex, out StationBuffDefinition definition)
        {
            for (var index = 0; index < StationDefinitions.Length; index++)
            {
                var candidate = StationDefinitions[index];
                if (candidate.TileType == tileType)
                {
                    definitionIndex = index;
                    definition = candidate;
                    return true;
                }
            }

            definitionIndex = -1;
            definition = null;
            return false;
        }

        private static bool TryGetTileCollectionBounds(object tiles, out int maxTilesX, out int maxTilesY)
        {
            maxTilesX = 0;
            maxTilesY = 0;
            var array = tiles as Array;
            if (array == null || array.Rank != 2)
            {
                return false;
            }

            maxTilesX = array.GetLength(0);
            maxTilesY = array.GetLength(1);
            return maxTilesX > 0 && maxTilesY > 0;
        }

        private static object GetTile(object tiles, int x, int y)
        {
            if (tiles == null || x < 0 || y < 0)
            {
                return null;
            }

            try
            {
                if (!_disableTileFastPathForTesting)
                {
                    var accessor = GetTileCollectionAccessor(tiles.GetType());
                    if (accessor != null)
                    {
                        return accessor.GetTile(tiles, x, y);
                    }
                }
            }
            catch
            {
            }

            var array = tiles as Array;
            if (array == null || array.Rank != 2 || x >= array.GetLength(0) || y >= array.GetLength(1))
            {
                return null;
            }

            RecordTileFastPathStatus("array-reflection-fallback");
            return array.GetValue(x, y);
        }

        private static bool IsTileActive(object tile)
        {
            bool active;
            return TryReadActiveTypeAndFrame(tile, out active, out _, out _, out _) && active;
        }

        private static bool TryReadActiveTypeAndFrame(object tile, out bool active, out int tileType, out int frameX, out int frameY)
        {
            active = false;
            tileType = 0;
            frameX = 0;
            frameY = 0;
            if (tile == null)
            {
                return false;
            }

            try
            {
                if (!_disableTileFastPathForTesting)
                {
                    var accessor = GetTileAccessor(tile.GetType());
                    if (accessor != null &&
                        accessor.TryReadActive(tile, out active) &&
                        accessor.TryReadType(tile, out tileType))
                    {
                        accessor.TryReadFrameX(tile, out frameX);
                        accessor.TryReadFrameY(tile, out frameY);
                        RecordTileFastPathStatus(accessor.Status);
                        return true;
                    }
                }
            }
            catch
            {
            }

            active = ReadBool(tile, "active", false);
            tileType = ReadInt(tile, "type", 0);
            frameX = ReadInt(tile, "frameX", 0);
            frameY = ReadInt(tile, "frameY", 0);
            RecordTileFastPathStatus("member-cache-fallback");
            return true;
        }

        private static int ReadTileType(object tile)
        {
            bool active;
            int tileType;
            int frameX;
            int frameY;
            return TryReadActiveTypeAndFrame(tile, out active, out tileType, out frameX, out frameY) ? tileType : 0;
        }

        private static bool TryReadScanCache(StationBuffScanSignature signature, long tick, out List<StationBuffTarget> targets, out string message)
        {
            targets = null;
            message = string.Empty;
            lock (DiagnosticsSyncRoot)
            {
                if (_lastScanCache == null ||
                    !_lastScanCache.Signature.Equals(signature) ||
                    tick < _lastScanCache.Tick ||
                    tick - _lastScanCache.Tick > ScanCacheTtlTicks)
                {
                    return false;
                }

                _scanCacheHitCount++;
                targets = CloneTargets(_lastScanCache.Targets);
                message = _lastScanCache.Message;
                _lastDecision = _lastScanCache.Success
                    ? "cacheHit:" + message
                    : "cacheHitEmpty:" + message;
                return true;
            }
        }

        private static void WriteScanCache(StationBuffScanSignature signature, long tick, bool success, List<StationBuffTarget> targets, string message)
        {
            lock (DiagnosticsSyncRoot)
            {
                _lastScanCache = new StationBuffScanCacheEntry
                {
                    Signature = signature,
                    Tick = tick,
                    Success = success,
                    Targets = CloneTargetArray(targets),
                    Message = message ?? string.Empty
                };
            }
        }

        private static void RecordScanCacheMiss()
        {
            lock (DiagnosticsSyncRoot)
            {
                _scanCacheMissCount++;
            }
        }

        private static void RecordScanCompleted(int tilesVisited, double elapsedMs)
        {
            lock (DiagnosticsSyncRoot)
            {
                _scanCount++;
                _tilesVisitedLast = Math.Max(0, tilesVisited);
                _lastScanMs = elapsedMs < 0d ? 0d : elapsedMs;
            }
        }

        private static void RecordTileFastPathStatus(string status)
        {
            if (!string.IsNullOrWhiteSpace(status))
            {
                _tileFastPathStatus = status;
            }
        }

        private static void RecordLastDecision(string decision)
        {
            lock (DiagnosticsSyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
            }
        }

        private static List<StationBuffTarget> CloneTargets(StationBuffTarget[] targets)
        {
            var result = new List<StationBuffTarget>();
            if (targets == null)
            {
                return result;
            }

            for (var index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null)
                {
                    result.Add(CloneTarget(targets[index]));
                }
            }

            return result;
        }

        private static StationBuffTarget[] CloneTargetArray(List<StationBuffTarget> targets)
        {
            if (targets == null || targets.Count <= 0)
            {
                return new StationBuffTarget[0];
            }

            var result = new StationBuffTarget[targets.Count];
            for (var index = 0; index < targets.Count; index++)
            {
                result[index] = CloneTarget(targets[index]);
            }

            return result;
        }

        private static StationBuffTarget CloneTarget(StationBuffTarget target)
        {
            if (target == null)
            {
                return null;
            }

            return new StationBuffTarget
            {
                TileX = target.TileX,
                TileY = target.TileY,
                TileType = target.TileType,
                BuffType = target.BuffType,
                OriginX = target.OriginX,
                OriginY = target.OriginY,
                Width = target.Width,
                Height = target.Height,
                FrameX = target.FrameX,
                FrameY = target.FrameY,
                DistanceSquared = target.DistanceSquared,
                Name = target.Name ?? string.Empty
            };
        }

        private static int RuntimeHelpersGetHashCode(object instance)
        {
            return instance == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(instance);
        }

        private static TileCollectionAccessor GetTileCollectionAccessor(Type type)
        {
            if (type == null)
            {
                return null;
            }

            lock (AccessorSyncRoot)
            {
                TileCollectionAccessor cached;
                if (TileCollectionAccessors.TryGetValue(type, out cached))
                {
                    return cached;
                }

                var resolved = TileCollectionAccessor.Create(type);
                TileCollectionAccessors[type] = resolved;
                return resolved;
            }
        }

        private static TileAccessor GetTileAccessor(Type type)
        {
            if (type == null)
            {
                return null;
            }

            lock (AccessorSyncRoot)
            {
                TileAccessor cached;
                if (TileAccessors.TryGetValue(type, out cached))
                {
                    return cached;
                }

                var resolved = TileAccessor.Create(type);
                TileAccessors[type] = resolved;
                return resolved;
            }
        }

        private static float ReadCenterX(object instance)
        {
            return ReadFloat(instance, "position", "X") + ReadInt(instance, "width", 0) / 2f;
        }

        private static float ReadCenterY(object instance)
        {
            return ReadFloat(instance, "position", "Y") + ReadInt(instance, "height", 0) / 2f;
        }

        private static void BuildFallbackTileReachRegion(object player, out int left, out int top, out int right, out int bottom)
        {
            var centerTileX = (int)(ReadCenterX(player) / 16f);
            var centerTileY = (int)(ReadCenterY(player) / 16f);
            const int horizontalReachTiles = 10;
            const int verticalReachTiles = 8;
            left = centerTileX - horizontalReachTiles;
            right = centerTileX + horizontalReachTiles;
            top = centerTileY - verticalReachTiles;
            bottom = centerTileY + verticalReachTiles;
        }

        private static float ReadFloat(object instance, string vectorName, string componentName)
        {
            var vector = GetMember(instance, vectorName);
            var raw = GetMember(vector, componentName);
            try { return raw == null ? 0f : Convert.ToSingle(raw); }
            catch { return 0f; }
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            var raw = GetMember(instance, name);
            try { return raw == null ? fallback : Convert.ToInt32(raw); }
            catch { return fallback; }
        }

        private static bool ReadBool(object instance, string name, bool fallback)
        {
            var raw = GetMember(instance, name);
            if (raw == null && instance != null)
            {
                try
                {
                    var method = instance.GetType().GetMethod(name, InstanceFlags, null, Type.EmptyTypes, null);
                    if (method != null)
                    {
                        raw = method.Invoke(instance, null);
                    }
                }
                catch
                {
                }
            }

            try { return raw == null ? fallback : Convert.ToBoolean(raw); }
            catch { return fallback; }
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

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property)
                ? property.GetValue(instance, null)
                : null;
        }

        private static object GetStatic(Type type, string name)
        {
            if (type == null)
            {
                return null;
            }

            if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
            {
                return field.GetValue(null);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, true, out var property)
                ? property.GetValue(null, null)
                : null;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private sealed class StationBuffDefinition
        {
            public readonly int TileType;
            public readonly int BuffType;
            public readonly string Name;
            public readonly int Width;
            public readonly int Height;
            public readonly bool OpensInventoryPanel;
            public int Mask;

            public StationBuffDefinition(int tileType, int buffType, string name, int width, int height, bool opensInventoryPanel)
            {
                TileType = tileType;
                BuffType = buffType;
                Name = name ?? string.Empty;
                Width = width;
                Height = height;
                OpensInventoryPanel = opensInventoryPanel;
            }
        }

        private struct StationBuffScanSignature
        {
            private readonly int _tileCollectionId;
            private readonly int _maxTilesX;
            private readonly int _maxTilesY;
            private readonly int _left;
            private readonly int _top;
            private readonly int _right;
            private readonly int _bottom;
            private readonly int _playerTileX;
            private readonly int _playerTileY;
            private readonly int _missingBuffMask;

            public StationBuffScanSignature(
                int tileCollectionId,
                int maxTilesX,
                int maxTilesY,
                int left,
                int top,
                int right,
                int bottom,
                int playerTileX,
                int playerTileY,
                int missingBuffMask)
            {
                _tileCollectionId = tileCollectionId;
                _maxTilesX = maxTilesX;
                _maxTilesY = maxTilesY;
                _left = left;
                _top = top;
                _right = right;
                _bottom = bottom;
                _playerTileX = playerTileX;
                _playerTileY = playerTileY;
                _missingBuffMask = missingBuffMask;
            }

            public bool Equals(StationBuffScanSignature other)
            {
                return _tileCollectionId == other._tileCollectionId &&
                       _maxTilesX == other._maxTilesX &&
                       _maxTilesY == other._maxTilesY &&
                       _left == other._left &&
                       _top == other._top &&
                       _right == other._right &&
                       _bottom == other._bottom &&
                       _playerTileX == other._playerTileX &&
                       _playerTileY == other._playerTileY &&
                       _missingBuffMask == other._missingBuffMask;
            }
        }

        private sealed class StationBuffScanCacheEntry
        {
            public StationBuffScanSignature Signature;
            public long Tick;
            public bool Success;
            public StationBuffTarget[] Targets;
            public string Message;
        }

        private sealed class TileCollectionAccessor
        {
            private readonly Func<object, int, int, object> _getter;
            private readonly bool _isArray;

            private TileCollectionAccessor(Func<object, int, int, object> getter, bool isArray)
            {
                _getter = getter;
                _isArray = isArray;
            }

            public static TileCollectionAccessor Create(Type type)
            {
                if (type == null)
                {
                    return null;
                }

                if (type.IsArray && type.GetArrayRank() == 2)
                {
                    var getter = CompileTwoDimensionalArrayGetter(type);
                    return getter == null ? null : new TileCollectionAccessor(getter, true);
                }

                var indexer = FindTwoIntIndexer(type);
                if (indexer == null)
                {
                    return null;
                }

                var indexerGetter = CompileIndexerGetter(type, indexer);
                return indexerGetter == null ? null : new TileCollectionAccessor(indexerGetter, false);
            }

            public object GetTile(object collection, int x, int y)
            {
                if (_getter == null || collection == null || x < 0 || y < 0)
                {
                    return null;
                }

                if (_isArray)
                {
                    var array = collection as Array;
                    if (array == null || x >= array.GetLength(0) || y >= array.GetLength(1))
                    {
                        return null;
                    }
                }

                RecordTileFastPathStatus(_isArray ? "array-compiled" : "indexer-compiled");
                return _getter(collection, x, y);
            }

            private static PropertyInfo FindTwoIntIndexer(Type type)
            {
                try
                {
                    var properties = type.GetProperties(InstanceFlags);
                    for (var index = 0; index < properties.Length; index++)
                    {
                        var property = properties[index];
                        if (!property.CanRead)
                        {
                            continue;
                        }

                        var parameters = property.GetIndexParameters();
                        if (parameters.Length == 2 &&
                            parameters[0].ParameterType == typeof(int) &&
                            parameters[1].ParameterType == typeof(int))
                        {
                            return property;
                        }
                    }
                }
                catch
                {
                }

                return null;
            }

            private static Func<object, int, int, object> CompileTwoDimensionalArrayGetter(Type type)
            {
                try
                {
                    var collection = Expression.Parameter(typeof(object), "collection");
                    var x = Expression.Parameter(typeof(int), "x");
                    var y = Expression.Parameter(typeof(int), "y");
                    var body = Expression.ArrayIndex(Expression.Convert(collection, type), x, y);
                    return Expression.Lambda<Func<object, int, int, object>>(
                        Expression.Convert(body, typeof(object)),
                        collection,
                        x,
                        y).Compile();
                }
                catch
                {
                    return null;
                }
            }

            private static Func<object, int, int, object> CompileIndexerGetter(Type type, PropertyInfo property)
            {
                try
                {
                    var collection = Expression.Parameter(typeof(object), "collection");
                    var x = Expression.Parameter(typeof(int), "x");
                    var y = Expression.Parameter(typeof(int), "y");
                    var body = Expression.Property(Expression.Convert(collection, type), property, x, y);
                    return Expression.Lambda<Func<object, int, int, object>>(
                        Expression.Convert(body, typeof(object)),
                        collection,
                        x,
                        y).Compile();
                }
                catch
                {
                    return null;
                }
            }
        }

        private sealed class TileAccessor
        {
            private readonly MemberReader[] _activeReaders;
            private readonly MemberReader[] _typeReaders;
            private readonly MemberReader[] _frameXReaders;
            private readonly MemberReader[] _frameYReaders;

            private TileAccessor(MemberReader[] activeReaders, MemberReader[] typeReaders, MemberReader[] frameXReaders, MemberReader[] frameYReaders, string status)
            {
                _activeReaders = activeReaders ?? new MemberReader[0];
                _typeReaders = typeReaders ?? new MemberReader[0];
                _frameXReaders = frameXReaders ?? new MemberReader[0];
                _frameYReaders = frameYReaders ?? new MemberReader[0];
                Status = string.IsNullOrWhiteSpace(status) ? "member-compiled" : status;
            }

            public string Status { get; private set; }

            public static TileAccessor Create(Type type)
            {
                if (type == null)
                {
                    return null;
                }

                var active = BuildReaders(type, new[] { "HasTile", "IsActive", "active" });
                var tileType = BuildReaders(type, new[] { "TileType", "type" });
                if (active.Length <= 0 || tileType.Length <= 0)
                {
                    return null;
                }

                return new TileAccessor(
                    active,
                    tileType,
                    BuildReaders(type, new[] { "TileFrameX", "frameX" }),
                    BuildReaders(type, new[] { "TileFrameY", "frameY" }),
                    "member-compiled");
            }

            public bool TryReadActive(object tile, out bool active)
            {
                active = false;
                object raw;
                if (!TryReadFirst(_activeReaders, tile, out raw))
                {
                    return false;
                }

                try
                {
                    active = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public bool TryReadType(object tile, out int tileType)
            {
                return TryReadInt(_typeReaders, tile, out tileType);
            }

            public bool TryReadFrameX(object tile, out int frameX)
            {
                return TryReadInt(_frameXReaders, tile, out frameX);
            }

            public bool TryReadFrameY(object tile, out int frameY)
            {
                return TryReadInt(_frameYReaders, tile, out frameY);
            }

            private static bool TryReadInt(MemberReader[] readers, object instance, out int value)
            {
                value = 0;
                object raw;
                if (!TryReadFirst(readers, instance, out raw))
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

            private static bool TryReadFirst(MemberReader[] readers, object instance, out object value)
            {
                value = null;
                if (readers == null || instance == null)
                {
                    return false;
                }

                for (var index = 0; index < readers.Length; index++)
                {
                    var reader = readers[index];
                    if (reader != null && reader.TryRead(instance, out value))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static MemberReader[] BuildReaders(Type type, string[] names)
            {
                var readers = new List<MemberReader>();
                for (var index = 0; names != null && index < names.Length; index++)
                {
                    var reader = ResolveReader(type, names[index]);
                    if (reader != null)
                    {
                        readers.Add(reader);
                    }
                }

                return readers.ToArray();
            }

            private static MemberReader ResolveReader(Type type, string name)
            {
                if (type == null || string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                try
                {
                    var field = type.GetField(name, InstanceFlags);
                    if (field != null)
                    {
                        return MemberReader.FromField(field);
                    }

                    var property = type.GetProperty(name, InstanceFlags);
                    if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                    {
                        return MemberReader.FromProperty(property);
                    }

                    var method = type.GetMethod(name, InstanceFlags, null, Type.EmptyTypes, null);
                    if (method != null)
                    {
                        return MemberReader.FromMethod(method);
                    }
                }
                catch
                {
                }

                return null;
            }
        }

        private sealed class MemberReader
        {
            private readonly Func<object, object> _reader;

            private MemberReader(Func<object, object> reader)
            {
                _reader = reader;
            }

            public static MemberReader FromField(FieldInfo field)
            {
                if (field == null)
                {
                    return null;
                }

                return new MemberReader(CompileFieldReader(field) ?? field.GetValue);
            }

            public static MemberReader FromProperty(PropertyInfo property)
            {
                if (property == null)
                {
                    return null;
                }

                return new MemberReader(CompilePropertyReader(property) ?? (instance => property.GetValue(instance, null)));
            }

            public static MemberReader FromMethod(MethodInfo method)
            {
                if (method == null)
                {
                    return null;
                }

                return new MemberReader(CompileMethodReader(method) ?? (instance => method.Invoke(instance, null)));
            }

            public bool TryRead(object instance, out object value)
            {
                value = null;
                if (_reader == null || instance == null)
                {
                    return false;
                }

                try
                {
                    value = _reader(instance);
                    return value != null;
                }
                catch
                {
                    return false;
                }
            }

            private static Func<object, object> CompileFieldReader(FieldInfo field)
            {
                try
                {
                    var instance = Expression.Parameter(typeof(object), "instance");
                    var body = Expression.Field(Expression.Convert(instance, field.DeclaringType), field);
                    return CompileBoxedReader(instance, body);
                }
                catch
                {
                    return null;
                }
            }

            private static Func<object, object> CompilePropertyReader(PropertyInfo property)
            {
                try
                {
                    var instance = Expression.Parameter(typeof(object), "instance");
                    var body = Expression.Property(Expression.Convert(instance, property.DeclaringType), property);
                    return CompileBoxedReader(instance, body);
                }
                catch
                {
                    return null;
                }
            }

            private static Func<object, object> CompileMethodReader(MethodInfo method)
            {
                try
                {
                    var instance = Expression.Parameter(typeof(object), "instance");
                    var body = Expression.Call(Expression.Convert(instance, method.DeclaringType), method);
                    return CompileBoxedReader(instance, body);
                }
                catch
                {
                    return null;
                }
            }

            private static Func<object, object> CompileBoxedReader(ParameterExpression instance, Expression body)
            {
                return Expression.Lambda<Func<object, object>>(
                    Expression.Convert(body, typeof(object)),
                    instance).Compile();
            }
        }
    }
}
