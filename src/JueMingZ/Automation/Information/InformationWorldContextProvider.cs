using System;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.Records;

namespace JueMingZ.Automation.Information
{
    // Context construction centralizes read-only world facts; unavailable UI/world state skips the pass.
    internal enum InformationWorldContextProfile
    {
        Status = 0,
        FullRecord = 1
    }

    internal static class InformationWorldContextProvider
    {
        private const ulong FileDataRefreshTicks = 60;
        private static readonly object SyncRoot = new object();
        private static InformationWorldContext _cachedContext;
        private static ContextCacheSignature _cachedSignature;
        private static InformationWorldContextProfile _cachedProfile;
        private static FileDataCache _fileDataCache;
        private static long _cacheHitCount;
        private static long _cacheMissCount;
        private static long _fileDataRefreshCount;
        private static string _lastProfile = string.Empty;

        public static bool TryBuild(out InformationWorldContext context, out string skipReason)
        {
            return TryBuild(InformationWorldContextProfile.FullRecord, out context, out skipReason);
        }

        internal static bool TryBuild(InformationWorldContextProfile profile, out InformationWorldContext context, out string skipReason)
        {
            context = null;
            skipReason = string.Empty;
            profile = NormalizeProfile(profile);

            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                skipReason = "terrariaRuntimeTypesUnavailable";
                LogThrottle.WarnThrottled(
                    "information-runtime-types-unavailable",
                    TimeSpan.FromSeconds(30),
                    "InformationWorldContextProvider",
                    "Terraria runtime types are unavailable: " + TerrariaRuntimeTypes.LastError);
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                skipReason = "mainTypeUnavailable";
                return false;
            }

            if (TerrariaMainCompat.IsInMainMenu)
            {
                skipReason = "mainMenu";
                return false;
            }

            Terraria.Player player;
            if (!TerrariaMainCompat.TryGetLocalPlayer(out player))
            {
                skipReason = "localPlayerUnavailable";
                return false;
            }

            if (!TerrariaPlayerReadCompat.IsActive(player))
            {
                skipReason = "localPlayerInactive";
                return false;
            }

            var screenPosition = TerrariaMainCompat.ScreenPosition;
            var screenX = screenPosition.X;
            var screenY = screenPosition.Y;

            var screenWidth = TerrariaMainCompat.ScreenWidth;
            var screenHeight = TerrariaMainCompat.ScreenHeight;
            if (screenWidth <= 0)
            {
                screenWidth = 1280;
            }

            if (screenHeight <= 0)
            {
                screenHeight = 720;
            }

            var playerCenter = TerrariaPlayerReadCompat.Center(player);
            var playerX = playerCenter.X;
            var playerY = playerCenter.Y;
            var updateCount = (ulong)TerrariaMainCompat.GameUpdateCount;
            var worldName = BuildWorldName(mainType);
            var worldKey = BuildWorldKey(mainType, worldName);
            var signature = new ContextCacheSignature(
                mainType,
                player,
                updateCount,
                screenX,
                screenY,
                screenWidth,
                screenHeight,
                playerX,
                playerY,
                worldKey);

            lock (SyncRoot)
            {
                if (_cachedContext != null &&
                    _cachedSignature.Equals(signature) &&
                    ProfileSatisfies(_cachedProfile, profile))
                {
                    _cacheHitCount++;
                    _lastProfile = ToDiagnosticProfile(profile);
                    context = _cachedContext;
                    return true;
                }

                _cacheMissCount++;
            }

            var playerName = TerrariaPlayerReadCompat.Name(player);
            var playerRecordKey = string.Empty;
            var worldRecordKey = string.Empty;
            if (RequiresFileData(profile))
            {
                var fileData = GetFileData(mainType, player, updateCount, worldKey, playerName, worldName);
                playerName = fileData.PlayerName;
                worldName = fileData.WorldName;
                playerRecordKey = fileData.PlayerRecordKey;
                worldRecordKey = fileData.WorldRecordKey;
            }

            context = new InformationWorldContext
            {
                MainType = mainType,
                LocalPlayer = player,
                ScreenX = screenX,
                ScreenY = screenY,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                PlayerCenterX = playerX,
                PlayerCenterY = playerY,
                GameUpdateCount = updateCount,
                WorldKey = worldKey,
                PlayerRecordKey = playerRecordKey,
                WorldRecordKey = worldRecordKey,
                PlayerName = playerName,
                WorldName = worldName,
                WorldContextProfile = ToDiagnosticProfile(profile)
            };

