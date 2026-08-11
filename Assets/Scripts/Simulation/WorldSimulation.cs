using System;

using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class WorldSimulation : IWorld
    {
        public const int MonthlyTicks = 720;
        private readonly TraitRegistry _traits;
        private readonly ClearanceLevel _currentClearance;

        public WorldSimulation(WorldState state, ClearanceLevel currentClearance, TraitRegistry? traits = null)
        {
            State = state;
            _currentClearance = currentClearance;
            _traits = traits ?? TraitRegistry.CreateDefault();
        }

        public WorldState State { get; }

        public TickResult Tick(ReadOnlySpan<ICommand> commands)
        {
            if (State.Failure.IsEnded)
            {
                throw new InvalidOperationException("The session has ended.");
            }

            var events = new EventBuffer();
            var query = new WorldQuery(State, _currentClearance);
            foreach (var command in commands)
            {
                var validation = command.Validate(query);
                if (validation.IsValid)
                {
                    command.Apply(State, events);
                }
                else
                {
                    events.Emit(new DomainEvent
                    {
                        Kind = DomainEventKind.CommandRejected,
                        Tick = State.Tick,
                        Detail = validation.Error
                    });
                }
            }

            if (State.Failure.IsEnded)
            {
                return new TickResult(State, events.Events);
            }

            State.Tick++;
            // 中文：推进阶段顺序是确定性存档契约：指令（上方）→ trait → 收容概率 → 周期结算/失败 → 事件汇总（返回）。不得按性能便利交换阶段，否则相同种子会产生不同历史。
            // English: Phase order is a deterministic save contract: commands (above) -> traits -> containment probability -> cycle settlement/failure -> event aggregation (return). Phases must not be reordered for convenience because identical seeds would produce different histories.
            ProcessTraits(query, events);
            ContainmentRiskService.Process(State, events);
            if (State.Failure.IsEnded)
            {
                return new TickResult(State, events.Events);
            }

            // 中文：帷幕事件在收容判定后、月结前按固定小时区间推进；该顺序进入确定性存档契约，且事件服务不消费随机流。
            // English: Veil incidents advance at fixed hourly intervals after containment checks and before month-end settlement; this order is part of the deterministic save contract and the service consumes no random stream.
            VeilIncidentService.Process(State, events);

            if (State.Tick % MonthlyTicks == 0)
            {
                var cashFlow = MonthlySettlementService.Settle(State);
                events.Emit(new DomainEvent
                {
                    Kind = DomainEventKind.MonthlySettlement,
                    Tick = State.Tick,
                    Amount = cashFlow
                });
            }

            return new TickResult(State, events.Events);
        }

        private void ProcessTraits(IWorldQuery query, IEventSink events)
        {
            foreach (var anomaly in State.Anomalies)
            {
                var site = query.FindSite(anomaly.SiteId);
                if (site == null)
                {
                    continue;
                }

                var context = new TraitContext(State, anomaly, site, query);
                foreach (var trait in anomaly.Definition.Traits)
                {
                    _traits.Resolve(trait.Trait)?.Tick(in context, trait, events);
                }
            }
        }

    }
}
