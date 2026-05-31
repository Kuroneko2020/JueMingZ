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
        public static int AvailableCandidateCount { get { return GetAvailableCandidates().Count; } }

        public static BuffPotionScanResult RefreshBuffCandidates(string reason)
        {
            BuffPotionScanResult scan;
            try
            {
                scan = BuffPotionCatalog.RefreshCandidates();
            }
            catch (Exception error)
            {
                scan = new BuffPotionScanResult
                {
                    Error = error.Message,
                    Message = "Buff potion scan failed."
                };
                Logger.Warn("LegacyMainUiState", "Buff potion scan failed: " + error.Message);
            }

            lock (SyncRoot)
            {
                _lastScan = scan ?? new BuffPotionScanResult();
                _candidateScanAttempted = true;
                _lastCandidateRefreshUtc = DateTime.UtcNow;
                _lastStatus = BuildAutoBuffStatusLocked();
            }

            return scan;
        }

        public static bool RefreshBuffCandidatesIfStale(TimeSpan maxAge, string reason)
        {
            if (maxAge < TimeSpan.Zero)
            {
                maxAge = TimeSpan.Zero;
            }

            lock (SyncRoot)
            {
                if (_candidateScanAttempted && DateTime.UtcNow - _lastCandidateRefreshUtc < maxAge)
                {
                    return false;
                }
            }

            RefreshBuffCandidates(reason);
            return true;
        }

        public static List<BuffPotionCandidate> GetCandidates()
        {
            var activeBuffs = BuffPotionDiagnostics.GetCurrentActiveBuffTypes();
            lock (SyncRoot)
            {
                RefreshWhitelistFlagsLocked();
                var result = new List<BuffPotionCandidate>();
                if (_lastScan == null || _lastScan.Candidates == null)
                {
                    return result;
                }

                for (var index = 0; index < _lastScan.Candidates.Count; index++)
                {
                    var candidate = _lastScan.Candidates[index];
                    if (candidate != null)
                    {
                        var clone = candidate.Clone();
                        clone.IsActive = clone.BuffType > 0 && activeBuffs.Contains(clone.BuffType);
                        if (clone.IsActive)
                        {
                            clone.CanApply = false;
                            clone.SkipReason = "AlreadyActive";
                        }

                        result.Add(clone);
                    }
                }

                return result;
            }
        }

        public static List<BuffPotionWhitelistEntry> GetWhitelistEntries()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (settings.AutoBuffWhitelist == null)
            {
                settings.AutoBuffWhitelist = new List<BuffPotionWhitelistEntry>();
            }

            var result = new List<BuffPotionWhitelistEntry>();
            for (var index = 0; index < settings.AutoBuffWhitelist.Count; index++)
            {
                var entry = settings.AutoBuffWhitelist[index];
                if (entry == null)
                {
                    continue;
                }

                result.Add(new BuffPotionWhitelistEntry
                {
                    ItemType = entry.ItemType,
                    BuffType = entry.BuffType,
                    ItemName = entry.ItemName ?? string.Empty,
                    BuffName = entry.BuffName ?? string.Empty
                });
            }

            return result;
        }

        public static BuffPotionCandidate FindLiveCandidate(int itemType)
        {
            var activeBuffs = BuffPotionDiagnostics.GetCurrentActiveBuffTypes();
            lock (SyncRoot)
            {
                if (_lastScan == null || _lastScan.Candidates == null)
                {
                    return null;
                }

                for (var index = 0; index < _lastScan.Candidates.Count; index++)
                {
                    var candidate = _lastScan.Candidates[index];
                    if (candidate != null && candidate.ItemType == itemType)
                    {
                        var clone = candidate.Clone();
                        clone.IsActive = clone.BuffType > 0 && activeBuffs.Contains(clone.BuffType);
                        if (clone.IsActive)
                        {
                            clone.CanApply = false;
                            clone.SkipReason = "AlreadyActive";
                        }

                        return clone;
                    }
                }
            }

            return null;
        }

        private static void RefreshWhitelistFlagsLocked()
        {
            if (_lastScan == null || _lastScan.Candidates == null)
            {
                return;
            }

            var whitelist = BuffPotionWhitelistService.GetWhitelistedItemTypes();
            for (var index = 0; index < _lastScan.Candidates.Count; index++)
            {
                var candidate = _lastScan.Candidates[index];
                if (candidate != null)
                {
                    candidate.IsWhitelisted = whitelist.Contains(candidate.ItemType);
                }
            }

            _lastStatus = BuildAutoBuffStatusLocked();
        }

        private static string BuildAutoBuffStatusLocked()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (!settings.AutoBuffEnabled)
            {
                return "自动增益已关闭";
            }

            if (BuffPotionWhitelistService.Count <= 0)
            {
                return "自动增益已开启：已选列表为空";
            }

            if (_lastScan == null || _lastScan.Candidates == null || _lastScan.Candidates.Count == 0)
            {
                return "自动增益已开启：等待刷新候选";
            }

            for (var index = 0; index < _lastScan.Candidates.Count; index++)
            {
                var candidate = _lastScan.Candidates[index];
                if (candidate != null && candidate.IsWhitelisted && candidate.CanApply)
                {
                    return "自动增益已开启：等待补充 " + candidate.BuffName;
                }
            }

            return "自动增益已开启：等待 Buff 缺失";
        }
    }
}
