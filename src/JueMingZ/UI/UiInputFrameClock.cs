using System;
using JueMingZ.Compat;

namespace JueMingZ.UI
{
    internal static class UiInputFrameClock
    {
        private static readonly object SyncRoot = new object();
        private static long _drawFrameId;
        private static UiInputFrameKey _currentFrameKey;

        public static long DrawFrameId
        {
            get { lock (SyncRoot) { return _drawFrameId; } }
        }

        public static UiInputFrameKey CurrentFrameKey
        {
            get { lock (SyncRoot) { return _currentFrameKey; } }
        }

        public static UiInputFrameKey BeginDrawFrame(string source)
        {
            lock (SyncRoot)
            {
                _drawFrameId++;
                _currentFrameKey = UiInputFrameKey.Draw(_drawFrameId, source);
                return _currentFrameKey;
            }
        }

        public static UiInputFrameKey BeginUpdateFrame(string phase)
        {
            lock (SyncRoot)
            {
                _currentFrameKey = UiInputFrameKey.Update(ReadGameUpdateCountSafe(), phase);
                return _currentFrameKey;
            }
        }

        public static UiInputFrameKey BeginInputFrame(string phase)
        {
            lock (SyncRoot)
            {
                _currentFrameKey = UiInputFrameKey.Input(ReadGameUpdateCountSafe(), phase);
                return _currentFrameKey;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _drawFrameId = 0;
                _currentFrameKey = UiInputFrameKey.None;
            }
        }

        private static uint ReadGameUpdateCountSafe()
        {
            try
            {
                return TerrariaMainCompat.GameUpdateCount;
            }
            catch
            {
                return 0;
            }
        }
    }

    internal enum UiInputFrameKind
    {
        None = 0,
        Draw = 1,
        Update = 2,
        Input = 3
    }

    internal struct UiInputFrameKey
    {
        private readonly UiInputFrameKind _kind;
        private readonly long _id;
        private readonly string _phase;

        private UiInputFrameKey(UiInputFrameKind kind, long id, string phase)
        {
            _kind = kind;
            _id = id;
            _phase = phase ?? string.Empty;
        }

        public static UiInputFrameKey None
        {
            get { return new UiInputFrameKey(UiInputFrameKind.None, 0, string.Empty); }
        }

        public static UiInputFrameKey Draw(long drawFrameId, string source)
        {
            return new UiInputFrameKey(UiInputFrameKind.Draw, drawFrameId, source);
        }

        public static UiInputFrameKey Update(uint gameUpdateCount, string phase)
        {
            return new UiInputFrameKey(UiInputFrameKind.Update, gameUpdateCount, phase);
        }

        public static UiInputFrameKey Input(uint gameUpdateCount, string phase)
        {
            return new UiInputFrameKey(UiInputFrameKind.Input, gameUpdateCount, phase);
        }

        public bool IsValid
        {
            get { return _kind != UiInputFrameKind.None; }
        }

        public bool Equals(UiInputFrameKey other)
        {
            return _kind == other._kind &&
                   _id == other._id &&
                   string.Equals(_phase, other._phase, StringComparison.Ordinal);
        }
    }
}
