using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JueMingZ.Compat;

namespace JueMingZ.Records
{
    public static class PlayerWorldIdentityResolver
    {
        private const int SchemaVersion = 1;

        public static bool TryResolveCurrent(out PlayerWorldIdentityResolution resolution)
        {
            PlayerWorldIdentityFacts facts;
            string failureReason;
            if (!TryBuildCurrentFacts(out facts, out failureReason))
            {
                resolution = BuildFailure(failureReason);
                return false;
            }

            return TryResolveAndPersist(facts, out resolution);
        }

        public static bool TryResolveCurrentReadOnly(out PlayerWorldIdentityResolution resolution)
        {
            PlayerWorldIdentityFacts facts;
            string failureReason;
            if (!TryBuildCurrentFacts(out facts, out failureReason))
            {
                resolution = BuildFailure(failureReason);
                return false;
            }

            return TryResolve(facts, out resolution);
        }

        public static bool TryResolveAndPersist(PlayerWorldIdentityFacts facts, out PlayerWorldIdentityResolution resolution)
        {
            if (!TryResolve(facts, out resolution))
            {
                return false;
            }

            PersistIdentityFiles(resolution);
            return true;
        }

        internal static bool TryResolveForTesting(PlayerWorldIdentityFacts facts, out PlayerWorldIdentityResolution resolution)
        {
            return TryResolve(facts, out resolution);
        }

        internal static bool TryPersistResolved(PlayerWorldIdentityResolution resolution)
        {
            if (resolution == null ||
                !resolution.IsResolved ||
                string.IsNullOrWhiteSpace(resolution.PlayerId) ||
                string.IsNullOrWhiteSpace(resolution.WorldId) ||
                string.IsNullOrWhiteSpace(resolution.PairId))
            {
                return false;
            }

            try
            {
                PersistIdentityFiles(resolution);
                return resolution.IdentityFilesWritten;
            }
            catch (Exception error)
            {
                resolution.IdentityFilesWritten = false;
                resolution.StorageMessage = error.GetType().Name + ": " + error.Message;
                resolution.DiagnosticSummary = BuildDiagnosticSummary(resolution);
                return false;
            }
        }

        private static bool TryBuildCurrentFacts(out PlayerWorldIdentityFacts facts, out string failureReason)
        {
            facts = null;
            failureReason = string.Empty;

            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                failureReason = "terrariaRuntimeTypesUnavailable:" + TerrariaRuntimeTypes.LastError;
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                failureReason = "mainTypeUnavailable";
                return false;
            }

            bool gameMenu;
            if (TryReadStaticBool(mainType, "gameMenu", out gameMenu) && gameMenu)
            {
                failureReason = "mainMenu";
                return false;
            }

            var playerFile = ReadStaticMember(mainType, "ActivePlayerFileData");
            var worldFile = ReadStaticMember(mainType, "ActiveWorldFileData");
            var player = ReadLocalPlayer(mainType);

            var mainWorldId = 0;
            var hasMainWorldId = TryReadStaticInt(mainType, "worldID", out mainWorldId);
            var worldId = 0;
            var hasWorldId = TryReadInt(worldFile, "WorldId", out worldId);
            if (!hasWorldId && hasMainWorldId)
            {
                worldId = mainWorldId;
                hasWorldId = true;
            }

            var worldSizeX = ReadFirstPositiveInt(worldFile, "WorldSizeX", mainType, "maxTilesX");
            var worldSizeY = ReadFirstPositiveInt(worldFile, "WorldSizeY", mainType, "maxTilesY");
            var mainWorldName = FirstNonEmpty(ReadStaticString(mainType, "worldName"), ReadStaticString(mainType, "worldNameClean"));
            var mainWorldPathName = ReadStaticString(mainType, "worldPathName");

