using System;
using System.Reflection;
using JueMingZ.Diagnostics;
using Microsoft.Xna.Framework;

namespace JueMingZ.Compat
{
    public static partial class TerrariaTextInputCompat
    {
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static bool _resolvedImePanelMethods;
        private static MethodInfo _setImePanelAnchorMethod;
        private static MethodInfo _drawImePanelMethod;
        private static Action<Vector2, float> _setImePanelAnchorDelegate;
        private static Action _drawImePanelDelegate;
        private static object _setImePanelAnchorDelegateTarget;
        private static object _drawImePanelDelegateTarget;
        private static int _imePanelReflectionResolveCount;
        private static bool _imePanelAnchorAttachedThisFrame;
        private static bool _imePanelDrawnThisFrame;
        private static long _imePanelFrameResetCount;
        private static long _imePanelAnchorAttachAttemptCount;
        private static long _imePanelAnchorAttachSuccessCount;
        private static long _imePanelDrawAttemptCount;
        private static long _imePanelDrawSuccessCount;
        private static long _imePanelDrawSkippedNoAnchorCount;
        private static long _imePanelDrawRejectedActiveSpriteBatchCount;
        private static long _imePanelInvokeFallbackCount;
        private static string _imePanelLastStatus = string.Empty;
        private static string _imePanelLastMessage = string.Empty;

        public static void BeginImePanelFrame()
        {
            lock (SyncRoot)
            {
                _imePanelAnchorAttachedThisFrame = false;
                _imePanelDrawnThisFrame = false;
                _imePanelFrameResetCount++;
                SetImePanelStatusLocked("frameReset", "LegacyTextInput IME panel frame state reset.", false);
            }
        }

        public static bool TrySetImePanelAnchor(float x, float y, float xAnchor, out string message)
        {
            message = string.Empty;
            IncrementImePanelAnchorAttempt();
            try
            {
                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType == null)
                {
                    message = "Terraria.Main unavailable; native IME panel anchor cannot be set.";
                    SetImePanelStatus("mainMissing", message);
                    LogImePanelWarning("terraria-text-input-ime-panel-main-missing", message);
                    return false;
                }

                ResolveImePanelMethods(mainType);
                if (_setImePanelAnchorMethod == null)
                {
                    message = "Terraria.Main.SetIMEPanelAnchor(Vector2, float) not found; native IME panel anchor is unavailable.";
                    SetImePanelStatus("anchorApiMissing", message);
                    LogImePanelWarning("terraria-text-input-ime-panel-anchor-api-missing", message);
                    return false;
                }

                var instance = GetMainInstance(mainType);
                if (instance == null)
                {
                    message = "Terraria.Main.instance unavailable; native IME panel anchor cannot be set.";
                    SetImePanelStatus("instanceMissing", message);
                    LogImePanelWarning("terraria-text-input-ime-panel-instance-missing", message);
                    return false;
                }

                var anchor = new Vector2(x, y);
                var setAnchor = ResolveSetImePanelAnchorDelegate(instance);
                if (setAnchor != null)
                {
                    setAnchor(anchor, xAnchor);
                }
                else
                {
                    MarkImePanelInvokeFallback();
                    _setImePanelAnchorMethod.Invoke(instance, new object[] { anchor, xAnchor });
                }

                lock (SyncRoot)
                {
                    _imePanelAnchorAttachedThisFrame = true;
                    _imePanelAnchorAttachSuccessCount++;
                }

                message = "Terraria native IME panel anchor OK.";
                SetImePanelStatus("anchorAttached", message);
                return true;
            }
            catch (Exception error)
            {
                message = "Set native IME panel anchor failed: " + error.Message;
                SetImePanelStatus("anchorFailed", message);
                LogImePanelWarning("terraria-text-input-ime-panel-anchor-failed", message);
                return false;
            }
        }

