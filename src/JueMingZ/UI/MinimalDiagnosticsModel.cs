using System;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public sealed class MinimalDiagnosticsModel
    {
        public bool Loaded { get; set; }
        public string Version { get; set; }
        public string NetModeDescription { get; set; }
        public bool HookUpdateInstalled { get; set; }
        public DateTime? LastUpdateUtc { get; set; }
        public int FeatureCount { get; set; }
        public int EnabledFeatureCount { get; set; }
        public int PendingActionCount { get; set; }
        public string RunningAction { get; set; }
        public string LastActionResult { get; set; }
        public string LastError { get; set; }

        public static MinimalDiagnosticsModel FromSnapshot(DiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new MinimalDiagnosticsModel();
            }

            return new MinimalDiagnosticsModel
            {
                Loaded = snapshot.Loaded,
                Version = snapshot.Version,
                NetModeDescription = snapshot.NetModeDescription,
                HookUpdateInstalled = snapshot.HookUpdateInstalled,
                LastUpdateUtc = snapshot.LastUpdateUtc,
                FeatureCount = snapshot.FeatureCount,
                EnabledFeatureCount = snapshot.EnabledFeatureCount,
                PendingActionCount = snapshot.PendingActionCount,
                RunningAction = snapshot.RunningAction,
                LastActionResult = snapshot.LastActionResult,
                LastError = snapshot.LastError
            };
        }
    }
}
