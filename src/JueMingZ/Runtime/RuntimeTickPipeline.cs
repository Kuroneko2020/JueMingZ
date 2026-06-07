using System;
using System.Diagnostics;

namespace JueMingZ.Runtime
{
    internal sealed class RuntimeTickPipeline
    {
        private readonly RuntimeTickStage[] _stages;

        public RuntimeTickPipeline(params RuntimeTickStage[] stages)
        {
            _stages = stages ?? new RuntimeTickStage[0];
        }

        public void Run(RuntimeTickContext context)
        {
            // Execute stages in declaration order; timing collection must not alter stage semantics.
            for (var index = 0; index < _stages.Length; index++)
            {
                var stage = _stages[index];
                var stageStart = Stopwatch.GetTimestamp();
                try
                {
                    stage.Execute(context);
                }
                finally
                {
                    if (context != null)
                    {
                        // Stage timings feed snapshot counters only; normal
                        // ticks must not emit per-stage performance events.
                        context.RecordStageTiming(
                            stage.Name,
                            RuntimeTickContext.GetElapsedMilliseconds(stageStart, Stopwatch.GetTimestamp()));
                    }
                }
            }
        }
    }

    internal sealed class RuntimeTickStage
    {
        private readonly Action<RuntimeTickContext> _execute;

        public RuntimeTickStage(string name, Action<RuntimeTickContext> execute)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public string Name { get; private set; }

        public void Execute(RuntimeTickContext context)
        {
            _execute(context);
        }
    }
}
