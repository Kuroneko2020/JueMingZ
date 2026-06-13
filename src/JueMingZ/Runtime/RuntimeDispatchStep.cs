namespace JueMingZ.Runtime
{
    internal enum RuntimeDispatchLane
    {
        AlwaysMaintenance,
        ReadOnlyDisplay,
        ActionSubmitting
    }

    internal sealed class RuntimeDispatchStep
    {
        public RuntimeDispatchStep(string serviceName, string operationTimingName, int cadenceTicks)
            : this(serviceName, operationTimingName, cadenceTicks, RuntimeDispatchLane.ActionSubmitting)
        {
        }

        public RuntimeDispatchStep(
            string serviceName,
            string operationTimingName,
            int cadenceTicks,
            RuntimeDispatchLane lane)
        {
            ServiceName = serviceName ?? string.Empty;
            OperationTimingName = operationTimingName ?? string.Empty;
            CadenceTicks = cadenceTicks;
            Lane = lane;
        }

        public string ServiceName { get; private set; }
        public string OperationTimingName { get; private set; }
        public int CadenceTicks { get; private set; }
        public RuntimeDispatchLane Lane { get; private set; }
    }
}
