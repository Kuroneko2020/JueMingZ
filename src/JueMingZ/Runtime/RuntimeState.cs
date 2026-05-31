using System;

namespace JueMingZ.Runtime
{
    public sealed class RuntimeState
    {
        public bool Loaded { get; set; }
        public DateTime? InitializedUtc { get; set; }
        public DateTime? FirstUpdateUtc { get; set; }
        public DateTime? LastUpdateUtc { get; set; }
        public long UpdateCount { get; set; }
        public bool FirstUpdateLogged { get; set; }
        public DateTime? LastHeartbeatUtc { get; set; }
        public string GameModeDescription { get; set; } = "Unknown";
        public string LastGameModeDescription { get; set; } = "Unknown";
        public string LastError { get; set; } = string.Empty;
        public bool LateBootstrapCompleted { get; set; }
        public DateTime? LateBootstrapCompletedUtc { get; set; }

        public void MarkInitialized()
        {
            Loaded = true;
            InitializedUtc = DateTime.UtcNow;
        }

        public void MarkUpdate(string gameModeDescription)
        {
            var now = DateTime.UtcNow;
            if (!FirstUpdateUtc.HasValue)
            {
                FirstUpdateUtc = now;
            }

            LastUpdateUtc = now;
            UpdateCount++;
            LastGameModeDescription = gameModeDescription ?? "Unknown";
            GameModeDescription = LastGameModeDescription;
        }

        public void MarkHeartbeat()
        {
            LastHeartbeatUtc = DateTime.UtcNow;
        }

        public void MarkShutdown()
        {
            Loaded = false;
        }

        public void MarkLateBootstrapCompleted()
        {
            LateBootstrapCompleted = true;
            LateBootstrapCompletedUtc = DateTime.UtcNow;
        }
    }
}