        public static bool TryDrawImePanelAfterSpriteBatchEnded(bool spriteBatchBegun, out string message)
        {
            message = string.Empty;
            IncrementImePanelDrawAttempt();
            try
            {
                if (spriteBatchBegun)
                {
                    message = "Native IME panel draw skipped because Terraria interface SpriteBatch is still active.";
                    MarkImePanelDrawRejectedActiveSpriteBatch();
                    SetImePanelStatus("drawRejectedActiveSpriteBatch", message);
                    LogImePanelWarning("terraria-text-input-ime-panel-draw-active-spritebatch", message);
                    return false;
                }

                lock (SyncRoot)
                {
                    if (!_imePanelAnchorAttachedThisFrame)
                    {
                        message = "Native IME panel draw skipped because no LegacyTextInput anchor was attached this frame.";
                        _imePanelDrawSkippedNoAnchorCount++;
                        SetImePanelStatusLocked("drawSkippedNoAnchor", message, false);
                        return true;
                    }
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType == null)
                {
                    message = "Terraria.Main unavailable; native IME panel cannot be drawn.";
                    SetImePanelStatus("drawMainMissing", message);
                    LogImePanelWarning("terraria-text-input-ime-panel-draw-main-missing", message);
                    return false;
                }

                ResolveImePanelMethods(mainType);
                if (_drawImePanelMethod == null)
                {
                    message = "Terraria.Main.DrawIMEPanel() not found; native IME panel draw is unavailable.";
                    SetImePanelStatus("drawApiMissing", message);
                    LogImePanelWarning("terraria-text-input-ime-panel-draw-api-missing", message);
                    return false;
                }

                var instance = GetMainInstance(mainType);
                if (instance == null)
                {
                    message = "Terraria.Main.instance unavailable; native IME panel cannot be drawn.";
                    SetImePanelStatus("drawInstanceMissing", message);
                    LogImePanelWarning("terraria-text-input-ime-panel-draw-instance-missing", message);
                    return false;
                }

                var drawPanel = ResolveDrawImePanelDelegate(instance);
                if (drawPanel != null)
                {
                    drawPanel();
                }
                else
                {
                    MarkImePanelInvokeFallback();
                    _drawImePanelMethod.Invoke(instance, null);
                }

                lock (SyncRoot)
                {
                    _imePanelDrawnThisFrame = true;
                    _imePanelDrawSuccessCount++;
                }

                message = "Terraria native IME panel draw OK.";
                SetImePanelStatus("drawn", message);
                return true;
            }
            catch (Exception error)
            {
                message = "Draw native IME panel failed: " + error.Message;
                SetImePanelStatus("drawFailed", message);
                LogImePanelWarning("terraria-text-input-ime-panel-draw-failed", message);
                return false;
            }
        }

        internal static TextInputImePanelDiagnostics GetImePanelDiagnosticsForSnapshot()
        {
            lock (SyncRoot)
            {
                return new TextInputImePanelDiagnostics
                {
                    AnchorAttachedThisFrame = _imePanelAnchorAttachedThisFrame,
                    DrawnThisFrame = _imePanelDrawnThisFrame,
                    ReflectionResolved = _resolvedImePanelMethods,
                    SetAnchorApiAvailable = _setImePanelAnchorMethod != null,
                    DrawApiAvailable = _drawImePanelMethod != null,
                    SetAnchorDelegateReady = _setImePanelAnchorDelegate != null,
                    DrawDelegateReady = _drawImePanelDelegate != null,
                    ReflectionResolveCount = _imePanelReflectionResolveCount,
                    FrameResetCount = _imePanelFrameResetCount,
                    AnchorAttachAttemptCount = _imePanelAnchorAttachAttemptCount,
                    AnchorAttachSuccessCount = _imePanelAnchorAttachSuccessCount,
                    DrawAttemptCount = _imePanelDrawAttemptCount,
                    DrawSuccessCount = _imePanelDrawSuccessCount,
                    DrawSkippedNoAnchorCount = _imePanelDrawSkippedNoAnchorCount,
                    DrawRejectedActiveSpriteBatchCount = _imePanelDrawRejectedActiveSpriteBatchCount,
                    InvokeFallbackCount = _imePanelInvokeFallbackCount,
                    LastStatus = _imePanelLastStatus,
                    LastMessage = _imePanelLastMessage
                };
            }
        }

        internal static int ImePanelReflectionResolveCountForTesting
        {
            get { lock (SyncRoot) { return _imePanelReflectionResolveCount; } }
        }

        internal static bool ImePanelAnchorAttachedForTesting
        {
            get { lock (SyncRoot) { return _imePanelAnchorAttachedThisFrame; } }
        }

        internal static bool ImePanelDrawnForTesting
        {
            get { lock (SyncRoot) { return _imePanelDrawnThisFrame; } }
        }

        internal static void ResetImePanelReflectionCacheForTesting()
        {
            lock (SyncRoot)
            {
                _resolvedImePanelMethods = false;
                _setImePanelAnchorMethod = null;
                _drawImePanelMethod = null;
                _setImePanelAnchorDelegate = null;
                _drawImePanelDelegate = null;
                _setImePanelAnchorDelegateTarget = null;
                _drawImePanelDelegateTarget = null;
                _imePanelReflectionResolveCount = 0;
                _imePanelAnchorAttachedThisFrame = false;
                _imePanelDrawnThisFrame = false;
                _imePanelFrameResetCount = 0;
                _imePanelAnchorAttachAttemptCount = 0;
                _imePanelAnchorAttachSuccessCount = 0;
                _imePanelDrawAttemptCount = 0;
                _imePanelDrawSuccessCount = 0;
                _imePanelDrawSkippedNoAnchorCount = 0;
                _imePanelDrawRejectedActiveSpriteBatchCount = 0;
                _imePanelInvokeFallbackCount = 0;
                _imePanelLastStatus = string.Empty;
                _imePanelLastMessage = string.Empty;
            }
        }

