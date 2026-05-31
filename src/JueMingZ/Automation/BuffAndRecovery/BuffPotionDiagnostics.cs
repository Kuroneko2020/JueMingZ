using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.BuffAndRecovery
{
    public static class BuffPotionDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<BuffPotionCandidate> Candidates = new List<BuffPotionCandidate>();
        private static int _selectedCandidateIndex = -1;
        private static string _lastBuffPotionResult = string.Empty;
        private static string _lastBuffPotionExecutionMode = string.Empty;
        private static string _lastBuffPotionNetworkMode = string.Empty;
        private static string _lastBuffPotionSyncResult = string.Empty;
        private static string _lastScanMessage = string.Empty;

        public static void UpdateFromScan(BuffPotionScanResult scan)
        {
            var whitelist = BuffPotionWhitelistService.GetWhitelistedItemTypes();
            lock (SyncRoot)
            {
                Candidates.Clear();
                if (scan != null && scan.Candidates != null)
                {
                    for (var index = 0; index < scan.Candidates.Count; index++)
                    {
                        var candidate = scan.Candidates[index];
                        if (candidate != null)
                        {
                            Candidates.Add(candidate.Clone());
                        }
                    }
                }

                RefreshWhitelistFlagsLocked(whitelist);
                if (Candidates.Count <= 0)
                {
                    _selectedCandidateIndex = -1;
                }
                else if (_selectedCandidateIndex < 0 || _selectedCandidateIndex >= Candidates.Count)
                {
                    _selectedCandidateIndex = 0;
                }

                _lastScanMessage = scan == null
                    ? string.Empty
                    : (string.IsNullOrWhiteSpace(scan.Error) ? scan.Message : scan.Error);
            }
        }

        public static void RefreshWhitelistFlags()
        {
            var whitelist = BuffPotionWhitelistService.GetWhitelistedItemTypes();
            lock (SyncRoot)
            {
                RefreshWhitelistFlagsLocked(whitelist);
            }
        }

        public static BuffPotionCandidate GetSelectedCandidate()
        {
            lock (SyncRoot)
            {
                if (_selectedCandidateIndex < 0 || _selectedCandidateIndex >= Candidates.Count)
                {
                    return null;
                }

                return Candidates[_selectedCandidateIndex].Clone();
            }
        }

        public static BuffPotionCandidate MoveSelectedCandidate(int delta)
        {
            lock (SyncRoot)
            {
                if (Candidates.Count <= 0)
                {
                    _selectedCandidateIndex = -1;
                    return null;
                }

                _selectedCandidateIndex += delta;
                if (_selectedCandidateIndex < 0)
                {
                    _selectedCandidateIndex = Candidates.Count - 1;
                }
                else if (_selectedCandidateIndex >= Candidates.Count)
                {
                    _selectedCandidateIndex = 0;
                }

                return Candidates[_selectedCandidateIndex].Clone();
            }
        }

        public static IReadOnlyList<BuffPotionCandidate> GetCandidates()
        {
            lock (SyncRoot)
            {
                var copy = new List<BuffPotionCandidate>(Candidates.Count);
                for (var index = 0; index < Candidates.Count; index++)
                {
                    copy.Add(Candidates[index].Clone());
                }

                return copy;
            }
        }

        public static HashSet<int> GetCurrentActiveBuffTypes()
        {
            return BuffPotionCatalog.ReadActiveBuffTypesForUi();
        }

        public static void RecordResult(string result, string executionMode, string networkMode, string syncResult)
        {
            lock (SyncRoot)
            {
                _lastBuffPotionResult = result ?? string.Empty;
                _lastBuffPotionExecutionMode = executionMode ?? string.Empty;
                _lastBuffPotionNetworkMode = networkMode ?? string.Empty;
                _lastBuffPotionSyncResult = syncResult ?? string.Empty;
            }
        }

        public static BuffPotionStateSnapshot GetSnapshot()
        {
            var whitelistCount = BuffPotionWhitelistService.Count;
            lock (SyncRoot)
            {
                var selected = _selectedCandidateIndex >= 0 && _selectedCandidateIndex < Candidates.Count
                    ? Candidates[_selectedCandidateIndex]
                    : null;
                return new BuffPotionStateSnapshot
                {
                    CandidateCount = Candidates.Count,
                    WhitelistCount = whitelistCount,
                    SelectedCandidateIndex = _selectedCandidateIndex,
                    SelectedCandidateItemName = selected == null ? string.Empty : selected.ItemName,
                    SelectedCandidateBuffName = selected == null ? string.Empty : selected.BuffName,
                    LastBuffPotionResult = _lastBuffPotionResult,
                    LastBuffPotionExecutionMode = _lastBuffPotionExecutionMode,
                    LastBuffPotionNetworkMode = _lastBuffPotionNetworkMode,
                    LastBuffPotionSyncResult = _lastBuffPotionSyncResult,
                    LastScanMessage = _lastScanMessage
                };
            }
        }

        private static void RefreshWhitelistFlagsLocked(HashSet<int> whitelist)
        {
            if (whitelist == null)
            {
                whitelist = new HashSet<int>();
            }

            for (var index = 0; index < Candidates.Count; index++)
            {
                Candidates[index].IsWhitelisted = whitelist.Contains(Candidates[index].ItemType);
            }
        }
    }
}
