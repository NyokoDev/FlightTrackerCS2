namespace FlightTracker.Systems
{
    public sealed class TrackedFlight
    {
        public int EntityIndex { get; set; }
        public int EntityVersion { get; set; }

        public string Name { get; set; } = "Unknown Aircraft";
        public string Status { get; set; } = "Unknown";

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public float Speed { get; set; }
        public float Altitude { get; set; }
    }
}