using System;

namespace JueMingZ.Diagnostics
{
    // Records hook outcomes only; installers own reflection and side effects so snapshots can read this safely.
    public static class HookDiagnostics
    {
        private static readonly object SyncRoot = new object();

        public static bool HookInstallAttempted { get; private set; }
        public static bool SafeBootstrapHookInstalled { get; private set; }
        public static bool HookUpdateInstalled { get; private set; }
        public static bool DrawHookInstalled { get; private set; }
        public static bool InterfaceLayerHookInstalled { get; private set; }
        public static bool ItemCheckHookInstalled { get; private set; }
        public static bool PlayerDeathHookInstalled { get; private set; }
        public static bool TeleportRodHookInstalled { get; private set; }
        public static bool GoblinExecutionHookInstalled { get; private set; }
        public static string SafeBootstrapHookMethod { get; private set; } = string.Empty;
        public static string UpdateHookMethod { get; private set; } = string.Empty;
        public static string DrawHookMethod { get; private set; } = string.Empty;
        public static string InterfaceLayerHookMethod { get; private set; } = string.Empty;
        public static string ItemCheckHookMethod { get; private set; } = string.Empty;
        public static string ItemCheckHookMessage { get; private set; } = string.Empty;
        public static string ItemCheckHookError { get; private set; } = string.Empty;
        public static string PlayerDeathHookMethod { get; private set; } = string.Empty;
        public static string PlayerDeathHookMessage { get; private set; } = string.Empty;
        public static string PlayerDeathHookError { get; private set; } = string.Empty;
        public static string TeleportRodHookMethod { get; private set; } = string.Empty;
        public static string TeleportRodHookMessage { get; private set; } = string.Empty;
        public static string TeleportRodHookError { get; private set; } = string.Empty;
        public static string GoblinExecutionHookMethod { get; private set; } = string.Empty;
        public static string GoblinExecutionHookMessage { get; private set; } = string.Empty;
        public static string GoblinExecutionHookError { get; private set; } = string.Empty;
        public static string LastInstallMessage { get; private set; } = string.Empty;
        public static string LastInstallError { get; private set; } = string.Empty;
        public static DateTime? LastInstallAttemptUtc { get; private set; }

        public static void MarkSkipped(string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                HookUpdateInstalled = false;
                UpdateHookMethod = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkSucceeded(string methodName, string message)
        {
            MarkUpdateHookSucceeded(methodName, message);
        }

        public static void MarkSafeBootstrapInstalled(string methodName, string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                SafeBootstrapHookInstalled = true;
                SafeBootstrapHookMethod = methodName ?? string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkUpdateHookSucceeded(string methodName, string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                HookUpdateInstalled = true;
                UpdateHookMethod = methodName ?? string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkDrawHookSucceeded(string methodName, string message)
        {
            MarkInterfaceLayerHookSucceeded(methodName, message);
        }

        public static void MarkInterfaceLayerHookSucceeded(string methodName, string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                DrawHookInstalled = true;
                InterfaceLayerHookInstalled = true;
                DrawHookMethod = methodName ?? string.Empty;
                InterfaceLayerHookMethod = methodName ?? string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkDrawHookSkipped(string message)
        {
            MarkInterfaceLayerHookSkipped(message);
        }

        public static void MarkInterfaceLayerHookSkipped(string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                DrawHookInstalled = false;
                InterfaceLayerHookInstalled = false;
                DrawHookMethod = string.Empty;
                InterfaceLayerHookMethod = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkDrawHookFailed(string message, Exception error)
        {
            MarkInterfaceLayerHookFailed(message, error);
        }

        public static void MarkItemCheckHookSucceeded(string methodName, string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                ItemCheckHookInstalled = true;
                ItemCheckHookMethod = methodName ?? string.Empty;
                ItemCheckHookMessage = message ?? string.Empty;
                ItemCheckHookError = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkItemCheckHookSkipped(string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                ItemCheckHookInstalled = false;
                ItemCheckHookMethod = string.Empty;
                ItemCheckHookMessage = message ?? string.Empty;
                ItemCheckHookError = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkItemCheckHookFailed(string message, Exception error)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                ItemCheckHookInstalled = false;
                ItemCheckHookMethod = string.Empty;
                ItemCheckHookMessage = message ?? string.Empty;
                ItemCheckHookError = error == null ? string.Empty : error.ToString();
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = error == null ? string.Empty : error.ToString();
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkPlayerDeathHookSucceeded(string methodName, string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                PlayerDeathHookInstalled = true;
                PlayerDeathHookMethod = methodName ?? string.Empty;
                PlayerDeathHookMessage = message ?? string.Empty;
                PlayerDeathHookError = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkPlayerDeathHookSkipped(string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                PlayerDeathHookInstalled = false;
                PlayerDeathHookMethod = string.Empty;
                PlayerDeathHookMessage = message ?? string.Empty;
                PlayerDeathHookError = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkPlayerDeathHookFailed(string message, Exception error)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                PlayerDeathHookInstalled = false;
                PlayerDeathHookMethod = string.Empty;
                PlayerDeathHookMessage = message ?? string.Empty;
                PlayerDeathHookError = error == null ? string.Empty : error.ToString();
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = error == null ? string.Empty : error.ToString();
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkTeleportRodHookSucceeded(string methodName, string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                TeleportRodHookInstalled = true;
                TeleportRodHookMethod = methodName ?? string.Empty;
                TeleportRodHookMessage = message ?? string.Empty;
                TeleportRodHookError = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkTeleportRodHookSkipped(string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                TeleportRodHookInstalled = false;
                TeleportRodHookMethod = string.Empty;
                TeleportRodHookMessage = message ?? string.Empty;
                TeleportRodHookError = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkTeleportRodHookFailed(string message, Exception error)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                TeleportRodHookInstalled = false;
                TeleportRodHookMethod = string.Empty;
                TeleportRodHookMessage = message ?? string.Empty;
                TeleportRodHookError = error == null ? string.Empty : error.ToString();
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = error == null ? string.Empty : error.ToString();
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkGoblinExecutionHookSucceeded(string methodName, string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                GoblinExecutionHookInstalled = true;
                GoblinExecutionHookMethod = methodName ?? string.Empty;
                GoblinExecutionHookMessage = message ?? string.Empty;
                GoblinExecutionHookError = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkGoblinExecutionHookSkipped(string message)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                GoblinExecutionHookInstalled = false;
                GoblinExecutionHookMethod = string.Empty;
                GoblinExecutionHookMessage = message ?? string.Empty;
                GoblinExecutionHookError = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = string.Empty;
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkGoblinExecutionHookFailed(string message, Exception error)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                GoblinExecutionHookInstalled = false;
                GoblinExecutionHookMethod = string.Empty;
                GoblinExecutionHookMessage = message ?? string.Empty;
                GoblinExecutionHookError = error == null ? string.Empty : error.ToString();
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = error == null ? string.Empty : error.ToString();
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkInterfaceLayerHookFailed(string message, Exception error)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                DrawHookInstalled = false;
                InterfaceLayerHookInstalled = false;
                DrawHookMethod = string.Empty;
                InterfaceLayerHookMethod = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = error == null ? string.Empty : error.ToString();
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }

        public static void MarkFailed(string message, Exception error)
        {
            lock (SyncRoot)
            {
                HookInstallAttempted = true;
                HookUpdateInstalled = false;
                UpdateHookMethod = string.Empty;
                LastInstallMessage = message ?? string.Empty;
                LastInstallError = error == null ? string.Empty : error.ToString();
                LastInstallAttemptUtc = DateTime.UtcNow;
            }
        }
    }
}
