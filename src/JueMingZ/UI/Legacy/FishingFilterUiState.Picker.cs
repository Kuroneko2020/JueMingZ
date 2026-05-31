using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    internal static partial class FishingFilterUiState
    {
        public static void OpenPicker(AppSettings settings, IList<FishingCatchCandidate> candidates, string message)
        {
            OpenPickerInternal(settings, candidates, PickerSourceCurrent, string.Empty, message);
        }

        public static void OpenGlobalSearchPicker(AppSettings settings, IList<FishingCatchCandidate> candidates, string query, string message)
        {
            OpenPickerInternal(settings, candidates, PickerSourceGlobal, NormalizeSearchQuery(query), message);
        }

        private static void OpenPickerInternal(
            AppSettings settings,
            IList<FishingCatchCandidate> candidates,
            string source,
            string query,
            string message)
        {
            var signature = BuildModeSignature(settings);
            lock (SyncRoot)
            {
                ResetLocked();
                _modeSignature = signature;
                _pickerOpen = true;
                _pickerSource = string.IsNullOrWhiteSpace(source) ? PickerSourceCurrent : source;
                _globalSearchQuery = string.Equals(_pickerSource, PickerSourceGlobal, StringComparison.Ordinal)
                    ? NormalizeSearchQuery(query)
                    : string.Empty;
                _pickerMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (candidates != null)
                {
                    for (var index = 0; index < candidates.Count; index++)
                    {
                        var candidate = NormalizeCandidate(candidates[index]);
                        if (candidate == null || !seen.Add(candidate.Key))
                        {
                            continue;
                        }

                        PickerCandidates.Add(candidate);
                    }
                }

                ClampScrollLocked();
            }
        }

        public static bool IsGlobalSearchQuery(string query)
        {
            lock (SyncRoot)
            {
                return string.Equals(_pickerSource, PickerSourceGlobal, StringComparison.Ordinal) &&
                       string.Equals(_globalSearchQuery, NormalizeSearchQuery(query), StringComparison.Ordinal);
            }
        }

        public static void ClosePicker(AppSettings settings, string message)
        {
            var signature = BuildModeSignature(settings);
            lock (SyncRoot)
            {
                ResetPickerLocked();
                _modeSignature = signature;
                _pickerMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            }
        }

        public static List<FishingCatchCandidate> GetPickerCandidates()
        {
            lock (SyncRoot)
            {
                var snapshot = new List<FishingCatchCandidate>(PickerCandidates.Count);
                for (var index = 0; index < PickerCandidates.Count; index++)
                {
                    snapshot.Add(Clone(PickerCandidates[index]));
                }

                return snapshot;
            }
        }

        public static List<FishingCatchCandidate> GetSelectedCandidates()
        {
            lock (SyncRoot)
            {
                var selected = new List<FishingCatchCandidate>();
                for (var index = 0; index < PickerCandidates.Count; index++)
                {
                    var candidate = PickerCandidates[index];
                    if (candidate != null && PickerSelectedKeys.Contains(candidate.Key))
                    {
                        selected.Add(Clone(candidate));
                    }
                }

                return selected;
            }
        }

        public static bool IsSelected(string kind, int id)
        {
            lock (SyncRoot)
            {
                return PickerSelectedKeys.Contains(BuildKey(kind, id));
            }
        }

        public static bool ToggleSelection(string kind, int id)
        {
            lock (SyncRoot)
            {
                var key = BuildKey(kind, id);
                if (string.IsNullOrWhiteSpace(key) || !ContainsCandidateKeyLocked(key))
                {
                    return false;
                }

                if (PickerSelectedKeys.Contains(key))
                {
                    PickerSelectedKeys.Remove(key);
                    return false;
                }

                PickerSelectedKeys.Add(key);
                return true;
            }
        }

        public static void ClearSelection()
        {
            lock (SyncRoot)
            {
                PickerSelectedKeys.Clear();
            }
        }

        public static string BuildKey(string kind, int id)
        {
            var normalizedKind = NormalizeKind(kind);
            return string.IsNullOrWhiteSpace(normalizedKind) || id <= 0
                ? string.Empty
                : normalizedKind + ":" + id.ToString(CultureInfo.InvariantCulture);
        }

        public static string NormalizeKind(string kind)
        {
            if (string.Equals(kind, FishingCatchKinds.Item, StringComparison.OrdinalIgnoreCase))
            {
                return FishingCatchKinds.Item;
            }

            if (string.Equals(kind, FishingCatchKinds.NPC, StringComparison.OrdinalIgnoreCase))
            {
                return FishingCatchKinds.NPC;
            }

            return string.Empty;
        }

        private static bool ContainsCandidateKeyLocked(string key)
        {
            for (var index = 0; index < PickerCandidates.Count; index++)
            {
                var candidate = PickerCandidates[index];
                if (candidate != null && string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static FishingCatchCandidate NormalizeCandidate(FishingCatchCandidate candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            var kind = NormalizeKind(candidate.Kind);
            if (string.IsNullOrWhiteSpace(kind) || candidate.Id <= 0)
            {
                return null;
            }

            return new FishingCatchCandidate
            {
                Kind = kind,
                Id = candidate.Id,
                DisplayName = string.IsNullOrWhiteSpace(candidate.DisplayName)
                    ? candidate.DisplayNameSnapshot ?? string.Empty
                    : candidate.DisplayName.Trim(),
                DisplayNameSnapshot = string.IsNullOrWhiteSpace(candidate.DisplayNameSnapshot)
                    ? candidate.DisplayName ?? string.Empty
                    : candidate.DisplayNameSnapshot.Trim(),
                IsCrate = candidate.IsCrate,
                IsQuestFish = candidate.IsQuestFish,
                IsEnemy = candidate.IsEnemy
            };
        }

        private static FishingCatchCandidate Clone(FishingCatchCandidate candidate)
        {
            return candidate == null ? null : new FishingCatchCandidate
            {
                Kind = candidate.Kind,
                Id = candidate.Id,
                DisplayName = candidate.DisplayName,
                DisplayNameSnapshot = candidate.DisplayNameSnapshot,
                IsCrate = candidate.IsCrate,
                IsQuestFish = candidate.IsQuestFish,
                IsEnemy = candidate.IsEnemy
            };
        }

        private static void ResetPickerLocked()
        {
            _pickerOpen = false;
            _pickerMessage = string.Empty;
            _pickerSource = PickerSourceCurrent;
            _globalSearchQuery = string.Empty;
            _pickerScrollOffset = 0;
            _pickerMaxScroll = 0;
            _pickerViewport = new LegacyUiRect();
            PickerCandidates.Clear();
            PickerSelectedKeys.Clear();
        }

        private static string NormalizeSearchQuery(string query)
        {
            return string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        }
    }
}
