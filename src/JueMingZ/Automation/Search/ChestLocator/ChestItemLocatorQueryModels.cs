using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace JueMingZ.Automation.Search.ChestLocator
{
    internal sealed class ChestItemLocatorQuery
    {
        public string RawText { get; private set; }

        public string NormalizedText { get; private set; }

        public int CandidateLimit { get; private set; }

        public ChestItemLocatorQuery(string rawText, int candidateLimit)
        {
            RawText = rawText ?? string.Empty;
            NormalizedText = RawText.Trim();
            CandidateLimit = candidateLimit;
        }
    }

    internal sealed class ChestItemLocatorCandidate
    {
        public int ItemType { get; private set; }

        public string DisplayName { get; private set; }

        public string InternalName { get; private set; }

        public string IdText
        {
            get { return "#" + ItemType.ToString(CultureInfo.InvariantCulture); }
        }

        public ChestItemLocatorCandidate(int itemType, string displayName, string internalName)
        {
            ItemType = itemType;
            DisplayName = displayName ?? string.Empty;
            InternalName = internalName ?? string.Empty;
        }
    }

    internal sealed class ChestItemLocatorQueryResult
    {
        public const string StatusOk = "ok";
        public const string StatusEmptyInput = "emptyInput";
        public const string StatusUnknownItemId = "unknownItemId";
        public const string StatusNoMatch = "noMatch";
        public const string StatusTooManyCandidates = "tooManyCandidates";

        private readonly HashSet<int> _matchItemTypes;

        public ChestItemLocatorQuery Query { get; private set; }

        public string Status { get; private set; }

        public bool Succeeded { get; private set; }

        public bool IsTruncated { get; private set; }

        public IReadOnlyList<ChestItemLocatorCandidate> Candidates { get; private set; }

        public IReadOnlyCollection<int> MatchItemTypes { get; private set; }

        public int CandidateCount
        {
            get { return Candidates.Count; }
        }

        private ChestItemLocatorQueryResult(
            ChestItemLocatorQuery query,
            string status,
            bool succeeded,
            bool isTruncated,
            IList<ChestItemLocatorCandidate> candidates)
        {
            Query = query ?? new ChestItemLocatorQuery(string.Empty, ChestItemLocatorQueryResolver.DefaultCandidateLimit);
            Status = status ?? string.Empty;
            Succeeded = succeeded;
            IsTruncated = isTruncated;

            var candidateCopy = new List<ChestItemLocatorCandidate>();
            var matchTypes = new HashSet<int>();
            if (candidates != null)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    var candidate = candidates[index];
                    if (candidate == null || candidate.ItemType <= 0)
                    {
                        continue;
                    }

                    candidateCopy.Add(candidate);
                    if (succeeded)
                    {
                        matchTypes.Add(candidate.ItemType);
                    }
                }
            }

            var matchTypeList = new List<int>(matchTypes);
            matchTypeList.Sort();
            Candidates = new ReadOnlyCollection<ChestItemLocatorCandidate>(candidateCopy);
            MatchItemTypes = new ReadOnlyCollection<int>(matchTypeList);
            _matchItemTypes = matchTypes;
        }

        public static ChestItemLocatorQueryResult Failed(ChestItemLocatorQuery query, string status)
        {
            return new ChestItemLocatorQueryResult(query, status, false, false, new List<ChestItemLocatorCandidate>());
        }

        public static ChestItemLocatorQueryResult TooManyCandidates(
            ChestItemLocatorQuery query,
            IList<ChestItemLocatorCandidate> candidates)
        {
            return new ChestItemLocatorQueryResult(query, StatusTooManyCandidates, false, true, candidates);
        }

        public static ChestItemLocatorQueryResult Success(
            ChestItemLocatorQuery query,
            IList<ChestItemLocatorCandidate> candidates)
        {
            return new ChestItemLocatorQueryResult(query, StatusOk, true, false, candidates);
        }

        public bool MatchesItemType(int itemType)
        {
            return itemType > 0 && _matchItemTypes.Contains(itemType);
        }

        public HashSet<int> CreateMatchSet()
        {
            // Later chest scanning must do O(1) item.type checks, not per-slot string matching.
            return new HashSet<int>(_matchItemTypes);
        }
    }
}
