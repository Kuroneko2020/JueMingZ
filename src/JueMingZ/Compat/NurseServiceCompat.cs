using System;
using System.Collections;
using System.Reflection;

namespace JueMingZ.Compat
{
    public sealed class NurseTarget
    {
        public int NpcIndex { get; set; }
        public string Name { get; set; }

        public NurseTarget()
        {
            NpcIndex = -1;
            Name = string.Empty;
        }
    }

    public sealed class NurseHealResult
    {
        public int HealCost { get; set; }
        public int LifeBefore { get; set; }
        public int LifeAfter { get; set; }
        public int RemovableDebuffsBefore { get; set; }
        public int RemovableDebuffsAfter { get; set; }
        public bool ChatOpened { get; set; }
        public bool ChatClosed { get; set; }
        public bool HealInvoked { get; set; }
        public string Message { get; set; }

        public NurseHealResult()
        {
            Message = string.Empty;
        }
    }

    public static class NurseServiceCompat
    {
        private const int NurseNpcType = 18;

        public static bool NeedsNurse(object player)
        {
            if (player == null)
            {
                return false;
            }

            return ReadInt(player, "statLife", 0) < ReadInt(player, "statLifeMax2", 0) ||
                   CountRemovableDebuffs(player) > 0;
        }

        public static bool TryFindReachableNurse(object player, out NurseTarget target, out string message)
        {
            target = null;
            message = string.Empty;
            if (player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            var npcs = GetStatic(TerrariaRuntimeTypes.MainType, "npc") as IList;
            if (npcs == null)
            {
                message = "Main.npc is unavailable.";
                return false;
            }

            var playerCenterX = ReadCenterX(player);
            var playerCenterY = ReadCenterY(player);
            var bestDistance = float.MaxValue;
            for (var index = 0; index < npcs.Count; index++)
            {
                var npc = npcs[index];
                if (npc == null || !ReadBool(npc, "active", false) || ReadInt(npc, "type", 0) != NurseNpcType)
                {
                    continue;
                }

                if (!IsNpcReachable(player, npc))
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
                target = new NurseTarget
                {
                    NpcIndex = index,
                    Name = ReadName(npc)
                };
            }

            if (target == null)
            {
                message = "No reachable nurse found.";
                return false;
            }

            return true;
        }

        public static bool TryOpenAndHeal(int npcIndex, out NurseHealResult result)
        {
            result = new NurseHealResult();
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                result.Message = "Local player unavailable.";
                return false;
            }

            var npcs = GetStatic(TerrariaRuntimeTypes.MainType, "npc") as IList;
            if (npcs == null || npcIndex < 0 || npcIndex >= npcs.Count)
            {
                result.Message = "Nurse index is outside Main.npc bounds.";
                return false;
            }

            var nurse = npcs[npcIndex];
            if (nurse == null || !ReadBool(nurse, "active", false) || ReadInt(nurse, "type", 0) != NurseNpcType)
            {
                result.Message = "Target nurse is no longer active.";
                return false;
            }

            result.LifeBefore = ReadInt(player, "statLife", 0);
            result.RemovableDebuffsBefore = CountRemovableDebuffs(player);
            TryOpenNurseChat(player, nurse, npcIndex, result);
            try
            {
                result.HealCost = TryGetNurseHealCost(player, nurse);
                if (result.HealCost <= 0)
                {
                    result.LifeAfter = ReadInt(player, "statLife", 0);
                    result.RemovableDebuffsAfter = CountRemovableDebuffs(player);
                    result.Message = "Nurse heal cost is zero; no healing or removable debuff was needed.";
                    return false;
                }

                var method = FindStaticMethod(TerrariaRuntimeTypes.MainType, "NPCChatText_DoNurseHeal", typeof(int));
                if (method == null)
                {
                    result.Message = "Main.NPCChatText_DoNurseHeal was not found.";
                    return false;
                }

                try
                {
                    method.Invoke(null, new object[] { result.HealCost });
                    result.HealInvoked = true;
                    result.LifeAfter = ReadInt(player, "statLife", 0);
                    result.RemovableDebuffsAfter = CountRemovableDebuffs(player);
                    result.Message = "Nurse heal button handler invoked.";
                    return result.LifeAfter > result.LifeBefore || result.RemovableDebuffsAfter < result.RemovableDebuffsBefore;
                }
                catch (Exception error)
                {
                    result.LifeAfter = ReadInt(player, "statLife", 0);
                    result.RemovableDebuffsAfter = CountRemovableDebuffs(player);
                    result.Message = "Nurse heal failed: " + Unwrap(error);
                    return false;
                }
            }
            finally
            {
                if (result.ChatOpened)
                {
                    result.ChatClosed = TryCloseNpcChat(player);
                }
            }
        }