            facts = new PlayerWorldIdentityFacts
            {
                PlayerPath = ReadString(playerFile, "Path"),
                PlayerIsCloudSave = ReadBool(playerFile, "IsCloudSave"),
                PlayerName = FirstNonEmpty(ReadString(playerFile, "Name"), ReadString(player, "name"), ReadString(player, "Name")),
                WorldPath = FirstNonEmpty(ReadString(worldFile, "Path"), mainWorldPathName),
                WorldIsCloudSave = ReadBool(worldFile, "IsCloudSave"),
                WorldName = FirstNonEmpty(ReadString(worldFile, "Name"), mainWorldName),
                WorldUniqueId = ReadGuidString(worldFile, "UniqueId"),
                WorldId = worldId,
                HasWorldId = hasWorldId,
                MapFileName = ReadString(worldFile, "MapFileName"),
                WorldSizeX = worldSizeX,
                WorldSizeY = worldSizeY,
                MainWorldName = mainWorldName,
                MainWorldId = mainWorldId,
                HasMainWorldId = hasMainWorldId,
                MainWorldPathName = mainWorldPathName
            };

            return true;
        }

        private static bool TryResolve(PlayerWorldIdentityFacts facts, out PlayerWorldIdentityResolution resolution)
        {
            resolution = null;
            if (facts == null)
            {
                resolution = BuildFailure("identityFactsUnavailable");
                return false;
            }

            var playerDisplayName = CleanDisplayName(facts.PlayerName);
            var playerPathHash = BuildPathHash(facts.PlayerPath, facts.PlayerIsCloudSave);
            string playerId;
            string playerSource;
            if (!string.IsNullOrWhiteSpace(playerPathHash))
            {
                playerId = "player-" + Sha256Hex("player-path\n" + playerPathHash);
                playerSource = PlayerWorldIdentitySourceKind.PlayerPathHash;
            }
            else if (!string.IsNullOrWhiteSpace(playerDisplayName))
            {
                playerId = "player-fallback-" + Sha256Hex("player-display\n" + NormalizeKeyPart(playerDisplayName));
                playerSource = PlayerWorldIdentitySourceKind.PlayerDisplayNameFallback;
            }
            else
            {
                resolution = BuildFailure("playerIdentityUnavailable");
                return false;
            }

            var worldDisplayName = FirstNonEmpty(CleanDisplayName(facts.WorldName), CleanDisplayName(facts.MainWorldName));
            var worldPathHash = BuildPathHash(FirstNonEmpty(facts.WorldPath, facts.MainWorldPathName), facts.WorldIsCloudSave);
            var uniqueId = NormalizeGuid(facts.WorldUniqueId);
            var mapFileName = CleanFileIdentity(facts.MapFileName);
            var worldIdValue = facts.HasWorldId ? facts.WorldId : (facts.HasMainWorldId ? facts.MainWorldId : 0);
            var hasWorldId = facts.HasWorldId || facts.HasMainWorldId;

            string worldId;
            string worldSource;
            if (!string.IsNullOrWhiteSpace(uniqueId))
            {
                worldId = "world-" + Sha256Hex("world-unique\n" + uniqueId);
                worldSource = PlayerWorldIdentitySourceKind.WorldUniqueId;
            }
            else if (!string.IsNullOrWhiteSpace(mapFileName))
            {
                worldId = "world-" + Sha256Hex("world-map\n" + NormalizeKeyPart(mapFileName));
                worldSource = PlayerWorldIdentitySourceKind.WorldMapFileName;
            }
            else if (hasWorldId && !string.IsNullOrWhiteSpace(worldPathHash))
            {
                worldId = "world-" + Sha256Hex("world-id-path\n" + worldIdValue.ToString(CultureInfo.InvariantCulture) + "\n" + worldPathHash);
                worldSource = PlayerWorldIdentitySourceKind.WorldIdPathHash;
            }
            else if (!string.IsNullOrWhiteSpace(worldPathHash))
            {
                worldId = "world-fallback-" + Sha256Hex("world-path\n" + worldPathHash);
                worldSource = PlayerWorldIdentitySourceKind.WorldPathHashFallback;
            }
            else if (hasWorldId && !string.IsNullOrWhiteSpace(worldDisplayName))
            {
                worldId = "world-fallback-" + Sha256Hex("world-id-display\n" + worldIdValue.ToString(CultureInfo.InvariantCulture) + "\n" + NormalizeKeyPart(worldDisplayName));
                worldSource = PlayerWorldIdentitySourceKind.WorldIdDisplayNameFallback;
            }
            else if (!string.IsNullOrWhiteSpace(worldDisplayName))
            {
                worldId = "world-fallback-" + Sha256Hex("world-display\n" + NormalizeKeyPart(worldDisplayName));
                worldSource = PlayerWorldIdentitySourceKind.WorldDisplayNameFallback;
            }
            else
            {
                resolution = BuildFailure("worldIdentityUnavailable");
                return false;
            }

            var pairId = "pair-" + Sha256Hex(playerId + "\n" + worldId);
            resolution = new PlayerWorldIdentityResolution
            {
                IsResolved = true,
                PlayerId = playerId,
                WorldId = worldId,
                PairId = pairId,
                PlayerDisplayName = playerDisplayName,
                WorldDisplayName = worldDisplayName,
                PlayerIdentitySourceKind = playerSource,
                WorldIdentitySourceKind = worldSource,
                PlayerPathHash = playerPathHash,
                WorldPathHash = worldPathHash,
                PlayerIsCloudSave = facts.PlayerIsCloudSave,
                WorldIsCloudSave = facts.WorldIsCloudSave,
                WorldUniqueId = uniqueId,
                MapFileName = mapFileName,
                TerrariaWorldId = worldIdValue,
                HasTerrariaWorldId = hasWorldId,
                WorldSizeX = Math.Max(0, facts.WorldSizeX),
                WorldSizeY = Math.Max(0, facts.WorldSizeY)
            };
            resolution.DiagnosticSummary = BuildDiagnosticSummary(resolution);
            return true;
        }

