using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class TileInteractionHookInstaller
    {
        // Tile interaction installation is bound to the known vanilla hook target.
        // Missing reflection must skip rather than writing tile or buff state directly.
        private const string HarmonyId = "JueMingZ.TileInteractions.0036";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Tile interaction hooks already installed.", string.Empty);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Tile interaction hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; tile interaction hooks cannot install.";
                    Logger.Warn("TileInteractionHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; tile interaction hooks cannot install.";
                    Logger.Warn("TileInteractionHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    var message = "Terraria runtime types unavailable; tile interaction hooks cannot install: " + TerrariaRuntimeTypes.LastError;
                    Logger.Warn("TileInteractionHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerType = TerrariaRuntimeTypes.PlayerType;
                if (playerType == null)
                {
                    const string message = "Terraria.Player type not found; tile interaction hooks cannot install.";
                    Logger.Warn("TileInteractionHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var targets = FindTileInteractionMethods(playerType);
                if (targets.Count == 0)
                {
                    var message = "No Terraria.Player tile interaction hook candidates found. Candidates: " + FormatTileInteractionCandidates(playerType);
                    Logger.Warn("TileInteractionHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(TileInteractionHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                var postfixMethod = typeof(TileInteractionHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null || postfixMethod == null)
                {
                    throw new MissingMethodException("TileInteractionHookCallbacks Prefix/Postfix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                for (var index = 0; index < targets.Count; index++)
                {
                    SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, targets[index], prefix, postfix);
                }

                Interlocked.Exchange(ref _installed, 1);
                var signature = string.Join("; ", targets.Select(SafeBootstrapInstaller.FormatMethodSignature).ToArray());
                var successMessage = "Tile interaction hooks installed: " + signature;
                Logger.Info("TileInteractionHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Tile interaction hook installation failed.";
                Logger.Error("TileInteractionHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static List<MethodInfo> FindTileInteractionMethods(Type playerType)
        {
            var names = new[] { "LookForTileInteractions", "TileInteractionsCheck", "TileInteractionsUse" };
            return playerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => names.Contains(method.Name))
                .Where(IsTileInteractionCandidate)
                .OrderBy(method => method.Name)
                .ToList();
        }

        private static bool IsTileInteractionCandidate(MethodInfo method)
        {
            if (method == null || method.IsStatic || method.IsAbstract || method.IsSpecialName || method.ContainsGenericParameters)
            {
                return false;
            }

            if (method.ReturnType != typeof(void))
            {
                return false;
            }

            var parameters = method.GetParameters();
            if (string.Equals(method.Name, "LookForTileInteractions", StringComparison.Ordinal))
            {
                return parameters.Length == 0;
            }

            return parameters.Length == 2 &&
                   parameters[0].ParameterType == typeof(int) &&
                   parameters[1].ParameterType == typeof(int);
        }

        private static string FormatTileInteractionCandidates(Type playerType)
        {
            try
            {
                var candidates = playerType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name.IndexOf("TileInteractions", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(SafeBootstrapInstaller.FormatMethodSignature)
                    .ToArray();
                return candidates.Length == 0 ? "<none>" : string.Join(" | ", candidates);
            }
            catch (Exception error)
            {
                return "<failed to list candidates: " + error.Message + ">";
            }
        }
    }
}
