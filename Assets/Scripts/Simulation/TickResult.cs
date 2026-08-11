using System.Collections.Generic;

namespace Scp.Simulation
{
    public sealed class TickResult
    {
        public TickResult(WorldState state, IReadOnlyList<DomainEvent> events)
        {
            State = state;
            Events = events;
        }

        public WorldState State { get; }

        public IReadOnlyList<DomainEvent> Events { get; }
    }
}
