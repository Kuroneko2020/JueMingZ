using System;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;

namespace JueMingZ.Compat
{
    public static class TerrariaRuntimeTypes
    {
        private static readonly object SyncRoot = new object();
        private static Type _mainType;
        private static Type _playerType;
        private static Type _vector2Type;
        private static string _lastError = string.Empty;

        public static Type MainType { get { EnsureInitializedLateOnly(); return _mainType; } }
        public static Type PlayerType { get { EnsureInitializedLateOnly(); return _playerType; } }
        public static Type Vector2Type { get { EnsureInitializedLateOnly(); return _vector2Type; } }
        public static string LastError { get { return _lastError; } }

        public static bool EnsureInitializedLateOnly()
        {
            lock (SyncRoot)
            {
                if (_mainType != null)
                {
                    return true;
                }

                try
                {
                    _mainType = GameMode.FindTerrariaMainType();
                    if (_mainType == null)
                    {
                        _lastError = "Terraria.Main type not found.";
                        return false;
                    }

                    _playerType = FindType("Terraria.Player");
                    _vector2Type = FindType("Microsoft.Xna.Framework.Vector2");
                    _lastError = string.Empty;
                    return true;
                }
                catch (Exception error)
                {
                    _lastError = error.Message;
                    LogThrottle.WarnThrottled(
                        "terraria-runtime-types-init-failed",
                        TimeSpan.FromSeconds(30),
                        "TerrariaRuntimeTypes",
                        "Terraria runtime type cache init failed: " + error.Message);
                    return false;
                }
            }
        }

        private static Type FindType(string fullName)
        {
            return TerrariaTypeCache.Find(fullName);
        }
    }
}
