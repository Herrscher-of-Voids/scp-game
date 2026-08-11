using Scp.Domain;

namespace Scp.Simulation
{
    public static class StrategicCapabilityService
    {
        public static bool IsAvailable(WorldState world, StrategicCapability capability)
        {
            foreach (var anomaly in world.Anomalies)
            {
                if (anomaly.IsContained && anomaly.IsFacilityIntact && anomaly.HasCapability(capability))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
