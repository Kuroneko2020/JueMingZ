using System;
using System.Reflection;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class DebugCommandsCompat
    {
        // Debug command access is diagnostic-only; unresolved ChatManager
        // members fail closed instead of simulating chat commands.
        private const BindingFlags StaticFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly object SyncRoot = new object();
        private static string _lastStatus = "notAttempted";
        private static string _lastMessage = string.Empty;
        private static DateTime? _lastAttemptUtc;

        public static string LastStatus
        {
            get { lock (SyncRoot) { return _lastStatus; } }
        }

        public static string LastMessage
        {
            get { lock (SyncRoot) { return _lastMessage; } }
        }

        public static DateTime? LastAttemptUtc
        {
            get { lock (SyncRoot) { return _lastAttemptUtc; } }
        }

        public static bool TryOpenDebugCommandsHelp(out string message)
        {
            message = string.Empty;
            lock (SyncRoot)
            {
                _lastAttemptUtc = DateTime.UtcNow;
            }

            try
            {
                DebugUiLocalizationCompat.TryLocalizeDebugCommands();

                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType == null)
                {
                    return Fail("Terraria.Main unavailable.", out message);
                }

                var chatManagerType = mainType.Assembly.GetType("Terraria.UI.Chat.ChatManager", false);
                if (chatManagerType == null)
                {
                    return Fail("Terraria.UI.Chat.ChatManager type not found.", out message);
                }

                var debugCommandsField = chatManagerType.GetField("DebugCommands", StaticFieldFlags);
                if (debugCommandsField == null)
                {
                    return Fail("ChatManager.DebugCommands field not found.", out message);
                }

                var debugProcessor = debugCommandsField.GetValue(null);
                if (debugProcessor == null)
                {
                    return Fail("ChatManager.DebugCommands is null.", out message);
                }

                var processMethod = debugProcessor.GetType().GetMethod("Process", new[] { typeof(byte), typeof(string) });
                if (processMethod == null)
                {
                    return Fail("DebugCommands.Process(byte,string) method not found.", out message);
                }

                var myPlayerField = mainType.GetField("myPlayer", StaticFieldFlags);
                if (myPlayerField == null)
                {
                    return Fail("Terraria.Main.myPlayer field not found.", out message);
                }

                var rawPlayerIndex = myPlayerField.GetValue(null);
                var playerIndex = rawPlayerIndex == null ? 0 : Convert.ToInt32(rawPlayerIndex);
                if (playerIndex < 0)
                {
                    playerIndex = 0;
                }
                else if (playerIndex > byte.MaxValue)
                {
                    playerIndex = byte.MaxValue;
                }

                var processed = processMethod.Invoke(debugProcessor, new object[] { (byte)playerIndex, "/hh" });
                var accepted = processed == null || Convert.ToBoolean(processed);
                if (!accepted)
                {
                    return Fail("DebugCommands rejected /hh.", out message);
                }

                message = "DebugCommands accepted /hh.";
                RecordSuccess(message);
                Logger.Info("DebugCommandsCompat", message);
                return true;
            }
            catch (Exception error)
            {
                var inner = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                var detail = inner == null ? error.Message : inner.Message;
                RuntimeDiagnostics.RecordError("DebugCommandsCompat.TryOpenDebugCommandsHelp", inner ?? error);
                return Fail("DebugCommands invocation failed: " + detail, out message, inner ?? error);
            }
        }

        private static bool Fail(string detail, out string message, Exception error = null)
        {
            message = detail ?? string.Empty;
            lock (SyncRoot)
            {
                _lastStatus = "failed";
                _lastMessage = message;
            }

            if (error == null)
            {
                Logger.Warn("DebugCommandsCompat", message);
            }
            else
            {
                Logger.Error("DebugCommandsCompat", message, error);
            }

            return false;
        }

        private static void RecordSuccess(string detail)
        {
            lock (SyncRoot)
            {
                _lastStatus = "opened";
                _lastMessage = detail ?? string.Empty;
            }
        }
    }
}