            lock (SyncRoot)
            {
                _cachedContext = context;
                _cachedSignature = signature;
                _cachedProfile = profile;
                _lastProfile = ToDiagnosticProfile(profile);
            }

            return true;
        }

        public static long CacheHitCount
        {
            get { lock (SyncRoot) { return _cacheHitCount; } }
        }

        public static long CacheMissCount
        {
            get { lock (SyncRoot) { return _cacheMissCount; } }
        }

        public static long FileDataRefreshCount
        {
            get { lock (SyncRoot) { return _fileDataRefreshCount; } }
        }

        public static string LastProfile
        {
            get { lock (SyncRoot) { return _lastProfile; } }
        }

        internal static void ResetCacheForTesting()
        {
            lock (SyncRoot)
            {
                _cachedContext = null;
                _cachedSignature = new ContextCacheSignature();
                _cachedProfile = InformationWorldContextProfile.Status;
                _fileDataCache = null;
                _cacheHitCount = 0;
                _cacheMissCount = 0;
                _fileDataRefreshCount = 0;
                _lastProfile = string.Empty;
            }
        }

        internal static bool CanReuseCachedContextForTesting(
            InformationWorldContext cachedContext,
            InformationWorldContextProfile cachedProfile,
            InformationWorldContextProfile requestedProfile,
            InformationWorldContext probeContext)
        {
            if (cachedContext == null || probeContext == null)
            {
                return false;
            }

            var cachedSignature = BuildSignatureFromContext(cachedContext);
            var probeSignature = BuildSignatureFromContext(probeContext);
            return cachedSignature.Equals(probeSignature) &&
                   ProfileSatisfies(NormalizeProfile(cachedProfile), NormalizeProfile(requestedProfile));
        }

        internal static bool RequiresFileDataForTesting(InformationWorldContextProfile profile)
        {
            return RequiresFileData(NormalizeProfile(profile));
        }

        internal static bool ShouldRefreshFileDataForTesting(
            InformationWorldContextProfile profile,
            object cachedPlayer,
            string cachedWorldKey,
            ulong cachedTick,
            object currentPlayer,
            string currentWorldKey,
            ulong currentTick)
        {
            return ShouldRefreshFileData(
                NormalizeProfile(profile),
                cachedPlayer,
                cachedWorldKey,
                cachedTick,
                currentPlayer,
                currentWorldKey,
                currentTick);
        }

        private static string BuildWorldKey(Type mainType, string worldName)
        {
            var worldId = InformationReflection.GetStaticMember(mainType, "worldID");
            var id = worldId == null ? string.Empty : Convert.ToString(worldId, CultureInfo.InvariantCulture);
            return (string.IsNullOrWhiteSpace(worldName) ? "unknown" : worldName.Trim()) + "#" + id;
        }

        private static string BuildWorldName(Type mainType)
        {
            return FirstNonEmpty(
                InformationReflection.TryReadStaticString(mainType, "worldName"),
                InformationReflection.TryReadStaticString(mainType, "worldNameClean"));
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first.Trim() : (second ?? string.Empty).Trim();
        }

        private static FileDataCache GetFileData(Type mainType, object player, ulong updateCount, string worldKey, string fallbackPlayerName, string fallbackWorldName)
        {
            lock (SyncRoot)
            {
                if (_fileDataCache != null &&
                    !ShouldRefreshFileData(
                        InformationWorldContextProfile.FullRecord,
                        _fileDataCache.Player,
                        _fileDataCache.WorldKey,
                        _fileDataCache.LastRefreshTick,
                        player,
                        worldKey,
                        updateCount))
                {
                    return _fileDataCache;
                }
            }

            var playerFile = InformationReflection.GetStaticMember(mainType, "ActivePlayerFileData");
            var worldFile = InformationReflection.GetStaticMember(mainType, "ActiveWorldFileData");
            var playerName = FirstNonEmpty(
                InformationReflection.TryReadString(playerFile, "Name"),
                fallbackPlayerName);
            var playerPath = InformationReflection.TryReadString(playerFile, "Path");
            var worldPath = InformationReflection.TryReadString(worldFile, "Path");
            var worldName = FirstNonEmpty(InformationReflection.TryReadString(worldFile, "Name"), fallbackWorldName);
            var refreshed = new FileDataCache
            {
                Player = player,
                WorldKey = worldKey ?? string.Empty,
                LastRefreshTick = updateCount,
                PlayerName = playerName,
                WorldName = worldName,
                PlayerRecordKey = PlayerWorldBehaviorStore.BuildIdentityKey(playerPath, playerName),
                WorldRecordKey = PlayerWorldBehaviorStore.BuildIdentityKey(worldPath, worldKey)
            };

            lock (SyncRoot)
            {
                _fileDataCache = refreshed;
                _fileDataRefreshCount++;
            }

            return refreshed;
        }

