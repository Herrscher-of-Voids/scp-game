using System;

namespace Scp.Domain
{
    public sealed class ScpDefinition
    {
        public ScpId Id { get; set; }

        public ObjectClass Class { get; set; }

        public TraitInstance[] Traits { get; set; } = Array.Empty<TraitInstance>();

        public ContainmentRequirement Requirement { get; set; } = new ContainmentRequirement();

        public int BaseBreachChance { get; set; }

        public ResearchValueCurve ResearchValue { get; set; } = new ResearchValueCurve();

        public StrategicCapability[] Capabilities { get; set; } = Array.Empty<StrategicCapability>();
    }
}
