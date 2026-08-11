using System;
using System.IO;
using System.Linq;

using Scp.Application;
using Scp.Domain;
using Scp.Simulation;

namespace Scp.Host.Console
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            return args.Contains("--m0", StringComparer.OrdinalIgnoreCase) ? RunM0() : RunM1();
        }

        private static int RunM1()
        {
            var definitions = new ScpContentLoader().LoadDirectory(
                ContentPathResolver.FindScpDirectory(Environment.CurrentDirectory));
            // 中文：正式设施目录与 SCP 内容同为构建输入；加载器已强制校验 89 项、唯一 ID 与来源字段。
            // English: The official facility catalogue is a build input alongside SCP content; the loader already enforces 89 entities, unique IDs, and source fields.
            var facilities = new FacilityDataLoader().LoadFile(
                ContentPathResolver.FindFacilityFile(Environment.CurrentDirectory));
            // 世界构建逻辑与 Unity Presentation 层共用同一个工厂，确保两侧确定性一致。
            var world = OverseerScenarioFactory.CreateDemoWorld(definitions, facilities);
            var council = world.Council;
            var sites = world.Sites;
            var anomalies = world.Anomalies;
            var session = new GameSession(world, new OverseerPerspective());
            var query = new WorldQuery(world, ClearanceLevel.Level5);
            var restart = new SubmitProposalCommand
            {
                Kind = ProposalKind.WorldRestart,
                Threshold = ProposalThreshold.Unanimous,
                Position = new AxisPosition(0, 0, 0)
            };
            System.Console.WriteLine("=== M1 O5 垂直切片 ===");
            System.Console.WriteLine($"配置: {definitions.Length}，设施: {sites.Length}，议会席位: {council.Seats.Length}");
            System.Console.WriteLine($"能力资产完好时重启提案可提交: {restart.Validate(query).IsValid}");
            var reconstructionAsset = anomalies.Single(item => item.HasCapability(StrategicCapability.WorldReconstruction));
            reconstructionAsset.IsContained = false;
            System.Console.WriteLine($"能力资产失去后重启提案可提交: {restart.Validate(query).IsValid}");
            reconstructionAsset.IsContained = true;
            session.Submit(new SubmitProposalCommand
            {
                Kind = ProposalKind.LiftContactRestriction,
                Threshold = ProposalThreshold.SimpleMajority,
                Position = new AxisPosition(20, 10, 30)
            });
            session.Submit(new LobbySeatCommand { SeatId = council.Seats.First(seat => !seat.IsPlayer).Id });
            session.Submit(new AuditSiteCommand { SiteId = sites[0].Id });
            session.Submit(new CastPlayerVoteCommand { ProposalId = 1, Choice = VoteChoice.Support });
            AdvanceMonth(session);
            PrintCycle(world);
            session.Submit(new PressureSeatCommand { SeatId = council.Seats.Last(seat => !seat.IsPlayer).Id });
            session.Submit(new AllocateBudgetCommand { Budget = InsolventBudget() });
            while (!world.Failure.IsEnded && world.Council.CurrentCycle < 24)
            {
                AdvanceMonth(session);
                PrintCycle(world);
            }

            if (!world.Failure.IsEnded)
            {
                return 1;
            }

            System.Console.WriteLine("--- 公开投票记录 ---");
            foreach (var record in world.Council.VoteRecords)
            {
                var votes = string.Join(", ", record.Votes.Select(vote => $"O5-{vote.SeatId.Number}:{vote.Choice}"));
                System.Console.WriteLine($"提案 {record.ProposalId} {record.Kind} 通过={record.Passed} | {votes}");
            }

            System.Console.WriteLine("--- 文字尾声 ---");
            System.Console.WriteLine(new EpilogueService().Create(world));
            return 0;
        }

        private static int RunM0()
        {
            var siteId = new SiteId(1);
            var world = new WorldState
            {
                Funds = 1000000,
                Random = new DeterministicRandom(20260805),
                Sites = new[]
                {
                    new SiteState
                    {
                        Id = siteId,
                        Continent = Continent.Asia,
                        SecurityLevel = 4,
                        AvailableObservers = 2
                    }
                },
                Anomalies = new[]
                {
                    new AnomalyInstance
                    {
                        SiteId = siteId,
                        IsObserved = true,
                        ObserverCount = 2,
                        Definition = CreateDemoDefinition(new ScpId(1))
                    }
                }
            };
            var session = new GameSession(world, new BasicPerspective(IdentityRole.Overseer, ClearanceLevel.Level5));
            var result = session.Advance(2400);
            var savePath = Path.Combine(Environment.CurrentDirectory, "output", "m0-save.json");
            session.Save(savePath);
            var loaded = new SaveService().Load(savePath);
            var matches = loaded.World.Tick == world.Tick && loaded.World.Funds == world.Funds &&
                loaded.World.Random.State0 == world.Random.State0 && loaded.World.Random.State1 == world.Random.State1;
            System.Console.WriteLine($"Tick: {world.Tick}");
            System.Console.WriteLine($"Funds: {world.Funds}");
            System.Console.WriteLine($"Events: {result.Events.Count}");
            System.Console.WriteLine($"RoundTrip: {(matches ? "OK" : "FAILED")}");
            return matches ? 0 : 1;
        }

        private static void AdvanceMonth(GameSession session)
        {
            session.Advance(WorldSimulation.MonthlyTicks);
        }

        private static void PrintCycle(WorldState world)
        {
            System.Console.WriteLine(
                $"周期 {world.Council.CurrentCycle}: 资金={world.Funds}, 现金流={world.Economy.LastCashFlow}, 帷幕={world.Veil.Global}, Alpha-1={(world.Council.AlphaOne.IsActive ? "Active" : "Sealed")}, 终局={world.Failure.EndReason}");
        }

        private static BudgetState InsolventBudget()
        {
            return new BudgetState
            {
                SiteOperations = 1800000,
                Research = 700000,
                Security = 500000,
                MobileTaskForces = 300000,
                AlphaOne = 0,
                VeilOperations = Enumerable.Repeat(100000L, 7).ToArray(),
                EmergencyReserve = 0
            };
        }

        private static ScpDefinition CreateDemoDefinition(ScpId id)
        {
            return new ScpDefinition
            {
                Id = id,
                Class = ObjectClass.Euclid,
                Requirement = new ContainmentRequirement { MonthlyCost = 2500 },
                Traits = new[]
                {
                    new TraitInstance
                    {
                        Trait = ScpTrait.YieldResource,
                        Params = new[]
                        {
                            new TraitParam(TraitParamKey.ResourcePerCycle, 5),
                            new TraitParam(TraitParamKey.CycleTicks, 24)
                        }
                    }
                }
            };
        }
    }
}
