using System;
using System.Diagnostics;
using JueMingZ.Actions;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.Runtime;

namespace JueMingZ.Bootstrap
{
    public static class JueMingZBootstrap
    {
        private static readonly object SyncRoot = new object();
        private static bool _started;

        public static FeatureRegistry FeatureRegistry { get; private set; }
        public static FeatureManager FeatureManager { get; private set; }
        public static InputActionQueue ActionQueue { get; private set; }

        public static void Start()
        {
            lock (SyncRoot)
            {
                if (_started)
                {
                    Logger.Debug("Bootstrap", "Bootstrap request ignored: JueMingZ already started.");
                    return;
                }

                _started = true;
            }

            try
            {
                Logger.Initialize();
                Logger.Info("Bootstrap", "Bootstrap Start");
                Logger.Info("Bootstrap", "决明-Z 启动");
                Logger.Info("Bootstrap", "JueMingZ version: " + JueMingZRuntime.Version);
                Logger.Info("Bootstrap", "Start time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                Logger.Info("Bootstrap", "Process: " + GetProcessName());
                Logger.Info("Bootstrap", "BaseDirectory: " + AppDomain.CurrentDomain.BaseDirectory);
                Logger.Info("Bootstrap", "LogDirectory: " + Logger.LogDirectory);

                var dependency = DependencyChecker.Check();
                Logger.Info("Bootstrap", "Observed Terraria assembly: " + dependency.TerrariaAssemblyObserved);
                Logger.Info("Bootstrap", "Observed ReLogic assembly: " + dependency.ReLogicAssemblyObserved);
                Logger.Info("Bootstrap", "Observed XNA assembly: " + dependency.XnaAssemblyObserved);
                Logger.Info("Bootstrap", "Terraria version: EarlyUnavailable");
                Logger.Info("Bootstrap", "netMode readable: false (EarlyUnavailable)");
                Logger.Info("Bootstrap", "Runtime mode: EarlyBootstrap; Terraria.Main static state has not been read.");

                ConfigService.Initialize();

                FeatureRegistry = FeatureRegistry.CreateDefault();
                FeatureManager = new FeatureManager(FeatureRegistry, ConfigService.FeatureSettings);
                ActionQueue = new InputActionQueue();
                Logger.Info("Bootstrap", "Feature registry count: " + FeatureRegistry.Count);

                JueMingZRuntime.Initialize(FeatureRegistry, FeatureManager, ActionQueue);
                Logger.Info("Bootstrap", "JueMingZ M0 Bootstrap completed.");
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("Bootstrap.Start", error);
                Logger.Fatal("Bootstrap", "JueMingZ Bootstrap failed; exception swallowed to avoid blocking Terraria startup.", error);
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (!_started)
                {
                    return;
                }

                _started = false;
            }

            try
            {
                JueMingZRuntime.Shutdown();
                Logger.Info("Bootstrap", "JueMingZ shutdown completed.");
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("Bootstrap.Shutdown", error);
                Logger.Error("Bootstrap", "JueMingZ shutdown failed; exception swallowed.", error);
            }
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
}
