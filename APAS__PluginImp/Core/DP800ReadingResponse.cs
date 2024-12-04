namespace APAS.Plugin.RIGOL.DP800s.Core
{
    // ReSharper disable once InconsistentNaming
    internal class DP800ReadingResponse
    {
        public PowerSupplyChannel ChannelInstance { get; set; }

        public bool IsEnabled { get; set; }

        public double RtVoltage { get; set; }

        public double RtCurrent { get; set; }

        public double RtWatt { get; set; }
    }
}