        private static PlayerWorldIdentityResolution BuildFailure(string failureReason)
        {
            return new PlayerWorldIdentityResolution
            {
                IsResolved = false,
                FailureReason = string.IsNullOrWhiteSpace(failureReason) ? "identityUnavailable" : failureReason,
                DiagnosticSummary = "failed:" + (string.IsNullOrWhiteSpace(failureReason) ? "identityUnavailable" : failureReason)
            };
        }

        private static void PersistIdentityFiles(PlayerWorldIdentityResolution resolution)
        {
            var now = FormatUtc(DateTime.UtcNow);
            var playerPath = PlayerWorldFeatureDataRoot.BuildPlayerIdentityPath(resolution.PlayerId);
            var worldPath = PlayerWorldFeatureDataRoot.BuildWorldIdentityPath(resolution.WorldId);
            var pairPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldIdentityPath(resolution.PairId);

            var playerFile = LoadOrCreate(playerPath, () => new PlayerIdentityFile());
            UpdatePlayerIdentityFile(playerFile, resolution, now);

            var worldFile = LoadOrCreate(worldPath, () => new WorldIdentityFile());
            UpdateWorldIdentityFile(worldFile, resolution, now);

            var pairFile = LoadOrCreate(pairPath, () => new PlayerWorldIdentityFile());
            UpdatePairIdentityFile(pairFile, resolution, now);

            string playerMessage;
            var playerSaved = PlayerWorldFeatureDataStore.TryWriteJson(playerPath, playerFile, out playerMessage);
            string worldMessage;
            var worldSaved = PlayerWorldFeatureDataStore.TryWriteJson(worldPath, worldFile, out worldMessage);
            string pairMessage;
            var pairSaved = PlayerWorldFeatureDataStore.TryWriteJson(pairPath, pairFile, out pairMessage);

            resolution.IdentityFilesWritten = playerSaved && worldSaved && pairSaved;
            resolution.StorageMessage =
                "player=" + playerMessage +
                ";world=" + worldMessage +
                ";pair=" + pairMessage;
            resolution.DiagnosticSummary = BuildDiagnosticSummary(resolution);
        }

        private static T LoadOrCreate<T>(string path, Func<T> create)
            where T : class
        {
            T loaded;
            string message;
            if (PlayerWorldFeatureDataStore.TryReadJson(path, out loaded, out message))
            {
                return loaded;
            }

            return create();
        }

        private static void UpdatePlayerIdentityFile(PlayerIdentityFile file, PlayerWorldIdentityResolution resolution, string now)
        {
            EnsurePlayerFileShape(file);
            file.SchemaVersion = SchemaVersion;
            file.PlayerId = resolution.PlayerId;
            file.IdentitySourceKind = resolution.PlayerIdentitySourceKind;
            file.DisplayName = resolution.PlayerDisplayName;
            file.PathHash = resolution.PlayerPathHash;
            file.IsCloudSave = resolution.PlayerIsCloudSave;
            if (string.IsNullOrWhiteSpace(file.FirstSeenUtc))
            {
                file.FirstSeenUtc = now;
            }

            file.LastSeenUtc = now;
            file.DiagnosticSummary = resolution.DiagnosticSummary;
            AddAlias(file.ObservedAliases, "playerPathHash", resolution.PlayerPathHash, now);
            AddAlias(file.ObservedAliases, "playerDisplayName", resolution.PlayerDisplayName, now);
            AddAlias(file.ObservedAliases, "identitySource", resolution.PlayerIdentitySourceKind, now);
        }

