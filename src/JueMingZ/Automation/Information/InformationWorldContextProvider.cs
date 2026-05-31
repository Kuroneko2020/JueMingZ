using System;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.Records;

namespace JueMingZ.Automation.Information
{
    internal static class InformationWorldContextProvider
    {
        public static bool TryBuild(out InformationWorldContext context, out string skipReason)
        {
            context = null;
            skipReason = string.Empty;

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

            var worldKey = BuildWorldKey(mainType);
            var worldName = BuildWorldName(mainType);
            var playerFile = InformationReflection.GetStaticMember(mainType, "ActivePlayerFileData");
            var worldFile = InformationReflection.GetStaticMember(mainType, "ActiveWorldFileData");
            var playerName = FirstNonEmpty(
                InformationReflection.TryReadString(playerFile, "Name"),
                TerrariaPlayerReadCompat.Name(player));
            var playerPath = InformationReflection.TryReadString(playerFile, "Path");
            var worldPath = InformationReflection.TryReadString(worldFile, "Path");
            worldName = FirstNonEmpty(InformationReflection.TryReadString(worldFile, "Name"), worldName);

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
                PlayerRecordKey = PlayerWorldBehaviorStore.BuildIdentityKey(playerPath, playerName),
                WorldRecordKey = PlayerWorldBehaviorStore.BuildIdentityKey(worldPath, worldKey),
                PlayerName = playerName,
                WorldName = worldName
            };
            return true;
        }

        private static object GetLocalPlayer(Type mainType)
        {
            var local = InformationReflection.GetStaticMember(mainType, "LocalPlayer");
            if (local != null)
            {
                return local;
            }

            var players = InformationReflection.GetStaticMember(mainType, "player");
            int index;
            InformationReflection.TryReadStaticInt(mainType, "myPlayer", out index);
            if (index < 0)
            {
                index = 0;
            }

            return InformationReflection.GetIndexedValue(players, index);
        }

        private static string BuildWorldKey(Type mainType)
        {
            var worldName = BuildWorldName(mainType);
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
    }
}
