using System.Collections.Generic;
using System.Linq;

namespace JueMingZ.Actions
{
    public static class InputActionDiagnostics
    {
        public static IReadOnlyList<InputActionResult> CopyRecentResults(IEnumerable<InputActionResult> results)
        {
            return results == null
                ? new List<InputActionResult>()
                : results.ToList();
        }
    }
}
