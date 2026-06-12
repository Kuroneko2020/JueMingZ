using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Search.ChestLocator;

namespace JueMingZ.UI.Legacy
{
    internal static class SearchChestLocatorUiState
    {
        public const string InputId = "search-chest-locator:input";
        public const string SubmitButtonId = "search-chest-locator:submit";
        public const string ClearButtonId = "search-chest-locator:clear";
        public const string CandidateElementPrefix = "search-chest-locator:candidate:";
        public const int CandidateMaxResults = ChestItemLocatorQueryResolver.DefaultCandidateLimit;
        public const string SearchRangeNoResultMessage = "搜索范围内未找到物品";

        private static readonly object SyncRoot = new object();
        private static readonly List<ChestItemLocatorCandidate> Candidates = new List<ChestItemLocatorCandidate>();

        private static string _queryText = string.Empty;
        private static string _candidateMessage = string.Empty;
        private static string _statusMessage = "输入物品名、内部名或 #ID 后点击定位。";
        private static string _degradeMessage = string.Empty;
        private static string _noticeMessage = string.Empty;
        private static ChestItemLocatorQueryResult _queryResult = ChestItemLocatorQueryResolver.Resolve(string.Empty);
        private static ChestItemLocatorSnapshot _lastSnapshot = ChestItemLocatorSnapshot.Empty;
        private static int _selectedItemType;
        private static long _queryVersion;

        public static string QueryText
        {
            get { lock (SyncRoot) { return _queryText; } }
        }

        public static string CandidateMessage
        {
            get { lock (SyncRoot) { return _candidateMessage; } }
        }

        public static string StatusMessage
        {
            get { lock (SyncRoot) { return _statusMessage ?? string.Empty; } }
        }

        public static string DegradeMessage
        {
            get { lock (SyncRoot) { return _degradeMessage ?? string.Empty; } }
        }

        public static string NoticeMessage
        {
            get { lock (SyncRoot) { return _noticeMessage ?? string.Empty; } }
        }

        public static bool HasNotice
        {
            get { lock (SyncRoot) { return !string.IsNullOrWhiteSpace(_noticeMessage); } }
        }

        public static int CandidateCount
        {
            get { lock (SyncRoot) { return Candidates.Count; } }
        }

        public static int SelectedItemType
        {
            get { lock (SyncRoot) { return _selectedItemType; } }
        }

        public static long QueryVersion
        {
            get { lock (SyncRoot) { return _queryVersion; } }
        }

        public static bool HasSnapshot
        {
            get { lock (SyncRoot) { return _lastSnapshot != null && _lastSnapshot.HitCount > 0; } }
        }

        public static bool HasAnyState
        {
            get
            {
                lock (SyncRoot)
                {
                    return !string.IsNullOrWhiteSpace(_queryText) ||
                           Candidates.Count > 0 ||
                           _selectedItemType > 0 ||
                           (_lastSnapshot != null && _lastSnapshot != ChestItemLocatorSnapshot.Empty);
                }
            }
        }

        public static void UpdateDraft(string query)
        {
            var text = NormalizeQuery(query);
            lock (SyncRoot)
            {
                if (string.Equals(_queryText, text, StringComparison.Ordinal))
                {
                    return;
                }

                _queryText = text;
                _selectedItemType = 0;
                _degradeMessage = string.Empty;
                _noticeMessage = string.Empty;
                RebuildCandidatesLocked();
            }
        }

        public static bool SelectCandidateForSubmit(int itemType, out ChestItemLocatorQueryResult result, out long queryVersion, out string message)
        {
            result = null;
            queryVersion = 0;
            message = string.Empty;

            lock (SyncRoot)
            {
                if (itemType <= 0)
                {
                    message = "候选物品无效。";
                    ApplySubmitFailureLocked(message);
                    return false;
                }

                _selectedItemType = itemType;
                _queryText = "#" + itemType.ToString(CultureInfo.InvariantCulture);
                _queryResult = ChestItemLocatorQueryResolver.Resolve(_queryText, CandidateMaxResults);
                CopyCandidatesLocked(_queryResult);
                return TryBeginSubmitLocked(out result, out queryVersion, out message);
            }
        }

        public static bool TryBeginSubmit(out ChestItemLocatorQueryResult result, out long queryVersion, out string message)
        {
            lock (SyncRoot)
            {
                return TryBeginSubmitLocked(out result, out queryVersion, out message);
            }
        }

