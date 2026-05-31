using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Diagnostics;

namespace JueMingZ.Bootstrap
{
    public static class DependencyResolver
    {
        private const string EmbeddedHarmonyResourceName = "JueMingZ.Embedded.0Harmony.dll";

        private static readonly object AssemblyLoadLock = new object();
        private static int _registered;

        public static void Register()
        {
            if (Interlocked.Exchange(ref _registered, 1) == 1)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            Logger.Info("DependencyResolver", "AssemblyResolve registered.");
        }

        public static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var requestedName = new AssemblyName(args.Name).Name;
                if (IsRequiredDependency(requestedName))
                {
                    LogThrottle.InfoThrottled(
                        "dependency-resolve-request-" + requestedName,
                        TimeSpan.FromSeconds(30),
                        "DependencyResolver",
                        "Assembly resolve requested: " + args.Name);
                }
                else
                {
                    LogDebugThrottled(
                        "dependency-resolve-request-" + requestedName,
                        "Assembly resolve requested: " + args.Name);
                }

                Assembly assembly;
                if (TryLoadAssemblyBySimpleName(requestedName, out assembly))
                {
                    return assembly;
                }

                if (IsRequiredDependency(requestedName))
                {
                    LogThrottle.WarnThrottled(
                        "dependency-resolve-miss-" + requestedName,
                        TimeSpan.FromSeconds(30),
                        "DependencyResolver",
                        "Assembly resolve failed: " + requestedName);
                }
                else
                {
                    LogDebugThrottled(
                        "dependency-resolve-miss-" + requestedName,
                        "Optional Terraria dependency resolve missed: " + requestedName);
                }
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "dependency-resolve-error",
                    TimeSpan.FromSeconds(30),
                    "DependencyResolver",
                    "Assembly resolve failed with exception.", error);
            }

            return null;
        }

        public static bool TryLoadAssemblyBySimpleName(string simpleName, out Assembly assembly)
        {
            assembly = null;
            if (string.IsNullOrWhiteSpace(simpleName))
            {
                return false;
            }

            foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (string.Equals(loaded.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                    {
                        assembly = loaded;
                        return true;
                    }
                }
                catch
                {
                }
            }

            if (TryLoadEmbeddedAssemblyBySimpleName(simpleName, out assembly))
            {
                return true;
            }

            var fileName = simpleName + ".dll";
            foreach (var directory in GetProbeDirectories())
            {
                try
                {
                    var candidate = Path.Combine(directory, fileName);
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    assembly = Assembly.LoadFrom(candidate);
                    Logger.Info("DependencyResolver", "Assembly resolved: " + simpleName + " -> " + candidate);
                    return true;
                }
                catch (Exception error)
                {
                    if (IsRequiredDependency(simpleName))
                    {
                        LogThrottle.WarnThrottled(
                            "dependency-load-failed-" + simpleName,
                            TimeSpan.FromSeconds(30),
                            "DependencyResolver",
                            "Assembly load failed for " + simpleName + ": " + error.Message);
                    }
                    else
                    {
                        LogDebugThrottled(
                            "dependency-load-failed-" + simpleName,
                            "Optional Terraria dependency load failed for " + simpleName + ": " + error.Message);
                    }
                }
            }

            return false;
        }

        private static bool TryLoadEmbeddedAssemblyBySimpleName(string simpleName, out Assembly assembly)
        {
            assembly = null;
            if (!string.Equals(simpleName, "0Harmony", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            lock (AssemblyLoadLock)
            {
                foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (string.Equals(loaded.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                        {
                            assembly = loaded;
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }

                try
                {
                    var owner = typeof(DependencyResolver).Assembly;
                    using (var stream = owner.GetManifestResourceStream(EmbeddedHarmonyResourceName))
                    {
                        if (stream == null)
                        {
                            LogThrottle.WarnThrottled(
                                "dependency-embedded-missing-" + simpleName,
                                TimeSpan.FromSeconds(30),
                                "DependencyResolver",
                                "Embedded Harmony resource was not found: " + EmbeddedHarmonyResourceName);
                            return false;
                        }

                        using (var memory = new MemoryStream())
                        {
                            stream.CopyTo(memory);
                            assembly = Assembly.Load(memory.ToArray());
                        }
                    }

                    Logger.Info("DependencyResolver", "Assembly resolved from embedded resource: " + simpleName);
                    return true;
                }
                catch (Exception error)
                {
                    LogThrottle.WarnThrottled(
                        "dependency-embedded-load-failed-" + simpleName,
                        TimeSpan.FromSeconds(30),
                        "DependencyResolver",
                        "Embedded assembly load failed for " + simpleName + ": " + error.Message);
                    assembly = null;
                    return false;
                }
            }
        }

        public static IReadOnlyList<string> GetProbeDirectories()
        {
            var directories = new List<string>();
            AddDirectory(directories, AppDomain.CurrentDomain.BaseDirectory);

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                AddDirectory(directories, Path.Combine(baseDirectory, "JueMingZDev"));
            }

            try
            {
                var executingLocation = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrWhiteSpace(executingLocation))
                {
                    AddDirectory(directories, Path.GetDirectoryName(executingLocation));
                }
            }
            catch
            {
            }

            return directories
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddDirectory(ICollection<string> directories, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(directory);
                if (Directory.Exists(fullPath))
                {
                    directories.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        private static bool IsRequiredDependency(string simpleName)
        {
            return string.Equals(simpleName, "JueMingZ", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(simpleName, "0Harmony", StringComparison.OrdinalIgnoreCase);
        }

        private static void LogDebugThrottled(string key, string message)
        {
            if (LogThrottle.ShouldLog(key, TimeSpan.FromSeconds(30)))
            {
                Logger.Debug("DependencyResolver", message);
            }
        }
    }
}
