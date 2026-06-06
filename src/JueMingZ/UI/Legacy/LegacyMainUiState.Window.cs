using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainUiState
    {
        public static int X { get { EnsureLoaded(); lock (SyncRoot) { return _x; } } }

        public static bool ToggleVisible()
        {
            EnsureLoaded();
            bool visible;
            lock (SyncRoot)
            {
                _visible = !_visible;
                visible = _visible;
                if (visible)
                {
                    ClampToScreenLocked();
                    SaveWindowLocked();
                }
            }

            if (visible && !_candidateScanAttempted)
            {
                RefreshBuffCandidates("Ui.MainWindow.Open");
            }
            else if (!visible)
            {
                LegacyUiInput.ResetInteractionState();
                FishingFilterUiState.Reset();
                UiMouseCaptureService.ReleaseForOperationWindow();
            }

            RecordWindowToggle(visible);
            return visible;
        }

        public static void SetVisible(bool visible)
        {
            EnsureLoaded();
            lock (SyncRoot)
            {
                _visible = visible;
                if (visible)
                {
                    ClampToScreenLocked();
                }
            }

            if (!visible)
            {
                LegacyUiInput.ResetInteractionState();
                FishingFilterUiState.Reset();
                UiMouseCaptureService.ReleaseForOperationWindow();
            }
        }

        public static bool HideIfMainMenu(string source)
        {
            bool blocked;
            string reason;
            if (!GameMode.TryReadLegacyUiBlockedByVanillaMenuLateOnly(out blocked, out reason) || !blocked)
            {
                return false;
            }

            bool wasVisible;
            lock (SyncRoot)
            {
                wasVisible = _visible;
                _visible = false;
            }

            if (wasVisible)
            {
                LegacyUiInput.ResetInteractionState();
                FishingFilterUiState.Reset();
                UiMouseCaptureService.ReleaseForOperationWindow();
                RecordWindowMainMenuSuppressed(source, reason);
            }

            return true;
        }

        public static void SelectPage(string pageId)
        {
            if (!IsKnownPage(pageId))
            {
                pageId = "buff";
            }

            lock (SyncRoot)
            {
                if (_selectedPageId == pageId)
                {
                    return;
                }

                _selectedPageId = pageId;
                _scrollOffset = 0;
                _maxScroll = 0;
                FishingFilterUiState.Reset();
                var settings = ConfigService.AppSettings;
                if (settings != null)
                {
                    settings.LegacySelectedPageId = pageId;
                    ConfigService.SaveAll();
                }
            }
        }

        public static void SetWindow(int x, int y, int width, int height, bool save)
        {
            EnsureLoaded();
            lock (SyncRoot)
            {
                _x = Clamp(x, -4096, 4096);
                _y = Clamp(y, -4096, 4096);
                _width = LegacyUiMetrics.DefaultWidth;
                _height = LegacyUiMetrics.DefaultHeight;
                ClampToScreenLocked();
                if (save)
                {
                    SaveWindowLocked();
                }
            }
        }

        internal static void SetWindowForInteraction(int x, int y, int width, int height, bool save)
        {
            EnsureLoaded();
            lock (SyncRoot)
            {
                _x = Clamp(x, -4096, 4096);
                _y = Clamp(y, -4096, 4096);
                _width = LegacyUiMetrics.DefaultWidth;
                _height = LegacyUiMetrics.DefaultHeight;
                ClampToRecoverableScreenLocked();
                if (save)
                {
                    SaveWindowLocked();
                }
            }
        }

        public static void SaveWindow()
        {
            EnsureLoaded();
            lock (SyncRoot)
            {
                SaveWindowLocked();
            }
        }

        private static void ClampToScreenLocked()
        {
            int screenWidth;
            int screenHeight;
            ReadScreenSize(out screenWidth, out screenHeight);
            var scale = LegacyMainUiScale.ResolveForScreen(DiagnosticMouseStateReader.Read(), screenWidth, screenHeight);
            _width = LegacyUiMetrics.DefaultWidth;
            _height = LegacyUiMetrics.DefaultHeight;
            _x = Clamp(_x, 0, CalculateMaxBasePosition(screenWidth, _width, scale == null ? 1d : scale.EffectiveScaleX));
            _y = Clamp(_y, 0, CalculateMaxBasePosition(screenHeight, _height, scale == null ? 1d : scale.EffectiveScaleY));
        }

        private static void ClampToRecoverableScreenLocked()
        {
            int screenWidth;
            int screenHeight;
            ReadScreenSize(out screenWidth, out screenHeight);
            var scale = LegacyMainUiScale.ResolveForScreen(DiagnosticMouseStateReader.Read(), screenWidth, screenHeight);
            var effectiveScaleX = scale == null ? 1d : scale.EffectiveScaleX;
            var effectiveScaleY = scale == null ? 1d : scale.EffectiveScaleY;
            _width = LegacyUiMetrics.DefaultWidth;
            _height = LegacyUiMetrics.DefaultHeight;
            _x = Clamp(
                _x,
                0,
                CalculateMaxRecoverableBasePosition(
                    screenWidth,
                    _width,
                    effectiveScaleX,
                    LegacyUiMetrics.DragRecoverableVisibleWidth));
            _y = Clamp(
                _y,
                0,
                CalculateMaxRecoverableBasePosition(
                    screenHeight,
                    _height,
                    effectiveScaleY,
                    LegacyUiMetrics.DragRecoverableVisibleHeight));
        }

        private static int CalculateMaxBasePosition(int screenSize, int baseSize, double effectiveScale)
        {
            if (screenSize <= 0 || baseSize <= 0)
            {
                return 0;
            }

            if (effectiveScale <= 0.01d)
            {
                effectiveScale = 1d;
            }

            var safeSize = Math.Max(0, screenSize - 8);
            return Math.Max(0, (int)Math.Floor(safeSize / effectiveScale - baseSize));
        }

        private static int CalculateMaxRecoverableBasePosition(int screenSize, int baseSize, double effectiveScale, int visibleSize)
        {
            if (screenSize <= 0 || baseSize <= 0)
            {
                return 0;
            }

            if (effectiveScale <= 0.01d)
            {
                effectiveScale = 1d;
            }

            var safeSize = Math.Max(0, screenSize - 8);
            var recoverableVisibleSize = Clamp(visibleSize, 1, Math.Max(1, baseSize));
            return Math.Max(0, (int)Math.Floor(safeSize / effectiveScale - recoverableVisibleSize));
        }

        internal static int CalculateMaxBasePositionForTesting(int screenSize, int baseSize, double effectiveScale)
        {
            return CalculateMaxBasePosition(screenSize, baseSize, effectiveScale);
        }

        internal static int CalculateMaxRecoverableBasePositionForTesting(int screenSize, int baseSize, double effectiveScale, int visibleSize)
        {
            return CalculateMaxRecoverableBasePosition(screenSize, baseSize, effectiveScale, visibleSize);
        }

        private static void SaveWindowLocked()
        {
            var settings = ConfigService.AppSettings;
            if (settings == null)
            {
                return;
            }

            settings.LegacyMainWindowX = _x;
            settings.LegacyMainWindowY = _y;
            settings.LegacyMainWindowWidth = _width;
            settings.LegacyMainWindowHeight = _height;
            settings.LegacySelectedPageId = _selectedPageId;
            ConfigService.SaveAll();
        }

        private static bool IsKnownPage(string pageId)
        {
            if (string.IsNullOrWhiteSpace(pageId))
            {
                return false;
            }

            for (var index = 0; index < LegacyTabBar.Tabs.Length; index++)
            {
                if (LegacyTabBar.Tabs[index].Id == pageId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ReadScreenSize(out int width, out int height)
        {
            width = 1280;
            height = 720;
            try
            {
                var mainType = FindType("Terraria.Main");
                if (mainType == null)
                {
                    return;
                }

                width = ReadStaticInt(mainType, "screenWidth", width);
                height = ReadStaticInt(mainType, "screenHeight", height);
            }
            catch
            {
            }
        }

        private static int ReadStaticInt(Type type, string name, int fallback)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            try
            {
                var field = type.GetField(name, flags);
                if (field != null)
                {
                    return Convert.ToInt32(field.GetValue(null));
                }

                var property = type.GetProperty(name, flags);
                if (property != null && property.CanRead)
                {
                    return Convert.ToInt32(property.GetValue(null, null));
                }
            }
            catch
            {
            }

            return fallback;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
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
    }
}
