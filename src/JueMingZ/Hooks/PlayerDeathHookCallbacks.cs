using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.Records;

namespace JueMingZ.Hooks
{
    internal static class PlayerDeathHookCallbacks
    {
        private struct PlayerDeathHookState
        {
            public bool IsLocalPlayer;
            public bool WasDead;
        }

        private static void Prefix(object __instance, ref PlayerDeathHookState __state)
        {
            __state = new PlayerDeathHookState();
            try
            {
                __state.IsLocalPlayer = TerrariaInputCompat.TryIsLocalPlayer(__instance);
                __state.WasDead = ReadBool(__instance, "dead");
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("PlayerDeathHookCallbacks.Prefix", error);
                LogThrottle.WarnThrottled(
                    "player-death-hook-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "PlayerDeathHookCallbacks",
                    "Player death hook prefix failed: " + error.Message);
            }
        }

        private static void Postfix(object __instance, object[] __args, ref PlayerDeathHookState __state)
        {
            try
            {
                if (!__state.IsLocalPlayer || __state.WasDead || !ReadBool(__instance, "dead"))
                {
                    return;
                }

                var damageSource = __args != null && __args.Length > 0 ? __args[0] : null;
                var damage = __args != null && __args.Length > 1 ? Convert.ToDouble(__args[1]) : 0d;
                var hitDirection = __args != null && __args.Length > 2 ? Convert.ToInt32(__args[2]) : 0;
                var pvp = __args != null && __args.Length > 3 && Convert.ToBoolean(__args[3]);

                PlayerWorldDeathRecordResult result;
                PlayerWorldDeathRecorder.TryRecordCurrentDeathFromHook(__instance, damageSource, damage, hitDirection, pvp, out result);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("PlayerDeathHookCallbacks.Postfix", error);
                LogThrottle.ErrorThrottled(
                    "player-death-hook-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "PlayerDeathHookCallbacks",
                    "Player death hook postfix failed; exception swallowed.", error);
            }
        }

        internal static void PostfixForTesting(object player, object[] args, bool isLocalPlayer, bool wasDead)
        {
            var state = new PlayerDeathHookState
            {
                IsLocalPlayer = isLocalPlayer,
                WasDead = wasDead
            };
            Postfix(player, args, ref state);
        }

        private static bool ReadBool(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                System.Reflection.FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    return Convert.ToBoolean(field.GetValue(instance));
                }

                System.Reflection.PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanRead)
                {
                    return Convert.ToBoolean(property.GetValue(instance, null));
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
