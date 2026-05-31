using System;
using JueMingZ.Diagnostics;

namespace JueMingZ.Bootstrap
{
    public sealed class JueMingZAppDomainManager : AppDomainManager
    {
        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            try
            {
                base.InitializeNewDomain(appDomainInfo);
                Logger.Initialize();
                Logger.Info("AppDomainManager", "JueMingZAppDomainManager.InitializeNewDomain called.");
                DependencyResolver.Register();

                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                AssemblyLoadTracker.ObserveExistingAssemblies();
                AssemblyLoadTracker.TryStartSafeBootstrapIfReady();
            }
            catch (Exception error)
            {
                try
                {
                    Logger.Fatal("AppDomainManager", "AppDomainManager initialization failed; exception swallowed to avoid blocking Terraria startup.", error);
                }
                catch
                {
                }
            }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                AssemblyLoadTracker.ObserveAssembly(args.LoadedAssembly, true);
                AssemblyLoadTracker.TryStartSafeBootstrapIfReady();
            }
            catch (Exception error)
            {
                Logger.Error("AppDomainManager", "AssemblyLoad observer failed; exception swallowed.", error);
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            try
            {
                Logger.Fatal("AppDomainManager", "Unhandled exception observed by JueMingZ.", args.ExceptionObject as Exception);
            }
            catch
            {
            }
        }
    }
}