        public static void ApplySnapshot(long queryVersion, ChestItemLocatorSnapshot snapshot)
        {
            lock (SyncRoot)
            {
                if (queryVersion != _queryVersion)
                {
                    return;
                }

                _lastSnapshot = snapshot ?? ChestItemLocatorSnapshot.Empty;
                _statusMessage = BuildSnapshotMessage(_lastSnapshot);
                _degradeMessage = BuildSnapshotDegradeMessage(_lastSnapshot);
                _noticeMessage = ShouldShowNoResultNotice(_lastSnapshot) ? SearchRangeNoResultMessage : string.Empty;
            }
        }

        public static void ApplyContextUnavailable(long queryVersion, string skipReason)
        {
            lock (SyncRoot)
            {
                if (queryVersion != _queryVersion)
                {
                    return;
                }

                _lastSnapshot = ChestItemLocatorSnapshot.Empty;
                _statusMessage = "无法读取世界上下文，暂不能扫描附近箱子。";
                _degradeMessage = string.IsNullOrWhiteSpace(skipReason) ? string.Empty : "原因：" + skipReason.Trim();
                _noticeMessage = "无法读取世界上下文";
            }
        }

        public static void ApplySectionRequestResult(long queryVersion, ChestItemLocatorSectionRequestResult result)
        {
            lock (SyncRoot)
            {
                if (queryVersion != _queryVersion || result == null)
                {
                    return;
                }

                var message = BuildSectionRequestMessage(result);
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                _degradeMessage = AppendDegradeMessage(_degradeMessage, message);
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _queryText = string.Empty;
                _candidateMessage = string.Empty;
                _statusMessage = "输入物品名、内部名或 #ID 后点击定位。";
                _degradeMessage = string.Empty;
                _noticeMessage = string.Empty;
                _queryResult = ChestItemLocatorQueryResolver.Resolve(string.Empty);
                Candidates.Clear();
                _selectedItemType = 0;
                _lastSnapshot = ChestItemLocatorSnapshot.Empty;
                _queryVersion++;
            }
        }

        public static List<ChestItemLocatorCandidate> GetCandidates()
        {
            lock (SyncRoot)
            {
                return new List<ChestItemLocatorCandidate>(Candidates);
            }
        }

