using System;
using System.Collections;
using System.Reflection;

namespace JueMingZ.Compat
{
    public sealed class PlayerRenameResult
    {
        public bool Invoked { get; set; }
        public int NetMode { get; set; }
        public string PreviousName { get; set; }
        public string RequestedName { get; set; }
        public string FinalName { get; set; }
        public bool RenameMethodInvoked { get; set; }
        public bool PlayerNameChanged { get; set; }
        public bool AnglerFinishedBefore { get; set; }
        public bool AnglerFinishedAfter { get; set; }
        public bool AnglerFinishedRefreshed { get; set; }
        public bool NameAlreadyFinishedToday { get; set; }
        public string Message { get; set; }

        public PlayerRenameResult()
        {
            PreviousName = string.Empty;
            RequestedName = string.Empty;
            FinalName = string.Empty;
            Message = string.Empty;
        }
    }

    public static class PlayerRenameCompat
    {
        // Rename writes are single-player scoped unless explicitly allowed, and
        // the final name must be reread before reporting success.
        private const int MaxPlayerNameLength = 20;
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static bool TryReadCurrentPlayerName(out string name, out string message)
        {
            name = string.Empty;
            message = string.Empty;

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            object player;
            if (!TryGetLocalPlayer(mainType, out player) || player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            name = ReadString(player, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                message = "Local player name is empty.";
                return false;
            }

            return true;
        }

        public static string BuildIncrementedNameForTesting(string currentName)
        {
            return BuildIncrementedName(currentName);
        }

        public static bool TryRenameLocalPlayer(string requestedName, bool allowMultiplayer, out PlayerRenameResult result)
        {
            result = new PlayerRenameResult();
            result.RequestedName = requestedName ?? string.Empty;

            var normalized = NormalizePlayerName(requestedName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                result.Message = "Requested player name is empty.";
                return false;
            }

            if (normalized.Length > MaxPlayerNameLength)
            {
                result.Message = "Requested player name exceeds " + MaxPlayerNameLength + " characters.";
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                result.Message = "Terraria.Main unavailable.";
                return false;
            }

            int netMode;
            TryReadStaticInt(mainType, "netMode", out netMode);
            result.NetMode = netMode;
            if (netMode != 0 && !allowMultiplayer)
            {
                result.Message = "Player rename is single-player only in this build.";
                return false;
            }

            object player;
            if (!TryGetLocalPlayer(mainType, out player) || player == null)
            {
                result.Message = "Local player unavailable.";
                return false;
            }

            result.PreviousName = ReadString(player, "name");
            bool finishedBefore;
            if (TryReadAnglerQuestFinished(mainType, out finishedBefore))
            {
                result.AnglerFinishedBefore = finishedBefore;
            }

            string renameMessage;
            result.RenameMethodInvoked = TryInvokeActivePlayerRename(mainType, normalized, out renameMessage);
            var finalName = ReadString(player, "name");
            if (!string.Equals(finalName, normalized, StringComparison.Ordinal))
            {
                TrySetMember(player, "name", normalized);
                finalName = ReadString(player, "name");
            }

            result.FinalName = finalName;
            result.PlayerNameChanged = !string.Equals(result.PreviousName, result.FinalName, StringComparison.Ordinal);
            if (!string.Equals(finalName, normalized, StringComparison.Ordinal))
            {
                result.Message = "Player name write failed. " + renameMessage;
                return false;
            }

            bool nameFinished;
            bool refreshed;
            result.NameAlreadyFinishedToday = TryNameAlreadyFinishedToday(mainType, normalized, out nameFinished) && nameFinished;
            bool finishedAfter;
            refreshed = TryRefreshAnglerQuestFinished(mainType, normalized, out finishedAfter);
            result.AnglerFinishedAfter = finishedAfter;
            result.AnglerFinishedRefreshed = refreshed;
            result.Invoked = true;
            result.Message = result.PlayerNameChanged
                ? "Player renamed from " + result.PreviousName + " to " + result.FinalName + "."
                : "Player name already matched " + result.FinalName + ".";
            if (!string.IsNullOrWhiteSpace(renameMessage))
            {
                result.Message += " " + renameMessage;
            }

            return true;
        }

        public static string BuildIncrementedName(string currentName)
        {
            var name = NormalizePlayerName(currentName);
            if (string.IsNullOrEmpty(name))
            {
                return "1";
            }

            var suffixStart = name.Length;
            while (suffixStart > 0 && IsAsciiDigit(name[suffixStart - 1]))
            {
                suffixStart--;
            }

            if (suffixStart == name.Length)
            {
                return name + "1";
            }

            var prefix = name.Substring(0, suffixStart);
            var digits = name.Substring(suffixStart);
            return prefix + IncrementDigitString(digits);
        }

        private static string IncrementDigitString(string digits)
        {
            if (string.IsNullOrEmpty(digits))
            {
                return "1";
            }

            var chars = digits.ToCharArray();
            var carry = 1;
            for (var index = chars.Length - 1; index >= 0; index--)
            {
                var value = chars[index] - '0' + carry;
                if (value >= 10)
                {
                    chars[index] = '0';
                    carry = 1;
                }
                else
                {
                    chars[index] = (char)('0' + value);
                    carry = 0;
                    break;
                }
            }

            return carry > 0 ? "1" + new string(chars) : new string(chars);
        }

        private static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        public static string NormalizePlayerName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            return name.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("\t", " ").Trim();
        }

