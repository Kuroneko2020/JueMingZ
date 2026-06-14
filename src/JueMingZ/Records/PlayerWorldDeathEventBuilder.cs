using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using JueMingZ.Compat;

namespace JueMingZ.Records
{
    internal static class PlayerWorldDeathEventBuilder
    {
        private const float TileSize = 16f;

        public static bool TryBuildFromHook(
            PlayerWorldIdentityResolution identity,
            object player,
            object damageSource,
            double damage,
            int hitDirection,
            bool pvp,
            DateTime utcNow,
            out PlayerWorldDeathEvent deathEvent,
            out string message)
        {
            deathEvent = null;
            message = string.Empty;

            if (identity == null || !identity.IsResolved || string.IsNullOrWhiteSpace(identity.PairId))
            {
                message = "identity unresolved";
                return false;
            }

            var source = ReadSourceSnapshot(damageSource);
            float worldX;
            float worldY;
            var hasPosition = TryReadPlayerWorldCenter(player, out worldX, out worldY);
            if (!hasPosition)
            {
                worldX = -1f;
                worldY = -1f;
            }

            deathEvent = Build(
                identity.PairId,
                source,
                worldX,
                worldY,
                ResolveDeathText(damageSource, ReadPlayerName(player), source.SourceCustomReason),
                damage,
                hitDirection,
                pvp,
                utcNow);
            message = hasPosition ? "built" : "built:positionUnavailable";
            return true;
        }

        internal static PlayerWorldDeathEvent BuildForTesting(
            string pairId,
            PlayerWorldDeathSourceSnapshot source,
            float playerWorldX,
            float playerWorldY,
            string deathText,
            double damage,
            int hitDirection,
            bool pvp,
            DateTime utcNow)
        {
            return Build(pairId, source, playerWorldX, playerWorldY, deathText, damage, hitDirection, pvp, utcNow);
        }

        internal static PlayerWorldDeathSourceSnapshot ReadSourceSnapshotForTesting(object damageSource)
        {
            return ReadSourceSnapshot(damageSource);
        }

        private static PlayerWorldDeathEvent Build(
            string pairId,
            PlayerWorldDeathSourceSnapshot source,
            float playerWorldX,
            float playerWorldY,
            string deathText,
            double damage,
            int hitDirection,
            bool pvp,
            DateTime utcNow)
        {
            if (source == null)
            {
                source = new PlayerWorldDeathSourceSnapshot();
            }

            var normalizedUtc = utcNow.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
                : utcNow.ToUniversalTime();

            return new PlayerWorldDeathEvent
            {
                SchemaVersion = 1,
                EventId = Guid.NewGuid().ToString("N"),
                RealTimeUtc = FormatUtc(normalizedUtc),
                RealTimeLocalText = normalizedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                PlayerWorldX = playerWorldX,
                PlayerWorldY = playerWorldY,
                PlayerTileX = playerWorldX < 0f ? -1 : (int)Math.Floor(playerWorldX / TileSize),
                PlayerTileY = playerWorldY < 0f ? -1 : (int)Math.Floor(playerWorldY / TileSize),
                DeathText = deathText ?? string.Empty,
                Damage = damage,
                HitDirection = hitDirection,
                Pvp = pvp,
                SourceKind = string.IsNullOrWhiteSpace(source.SourceKind) ? PlayerWorldDeathSourceKind.Unknown : source.SourceKind,
                SourceNpcType = source.SourceNpcType,
                SourceProjectileType = source.SourceProjectileType,
                SourcePlayerName = source.SourcePlayerName ?? string.Empty,
                SourceOtherIndex = source.SourceOtherIndex,
                SourceCustomReason = source.SourceCustomReason ?? string.Empty,
                IdentityPairId = pairId ?? string.Empty
            };
        }