        private static bool ShouldRefreshFileData(
            InformationWorldContextProfile profile,
            object cachedPlayer,
            string cachedWorldKey,
            ulong cachedTick,
            object currentPlayer,
            string currentWorldKey,
            ulong currentTick)
        {
            if (!RequiresFileData(profile))
            {
                return false;
            }

            if (!object.ReferenceEquals(cachedPlayer, currentPlayer) ||
                !string.Equals(cachedWorldKey ?? string.Empty, currentWorldKey ?? string.Empty, StringComparison.Ordinal))
            {
                return true;
            }

            if (cachedTick == 0 || currentTick == 0 || currentTick < cachedTick)
            {
                return true;
            }

            return currentTick - cachedTick >= FileDataRefreshTicks;
        }

        private static ContextCacheSignature BuildSignatureFromContext(InformationWorldContext context)
        {
            return new ContextCacheSignature(
                context.MainType,
                context.LocalPlayer,
                context.GameUpdateCount,
                context.ScreenX,
                context.ScreenY,
                context.ScreenWidth,
                context.ScreenHeight,
                context.PlayerCenterX,
                context.PlayerCenterY,
                context.WorldKey);
        }

        private static bool RequiresFileData(InformationWorldContextProfile profile)
        {
            return profile == InformationWorldContextProfile.FullRecord;
        }

        private static bool ProfileSatisfies(InformationWorldContextProfile cached, InformationWorldContextProfile requested)
        {
            return cached == InformationWorldContextProfile.FullRecord ||
                   requested == InformationWorldContextProfile.Status;
        }

        private static InformationWorldContextProfile NormalizeProfile(InformationWorldContextProfile profile)
        {
            return profile == InformationWorldContextProfile.FullRecord
                ? InformationWorldContextProfile.FullRecord
                : InformationWorldContextProfile.Status;
        }

        private static string ToDiagnosticProfile(InformationWorldContextProfile profile)
        {
            return profile == InformationWorldContextProfile.FullRecord ? "fullRecord" : "status";
        }

        private struct ContextCacheSignature
        {
            private readonly Type _mainType;
            private readonly object _player;
            private readonly ulong _gameUpdateCount;
            private readonly float _screenX;
            private readonly float _screenY;
            private readonly int _screenWidth;
            private readonly int _screenHeight;
            private readonly float _playerCenterX;
            private readonly float _playerCenterY;
            private readonly string _worldKey;

            public ContextCacheSignature(
                Type mainType,
                object player,
                ulong gameUpdateCount,
                float screenX,
                float screenY,
                int screenWidth,
                int screenHeight,
                float playerCenterX,
                float playerCenterY,
                string worldKey)
            {
                _mainType = mainType;
                _player = player;
                _gameUpdateCount = gameUpdateCount;
                _screenX = screenX;
                _screenY = screenY;
                _screenWidth = screenWidth;
                _screenHeight = screenHeight;
                _playerCenterX = playerCenterX;
                _playerCenterY = playerCenterY;
                _worldKey = worldKey ?? string.Empty;
            }

            public bool Equals(ContextCacheSignature other)
            {
                return object.ReferenceEquals(_mainType, other._mainType) &&
                       object.ReferenceEquals(_player, other._player) &&
                       _gameUpdateCount == other._gameUpdateCount &&
                       _screenX.Equals(other._screenX) &&
                       _screenY.Equals(other._screenY) &&
                       _screenWidth == other._screenWidth &&
                       _screenHeight == other._screenHeight &&
                       _playerCenterX.Equals(other._playerCenterX) &&
                       _playerCenterY.Equals(other._playerCenterY) &&
                       string.Equals(_worldKey, other._worldKey, StringComparison.Ordinal);
            }
        }

        private sealed class FileDataCache
        {
            public object Player;
            public string WorldKey;
            public ulong LastRefreshTick;
            public string PlayerName;
            public string WorldName;
            public string PlayerRecordKey;
            public string WorldRecordKey;
        }
    }
}