        public static ChestItemLocatorSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return _lastSnapshot ?? ChestItemLocatorSnapshot.Empty;
            }
        }

        public static bool ClearSnapshotAfterChestOpened()
        {
            lock (SyncRoot)
            {
                if (_lastSnapshot == null ||
                    object.ReferenceEquals(_lastSnapshot, ChestItemLocatorSnapshot.Empty) ||
                    _lastSnapshot.HitCount <= 0)
                {
                    return false;
                }

                _lastSnapshot = ChestItemLocatorSnapshot.Empty;
                _statusMessage = "已打开箱子，定位提示已清除。";
                _degradeMessage = string.Empty;
                _noticeMessage = string.Empty;
                _queryVersion++;
                return true;
            }
        }

        public static bool ClearVisibleStateAfterHitClose()
        {
            lock (SyncRoot)
            {
                if (_lastSnapshot == null ||
                    object.ReferenceEquals(_lastSnapshot, ChestItemLocatorSnapshot.Empty) ||
                    _lastSnapshot.HitCount <= 0)
                {
                    return false;
                }

                _queryText = string.Empty;
                _candidateMessage = string.Empty;
                _statusMessage = "输入物品名、内部名或 #ID 后点击定位。";
                _degradeMessage = string.Empty;
                _noticeMessage = string.Empty;
                _queryResult = ChestItemLocatorQueryResolver.Resolve(string.Empty);
                Candidates.Clear();
                _selectedItemType = 0;
                // Keep the active snapshot and query version so the world
                // highlight survives reopening F5 until the player clears it,
                // opens a chest, or the overlay expires it naturally.
                return true;
            }
        }

        public static int BuildStateSignature()
        {
            unchecked
            {
                lock (SyncRoot)
                {
                    var hash = 17;
                    AddHash(ref hash, _queryText);
                    AddHash(ref hash, _candidateMessage);
                    AddHash(ref hash, _statusMessage);
                    AddHash(ref hash, _degradeMessage);
                    AddHash(ref hash, _noticeMessage);
                    AddHash(ref hash, _selectedItemType);
                    AddHash(ref hash, (int)_queryVersion);
                    AddHash(ref hash, (int)(_queryVersion >> 32));
                    AddHash(ref hash, Candidates.Count);
                    for (var index = 0; index < Candidates.Count; index++)
                    {
                        var candidate = Candidates[index];
                        AddHash(ref hash, candidate == null ? 0 : candidate.ItemType);
                        AddHash(ref hash, candidate == null ? string.Empty : candidate.DisplayName);
                        AddHash(ref hash, candidate == null ? string.Empty : candidate.InternalName);
                    }

                    AddSnapshotHash(ref hash, _lastSnapshot);
                    return hash;
                }
            }
        }

        public static string BuildUiStateJson()
        {
            lock (SyncRoot)
            {
                var snapshot = _lastSnapshot ?? ChestItemLocatorSnapshot.Empty;
                return "{" +
                       "\"query\":\"" + EscapeJson(_queryText) + "\"," +
                       "\"candidateCount\":" + Candidates.Count.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"selectedItemType\":" + _selectedItemType.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"queryVersion\":" + _queryVersion.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"snapshotStatus\":\"" + EscapeJson(snapshot.Status) + "\"," +
                       "\"hitCount\":" + snapshot.HitCount.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"matchedSlotCount\":" + snapshot.MatchedSlotCount.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"totalStack\":" + snapshot.TotalStack.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"status\":\"" + EscapeJson(_statusMessage) + "\"," +
                       "\"degrade\":\"" + EscapeJson(_degradeMessage) + "\"," +
                       "\"notice\":\"" + EscapeJson(_noticeMessage) + "\"" +
                       "}";
            }
        }

        internal static string[] GetSummaryLinesForTesting()
        {
            lock (SyncRoot)
            {
                return new[] { _statusMessage ?? string.Empty, _degradeMessage ?? string.Empty };
            }
        }

        internal static string GetNoticeMessageForTesting()
        {
            lock (SyncRoot)
            {
                return _noticeMessage ?? string.Empty;
            }
        }

        internal static void ResetForTesting()
        {
            Clear();
            lock (SyncRoot)
            {
                _queryVersion = 0;
            }
        }

        private static bool TryBeginSubmitLocked(out ChestItemLocatorQueryResult result, out long queryVersion, out string message)
        {
            RebuildCandidatesLocked();
            _queryVersion++;
            queryVersion = _queryVersion;
            result = _queryResult;

            if (result == null || !result.Succeeded)
            {
                message = BuildQueryStatusMessage(result);
                ApplySubmitFailureLocked(message);
                return false;
            }

            message = "已提交定位请求。";
            _lastSnapshot = ChestItemLocatorSnapshot.Empty;
            _statusMessage = "正在扫描附近已同步箱子。";
            _degradeMessage = string.Empty;
            _noticeMessage = string.Empty;
            return true;
        }

        private static void RebuildCandidatesLocked()
        {
            _queryResult = ChestItemLocatorQueryResolver.Resolve(_queryText, CandidateMaxResults);
            CopyCandidatesLocked(_queryResult);
            _candidateMessage = BuildQueryStatusMessage(_queryResult);
            if (_queryResult != null && _queryResult.Succeeded)
            {
                _statusMessage = "候选 " + _queryResult.CandidateCount.ToString(CultureInfo.InvariantCulture) + " 个，点击定位扫描附近箱子。";
                return;
            }

            if (string.IsNullOrWhiteSpace(_queryText))
            {
                _statusMessage = "输入物品名、内部名或 #ID 后点击定位。";
            }
        }

        private static void CopyCandidatesLocked(ChestItemLocatorQueryResult result)
        {
            Candidates.Clear();
            if (result == null || result.Candidates == null)
            {
                return;
            }

            for (var index = 0; index < result.Candidates.Count; index++)
            {
                var candidate = result.Candidates[index];
                if (candidate != null && candidate.ItemType > 0)
                {
                    Candidates.Add(candidate);
                }
            }
        }

        private static void ApplySubmitFailureLocked(string message)
        {
            _lastSnapshot = ChestItemLocatorSnapshot.Empty;
            _statusMessage = string.IsNullOrWhiteSpace(message) ? "无法提交定位请求。" : message.Trim();
            _degradeMessage = string.Empty;
            _noticeMessage = SearchRangeNoResultMessage;
        }

        private static bool ShouldShowNoResultNotice(ChestItemLocatorSnapshot snapshot)
        {
            return snapshot == null ||
                   !string.Equals(snapshot.Status, ChestItemLocatorSnapshot.StatusOk, StringComparison.Ordinal) ||
                   snapshot.HitCount <= 0;
        }

        private static string BuildQueryStatusMessage(ChestItemLocatorQueryResult result)
        {
            if (result == null)
            {
                return "定位查询不可用。";
            }

            if (string.Equals(result.Status, ChestItemLocatorQueryResult.StatusOk, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (string.Equals(result.Status, ChestItemLocatorQueryResult.StatusEmptyInput, StringComparison.Ordinal))
            {
                return "请输入要定位的物品。";
            }

            if (string.Equals(result.Status, ChestItemLocatorQueryResult.StatusUnknownItemId, StringComparison.Ordinal))
            {
                return "没有这个物品 ID。";
            }

            if (string.Equals(result.Status, ChestItemLocatorQueryResult.StatusNoMatch, StringComparison.Ordinal))
            {
                return "没有匹配物品。";
            }

            if (string.Equals(result.Status, ChestItemLocatorQueryResult.StatusTooManyCandidates, StringComparison.Ordinal))
            {
                return "候选过多，请缩小关键词或点选下方候选。";
            }

            return "定位查询暂不可用：" + result.Status;
        }

        private static string BuildSnapshotMessage(ChestItemLocatorSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "尚未扫描附近箱子。";
            }

            if (!string.Equals(snapshot.Status, ChestItemLocatorSnapshot.StatusOk, StringComparison.Ordinal))
            {
                return "扫描未完成：" + snapshot.Status;
            }

            if (snapshot.HitCount > 0)
            {
                return "命中 " + snapshot.HitCount.ToString(CultureInfo.InvariantCulture) +
                       " 个箱子，" + snapshot.MatchedSlotCount.ToString(CultureInfo.InvariantCulture) +
                       " 个槽位，共 " + snapshot.TotalStack.ToString(CultureInfo.InvariantCulture) + " 个。";
            }

            if (snapshot.ScannedChestCount > 0)
            {
                return "已扫描 " + snapshot.ScannedChestCount.ToString(CultureInfo.InvariantCulture) + " 个箱子，未发现目标物品。";
            }

            return "附近没有可读取的候选箱子。";
        }

        private static string BuildSnapshotDegradeMessage(ChestItemLocatorSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (snapshot.UnreadableChestCount > 0)
            {
                parts.Add(snapshot.UnreadableChestCount.ToString(CultureInfo.InvariantCulture) + " 个箱子内容不可读");
            }

            if (snapshot.CandidateLimitReached)
            {
                parts.Add("候选箱子达到扫描上限");
            }

            if (snapshot.HitLimitReached)
            {
                parts.Add("命中结果达到显示上限");
            }

            return parts.Count <= 0 ? string.Empty : string.Join("；", parts.ToArray());
        }

        private static string BuildSectionRequestMessage(ChestItemLocatorSectionRequestResult result)
        {
            if (result == null || !result.MultiplayerClient)
            {
                return string.Empty;
            }

            if (string.Equals(result.Status, ChestItemLocatorSectionRequestResult.StatusSent, StringComparison.Ordinal))
            {
                return "多人已请求当前 section 数据，稍后可再次定位刷新。";
            }

            if (string.Equals(result.Status, ChestItemLocatorSectionRequestResult.StatusThrottled, StringComparison.Ordinal))
            {
                return "多人 section 请求冷却中，当前仅使用已同步箱子。";
            }

            if (string.Equals(result.Status, ChestItemLocatorSectionRequestResult.StatusDisabled, StringComparison.Ordinal))
            {
                return "多人 section 请求已关闭，当前仅使用已同步箱子。";
            }

            if (string.Equals(result.Status, ChestItemLocatorSectionRequestResult.StatusFailed, StringComparison.Ordinal) ||
                string.Equals(result.Status, ChestItemLocatorSectionRequestResult.StatusInvalidSection, StringComparison.Ordinal))
            {
                return "多人 section 请求失败：" + FirstNonEmpty(result.FailureReason, result.Status);
            }

            return string.Empty;
        }

        private static string AppendDegradeMessage(string existing, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return existing ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(existing))
            {
                return message.Trim();
            }

            return existing.Trim() + "；" + message.Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string NormalizeQuery(string query)
        {
            return string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        }

        private static void AddSnapshotHash(ref int hash, ChestItemLocatorSnapshot snapshot)
        {
            AddHash(ref hash, snapshot == null ? string.Empty : snapshot.Status);
            AddHash(ref hash, snapshot == null ? 0 : snapshot.HitCount);
            AddHash(ref hash, snapshot == null ? 0 : snapshot.MatchedSlotCount);
            AddHash(ref hash, snapshot == null ? 0 : snapshot.TotalStack);
            AddHash(ref hash, snapshot != null && snapshot.CandidateLimitReached);
            AddHash(ref hash, snapshot != null && snapshot.HitLimitReached);
            AddHash(ref hash, snapshot == null ? 0 : snapshot.UnreadableChestCount);
        }

        private static void AddHash(ref int hash, int value)
        {
            hash = hash * 31 + value;
        }

        private static void AddHash(ref int hash, bool value)
        {
            hash = hash * 31 + (value ? 1 : 0);
        }

        private static void AddHash(ref int hash, string value)
        {
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(value ?? string.Empty);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