        private static void UpdateWorldIdentityFile(WorldIdentityFile file, PlayerWorldIdentityResolution resolution, string now)
        {
            EnsureWorldFileShape(file);
            file.SchemaVersion = SchemaVersion;
            file.WorldId = resolution.WorldId;
            file.IdentitySourceKind = resolution.WorldIdentitySourceKind;
            file.DisplayName = resolution.WorldDisplayName;
            file.PathHash = resolution.WorldPathHash;
            file.IsCloudSave = resolution.WorldIsCloudSave;
            file.UniqueId = resolution.WorldUniqueId;
            file.MapFileName = resolution.MapFileName;
            file.TerrariaWorldId = resolution.TerrariaWorldId;
            file.HasTerrariaWorldId = resolution.HasTerrariaWorldId;
            file.WorldSizeX = resolution.WorldSizeX;
            file.WorldSizeY = resolution.WorldSizeY;
            if (string.IsNullOrWhiteSpace(file.FirstSeenUtc))
            {
                file.FirstSeenUtc = now;
            }

            file.LastSeenUtc = now;
            file.DiagnosticSummary = resolution.DiagnosticSummary;
            AddAlias(file.ObservedAliases, "worldPathHash", resolution.WorldPathHash, now);
            AddAlias(file.ObservedAliases, "worldDisplayName", resolution.WorldDisplayName, now);
            AddAlias(file.ObservedAliases, "worldUniqueId", resolution.WorldUniqueId, now);
            AddAlias(file.ObservedAliases, "mapFileName", resolution.MapFileName, now);
            if (resolution.HasTerrariaWorldId)
            {
                AddAlias(file.ObservedAliases, "terrariaWorldId", resolution.TerrariaWorldId.ToString(CultureInfo.InvariantCulture), now);
            }

            AddAlias(file.ObservedAliases, "identitySource", resolution.WorldIdentitySourceKind, now);
        }

        private static void UpdatePairIdentityFile(PlayerWorldIdentityFile file, PlayerWorldIdentityResolution resolution, string now)
        {
            file.SchemaVersion = SchemaVersion;
            file.PairId = resolution.PairId;
            file.PlayerId = resolution.PlayerId;
            file.WorldId = resolution.WorldId;
            file.PlayerDisplayName = resolution.PlayerDisplayName;
            file.WorldDisplayName = resolution.WorldDisplayName;
            if (string.IsNullOrWhiteSpace(file.FirstSeenUtc))
            {
                file.FirstSeenUtc = now;
            }

            file.LastSeenUtc = now;
            file.DiagnosticSummary = resolution.DiagnosticSummary;
        }

        private static void EnsurePlayerFileShape(PlayerIdentityFile file)
        {
            if (file.ObservedAliases == null)
            {
                file.ObservedAliases = new List<PlayerWorldIdentityAlias>();
            }
        }

        private static void EnsureWorldFileShape(WorldIdentityFile file)
        {
            if (file.ObservedAliases == null)
            {
                file.ObservedAliases = new List<PlayerWorldIdentityAlias>();
            }
        }