        private static PlayerWorldDeathSourceSnapshot ReadSourceSnapshot(object damageSource)
        {
            var source = new PlayerWorldDeathSourceSnapshot();
            if (damageSource == null)
            {
                return source;
            }

            var customReason = ReadStringMember(damageSource, "_sourceCustomReason");
            var npcIndex = ReadIntMember(damageSource, "_sourceNPCIndex", -1);
            var projectileIndex = ReadIntMember(damageSource, "_sourceProjectileLocalIndex", -1);
            var projectileType = ReadIntMember(damageSource, "_sourceProjectileType", 0);
            var playerIndex = ReadIntMember(damageSource, "_sourcePlayerIndex", -1);
            var otherIndex = ReadIntMember(damageSource, "_sourceOtherIndex", -1);

            source.SourceNpcType = npcIndex >= 0 ? ReadNpcType(npcIndex) : -1;
            source.SourceProjectileType = projectileType > 0 ? projectileType : (projectileIndex >= 0 ? ReadProjectileType(projectileIndex) : -1);
            source.SourcePlayerName = playerIndex >= 0 ? ReadPlayerName(playerIndex) : string.Empty;
            source.SourceOtherIndex = otherIndex;
            source.SourceCustomReason = customReason ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(source.SourceCustomReason))
            {
                source.SourceKind = PlayerWorldDeathSourceKind.Custom;
            }
            else if (npcIndex >= 0)
            {
                source.SourceKind = PlayerWorldDeathSourceKind.Npc;
            }
            else if (projectileIndex >= 0 || source.SourceProjectileType > 0)
            {
                source.SourceKind = PlayerWorldDeathSourceKind.Projectile;
            }
            else if (playerIndex >= 0)
            {
                source.SourceKind = PlayerWorldDeathSourceKind.Player;
            }
            else if (otherIndex >= 0)
            {
                source.SourceKind = PlayerWorldDeathSourceKind.Other;
            }

            return source;
        }

        private static string ResolveDeathText(object damageSource, string playerName, string fallback)
        {
            if (damageSource == null)
            {
                return fallback ?? string.Empty;
            }

            try
            {
                var method = damageSource.GetType().GetMethod(
                    "GetDeathText",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(string) },
                    null);
                if (method == null)
                {
                    return fallback ?? string.Empty;
                }

                var networkText = method.Invoke(damageSource, new object[] { playerName ?? string.Empty });
                return Convert.ToString(networkText, CultureInfo.InvariantCulture) ?? fallback ?? string.Empty;
            }
            catch
            {
                return fallback ?? string.Empty;
            }
        }

        private static bool TryReadPlayerWorldCenter(object player, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (player == null)
            {
                return false;
            }

            var center = ReadMember(player, "Center");
            if (TryReadVector2(center, out x, out y))
            {
                return true;
            }

            var position = ReadMember(player, "position");
            if (!TryReadVector2(position, out x, out y))
            {
                return false;
            }

            x += ReadIntMember(player, "width", 0) / 2f;
            y += ReadIntMember(player, "height", 0) / 2f;
            return true;
        }

        private static bool TryReadVector2(object value, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (value == null)
            {
                return false;
            }

            object rawX;
            object rawY;
            if (!TryReadMember(value, "X", out rawX) || !TryReadMember(value, "Y", out rawY))
            {
                return false;
            }

            try
            {
                x = Convert.ToSingle(rawX, CultureInfo.InvariantCulture);
                y = Convert.ToSingle(rawY, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                x = 0f;
                y = 0f;
                return false;
            }
        }

        private static int ReadNpcType(int index)
        {
            var npc = ReadIndexedStaticArray("npc", index);
            return npc == null ? -1 : ReadIntMember(npc, "type", -1);
        }

        private static int ReadProjectileType(int index)
        {
            var projectile = ReadIndexedStaticArray("projectile", index);
            return projectile == null ? -1 : ReadIntMember(projectile, "type", -1);
        }

        private static string ReadPlayerName(int index)
        {
            var player = ReadIndexedStaticArray("player", index);
            return ReadPlayerName(player);
        }

        private static string ReadPlayerName(object player)
        {
            return player == null ? string.Empty : ReadStringMember(player, "name");
        }

        private static object ReadIndexedStaticArray(string memberName, int index)
        {
            if (index < 0 || !TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                return null;
            }

            var collection = ReadStaticMember(TerrariaRuntimeTypes.MainType, memberName);
            var list = collection as IList;
            if (list != null && index < list.Count)
            {
                return list[index];
            }

            var array = collection as Array;
            if (array != null && array.Rank == 1 && index < array.GetLength(0))
            {
                return array.GetValue(index);
            }

            return null;
        }

        private static string ReadStringMember(object instance, string name)
        {
            var raw = ReadMember(instance, name);
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

        private static int ReadIntMember(object instance, string name, int fallback)
        {
            var raw = ReadMember(instance, name);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static object ReadStaticMember(Type type, string name)
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
            object value;
            return TryReadMember(instance, name, out value) ? value : null;
        }

        private static bool TryReadMember(object instance, string name, out object value)
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
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanRead)
                {
                    value = property.GetValue(instance, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }
    }
}
