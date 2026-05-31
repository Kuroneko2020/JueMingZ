using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JueMingZ.Diagnostics;

namespace JueMingZ.Bootstrap
{
    public static class AssemblyLoadTracker
    {
        private static int _terrariaLoaded;
        private static int _reLogicLoaded;
        private static int _xnaLoaded;
        private static int _safeBootstrapStarted;

        public static bool TerrariaLoaded
        {
            get { return _terrariaLoaded == 1; }
        }

        public static bool ReLogicLoaded
        {
            get { return _reLogicLoaded == 1; }
        }

        public static bool XnaLoaded
        {
            get { return _xnaLoaded == 1; }
        }

        public static bool SafeBootstrapStarted
        {
            get { return _safeBootstrapStarted == 1; }
        }

        public static void ObserveExistingAssemblies()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                ObserveAssembly(assembly, false);
            }
        }

        public static void ObserveAssembly(Assembly assembly, bool fromLoadEvent)
        {
            if (assembly == null)
            {
                return;
            }

            string name;
            try
            {
                name = assembly.GetName().Name;
            }
            catch
            {
                return;
            }

            if (string.Equals(name, "Terraria", StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Exchange(ref _terrariaLoaded, 1) == 0)
                {
                    Logger.Info("AssemblyLoadTracker", "Observed Terraria assembly load.");
                }
            }
            else if (string.Equals(name, "ReLogic", StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Exchange(ref _reLogicLoaded, 1) == 0)
                {
                    Logger.Info("AssemblyLoadTracker", "Observed ReLogic assembly load.");
                }
            }
            else if (string.Equals(name, "Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Exchange(ref _xnaLoaded, 1) == 0)
                {
                    Logger.Info("AssemblyLoadTracker", "Observed Microsoft.Xna.Framework assembly load.");
                }
            }
        }

        public static void TryStartSafeBootstrapIfReady()
        {
            if (!TerrariaLoaded || !ReLogicLoaded)
            {
                return;
            }

            if (Interlocked.Exchange(ref _safeBootstrapStarted, 1) == 1)
            {
                return;
            }

            Logger.Info("AssemblyLoadTracker", "Terraria/ReLogic both loaded; starting safe bootstrap.");

            Task.Run(new Action(() =>
            {
                try
                {
                    SafeBootstrapInstaller.Install();
                }
                catch (Exception error)
                {
                    Logger.Error("AssemblyLoadTracker", "Safe bootstrap task failed; exception swallowed.", error);
                }
            }));
        }
    }
}
