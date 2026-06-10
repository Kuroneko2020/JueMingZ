namespace JueMingZ.Runtime
{
    internal sealed class RuntimeDispatchStep
    {
        public RuntimeDispatchStep(string serviceName, string operationTimingName, int cadenceTicks)
        {
            ServiceName = serviceName ?? string.Empty;
            OperationTimingName = operationTimingName ?? string.Empty;
            CadenceTicks = cadenceTicks;
        }

        public string ServiceName { get; private set; }
        public string OperationTimingName { get; private set; }
        public int CadenceTicks { get; private set; }
    }
}
