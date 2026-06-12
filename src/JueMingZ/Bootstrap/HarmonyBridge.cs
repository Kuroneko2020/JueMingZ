using System;
using System.Reflection;
using HarmonyLib;

namespace JueMingZ.Bootstrap
{
    internal static class HarmonyBridge
    {
        // Keep the first bootstrap patch on Harmony's typed API. On the user-reported
        // failing machine, the old reflection path tripped MonoMod generic constraints
        // when 0Harmony came from the embedded byte load.
        public static string Patch(
            string harmonyId,
            MethodInfo original,
            MethodInfo prefix,
            MethodInfo postfix,
            MethodInfo transpiler)
        {
            if (original == null)
            {
                throw new ArgumentNullException(nameof(original));
            }

            var harmony = new Harmony(harmonyId);
            harmony.Patch(
                original,
                prefix == null ? null : new HarmonyMethod(prefix),
                postfix == null ? null : new HarmonyMethod(postfix),
                transpiler == null ? null : new HarmonyMethod(transpiler),
                null);

            return typeof(Harmony).Assembly.FullName;
        }
    }
}
