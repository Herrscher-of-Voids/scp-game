using System.Collections.Generic;

namespace Scp.Simulation
{
    public sealed class EventBuffer : IEventSink
    {
        private readonly List<DomainEvent> _events = new List<DomainEvent>();

        public IReadOnlyList<DomainEvent> Events => _events;

        public void Emit(DomainEvent domainEvent)
        {
            _events.Add(domainEvent);
        }
    }
}
