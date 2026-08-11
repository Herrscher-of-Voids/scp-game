using System;

using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class ActObservationLockedHandler : ITraitHandler
    {
        public ScpTrait Trait => ScpTrait.ActObservationLocked;

        public void Tick(in TraitContext context, TraitInstance instance, IEventSink events)
        {
            var requiredObservers = instance.Get(TraitParamKey.RequiredObservers, 1);
            var reactionDelay = instance.Get(TraitParamKey.ReactionDelayTicks, 0);
            var stabilityLoss = instance.Get(TraitParamKey.StabilityLossPerTick, 1);
            var isLocked = context.Anomaly.IsObserved && context.Anomaly.ObserverCount >= requiredObservers;

            if (isLocked)
            {
                context.Anomaly.UnobservedTicks = 0;
                events.Emit(new DomainEvent
                {
                    Kind = DomainEventKind.ObservationLocked,
                    Tick = context.World.Tick,
                    ScpId = context.Anomaly.Definition.Id
                });
                return;
            }

            context.Anomaly.UnobservedTicks++;
            context.Anomaly.Stability = Math.Max(0, context.Anomaly.Stability - stabilityLoss);
            if (context.Anomaly.UnobservedTicks > reactionDelay)
            {
                context.Anomaly.ActionProgress++;
                events.Emit(new DomainEvent
                {
                    Kind = DomainEventKind.AnomalyActed,
                    Tick = context.World.Tick,
                    ScpId = context.Anomaly.Definition.Id,
                    Amount = context.Anomaly.ActionProgress
                });
            }
        }
    }
}
