using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;

namespace JueMingZ.Bootstrap
{
    public static class SafeBootstrapInstaller
    {
        private const string HarmonyId = "JueMingZ.SafeBootstrap.M1";
        private static int _installStarted;

        public static HookInstallResult Install()
        {
            if (Interlocked.Exchange(ref _installStarted, 1) == 1)
            {
                return HookInstallResult.Skipped("Safe bootstrap already started.");
            }

            try
            {
                JueMingZBootstrap.Start();

                var mainType = GameMode.FindTerrariaMainType();
                if (mainType == null)
                {
                    const string message = "Terraria.Main type not found; safe bootstrap hook skipped.";
                    HookDiagnostics.MarkSkipped(message);
                    Logger.Warn("SafeBootstrapInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var targetMethod = FindSafeBootstrapMethod(mainType);
                if (targetMethod == null)
                {
                    var message = "No safe Draw/Update(GameTime) candidate found. Candidates: " +
                                  FormatCandidateList(mainType, "Draw", "Update", "DoUpdate");
                    HookDiagnostics.MarkSkipped(message);
                    Logger.Warn("SafeBootstrapInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var signature = FormatMethodSignature(targetMethod);
                Logger.Info("SafeBootstrapInstaller", "safe bootstrap target resolved: " + signature);

                var postfixMethod = typeof(SafeBootstrapPostfix).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (postfixMethod == null)
                {
                    throw new MissingMethodException("SafeBootstrapPostfix.Postfix");
                }

                string typedHarmonyAssembly;
                Exception typedBridgeError;
                // The embedded Harmony path must use the typed bridge first; reflection
                // remains only as a compatibility fallback for older package shapes.
                if (TryPatchWithTypedBridge(targetMethod, postfixMethod, out typedHarmonyAssembly, out typedBridgeError))
                {
                    Logger.Info("SafeBootstrapInstaller", "Harmony typed bridge found / loaded: " + typedHarmonyAssembly);
                    var typedSuccessMessage = "safe bootstrap installed via typed bridge: " + signature;
                    HookDiagnostics.MarkSafeBootstrapInstalled(signature, typedSuccessMessage);
                    Logger.Info("SafeBootstrapInstaller", typedSuccessMessage);
                    return HookInstallResult.Success(typedSuccessMessage, signature);
                }

                Logger.Warn(
                    "SafeBootstrapInstaller",
                    "Typed Harmony bridge failed; falling back to reflection path: " +
                    (typedBridgeError == null ? "Unknown" : typedBridgeError.GetType().FullName + ": " + typedBridgeError.Message));

                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; safe bootstrap hook skipped.";
                    HookDiagnostics.MarkSkipped(message);
                    Logger.Warn("SafeBootstrapInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                Logger.Info("SafeBootstrapInstaller", "Harmony found / loaded: " + harmonyType.Assembly.FullName);

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; safe bootstrap hook skipped.";
                    HookDiagnostics.MarkSkipped(message);
                    Logger.Warn("SafeBootstrapInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmony = CreateHarmonyInstance(harmonyType, HarmonyId);
                var postfix = CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                PatchWithHarmony(harmonyType, harmony, targetMethod, postfix);

                var successMessage = "safe bootstrap installed via reflection fallback: " + signature;
                HookDiagnostics.MarkSafeBootstrapInstalled(signature, successMessage);
                Logger.Info("SafeBootstrapInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Safe bootstrap install failed; continuing with safe degradation.";
                HookDiagnostics.MarkFailed(message, error);
                Logger.Error("SafeBootstrapInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
        }

        private static bool TryPatchWithTypedBridge(
            MethodInfo targetMethod,
            MethodInfo postfixMethod,
            out string harmonyAssembly,
            out Exception error)
        {
            harmonyAssembly = string.Empty;
            error = null;

            try
            {
                harmonyAssembly = HarmonyBridge.Patch(HarmonyId, targetMethod, null, postfixMethod, null);
                return true;
            }
            catch (Exception patchError)
            {
                error = patchError;
                return false;
            }
        }

        private static MethodInfo FindSafeBootstrapMethod(Type mainType)
        {
            var methods = mainType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var name in new[] { "Draw", "Update", "DoUpdate" })
            {
                var candidate = methods
                    .Where(method => string.Equals(method.Name, name, StringComparison.Ordinal))
                    .Where(IsGameTimeMethod)
                    .OrderBy(method => method.GetParameters().Length)
                    .FirstOrDefault();

                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsGameTimeMethod(MethodInfo method)
        {
            if (method == null || method.ContainsGenericParameters || method.IsAbstract || method.IsStatic)
            {
                return false;
            }

            if (method.ReturnType != typeof(void))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 1 &&
                   string.Equals(parameters[0].ParameterType.Name, "GameTime", StringComparison.Ordinal);
        }

        internal static string FormatCandidateList(Type type, params string[] names)
        {
            try
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Where(method => names.Any(name => string.Equals(name, method.Name, StringComparison.Ordinal)))
                    .Select(FormatMethodSignature)
                    .ToList();

                return methods.Count == 0 ? "<none>" : string.Join(" | ", methods.ToArray());
            }
            catch (Exception error)
            {
                return "<failed to list candidates: " + error.Message + ">";
            }
        }

        internal static string FormatMethodSignature(MethodInfo method)
        {
            if (method == null)
            {
                return "<null>";
            }

            var parameters = method.GetParameters()
                .Select(parameter => parameter.ParameterType.FullName + " " + parameter.Name)
                .ToArray();
            return method.DeclaringType.FullName + "." + method.Name + "(" + string.Join(", ", parameters) + ")";
        }

        internal static object CreateHarmonyInstance(Type harmonyType, string harmonyId)
        {
            var constructor = harmonyType.GetConstructor(new[] { typeof(string) });
            if (constructor != null)
            {
                return constructor.Invoke(new object[] { harmonyId });
            }

            return Activator.CreateInstance(harmonyType);
        }

        internal static object CreateHarmonyMethod(Type harmonyMethodType, MethodInfo method)
        {
            var constructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
            if (constructor != null)
            {
                return constructor.Invoke(new object[] { method });
            }

            var instance = Activator.CreateInstance(harmonyMethodType);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var field = harmonyMethodType.GetField("method", flags);
            if (field != null && field.FieldType.IsAssignableFrom(typeof(MethodInfo)))
            {
                field.SetValue(instance, method);
                return instance;
            }

            var property = harmonyMethodType.GetProperty("method", flags);
            if (property != null && property.CanWrite && property.PropertyType.IsAssignableFrom(typeof(MethodInfo)))
            {
                property.SetValue(instance, method, null);
                return instance;
            }

            throw new MissingMethodException("Unable to create HarmonyMethod.");
        }

        internal static void PatchWithHarmony(Type harmonyType, object harmony, MethodInfo original, object postfix)
        {
            PatchWithHarmony(harmonyType, harmony, original, null, postfix);
        }

        internal static void PatchWithHarmony(Type harmonyType, object harmony, MethodInfo original, object prefix, object postfix)
        {
            PatchWithHarmony(harmonyType, harmony, original, prefix, postfix, null);
        }

        internal static void PatchWithHarmony(Type harmonyType, object harmony, MethodInfo original, object prefix, object postfix, object transpiler)
        {
            var patchMethod = harmonyType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => string.Equals(method.Name, "Patch", StringComparison.Ordinal))
                .Where(method =>
                {
                    var parameters = method.GetParameters();
                    return parameters.Length > 0 && typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType);
                })
                .OrderByDescending(method => method.GetParameters().Length)
                .FirstOrDefault();

            if (patchMethod == null)
            {
                throw new MissingMethodException("Harmony.Patch");
            }

            var patchParameters = patchMethod.GetParameters();
            var args = new object[patchParameters.Length];
            args[0] = original;

            for (var index = 1; index < patchParameters.Length; index++)
            {
                var parameterName = patchParameters[index].Name ?? string.Empty;
                if (parameterName.Equals("prefix", StringComparison.OrdinalIgnoreCase))
                {
                    args[index] = prefix;
                }
                else if (parameterName.Equals("postfix", StringComparison.OrdinalIgnoreCase))
                {
                    args[index] = postfix;
                }
                else if (parameterName.Equals("transpiler", StringComparison.OrdinalIgnoreCase))
                {
                    args[index] = transpiler;
                }
                else
                {
                    args[index] = null;
                }
            }

            patchMethod.Invoke(harmony, args);
        }

        private static class SafeBootstrapPostfix
        {
            private const int MaxHandoffAttempts = 12;
            private static int _attempts;
            private static int _completed;

            private static void Postfix()
            {
                if (_completed == 1)
                {
                    return;
                }

                var attempt = Interlocked.Increment(ref _attempts);
                if (attempt > MaxHandoffAttempts)
                {
                    if (attempt == MaxHandoffAttempts + 1)
                    {
                        Logger.Warn("SafeBootstrapInstaller", "safe bootstrap handoff attempts exhausted.");
                    }

                    return;
                }

                Logger.Info("SafeBootstrapInstaller", "safe bootstrap postfix entered (attempt " + attempt + "/" + MaxHandoffAttempts + ")");

                try
                {
                    if (LateBootstrap.TryLoadAfterMainAlive())
                    {
                        Interlocked.Exchange(ref _completed, 1);
                    }
                }
                catch (Exception error)
                {
                    Logger.Error("SafeBootstrapInstaller", "safe bootstrap postfix failed; will retry if budget remains.", error);
                }
            }
        }
    }
}
