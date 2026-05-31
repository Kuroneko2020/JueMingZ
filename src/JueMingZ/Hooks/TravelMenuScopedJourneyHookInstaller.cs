using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class TravelMenuScopedJourneyHookInstaller
    {
        private const string HarmonyId = "JueMingZ.TravelMenuScopedJourney.001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Travel menu scoped journey hooks already installed.", "scoped journey hooks");
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Travel menu scoped journey hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    return Skip("Harmony not found; travel menu scoped journey hooks cannot install.");
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    return Skip("HarmonyMethod not found; travel menu scoped journey hooks cannot install.");
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    return Skip("Terraria runtime types unavailable; travel menu scoped journey hooks cannot install: " + TerrariaRuntimeTypes.LastError);
                }

                var targets = ResolveTargets();
                var presentTargets = targets.Where(target => target.Method != null).ToList();
                if (presentTargets.Count == 0)
                {
                    var missingOnly = "Travel menu scoped journey hook targets missing: " + string.Join(", ", targets.Select(target => target.Name).ToArray()) + ".";
                    return Skip(missingOnly);
                }

                var callbacks = typeof(TravelMenuScopedJourneyHookCallbacks);
                var prefixMethod = callbacks.GetMethod("ScopedJourneyPrefix", BindingFlags.Static | BindingFlags.NonPublic);
                var postfixMethod = callbacks.GetMethod("ScopedJourneyPostfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null || postfixMethod == null)
                {
                    throw new MissingMethodException("Travel menu scoped journey callback methods not found.");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                var patched = new HashSet<string>(StringComparer.Ordinal);
                foreach (var target in presentTargets)
                {
                    var signature = SafeBootstrapInstaller.FormatMethodSignature(target.Method);
                    if (!patched.Add(signature))
                    {
                        continue;
                    }

                    SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target.Method, prefix, postfix);
                }

                Interlocked.Exchange(ref _installed, 1);
                var missing = targets.Where(target => target.Method == null).Select(target => target.Name).ToList();
                var installedSignatures = presentTargets.Select(target => SafeBootstrapInstaller.FormatMethodSignature(target.Method)).Distinct().ToArray();
                var message = "Travel menu scoped journey hooks installed: " + installedSignatures.Length + "/" + targets.Count +
                              (missing.Count == 0 ? "." : "; missing: " + string.Join(", ", missing.ToArray()) + ".");
                TravelMenuService.RecordScopedPowerHook(true, message);
                Logger.Info("TravelMenuScopedJourneyHookInstaller", message);
                return HookInstallResult.Success(message, string.Join(" | ", installedSignatures));
            }
            catch (Exception error)
            {
                var message = "Travel menu scoped journey hook installation failed: " + error.Message;
                TravelMenuService.RecordScopedPowerHook(false, message);
                Logger.Error("TravelMenuScopedJourneyHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static HookInstallResult Skip(string message)
        {
            TravelMenuService.RecordScopedPowerHook(false, message);
            Logger.Warn("TravelMenuScopedJourneyHookInstaller", message);
            return HookInstallResult.Skipped(message);
        }

        private static List<TargetSpec> ResolveTargets()
        {
            var targets = new List<TargetSpec>();
            var mainType = TerrariaRuntimeTypes.MainType;
            var playerType = TerrariaRuntimeTypes.PlayerType;
            var assembly = mainType == null ? null : mainType.Assembly;
            var npcType = assembly == null ? null : assembly.GetType("Terraria.NPC", false);
            var itemType = assembly == null ? null : assembly.GetType("Terraria.Item", false);
            var creativePowersType = assembly == null ? null : assembly.GetType("Terraria.GameContent.Creative.CreativePowers", false);
            var difficultySliderType = assembly == null ? null : assembly.GetType("Terraria.GameContent.Creative.CreativePowers+DifficultySliderPower", false);
            var spawnerType = npcType == null ? null : npcType.GetNestedType("Spawner", BindingFlags.Public | BindingFlags.NonPublic);

            targets.Add(new TargetSpec("Player.ToggleCreativeMenu", FindMethod(playerType, "ToggleCreativeMenu", false, typeof(void), parameters => parameters.Length == 0)));
            targets.Add(new TargetSpec("Main.UpdateCreativeGameModeOverride", FindMethod(mainType, "UpdateCreativeGameModeOverride", true, typeof(void), parameters => parameters.Length == 0)));
            targets.Add(new TargetSpec("Player.Hurt", FindMethod(playerType, "Hurt", false, null, parameters =>
                parameters.Length >= 3 &&
                parameters[0].ParameterType != null &&
                string.Equals(parameters[0].ParameterType.FullName, "Terraria.DataStructures.PlayerDeathReason", StringComparison.Ordinal) &&
                parameters[1].ParameterType == typeof(int) &&
                parameters[2].ParameterType == typeof(int))));
            targets.Add(new TargetSpec("Player.ResetEffects", FindMethod(playerType, "ResetEffects", false, typeof(void), parameters => parameters.Length == 0)));
            targets.Add(new TargetSpec("Player.GrabItems", FindMethod(playerType, "GrabItems", false, typeof(void), parameters => parameters.Length == 1 && parameters[0].ParameterType == typeof(int))));
            targets.Add(new TargetSpec("Player.GetItemGrabRange(Item)", FindMethod(playerType, "GetItemGrabRange", false, typeof(int), parameters => parameters.Length == 1 && itemType != null && parameters[0].ParameterType == itemType)));
            targets.Add(new TargetSpec("NPC.ScaleStats", FindMethod(npcType, "ScaleStats", false, typeof(void), parameters => parameters.Length == 2 && parameters[0].ParameterType == typeof(int?) && parameters[1].ParameterType == typeof(float?))));
            targets.Add(new TargetSpec("NPC.SpawnNPC", FindMethod(npcType, "SpawnNPC", true, typeof(void), parameters => parameters.Length == 0)));
            targets.Add(new TargetSpec("NPC.Spawner.SlimeRainSpawns", FindMethod(spawnerType, "SlimeRainSpawns", true, typeof(void), parameters => parameters.Length == 1 && playerType != null && parameters[0].ParameterType == playerType)));
            targets.Add(new TargetSpec("CreativePowers.DifficultySliderPower.Load", FindMethod(difficultySliderType, "Load", false, typeof(void), parameters => parameters.Length == 2 && parameters[0].ParameterType.FullName == "System.IO.BinaryReader" && parameters[1].ParameterType == typeof(int))));

            if (creativePowersType == null)
            {
                targets.Add(new TargetSpec("CreativePowers", null));
            }

            return targets;
        }

        private static MethodInfo FindMethod(Type type, string name, bool isStatic, Type returnType, Func<ParameterInfo[], bool> parameterMatcher)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            return type
                .GetMethods(flags)
                .Where(method => string.Equals(method.Name, name, StringComparison.Ordinal))
                .Where(method => !method.ContainsGenericParameters && !method.IsAbstract)
                .Where(method => returnType == null || method.ReturnType == returnType)
                .Where(method => parameterMatcher == null || parameterMatcher(method.GetParameters()))
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault();
        }

        private sealed class TargetSpec
        {
            public TargetSpec(string name, MethodInfo method)
            {
                Name = name ?? string.Empty;
                Method = method;
            }

            public string Name { get; private set; }
            public MethodInfo Method { get; private set; }
        }
    }
}
