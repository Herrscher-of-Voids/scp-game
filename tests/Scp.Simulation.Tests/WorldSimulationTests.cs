using System;
using System.Linq;

using NUnit.Framework;

using Scp.Domain;
using Scp.Simulation;

namespace Scp.Simulation.Tests
{
    public sealed class WorldSimulationTests
    {
        [Test]
        public void Tick_2400Ticks_ProducesDeterministicState()
        {
            // 中文：长期确定性快照使用已满足观察条件的稳定夹具，避免该测试被收容风险终局打断；失效概率由专门测试验证。
            // English: The long-running deterministic snapshot uses a fully observed stable fixture so the test is not interrupted by a containment terminal state; breach probability is covered separately.
            var first = CreateWorld(true);
            var second = CreateWorld(true);
            var firstSimulation = new WorldSimulation(first, ClearanceLevel.Level5);
            var secondSimulation = new WorldSimulation(second, ClearanceLevel.Level5);

            for (var index = 0; index < 2400; index++)
            {
                firstSimulation.Tick(ReadOnlySpan<ICommand>.Empty);
                secondSimulation.Tick(ReadOnlySpan<ICommand>.Empty);
            }

            Assert.That(first.Tick, Is.EqualTo(second.Tick));
            Assert.That(first.Funds, Is.EqualTo(second.Funds));
            Assert.That(first.Random.State0, Is.EqualTo(second.Random.State0));
            Assert.That(first.Anomalies[0].AccumulatedResource,
                Is.EqualTo(second.Anomalies[0].AccumulatedResource));
            Assert.That(first.Anomalies[0].ActionProgress, Is.EqualTo(second.Anomalies[0].ActionProgress));
        }

        [Test]
        public void Tick_UnauthorizedCommand_RejectsWithoutApplying()
        {
            var world = CreateWorld(false);
            var simulation = new WorldSimulation(world, ClearanceLevel.Level2);
            var command = new AdjustFundsCommand { Amount = 500 };

            var result = simulation.Tick(new ICommand[] { command });

            Assert.That(world.Funds, Is.EqualTo(10000));
            Assert.That(result.Events.Any(item => item.Kind == DomainEventKind.CommandRejected), Is.True);
        }

        [Test]
        public void ActObservationLocked_WhenObserved_PreventsAction()
        {
            var world = CreateWorld(true);
            var simulation = new WorldSimulation(world, ClearanceLevel.Level5);

            var result = simulation.Tick(ReadOnlySpan<ICommand>.Empty);

            Assert.That(world.Anomalies[0].ActionProgress, Is.Zero);
            Assert.That(world.Anomalies[0].Stability, Is.EqualTo(10000));
            Assert.That(result.Events.Any(item => item.Kind == DomainEventKind.ObservationLocked), Is.True);
        }

        [Test]
        public void YieldResource_OnCycle_AccumulatesDeterministically()
        {
            var world = CreateWorld(false);
            var simulation = new WorldSimulation(world, ClearanceLevel.Level5);

            for (var index = 0; index < 6; index++)
            {
                simulation.Tick(ReadOnlySpan<ICommand>.Empty);
            }

            Assert.That(world.Anomalies[0].AccumulatedResource, Is.EqualTo(30));
        }

        private static WorldState CreateWorld(bool observed)
        {
            return new WorldState
            {
                Funds = 10000,
                Random = new DeterministicRandom(11),
                // 中文：该夹具用于 trait 的长期确定性测试，因此提供足额站点运营与异常维护预算；收容风险由专门测试覆盖。
                // English: This fixture supports long-running trait determinism tests, so it fully funds site operations and anomaly maintenance; dedicated tests cover containment risk.
                Economy = new EconomyState
                {
                    FundingSource = FundingSource.GreyMarket,
                    Budget = new BudgetState { SiteOperations = 1000 }
                },
                Sites = new[]
                {
                    new SiteState
                    {
                        Id = new SiteId(1),
                        Continent = Continent.Asia,
                        SecurityLevel = 5,
                        AvailableObservers = 2
                    }
                },
                Anomalies = new[]
                {
                    new AnomalyInstance
                    {
                        SiteId = new SiteId(1),
                        IsObserved = observed,
                        ObserverCount = observed ? 2 : 0,
                        Definition = new ScpDefinition
                        {
                            Id = new ScpId(1),
                            Class = ObjectClass.Euclid,
                            Requirement = new ContainmentRequirement { MonthlyCost = 100 },
                            Traits = new[]
                            {
                                new TraitInstance
                                {
                                    Trait = ScpTrait.ActObservationLocked,
                                    Params = new[]
                                    {
                                        new TraitParam(TraitParamKey.RequiredObservers, 2),
                                        new TraitParam(TraitParamKey.ReactionDelayTicks, 0),
                                        new TraitParam(TraitParamKey.StabilityLossPerTick, 1)
                                    }
                                },
                                new TraitInstance
                                {
                                    Trait = ScpTrait.YieldResource,
                                    Params = new[]
                                    {
                                        new TraitParam(TraitParamKey.ResourcePerCycle, 10),
                                        new TraitParam(TraitParamKey.CycleTicks, 2)
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
