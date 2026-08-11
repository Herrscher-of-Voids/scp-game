using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class AnomalyInstance
    {
        private int _stability = 10000;

        public ScpDefinition Definition { get; set; } = new ScpDefinition();

        public SiteId SiteId { get; set; }

        public bool IsObserved { get; set; }

        public int ObserverCount { get; set; }

        public bool IsContained { get; set; } = true;

        public bool IsFacilityIntact { get; set; } = true;

        public int Stability
        {
            get => _stability;
            set => _stability = value < 0 ? 0 : value > 10000 ? 10000 : value;
        }

        public long AccumulatedResource { get; set; }

        public int MoraleAuraApplied { get; set; }

        public int UnobservedTicks { get; set; }

        public int ActionProgress { get; set; }

        public BreachStage BreachStage { get; set; }

        public bool HasTrait(ScpTrait trait)
        {
            foreach (var instance in Definition.Traits)
            {
                if (instance.Trait == trait)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasCapability(StrategicCapability capability)
        {
            foreach (var item in Definition.Capabilities)
            {
                if (item == capability)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
