using System;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Actions
{
    internal sealed class InputActionResultStore
    {
        private const int MaxRecentResults = 12;
        private readonly List<InputActionResult> _recentResults = new List<InputActionResult>();

        public int Count
        {
            get { return _recentResults.Count; }
        }

        public InputActionResult LastResult { get; private set; }

        public IReadOnlyList<InputActionResult> CopyRecentResults()
        {
            return InputActionDiagnostics.CopyRecentResults(_recentResults);
        }

        public bool TryGetResultByRequestId(Guid requestId, out InputActionResult result)
        {
            result = null;
            if (requestId == Guid.Empty)
            {
                return false;
            }

            if (LastResult != null && LastResult.RequestId == requestId)
            {
                result = LastResult;
                return true;
            }

            // Terminal result lookup is a recovery receipt path, not just a
            // diagnostics convenience for the snapshot writer.
            for (var index = _recentResults.Count - 1; index >= 0; index--)
            {
                var candidate = _recentResults[index];
                if (candidate != null && candidate.RequestId == requestId)
                {
                    result = candidate;
                    return true;
                }
            }

            return false;
        }

        public void Record(InputActionResult result)
        {
            if (result == null)
            {
                return;
            }

            LastResult = result;
            _recentResults.Add(result);
            // Recent terminal results are transaction receipts for callers such as
            // AutoStack; keep this as state, not just as diagnostic logging.
            while (_recentResults.Count > MaxRecentResults)
            {
                _recentResults.RemoveAt(0);
            }
        }

        public string GetRecentResultLineFromNewest(int newestIndex)
        {
            var index = _recentResults.Count - 1 - newestIndex;
            if (index < 0 || index >= _recentResults.Count)
            {
                return string.Empty;
            }

            var result = _recentResults[index];
            var time = result.FinishedUtc == default(DateTime)
                ? string.Empty
                : result.FinishedUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " ";
            return "[" + time.Trim() + "] " + result.Kind + " " + result.Status + ": " + (result.Message ?? string.Empty);
        }
    }
}