        private static bool TryGetLocalPlayer(Type mainType, out object player)
        {
            player = GetStatic(mainType, "LocalPlayer");
            if (player != null)
            {
                return true;
            }

            int myPlayer;
            if (!TryReadStaticInt(mainType, "myPlayer", out myPlayer))
            {
                return false;
            }

            var players = GetStatic(mainType, "player");
            player = GetIndexed(players, myPlayer);
            return player != null;
        }

        private static bool TryInvokeActivePlayerRename(Type mainType, string newName, out string message)
        {
            message = string.Empty;
            var fileData = GetStatic(mainType, "ActivePlayerFileData");
            if (fileData == null)
            {
                message = "ActivePlayerFileData unavailable; used direct Player.name fallback if needed.";
                return false;
            }

            try
            {
                var method = fileData.GetType().GetMethod("Rename", InstanceFlags, null, new[] { typeof(string) }, null);
                if (method == null)
                {
                    message = "PlayerFileData.Rename unavailable; used direct Player.name fallback if needed.";
                    return false;
                }

                method.Invoke(fileData, new object[] { newName });
                message = "PlayerFileData.Rename invoked.";
                return true;
            }
            catch (Exception error)
            {
                message = "PlayerFileData.Rename failed: " + error.Message;
                return false;
            }
        }

        private static bool TryReadAnglerQuestFinished(Type mainType, out bool finished)
        {
            finished = false;
            var raw = GetStatic(mainType, "anglerQuestFinished");
            if (TryConvertBool(raw, out finished))
            {
                return true;
            }

            int myPlayer;
            TryReadStaticInt(mainType, "myPlayer", out myPlayer);
            return TryConvertBool(GetIndexed(raw, myPlayer), out finished);
        }

        private static bool TryRefreshAnglerQuestFinished(Type mainType, string playerName, out bool finished)
        {
            finished = false;
            if (!TryNameAlreadyFinishedToday(mainType, playerName, out finished))
            {
                return false;
            }

            FieldInfo field;
            if (TerrariaMemberCache.TryGetField(mainType, "anglerQuestFinished", true, out field))
            {
                var raw = field.GetValue(null);
                if (field.FieldType == typeof(bool))
                {
                    field.SetValue(null, finished);
                    return true;
                }

                int myPlayer;
                TryReadStaticInt(mainType, "myPlayer", out myPlayer);
                if (TrySetIndexedBool(raw, myPlayer, finished))
                {
                    return true;
                }
            }

            PropertyInfo property;
            if (TerrariaMemberCache.TryGetProperty(mainType, "anglerQuestFinished", true, out property) &&
                property.CanWrite &&
                property.PropertyType == typeof(bool))
            {
                property.SetValue(null, finished, null);
                return true;
            }

            return false;
        }

        private static bool TryNameAlreadyFinishedToday(Type mainType, string playerName, out bool finished)
        {
            finished = false;
            var list = GetStatic(mainType, "anglerWhoFinishedToday");
            if (list == null || string.IsNullOrWhiteSpace(playerName))
            {
                return false;
            }

            var strings = list as IEnumerable;
            if (strings == null)
            {
                return false;
            }

            foreach (var item in strings)
            {
                var name = item == null ? string.Empty : item.ToString();
                if (string.Equals(name, playerName, StringComparison.Ordinal))
                {
                    finished = true;
                    return true;
                }
            }

            return true;
        }

        private static bool TrySetIndexedBool(object collection, int index, bool value)
        {
            var array = collection as Array;
            if (array != null && index >= 0 && index < array.Length && array.GetType().GetElementType() == typeof(bool))
            {
                array.SetValue(value, index);
                return true;
            }

            var list = collection as IList;
            if (list != null && index >= 0 && index < list.Count)
            {
                list[index] = value;
                return true;
            }

            return false;
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

        private static object GetIndexed(object collection, int index)
        {
            if (collection == null || index < 0)
            {
                return null;
            }

            var array = collection as Array;
            if (array != null && index < array.Length)
            {
                return array.GetValue(index);
            }

            var list = collection as IList;
            if (list != null && index < list.Count)
            {
                return list[index];
            }

            return null;
        }

        private static string ReadString(object instance, string name)
        {
            var raw = GetMember(instance, name);
            return raw == null ? string.Empty : raw.ToString();
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    return field.GetValue(instance);
                }

                PropertyInfo property;
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

        private static bool TrySetMember(object instance, string name, object value)
        {
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
                    field.SetValue(instance, value);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanWrite)
                {
                    property.SetValue(instance, value, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryReadStaticInt(Type type, string name, out int value)
        {
            value = 0;
            var raw = GetStatic(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConvertBool(object raw, out bool value)
        {
            value = false;
            if (raw == null)
            {
                return false;
            }

            if (raw is bool)
            {
                value = (bool)raw;
                return true;
            }

            try
            {
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
