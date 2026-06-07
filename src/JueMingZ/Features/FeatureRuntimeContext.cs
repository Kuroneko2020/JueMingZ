using System;
using JueMingZ.Actions;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Features
{
    // Runtime supplies already-read state and queues; feature updates must not re-read globals or mutate game data directly.
    public sealed class FeatureRuntimeContext
    {
        public DateTime UtcNow { get; set; }
        public string GameModeDescription { get; set; }
        public GameStateSnapshot GameState { get; set; }
        public InputActionQueue ActionQueue { get; set; }
        public RuntimeState RuntimeState { get; set; }
        public DateTime UpdateUtc { get; set; }
    }
}