        public static int CountRemovableDebuffs(object player)
        {
            if (player == null)
            {
                return 0;
            }

            var buffTypes = GetMember(player, "buffType") as IList;
            var buffTimes = GetMember(player, "buffTime") as IList;
            var debuff = GetStatic(TerrariaRuntimeTypes.MainType, "debuff") as IList;
            if (buffTypes == null || buffTimes == null || debuff == null)
            {
                return 0;
            }

            var count = 0;
            var max = Math.Min(buffTypes.Count, buffTimes.Count);
            for (var index = 0; index < max; index++)
            {
                var buffType = Convert.ToInt32(buffTypes[index]);
                var buffTime = Convert.ToInt32(buffTimes[index]);
                if (buffType <= 0 || buffTime <= 60 || buffType >= debuff.Count || !Convert.ToBoolean(debuff[buffType]))
                {
                    continue;
                }

                if (NurseCannotRemoveDebuff(buffType))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static void TryOpenNurseChat(object player, object nurse, int npcIndex, NurseHealResult result)
        {
            try
            {
                InvokeZero(player, "dropItemCheck");
                var setTalkNpc = FindInstanceMethod(player.GetType(), "SetTalkNPC", typeof(int));
                if (setTalkNpc != null)
                {
                    setTalkNpc.Invoke(player, new object[] { npcIndex });
                }
                else
                {
                    SetMember(player, "talkNPC", npcIndex);
                }

                var getChat = FindInstanceMethod(nurse.GetType(), "GetChat");
                var chat = getChat == null ? string.Empty : (getChat.Invoke(nurse, new object[0]) ?? string.Empty).ToString();
                SetStatic(TerrariaRuntimeTypes.MainType, "npcChatText", chat);
                result.ChatOpened = true;
            }
            catch
            {
                result.ChatOpened = false;
            }
        }

        private static bool TryCloseNpcChat(object player)
        {
            var ok = true;
            try
            {
                var setTalkNpc = player == null ? null : FindInstanceMethod(player.GetType(), "SetTalkNPC", typeof(int));
                if (setTalkNpc != null)
                {
                    setTalkNpc.Invoke(player, new object[] { -1 });
                }
                else if (player != null)
                {
                    ok &= SetMember(player, "talkNPC", -1);
                }

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

        private static int TryGetNurseHealCost(object player, object nurse)
        {
            var originalShoppingSettings = GetMember(player, "currentShoppingSettings");
            var changedSettings = TryApplyShoppingSettings(player, nurse);
            try
            {
                var method = FindStaticMethod(TerrariaRuntimeTypes.MainType, "GetNurseHealCost");
                if (method == null)
                {
                    return 0;
                }

                return Convert.ToInt32(method.Invoke(null, new object[0]));
            }
            catch
            {
                return 0;
            }
            finally
            {
                if (changedSettings)
                {
                    SetMember(player, "currentShoppingSettings", originalShoppingSettings);
                }
            }
        }

        private static bool TryApplyShoppingSettings(object player, object nurse)
        {
            try
            {
                var shopHelper = GetStatic(TerrariaRuntimeTypes.MainType, "ShopHelper");
                if (shopHelper == null)
                {
                    return false;
                }

                var method = FindInstanceMethod(shopHelper.GetType(), "GetShoppingSettings", player.GetType(), nurse.GetType());
                if (method == null)
                {
                    return false;
                }

                var settings = method.Invoke(shopHelper, new[] { player, nurse });
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
            if (!TryGetTileReachRegion(player, out left, out top, out right, out bottom))
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

        internal static bool TryGetTileReachRegion(object player, out int left, out int top, out int right, out int bottom)
        {
            left = top = right = bottom = 0;
            try
            {
                var type = FindType("Terraria.DataStructures.TileReachCheckSettings");
                if (type == null)
                {
                    return false;
                }

                var simple = GetStatic(type, "Simple");
                if (simple == null)
                {
                    return false;
                }

                var byRefMethod = FindTileRegionByRefMethod(type, player.GetType());
                if (byRefMethod != null)
                {
                    var args = new object[] { player, 0, 0, 0, 0, 0 };
                    byRefMethod.Invoke(simple, args);
                    left = Convert.ToInt32(args[1]);
                    top = Convert.ToInt32(args[2]);
                    right = Convert.ToInt32(args[3]);
                    bottom = Convert.ToInt32(args[4]);
                    return true;
                }

                var rectMethod = FindTileRegionRectangleMethod(type, player.GetType());
                if (rectMethod != null)
                {
                    var region = rectMethod.Invoke(simple, new object[] { player, 0 });
                    return TryReadRectangleRegion(region, out left, out top, out right, out bottom);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool NurseCannotRemoveDebuff(int buffType)
        {
            try
            {
                var setsType = FindType("Terraria.ID.BuffID+Sets");
                var raw = GetStatic(setsType, "NurseCannotRemoveDebuff") as IList;
                return raw != null && buffType >= 0 && buffType < raw.Count && Convert.ToBoolean(raw[buffType]);
            }
            catch
            {
                return false;
            }
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
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindTileRegionByRefMethod(Type type, Type playerType)
        {
            if (type == null || playerType == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "GetTileRegion", StringComparison.Ordinal) ||
                    method.ReturnType != typeof(void))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 6 &&
                    parameters[0].ParameterType.IsAssignableFrom(playerType) &&
                    parameters[1].ParameterType.IsByRef &&
                    parameters[2].ParameterType.IsByRef &&
                    parameters[3].ParameterType.IsByRef &&
                    parameters[4].ParameterType.IsByRef &&
                    parameters[5].ParameterType == typeof(int))
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindTileRegionRectangleMethod(Type type, Type playerType)
        {
            if (type == null || playerType == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "GetTileRegion", StringComparison.Ordinal) ||
                    method.ReturnType == typeof(void))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType.IsAssignableFrom(playerType) &&
                    parameters[1].ParameterType == typeof(int))
                {
                    return method;
                }
            }

            return null;
        }

        private static bool TryReadRectangleRegion(object region, out int left, out int top, out int right, out int bottom)
        {
            left = top = right = bottom = 0;
            if (region == null)
            {
                return false;
            }

            left = ReadInt(region, "X", 0);
            top = ReadInt(region, "Y", 0);
            var width = ReadInt(region, "Width", 0);
            var height = ReadInt(region, "Height", 0);
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            right = left + width - 1;
            bottom = top + height - 1;
            return true;
        }

        private static MethodInfo FindStaticMethod(Type type, string name, params Type[] parameterTypes)
        {
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
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
                    return parameters.Length == 0 ? method : null;
                }

                if (parameters.Length != parameterTypes.Length)
                {
                    continue;
                }

                var ok = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != parameterTypes[i])
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    return method;
                }
            }

            return null;
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

        private static string Unwrap(Exception error)
        {
            return error == null ? string.Empty : (error.InnerException == null ? error.Message : error.InnerException.Message);
        }
    }
}
