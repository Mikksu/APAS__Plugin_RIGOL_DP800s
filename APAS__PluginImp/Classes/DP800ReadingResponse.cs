namespace APAS__Plugin_RIGOL_DP800s.Classes
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
