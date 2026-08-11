using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class YieldResourceHandler : ITraitHandler
    {
        public ScpTrait Trait => ScpTrait.YieldResource;

        public void Tick(in TraitContext context, TraitInstance instance, IEventSink events)
        {
            var cycleTicks = instance.Get(TraitParamKey.CycleTicks, 1);
            if (context.World.Tick % cycleTicks != 0)
            {
                return;
            }

            var amount = instance.Get(TraitParamKey.ResourcePerCycle, 0);
            context.Anomaly.AccumulatedResource += amount;
            events.Emit(new DomainEvent
            {
                Kind = DomainEventKind.ResourceYielded,
                Tick = context.World.Tick,
                ScpId = context.Anomaly.Definition.Id,
                Amount = amount
            });
        }
    }
}
