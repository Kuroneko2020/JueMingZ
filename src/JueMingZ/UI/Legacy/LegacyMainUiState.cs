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
        private static readonly object SyncRoot = new object();
        private static bool _loaded;
        private static bool _visible;
        private static int _x = 320;
        private static int _y = 80;
        private static int _width = LegacyUiMetrics.DefaultWidth;
        private static int _height = LegacyUiMetrics.DefaultHeight;
        private static string _selectedPageId = "buff";
        private static int _scrollOffset;
        private static int _maxScroll;
        private static BuffPotionScanResult _lastScan = new BuffPotionScanResult();
        private static bool _candidateScanAttempted;
        private static DateTime _lastCandidateRefreshUtc = DateTime.MinValue;
        private static string _lastStatus = "自动增益状态等待刷新。";

        public static bool Visible
        {
            get { lock (SyncRoot) { return _visible; } }
        }

        public static int Y { get { EnsureLoaded(); lock (SyncRoot) { return _y; } } }
        public static int Width { get { EnsureLoaded(); lock (SyncRoot) { return _width; } } }
        public static int Height { get { EnsureLoaded(); lock (SyncRoot) { return _height; } } }
        public static string SelectedPageId { get { EnsureLoaded(); lock (SyncRoot) { return _selectedPageId; } } }
        public static int ScrollOffset { get { lock (SyncRoot) { return _scrollOffset; } } }
        public static int MaxScroll { get { lock (SyncRoot) { return _maxScroll; } } }
        public static int CandidateCount { get { lock (SyncRoot) { return _lastScan == null ? 0 : _lastScan.Candidates.Count; } } }
        public static int WhitelistCount { get { return BuffPotionWhitelistService.Count; } }
        public static string LastStatus { get { lock (SyncRoot) { return _lastStatus; } } }

        public static LegacyUiRect WindowRect
        {
            get
            {
                EnsureLoaded();
                lock (SyncRoot)
                {
                    return new LegacyUiRect(_x, _y, _width, _height);
                }
            }
        }

        public static void EnsureLoaded()
        {
            lock (SyncRoot)
            {
                if (_loaded)
                {
                    return;
                }

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                _x = Clamp(settings.LegacyMainWindowX <= 0 ? 320 : settings.LegacyMainWindowX, 0, 4096);
                _y = Clamp(settings.LegacyMainWindowY <= 0 ? 80 : settings.LegacyMainWindowY, 0, 4096);
                _width = LegacyUiMetrics.DefaultWidth;
                _height = LegacyUiMetrics.DefaultHeight;
                _selectedPageId = IsKnownPage(settings.LegacySelectedPageId) ? settings.LegacySelectedPageId : "buff";
                ClampToScreenLocked();
                _loaded = true;
            }
        }

        public static List<BuffPotionCandidate> GetAvailableCandidates()
        {
            var candidates = GetCandidates();
            var whitelist = BuffPotionWhitelistService.GetWhitelistedItemTypes();
            var result = new List<BuffPotionCandidate>();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate != null && !candidate.IsWhitelisted && (whitelist == null || !whitelist.Contains(candidate.ItemType)))
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

    }
}