        private static void AddAlias(List<PlayerWorldIdentityAlias> aliases, string kind, string value, string now)
        {
            if (aliases == null || string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var cleanValue = value.Trim();
            for (var index = 0; index < aliases.Count; index++)
            {
                var alias = aliases[index];
                if (alias == null)
                {
                    continue;
                }

                if (string.Equals(alias.Kind ?? string.Empty, kind, StringComparison.Ordinal) &&
                    string.Equals(alias.Value ?? string.Empty, cleanValue, StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(alias.FirstSeenUtc))
                    {
                        alias.FirstSeenUtc = now;
                    }

                    alias.LastSeenUtc = now;
                    return;
                }
            }

            aliases.Add(new PlayerWorldIdentityAlias
            {
                Kind = kind,
                Value = cleanValue,
                FirstSeenUtc = now,
                LastSeenUtc = now
            });
        }

        private static string BuildDiagnosticSummary(PlayerWorldIdentityResolution resolution)
        {
            if (resolution == null || !resolution.IsResolved)
            {
                return "failed";
            }

            return "playerSource=" + resolution.PlayerIdentitySourceKind +
                   ";worldSource=" + resolution.WorldIdentitySourceKind +
                   ";storage=" + (resolution.IdentityFilesWritten ? "saved" : "pending") +
                   (string.IsNullOrWhiteSpace(resolution.StorageMessage) ? string.Empty : ";" + resolution.StorageMessage);
        }

        private static object ReadLocalPlayer(Type mainType)
        {
            var localPlayer = ReadStaticMember(mainType, "LocalPlayer");
            if (localPlayer != null)
            {
                return localPlayer;
            }

            var playerCollection = ReadStaticMember(mainType, "player");
            int myPlayer;
            if (!TryReadStaticInt(mainType, "myPlayer", out myPlayer) || myPlayer < 0)
            {
                return null;
            }

            var list = playerCollection as IList;
            if (list != null && myPlayer < list.Count)
            {
                return list[myPlayer];
            }

            var array = playerCollection as Array;
            if (array != null && array.Rank == 1 && myPlayer < array.GetLength(0))
            {
                return array.GetValue(myPlayer);
            }

            return null;
        }

        private static int ReadFirstPositiveInt(object instance, string memberName, Type staticType, string staticMemberName)
        {
            int value;
            if (TryReadInt(instance, memberName, out value) && value > 0)
            {
                return value;
            }

            return TryReadStaticInt(staticType, staticMemberName, out value) && value > 0 ? value : 0;
        }

        private static object ReadStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                System.Reflection.FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    return field.GetValue(null);
                }

                System.Reflection.PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanRead)
                {
                    return property.GetValue(null, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                var type = instance.GetType();
                System.Reflection.FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    return field.GetValue(instance);
                }

                System.Reflection.PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanRead)
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static string ReadStaticString(Type type, string name)
        {
            return ConvertToString(ReadStaticMember(type, name));
        }

        private static string ReadString(object instance, string name)
        {
            return ConvertToString(ReadMember(instance, name));
        }

        private static bool ReadBool(object instance, string name)
        {
            bool value;
            return TryConvertBool(ReadMember(instance, name), out value) && value;
        }

        private static bool TryReadStaticBool(Type type, string name, out bool value)
        {
            return TryConvertBool(ReadStaticMember(type, name), out value);
        }

        private static bool TryReadStaticInt(Type type, string name, out int value)
        {
            return TryConvertInt(ReadStaticMember(type, name), out value);
        }

        private static bool TryReadInt(object instance, string name, out int value)
        {
            return TryConvertInt(ReadMember(instance, name), out value);
        }

        private static bool TryConvertBool(object raw, out bool value)
        {
            value = false;
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

        private static bool TryConvertInt(object raw, out int value)
        {
            value = 0;
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

        private static string ConvertToString(object raw)
        {
            if (raw == null)
            {
                return string.Empty;
            }

            try
            {
                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadGuidString(object instance, string name)
        {
            return NormalizeGuid(ConvertToString(ReadMember(instance, name)));
        }

        private static string NormalizeGuid(string value)
        {
            var clean = CleanFileIdentity(value);
            if (string.IsNullOrWhiteSpace(clean))
            {
                return string.Empty;
            }

            Guid parsed;
            return Guid.TryParse(clean, out parsed) && parsed != Guid.Empty ? parsed.ToString("D") : string.Empty;
        }

        private static string BuildPathHash(string path, bool isCloudSave)
        {
            var normalized = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            return Sha256Hex((isCloudSave ? "cloud" : "local") + "\n" + normalized);
        }

        private static string NormalizePath(string value)
        {
            return (value ?? string.Empty).Trim().Replace('\\', '/').ToLowerInvariant();
        }

        private static string CleanDisplayName(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string CleanFileIdentity(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeKeyPart(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string Sha256Hex(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(bytes.Length * 2);
                for (var index = 0; index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }
    }
}
