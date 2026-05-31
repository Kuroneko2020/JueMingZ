using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JueMingZ.Diagnostics;

namespace JueMingZ.Bootstrap
{
    public static class DependencyChecker
    {
        public static DependencyCheckResult Check()
        {
            var result = new DependencyCheckResult();

            try
            {
                result.ProcessName = GetProcessName();
                result.BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                result.LoadedAssemblyNames = GetLoadedAssemblyNames();
                result.TerrariaAssemblyObserved = ContainsAssembly(result.LoadedAssemblyNames, "Terraria");
                result.ReLogicAssemblyObserved = ContainsAssembly(result.LoadedAssemblyNames, "ReLogic");
                result.XnaAssemblyObserved = ContainsAssembly(result.LoadedAssemblyNames, "Microsoft.Xna.Framework");
                result.HarmonyAssemblyObserved = ContainsAssembly(result.LoadedAssemblyNames, "0Harmony") ||
                                                 result.LoadedAssemblyNames.Any(name => name.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0);
                result.JueMingZAssemblyObserved = ContainsAssembly(result.LoadedAssemblyNames, "JueMingZ");

                Logger.Info("DependencyChecker", "Observed Terraria assembly: " + result.TerrariaAssemblyObserved);
                Logger.Info("DependencyChecker", "Observed ReLogic assembly: " + result.ReLogicAssemblyObserved);
                Logger.Info("DependencyChecker", "Observed Harmony assembly: " + result.HarmonyAssemblyObserved);
            }
            catch (Exception error)
            {
                result.LastError = error.ToString();
                Logger.Warn("DependencyChecker", "Early dependency check failed; continuing with safe degradation.");
                Logger.Debug("DependencyChecker", error.ToString());
            }

            return result;
        }

        public static Type FindType(string fullName, string assemblyName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(assemblyName) &&
                        !string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var type = assembly.GetType(fullName, false);
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

        public static IReadOnlyList<string> GetLoadedAssemblyNames()
        {
            var names = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    names.Add(assembly.GetName().Name);
                }
                catch
                {
                }
            }

            return names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool ContainsAssembly(IEnumerable<string> names, string expectedName)
        {
            return names.Any(name => string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetProcessName()
        {
            try
            {
                return Process.GetCurrentProcess().ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    public sealed class DependencyCheckResult
    {
        public bool TerrariaAssemblyObserved { get; set; }
        public bool ReLogicAssemblyObserved { get; set; }
        public bool XnaAssemblyObserved { get; set; }
        public bool HarmonyAssemblyObserved { get; set; }
        public bool JueMingZAssemblyObserved { get; set; }
        public bool NetModeReadable { get; set; }
        public string TerrariaVersion { get; set; } = "EarlyUnavailable";
        public string GameModeDescription { get; set; } = "EarlyBootstrap";
        public string ProcessName { get; set; } = "Unknown";
        public string BaseDirectory { get; set; } = string.Empty;
        public IReadOnlyList<string> LoadedAssemblyNames { get; set; } = new List<string>();
        public string LastError { get; set; } = string.Empty;
    }
}
