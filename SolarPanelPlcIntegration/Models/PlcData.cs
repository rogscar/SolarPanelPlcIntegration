namespace PlcIntegration.Manufacturing
{
    public class PlcData
    {
        public int DeviceId { get; set; }
        public double Temperature { get; set; }
        public double Pressure { get; set; }
        public DateTime Timestamp { get; set; }
    }
}