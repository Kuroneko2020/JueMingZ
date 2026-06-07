using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Compat
{
    public sealed class TaxCollectorTarget
    {
        public int NpcIndex { get; set; }
        public int WhoAmI { get; set; }
        public string Name { get; set; }
        public int TaxMoney { get; set; }

        public TaxCollectorTarget()
        {
            NpcIndex = -1;
            WhoAmI = -1;
            Name = string.Empty;
        }
    }

    public sealed class TaxCollectResult
    {
        public int NpcIndex { get; set; }
        public int WhoAmI { get; set; }
        public string NpcName { get; set; }
        public int TaxMoneyBefore { get; set; }
        public int TaxMoneyAfter { get; set; }
        public bool ChatOpened { get; set; }
        public bool ChatClosed { get; set; }
        public bool CollectInvoked { get; set; }
        public bool ShoppingSettingsApplied { get; set; }
        public string Message { get; set; }

        public TaxCollectResult()
        {
            NpcIndex = -1;
            WhoAmI = -1;
            NpcName = string.Empty;
            Message = string.Empty;
        }
    }

    public static class TaxCollectorServiceCompat
    {
        // Tax collection uses vanilla NPC chat/service flow and verifies tax
        // money; callers must not edit player.taxMoney.
        public const int TaxCollectorNpcType = 441;

        private static readonly object SyncRoot = new object();
        private static int _cachedNpcIndex = -1;
        private static MethodInfo _collectTaxesMethod;
        private static readonly Dictionary<string, MethodInfo> MethodCache = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        public static bool TryFindReachableTaxCollector(object player, out TaxCollectorTarget target, out string message)
        {
            target = null;
            message = string.Empty;
            if (player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            var taxMoney = ReadInt(player, "taxMoney", 0);
            if (taxMoney <= 0)
            {
                message = "No tax money available.";
                return false;
            }

            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                message = TerrariaRuntimeTypes.LastError;
                return false;
            }

            var npcs = GetStatic(TerrariaRuntimeTypes.MainType, "npc") as IList;
            if (npcs == null)
            {
                message = "Main.npc is unavailable.";
                return false;
            }

            if (TryReadCachedTarget(player, npcs, taxMoney, out target))
            {
                return true;
            }

            var playerCenterX = ReadCenterX(player);
            var playerCenterY = ReadCenterY(player);
            var bestDistance = float.MaxValue;
            for (var index = 0; index < npcs.Count; index++)
            {
                var npc = npcs[index];
                if (!IsReachableTaxCollector(player, npc))
                {
                    continue;
                }

                var dx = ReadCenterX(npc) - playerCenterX;
                var dy = ReadCenterY(npc) - playerCenterY;
                var distance = dx * dx + dy * dy;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                target = BuildTarget(npc, index, taxMoney);
            }

            if (target == null)
            {
                CacheNpcIndex(-1);
                message = "No reachable tax collector found.";
                return false;
            }

            CacheNpcIndex(target.NpcIndex);
            return true;
        }

        public static bool TryOpenAndCollect(int npcIndex, out TaxCollectResult result)
        {
            result = new TaxCollectResult
            {
                NpcIndex = npcIndex
            };

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                result.Message = "Local player unavailable.";
                return false;
            }

            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                result.Message = TerrariaRuntimeTypes.LastError;
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            var npcs = GetStatic(mainType, "npc") as IList;
            if (npcs == null || npcIndex < 0 || npcIndex >= npcs.Count)
            {
                result.Message = "Tax collector index is outside Main.npc bounds.";
                CacheNpcIndex(-1);
                return false;
            }

            var npc = npcs[npcIndex];
            if (npc == null || !ReadBool(npc, "active", false) || ReadInt(npc, "type", 0) != TaxCollectorNpcType)
            {
                result.Message = "Target tax collector is no longer active.";
                CacheNpcIndex(-1);
                return false;
            }

            if (!IsNpcReachable(player, npc))
            {
                result.Message = "Target tax collector is no longer reachable.";
                return false;
            }

            result.WhoAmI = ReadInt(npc, "whoAmI", npcIndex);
            result.NpcName = ReadName(npc);
            result.TaxMoneyBefore = ReadInt(player, "taxMoney", 0);
            result.TaxMoneyAfter = result.TaxMoneyBefore;
            if (result.TaxMoneyBefore <= 0)
            {
                result.Message = "No tax money available.";
                return false;
            }

            object originalShoppingSettings = null;
            var changedSettings = false;
            TryOpenTaxCollectorChat(player, npc, npcIndex, result);
            if (!result.ChatOpened)
            {
                result.Message = "Tax collector chat could not be opened.";
                return false;
            }

            try
            {
                originalShoppingSettings = GetMember(player, "currentShoppingSettings");
                changedSettings = TryApplyShoppingSettings(player, npc);
                result.ShoppingSettingsApplied = changedSettings;

                var method = GetCollectTaxesMethod(mainType);
                if (method == null)
                {
                    result.Message = "Main.NPCChatText_DoTaxCollector was not found.";
                    return false;
                }

                try
                {
                    method.Invoke(null, new object[0]);
                    result.CollectInvoked = true;
                    result.TaxMoneyAfter = ReadInt(player, "taxMoney", 0);
                    result.Message = "Tax collector chat button handler invoked.";
                    return result.TaxMoneyAfter < result.TaxMoneyBefore;
                }
                catch (Exception error)
                {
                    result.TaxMoneyAfter = ReadInt(player, "taxMoney", 0);
                    result.Message = "Tax collect failed: " + Unwrap(error);
                    return false;
                }
            }
            finally
            {
                if (changedSettings)
                {
                    SetMember(player, "currentShoppingSettings", originalShoppingSettings);
                }

                if (result.ChatOpened)
                {
                    result.ChatClosed = TryCloseNpcChat(player);
                }
            }
        }

        private static bool TryReadCachedTarget(object player, IList npcs, int taxMoney, out TaxCollectorTarget target)
        {
            target = null;
            var index = GetCachedNpcIndex();
            if (index < 0 || npcs == null || index >= npcs.Count)
            {
                return false;
            }

            var npc = npcs[index];
            if (!IsReachableTaxCollector(player, npc))
            {
                CacheNpcIndex(-1);
                return false;
            }

            target = BuildTarget(npc, index, taxMoney);
            return true;
        }

        private static TaxCollectorTarget BuildTarget(object npc, int npcIndex, int taxMoney)
        {
            return new TaxCollectorTarget
            {
                NpcIndex = npcIndex,
                WhoAmI = ReadInt(npc, "whoAmI", npcIndex),
                Name = ReadName(npc),
                TaxMoney = taxMoney
            };
        }

        private static bool IsReachableTaxCollector(object player, object npc)
        {
            return npc != null &&
                   ReadBool(npc, "active", false) &&
                   ReadInt(npc, "type", 0) == TaxCollectorNpcType &&
                   IsNpcReachable(player, npc);
        }

        private static void TryOpenTaxCollectorChat(object player, object npc, int npcIndex, TaxCollectResult result)
        {
            var talkNpcSet = false;
            try
            {
                InvokeZero(player, "dropItemCheck");
                if (!TrySetTalkNpc(player, npcIndex))
                {
                    result.ChatOpened = false;
                    return;
                }

                talkNpcSet = true;
                var getChat = FindInstanceMethod(npc.GetType(), "GetChat");
                var chat = getChat == null ? string.Empty : (getChat.Invoke(npc, new object[0]) ?? string.Empty).ToString();
                if (!SetStatic(TerrariaRuntimeTypes.MainType, "npcChatText", chat))
                {
                    result.ChatOpened = false;
                    TryCloseNpcChat(player);
                    return;
                }

                result.ChatOpened = true;
            }
            catch
            {
                result.ChatOpened = false;
                if (talkNpcSet)
                {
                    TryCloseNpcChat(player);
                }
            }
        }

        private static bool TryCloseNpcChat(object player)
        {
            var ok = true;
            try
            {
                ok &= TrySetTalkNpc(player, -1);
                ok &= SetStatic(TerrariaRuntimeTypes.MainType, "npcChatText", string.Empty);
                SetStatic(TerrariaRuntimeTypes.MainType, "npcChatCornerItem", 0);
                SetStatic(TerrariaRuntimeTypes.MainType, "npcChatFocus1", false);
                SetStatic(TerrariaRuntimeTypes.MainType, "npcChatFocus2", false);
                SetStatic(TerrariaRuntimeTypes.MainType, "npcChatFocus3", false);
                SetStatic(TerrariaRuntimeTypes.MainType, "npcChatFocus4", false);
                SetStatic(TerrariaRuntimeTypes.MainType, "npcChatRelease", false);
            }
            catch
            {
                return false;
            }

            return ok;
        }

        private static bool TryApplyShoppingSettings(object player, object npc)
        {
            try
            {
                var shopHelper = GetStatic(TerrariaRuntimeTypes.MainType, "ShopHelper");
                if (shopHelper == null)
                {
                    return false;
                }

                var method = FindInstanceMethod(shopHelper.GetType(), "GetShoppingSettings", player.GetType(), npc.GetType());
                if (method == null)
                {
                    return false;
                }

                var settings = method.Invoke(shopHelper, new[] { player, npc });
                return SetMember(player, "currentShoppingSettings", settings);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsNpcReachable(object player, object npc)
        {
            int left;
            int top;
            int right;
            int bottom;
            if (!NurseServiceCompat.TryGetTileReachRegion(player, out left, out top, out right, out bottom))
            {
                var dx = ReadCenterX(player) - ReadCenterX(npc);
                var dy = ReadCenterY(player) - ReadCenterY(npc);
                return dx * dx + dy * dy <= 12f * 16f * 12f * 16f;
            }

            var npcLeft = (int)(ReadFloat(npc, "position", "X") / 16f);
            var npcTop = (int)(ReadFloat(npc, "position", "Y") / 16f);
            var npcRight = (int)((ReadFloat(npc, "position", "X") + ReadInt(npc, "width", 18)) / 16f);
            var npcBottom = (int)((ReadFloat(npc, "position", "Y") + ReadInt(npc, "height", 40)) / 16f);
            return npcRight >= left && npcLeft <= right && npcBottom >= top && npcTop <= bottom;
        }

        private static MethodInfo GetCollectTaxesMethod(Type mainType)
        {
            if (mainType == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                if (_collectTaxesMethod != null)
                {
                    return _collectTaxesMethod;
                }

                var methods = mainType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, "NPCChatText_DoTaxCollector", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (method.GetParameters().Length == 0)
                    {
                        _collectTaxesMethod = method;
                        break;
                    }
                }

                return _collectTaxesMethod;
            }
        }

        private static int GetCachedNpcIndex()
        {
            lock (SyncRoot)
            {
                return _cachedNpcIndex;
            }
        }

        private static void CacheNpcIndex(int npcIndex)
        {
            lock (SyncRoot)
            {
                _cachedNpcIndex = npcIndex;
            }
        }

        private static bool TrySetTalkNpc(object player, int npcIndex)
        {
            if (player == null)
            {
                return false;
            }

            var method = FindInstanceMethod(player.GetType(), "SetTalkNPC", typeof(int));
            if (method != null)
            {
                try
                {
                    method.Invoke(player, new object[] { npcIndex });
                    return true;
                }
                catch
                {
                }
            }

            return SetMember(player, "talkNPC", npcIndex);
        }

        private static string ReadName(object instance)
        {
            var value = GetMember(instance, "FullName") ?? GetMember(instance, "GivenName") ?? GetMember(instance, "TypeName");
            return value == null ? string.Empty : value.ToString();
        }

        private static float ReadCenterX(object instance)
        {
            return ReadFloat(instance, "position", "X") + ReadInt(instance, "width", 0) / 2f;
        }

        private static float ReadCenterY(object instance)
        {
            return ReadFloat(instance, "position", "Y") + ReadInt(instance, "height", 0) / 2f;
        }

        private static float ReadFloat(object instance, string vectorName, string componentName)
        {
            var vector = GetMember(instance, vectorName);
            var raw = GetMember(vector, componentName);
            try { return raw == null ? 0f : Convert.ToSingle(raw, CultureInfo.InvariantCulture); }
            catch { return 0f; }
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            var raw = GetMember(instance, name);
            try { return raw == null ? fallback : Convert.ToInt32(raw, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static bool ReadBool(object instance, string name, bool fallback)
        {
            var raw = GetMember(instance, name);
            try { return raw == null ? fallback : Convert.ToBoolean(raw, CultureInfo.InvariantCulture); }
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

        private static bool SetMember(object instance, string name, object value)
        {
            if (instance == null)
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
                {
                    field.SetValue(instance, value);
                    return true;
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, false, out var property) && property.CanWrite)
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

        private static bool SetStatic(Type type, string name, object value)
        {
            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
                {
                    field.SetValue(null, value);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static void InvokeZero(object instance, string name)
        {
            if (instance == null)
            {
                return;
            }

            var method = FindInstanceMethod(instance.GetType(), name);
            if (method != null)
            {
                method.Invoke(instance, new object[0]);
            }
        }

        private static MethodInfo FindInstanceMethod(Type type, string name, params Type[] parameterTypes)
        {
            if (type == null)
            {
                return null;
            }

            var key = BuildMethodKey(type, name, false, parameterTypes);
            lock (SyncRoot)
            {
                MethodInfo cached;
                if (MethodCache.TryGetValue(key, out cached))
                {
                    return cached;
                }
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameterTypes == null || parameterTypes.Length == 0)
                {
                    if (parameters.Length == 0)
                    {
                        CacheMethod(key, method);
                        return method;
                    }

                    continue;
                }

                if (parameters.Length != parameterTypes.Length)
                {
                    continue;
                }

                var ok = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (!parameters[i].ParameterType.IsAssignableFrom(parameterTypes[i]))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    CacheMethod(key, method);
                    return method;
                }
            }

            CacheMethod(key, null);
            return null;
        }

        private static void CacheMethod(string key, MethodInfo method)
        {
            lock (SyncRoot)
            {
                MethodCache[key] = method;
            }
        }

        private static string BuildMethodKey(Type type, string name, bool isStatic, Type[] parameterTypes)
        {
            var key = (type == null ? string.Empty : type.AssemblyQualifiedName) + "|" + name + "|" + (isStatic ? "true" : "false");
            if (parameterTypes == null || parameterTypes.Length == 0)
            {
                return key + "|0";
            }

            for (var index = 0; index < parameterTypes.Length; index++)
            {
                key += "|" + (parameterTypes[index] == null ? string.Empty : parameterTypes[index].AssemblyQualifiedName);
            }

            return key;
        }

        private static string Unwrap(Exception error)
        {
            return error == null ? string.Empty : (error.InnerException == null ? error.Message : error.InnerException.Message);
        }
    }
}