        private static void ResolveImePanelMethods(Type mainType)
        {
            lock (SyncRoot)
            {
                if (_resolvedImePanelMethods)
                {
                    return;
                }

                _resolvedImePanelMethods = true;
                _imePanelReflectionResolveCount++;
                if (mainType == null)
                {
                    return;
                }

                _setImePanelAnchorMethod = mainType.GetMethod(
                    "SetIMEPanelAnchor",
                    InstanceFlags,
                    null,
                    new[] { typeof(Vector2), typeof(float) },
                    null);
                _drawImePanelMethod = mainType.GetMethod(
                    "DrawIMEPanel",
                    InstanceFlags,
                    null,
                    Type.EmptyTypes,
                    null);
                _setImePanelAnchorDelegate = null;
                _drawImePanelDelegate = null;
                _setImePanelAnchorDelegateTarget = null;
                _drawImePanelDelegateTarget = null;
            }
        }

        private static Action<Vector2, float> ResolveSetImePanelAnchorDelegate(object instance)
        {
            if (instance == null || _setImePanelAnchorMethod == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                if (ReferenceEquals(_setImePanelAnchorDelegateTarget, instance) &&
                    _setImePanelAnchorDelegateTarget != null)
                {
                    return _setImePanelAnchorDelegate;
                }

                try
                {
                    _setImePanelAnchorDelegate = Delegate.CreateDelegate(
                        typeof(Action<Vector2, float>),
                        instance,
                        _setImePanelAnchorMethod,
                        false) as Action<Vector2, float>;
                }
                catch
                {
                    _setImePanelAnchorDelegate = null;
                }

                _setImePanelAnchorDelegateTarget = instance;
                return _setImePanelAnchorDelegate;
            }
        }

        private static Action ResolveDrawImePanelDelegate(object instance)
        {
            if (instance == null || _drawImePanelMethod == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                if (ReferenceEquals(_drawImePanelDelegateTarget, instance) &&
                    _drawImePanelDelegateTarget != null)
                {
                    return _drawImePanelDelegate;
                }

                try
                {
                    _drawImePanelDelegate = Delegate.CreateDelegate(
                        typeof(Action),
                        instance,
                        _drawImePanelMethod,
                        false) as Action;
                }
                catch
                {
                    _drawImePanelDelegate = null;
                }

                _drawImePanelDelegateTarget = instance;
                return _drawImePanelDelegate;
            }
        }

        private static object GetMainInstance(Type mainType)
        {
            if (mainType == null)
            {
                return null;
            }

            FieldInfo field;
            if (TerrariaMemberCache.TryGetField(mainType, "instance", true, out field))
            {
                return field.GetValue(null);
            }

            PropertyInfo property;
            if (TerrariaMemberCache.TryGetProperty(mainType, "instance", true, out property) &&
                property.CanRead)
            {
                return property.GetValue(null, null);
            }

            return null;
        }

        private static void IncrementImePanelAnchorAttempt()
        {
            lock (SyncRoot)
            {
                _imePanelAnchorAttachAttemptCount++;
            }
        }

        private static void IncrementImePanelDrawAttempt()
        {
            lock (SyncRoot)
            {
                _imePanelDrawAttemptCount++;
            }
        }

        private static void MarkImePanelDrawRejectedActiveSpriteBatch()
        {
            lock (SyncRoot)
            {
                _imePanelDrawRejectedActiveSpriteBatchCount++;
            }
        }

        private static void MarkImePanelInvokeFallback()
        {
            lock (SyncRoot)
            {
                _imePanelInvokeFallbackCount++;
            }
        }

        private static void SetImePanelStatus(string status, string message)
        {
            lock (SyncRoot)
            {
                SetImePanelStatusLocked(status, message, true);
            }
        }

        private static void SetImePanelStatusLocked(string status, string message, bool updateCompatLastMessage)
        {
            _imePanelLastStatus = status ?? string.Empty;
            _imePanelLastMessage = message ?? string.Empty;
            if (updateCompatLastMessage)
            {
                LastMessage = _imePanelLastMessage;
            }
        }

        private static void LogImePanelWarning(string key, string message)
        {
            LogThrottle.WarnThrottled(
                key,
                TimeSpan.FromSeconds(10),
                "TerrariaTextInputCompat",
                message);
        }
    }

    internal struct TextInputImePanelDiagnostics
    {
        public bool AnchorAttachedThisFrame { get; set; }
        public bool DrawnThisFrame { get; set; }
        public bool ReflectionResolved { get; set; }
        public bool SetAnchorApiAvailable { get; set; }
        public bool DrawApiAvailable { get; set; }
        public bool SetAnchorDelegateReady { get; set; }
        public bool DrawDelegateReady { get; set; }
        public int ReflectionResolveCount { get; set; }
        public long FrameResetCount { get; set; }
        public long AnchorAttachAttemptCount { get; set; }
        public long AnchorAttachSuccessCount { get; set; }
        public long DrawAttemptCount { get; set; }
        public long DrawSuccessCount { get; set; }
        public long DrawSkippedNoAnchorCount { get; set; }
        public long DrawRejectedActiveSpriteBatchCount { get; set; }
        public long InvokeFallbackCount { get; set; }
        public string LastStatus { get; set; }
        public string LastMessage { get; set; }
    }
}
