using System;
using JueMingZ.Actions;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Features
{
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
