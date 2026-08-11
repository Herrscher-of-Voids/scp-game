using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Scp.Application;
using Scp.Domain;
using Scp.Simulation;

namespace Scp.Simulation.Tests
{
    public sealed class M1SimulationTests
    {
        [Test]
        public void CouncilFactory_CreatesThirteenSeatsAndDeterministicHiddenPositions()
        {
            var firstRandom = new DeterministicRandom(71);
            var secondRandom = new DeterministicRandom(71);
            var first = CouncilFactory.Create(ref firstRandom);
            var second = CouncilFactory.Create(ref secondRandom);

            Assert.That(first.Seats, Has.Length.EqualTo(13));
            Assert.That(first.Seats.Count(seat => seat.IsPlayer), Is.EqualTo(1));
            Assert.That(first.Seats.Count(seat => !seat.IsPlayer), Is.EqualTo(12));
            Assert.That(first.Seats.Select(seat => seat.Position), Is.EqualTo(second.Seats.Select(seat => seat.Position)));
        }

        [TestCase(ProposalThreshold.SimpleMajority, 7)]
        [TestCase(ProposalThreshold.TwoThirds, 9)]
        [TestCase(ProposalThreshold.Unanimous, 13)]
        public void ProposalResolver_UsesRequiredThresholds(ProposalThreshold threshold, int expected)
        {
            Assert.That(ProposalResolver.RequiredVotes(threshold), Is.EqualTo(expected));
        }

        [Test]
        public void ProposalResolver_InsufficientVotesFailAndSameProposalIsPredictable()
        {
            var world = CreateCouncilWorld(3);
            foreach (var seat in world.Council.Seats.Where(seat => !seat.IsPlayer))
            {
                seat.Position = new AxisPosition(-100, -100, -100);
            }

            var first = NewProposal(1, ProposalThreshold.SimpleMajority, new AxisPosition(100, 100, 100));
            var second = NewProposal(2, ProposalThreshold.SimpleMajority, new AxisPosition(100, 100, 100));
            var firstRecord = ProposalResolver.Resolve(world, first);
            var secondRecord = ProposalResolver.Resolve(world, second);

            Assert.That(firstRecord.Passed, Is.False);
            Assert.That(firstRecord.Votes.Select(vote => vote.Choice), Is.EqualTo(secondRecord.Votes.Select(vote => vote.Choice)));
        }

        [Test]
        public void LobbyAndPressure_InfluenceVoteAndPressureDamagesRelationship()
        {
            var world = CreateCouncilWorld(4);
            var seat = world.Council.Seats.First(item => !item.IsPlayer);
            seat.Position = new AxisPosition(-40, -40, -40);
            var proposal = NewProposal(1, ProposalThreshold.SimpleMajority, new AxisPosition(40, 40, 40));
            var before = ProposalResolver.ResolveNpcVote(world, seat, proposal);
            var relationship = seat.Relationship;
            var pressure = new PressureSeatCommand { SeatId = seat.Id, PressureAmount = 300 };
            pressure.Apply(world, new EventBuffer());
            var after = ProposalResolver.ResolveNpcVote(world, seat, proposal);

            Assert.That(before, Is.EqualTo(VoteChoice.Oppose));
            Assert.That(after, Is.EqualTo(VoteChoice.Support));
            Assert.That(seat.Relationship, Is.LessThan(relationship));
        }

        [Test]
        public void SeatReplacement_IsDeterministicAndRespondsToWorldState()
        {
            var first = CreateCouncilWorld(5);
            var second = CreateCouncilWorld(5);
            first.Veil.ByContinent = Enumerable.Repeat(2500, 7).ToArray();
            second.Veil.ByContinent = Enumerable.Repeat(2500, 7).ToArray();
            first.Veil.RecalculateGlobal();
            second.Veil.RecalculateGlobal();
            var seatId = first.Council.Seats.First(seat => !seat.IsPlayer).Id;

            var a = SeatReplacementService.Replace(first, seatId, 4);
            var b = SeatReplacementService.Replace(second, seatId, 4);

            Assert.That(a.Position, Is.EqualTo(b.Position));
            Assert.That(a.Position.Containment, Is.LessThan(0));
            Assert.That(a.Position.VeilPolicy, Is.LessThan(0));
        }

        [Test]
        public void AlphaOne_IsTenTimesMtfAndRequiresThreeFundedCyclesToRebuild()
        {
            Assert.That(EconomyRules.AlphaOneMaintenanceCost, Is.EqualTo(EconomyRules.MobileTaskForceUnitCost * 10));
            var world = CreateCouncilWorld(8);
            world.Economy.Budget.AlphaOne = 0;
            MonthlySettlementService.Settle(world);
            Assert.That(world.Council.AlphaOne.IsActive, Is.False);
            world.Economy.Budget.AlphaOne = EconomyRules.AlphaOneMaintenanceCost;
            MonthlySettlementService.Settle(world);
            MonthlySettlementService.Settle(world);
            Assert.That(world.Council.AlphaOne.IsActive, Is.False);
            MonthlySettlementService.Settle(world);
            Assert.That(world.Council.AlphaOne.IsActive, Is.True);
        }

        [Test]
        public void WorldRandomHelpers_WriteBackMutableRandomState()
        {
            var world = CreateCouncilWorld(15);
            var before0 = world.Random.State0;
            var before1 = world.Random.State1;

            // 中文：使用非边界概率，确保 Chance 实际消耗一个随机值；0 与 10000 按设计无需推进状态。
            // English: A non-boundary probability ensures Chance consumes a random value; zero and ten thousand intentionally require no state advance.
            world.RandomChance(5000);
            Assert.That(world.Random.State0 == before0 && world.Random.State1 == before1, Is.False);

            var nextBefore0 = world.Random.State0;
            var nextBefore1 = world.Random.State1;
            world.NextRandomInt(0, 100);
            Assert.That(world.Random.State0 == nextBefore0 && world.Random.State1 == nextBefore1, Is.False);
        }

        [Test]
        public void RejectedProposal_IsLockedForThreeCyclesAndAmendmentIsAllowed()
        {
            var world = CreateCouncilWorld(16);
            var position = new AxisPosition(100, 100, 100);
            foreach (var seat in world.Council.Seats.Where(seat => !seat.IsPlayer))
            {
                seat.Position = new AxisPosition(-100, -100, -100);
            }

            var proposal = NewProposal(1, ProposalThreshold.SimpleMajority, position);
            world.Council.Proposals = new[] { proposal };
            ProposalResolver.Resolve(world, proposal);
            var query = new WorldQuery(world, ClearanceLevel.Level5);

            Assert.That(query.IsProposalCoolingDown(ProposalKind.Task, position), Is.True);
            world.Council.CurrentCycle += 3;
            Assert.That(query.IsProposalCoolingDown(ProposalKind.Task, position), Is.False);
            Assert.That(query.IsProposalCoolingDown(ProposalKind.Task, new AxisPosition(99, 100, 100)), Is.False);
        }

        [Test]
        public void ContainmentRisk_RespondsToStabilityBudgetAndTraits()
        {
            var world = CreateCouncilWorld(17);
            var site = new SiteState { Id = new SiteId(1), TrueStability = 9000, IsOperational = true };
            var calm = new AnomalyInstance
            {
                SiteId = site.Id,
                Stability = 9000,
                Definition = new ScpDefinition
                {
                    Class = ObjectClass.Safe,
                    Requirement = new ContainmentRequirement { MonthlyCost = 100 }
                }
            };
            world.Sites = new[] { site };
            world.Economy.Budget.SiteOperations = 1000;
            var baseline = ContainmentRiskService.CalculatePerTickRisk(world, site, calm);
            site.TrueStability = 1000;
            world.Economy.Budget.SiteOperations = 0;
            calm.Stability = 1000;
            calm.Definition.Class = ObjectClass.Keter;
            calm.Definition.Traits = new[] { new TraitInstance { Trait = ScpTrait.ContEscalating } };

            Assert.That(ContainmentRiskService.CalculatePerTickRisk(world, site, calm), Is.GreaterThan(baseline));
        }

        [Test]
        public void SiteReport_DistortionBenefitsReporterAndAuditShowsTruth()
        {
            var world = CreateCouncilWorld(9);
            world.Sites = new[] { new SiteState { Id = new SiteId(1), TrueStability = 5000, TrueCasualties = 20, ReportCredibility = 2000 } };
            MonthlySettlementService.Settle(world);
            var site = world.Sites[0];
            Assert.That(site.ReportedStability, Is.GreaterThanOrEqualTo(site.TrueStability));
            Assert.That(site.ReportedCasualties, Is.LessThanOrEqualTo(site.TrueCasualties));
            site.AuditCyclesRemaining = 1;
            MonthlySettlementService.Settle(world);
            Assert.That(site.ReportedStability, Is.EqualTo(site.TrueStability));
            Assert.That(site.ReportedCasualties, Is.EqualTo(site.TrueCasualties));
        }

        [Test]
        public void Veil_ComputesWeightedTotalSpreadsAndFailsAtThreeCriticalContinents()
        {
            var veil = new VeilState { ByContinent = new[] { 1000, 1000, 1000, 9000, 9000, 9000, 9000 } };
            veil.RecalculateGlobal();
            Assert.That(veil.Global, Is.GreaterThan(0));
            Assert.That(veil.HasFailed(), Is.True);
            var world = CreateCouncilWorld(10);
            world.Veil.ByContinent = new[] { 3000, 9000, 9000, 9000, 9000, 9000, 9000 };
            var neighborBefore = world.Veil.ByContinent[1];
            MonthlySettlementService.Settle(world);
            Assert.That(world.Veil.ByContinent[1], Is.LessThan(neighborBefore));
        }

        [Test]
        public void FiscalAndEthicsFailures_AreIndependentFromPublicImpeachment()
        {
            var fiscal = CreateCouncilWorld(11);
            // 中文：财政崩溃夹具显式关闭四类收入，避免集中临时基线掩盖本测试要验证的连续支付失败。English: The fiscal-collapse fixture explicitly disables all four channels so provisional baseline income cannot mask the consecutive insolvency under test.
            fiscal.Economy.FundingChannels = new[]{new FundingChannelState(),new FundingChannelState(),new FundingChannelState(),new FundingChannelState()};
            fiscal.Economy.Budget.SiteOperations = 2000000;
            fiscal.Economy.EmergencyReserveBalance = 0;
            MonthlySettlementService.Settle(fiscal);
            MonthlySettlementService.Settle(fiscal);
            MonthlySettlementService.Settle(fiscal);
            Assert.That(fiscal.Failure.EndReason, Is.EqualTo(GameEndReason.FiscalCollapse));

            var ethics = CreateCouncilWorld(12);
            ethics.Failure.HiddenEthicsRemovalRisk = 100;
            MonthlySettlementService.Settle(ethics);
            Assert.That(ethics.Failure.EndReason, Is.EqualTo(GameEndReason.EthicsRemoval));

            var impeachment = CreateCouncilWorld(13);
            foreach (var seat in impeachment.Council.Seats.Where(seat => !seat.IsPlayer).Take(6))
            {
                seat.Relationship = -50;
            }
            MonthlySettlementService.Settle(impeachment);
            Assert.That(impeachment.Council.ImpeachmentWarning, Is.True);
            MonthlySettlementService.Settle(impeachment);
            Assert.That(impeachment.Council.Proposals.Any(proposal => proposal.Kind == ProposalKind.Impeachment), Is.True);
        }

        [Test]
        public void StrategicCapabilities_ControlRestartAndAmnesticAvailability()
        {
            var world = CreateCouncilWorld(14);
            world.Anomalies = new[]
            {
                new AnomalyInstance
                {
                    Definition = new ScpDefinition { Capabilities = new[] { StrategicCapability.WorldReconstruction } }
                },
                new AnomalyInstance
                {
                    Definition = new ScpDefinition { Capabilities = new[] { StrategicCapability.AmnesticSupply } }
                }
            };
            var query = new WorldQuery(world, ClearanceLevel.Level5);
            var command = new SubmitProposalCommand { Kind = ProposalKind.WorldRestart, Threshold = ProposalThreshold.Unanimous };
            Assert.That(command.Validate(query).IsValid, Is.True);
            Assert.That(StrategicCapabilityService.IsAvailable(world, StrategicCapability.AmnesticSupply), Is.True);
            world.Anomalies[0].IsContained = false;
            world.Anomalies[1].IsFacilityIntact = false;
            Assert.That(command.Validate(query).IsValid, Is.False);
            Assert.That(StrategicCapabilityService.IsAvailable(world, StrategicCapability.AmnesticSupply), Is.False);
        }

        /// <summary>中文：覆盖单条附条件审批的严格解析、结构化持久化和业务事件。English: Covers strict parsing, structured persistence, and the business event for one conditional approval.</summary>
        [Test]
        public void ReportApproval_ConditionalDecisionPersistsParsedConditionsAndEvent()
        {
            var world = CreateCouncilWorld(21);
            world.Reports = ReportGenerationService.CreateInitial(0);
            world.NextReportSequence = world.Reports.Length;
            var command = new ReportApprovalCommand { ReportIds = new[] { "RPT-000001" }, Decision = ReportStatus.ConditionallyApproved, Conditions = "deadline_cycles=2;budget_cap=50000;audit_required=true" };
            var simulation = new WorldSimulation(world, ClearanceLevel.Level5);

            TickResult result = simulation.Tick(new ICommand[] { command });

            Assert.That(world.Reports[0].Status, Is.EqualTo(ReportStatus.ConditionallyApproved));
            Assert.That(world.ReportApprovals.Single().BudgetCap, Is.EqualTo(50000));
            Assert.That(world.ReportApprovals.Single().DeadlineCycles, Is.EqualTo(2));
            Assert.That(world.ReportApprovals.Single().AuditRequired, Is.True);
            Assert.That(result.Events.Any(item => item.Kind == DomainEventKind.ReportDecision), Is.True);
        }

        /// <summary>中文：验证非法条件和混类/高风险批量均原子拒绝。English: Verifies invalid conditions and mixed/high-risk batches are rejected atomically.</summary>
        [Test]
        public void ReportApproval_InvalidConditionsAndInvalidBatchDoNotMutateReports()
        {
            var world = CreateCouncilWorld(22);
            world.Reports = new[]
            {
                new ReportState { Id = "A", Category = ReportCategory.Facility, Risk = ReportRisk.Low, AllowsBatch = true },
                new ReportState { Id = "B", Category = ReportCategory.Anomaly, Risk = ReportRisk.High, AllowsBatch = false }
            };
            var query = new WorldQuery(world, ClearanceLevel.Level5);
            var malformed = new ReportApprovalCommand { ReportIds = new[] { "A" }, Decision = ReportStatus.ConditionallyApproved, Conditions = "unknown=1" };
            var batch = new ReportApprovalCommand { ReportIds = new[] { "A", "B" }, Decision = ReportStatus.Approved };

            Assert.That(malformed.Validate(query).IsValid, Is.False);
            Assert.That(batch.Validate(query).IsValid, Is.False);
            new WorldSimulation(world, ClearanceLevel.Level5).Tick(new ICommand[] { batch });
            Assert.That(world.Reports.All(item => item.Status == ReportStatus.Pending), Is.True);
            Assert.That(world.ReportApprovals, Is.Empty);
        }

        /// <summary>中文：月结算只补两份并保持 ID 单调稳定。English: Monthly settlement adds only two reports and keeps IDs stable and monotonic.</summary>
        [Test]
        public void MonthlySettlement_SupplementsAtMostTwoStableReports()
        {
            var world = CreateCouncilWorld(23);
            world.Reports = ReportGenerationService.CreateInitial(0);
            world.NextReportSequence = world.Reports.Length;

            MonthlySettlementService.Settle(world);

            Assert.That(world.Reports, Has.Length.EqualTo(6));
            Assert.That(world.Reports[4].Id, Is.EqualTo("RPT-000005"));
            Assert.That(world.Reports[5].Id, Is.EqualTo("RPT-000006"));
        }

        /// <summary>中文：验证四渠道同时结算且九个一级科目各计费一次，旧储备兼容值不收费。English: Verifies all four channels settle together, nine primary categories are charged once, and the legacy reserve value is not billed.</summary>
        [Test]
        public void FinanceSettlement_SumsFourChannelsAndTenPrimaryBudgets()
        {
            WorldState world=CreateCouncilWorld(31);world.Funds=1_000_000;world.Economy.FundingChannels=new[]{new FundingChannelState{Income=100,FixedCost=10},new FundingChannelState{Income=200,FixedCost=20},new FundingChannelState{Income=300,FixedCost=30},new FundingChannelState{Income=400,FixedCost=40}};
            world.Economy.Budget=new BudgetState{SiteOperations=1,ContainmentMaintenance=2,Research=3,Security=4,MobileTaskForces=5,AlphaOne=6,VeilAndCover=7,AdministrationAndIntelligence=8,PersonnelAndEthics=9,EmergencyReserve=10,ResearchDetail=new ResearchBudgetDetail{BasicResearch=1000},SecurityDetail=new SecurityBudgetDetail{SiteSecurity=1000}};
            long flow=MonthlySettlementService.Settle(world);
            Assert.That(flow,Is.EqualTo(900-45));Assert.That(world.Economy.CycleHistory,Has.Length.EqualTo(1));
        }

        /// <summary>中文：验证集中亿元级临时基线按四渠道净收入与十项一级预算各结算一次，研究、安全及七洲明细不重复收费。English: Verifies the centralized global-scale baseline settles four-channel net income and ten primary budgets exactly once without rebilling research, security, or continent detail.</summary>
        [Test]
        public void FinanceSettlement_GlobalBaselineProducesReadableBillionScaleFlow()
        {
            WorldState world=CreateCouncilWorld(311);world.Funds=EconomyRules.TemporaryStartingAvailableCash;world.Economy.FundingChannels=EconomyRules.CreateTemporaryFundingChannels();world.Economy.Budget=EconomyRules.CreateTemporaryPrimaryBudget();
            long income=EconomyRules.ParallelNetIncome(world.Economy.FundingChannels);long expenses=world.Economy.Budget.TotalSpending();long flow=MonthlySettlementService.Settle(world);
            Assert.That(income,Is.EqualTo(121_000_000_000L));Assert.That(expenses,Is.EqualTo(118_000_000_000L));Assert.That(world.Economy.Budget.NecessaryMonthlySpending(),Is.EqualTo(104_000_000_000L));Assert.That(flow,Is.EqualTo(3_000_000_000L));Assert.That(world.Economy.LastExpenses,Is.EqualTo(expenses));
        }

        /// <summary>中文：现金缺口由独立储备按上限覆盖；总资产守恒、经营净流量保持原始负值、期末现金和永久历史使用覆盖后口径。English: The independent reserve covers a cash gap up to its limit; total assets are conserved, operating flow remains negative, and closing cash plus permanent history use the post-cover basis.</summary>
        [Test]
        public void FinanceSettlement_ReserveDrawPreservesAssetsAndRecordsCoveredClosingCash()
        {
            WorldState world=CreateCouncilWorld(312);world.Funds=10;world.Economy.TotalAssets=1000;world.Economy.EmergencyReserveBalance=40;world.Economy.FundingChannels=new[]{new FundingChannelState(),new FundingChannelState(),new FundingChannelState(),new FundingChannelState()};world.Economy.Budget=new BudgetState{SiteOperations=50};world.Tick=72;
            long flow=MonthlySettlementService.Settle(world);
            Assert.Multiple(()=>{Assert.That(flow,Is.EqualTo(-50));Assert.That(world.Funds,Is.Zero);Assert.That(world.Economy.EmergencyReserveBalance,Is.Zero);Assert.That(world.Economy.TotalAssets,Is.EqualTo(1000));Assert.That(world.Economy.CycleHistory.Single().ClosingCash,Is.Zero);});FiscalHistoryRecord draw=world.Economy.FiscalHistory.Single(item=>item.Kind=="EmergencyReserveDraw");Assert.That(draw.Amount,Is.EqualTo(40));Assert.That(draw.Tick,Is.EqualTo(72));Assert.That(draw.Cycle,Is.EqualTo(1));
        }

        /// <summary>中文：验证草案保存不会改变正式预算，统一签发后才生效并记录预算与未处理抚恤拖延。English: Verifies draft save leaves enacted budget unchanged, while unified signing enacts it and records both budget and unresolved-compensation delay.</summary>
        [Test]
        public void FinanceDraft_OnlyEnactsOnSignAndRecordsDelay()
        {
            WorldState world=CreateCouncilWorld(32);world.Tick=48;world.Council.CurrentCycle=2;world.Economy.Budget=new BudgetState{SiteOperations=10};world.Economy.CompensationIncidents=new[]{new CompensationIncidentState{IncidentId="I",Status=CompensationStatus.Pending,Personnel=new[]{new FallenPersonnelCompensation{PersonnelId="P"}}}};
            var draft=new BudgetState{SiteOperations=99,ContainmentMaintenance=1,Research=1,Security=1,MobileTaskForces=1,AlphaOne=1,VeilAndCover=1,AdministrationAndIntelligence=1,PersonnelAndEthics=1,EmergencyReserve=1};new SaveBudgetDraftCommand{Budget=draft}.Apply(world,new EventBuffer());
            Assert.That(world.Economy.Budget.SiteOperations,Is.EqualTo(10));Assert.That(world.Economy.DraftRecordedTick,Is.EqualTo(48));Assert.That(world.Economy.DraftRecordedCycle,Is.EqualTo(2));new SignBudgetCommand().Apply(world,new EventBuffer());
            Assert.That(world.Economy.Budget.SiteOperations,Is.EqualTo(99));Assert.That(world.Economy.CompensationIncidents[0].Status,Is.EqualTo(CompensationStatus.Delayed));Assert.That(world.Economy.FiscalHistory.Any(item=>item.Kind=="BudgetSigned"),Is.True);
        }

        /// <summary>中文：验证逐人支付与明确拒绝都成为可读取的永久历史，并且支付精确扣减现金。English: Verifies per-person payment and explicit refusal become readable permanent history and payment deducts exact cash.</summary>
        [Test]
        public void Compensation_PaymentAndRefusalEnterHistory()
        {
            WorldState world=CreateCouncilWorld(33);world.Funds=1000;world.Economy.CompensationIncidents=new[]{new CompensationIncidentState{IncidentId="PAY",Personnel=new[]{new FallenPersonnelCompensation{PersonnelId="A",Amount=100},new FallenPersonnelCompensation{PersonnelId="B",Amount=200}}},new CompensationIncidentState{IncidentId="REF",Personnel=new[]{new FallenPersonnelCompensation{PersonnelId="C"}}}};
            new PayCompensationCommand{IncidentId="PAY"}.Apply(world,new EventBuffer());new DecideCompensationCommand{IncidentId="REF",Decision=CompensationStatus.Refused}.Apply(world,new EventBuffer());
            Assert.That(world.Funds,Is.EqualTo(700));Assert.That(world.Economy.FiscalHistory.Count(item=>item.Kind=="CompensationPaid"),Is.EqualTo(2));Assert.That(world.Economy.FiscalHistory.Any(item=>item.SubjectId=="REF"&&item.Decision=="Refused"),Is.True);
        }

        [Test]
        public void VeilIncident_ProgressesDeterministicallyAndActionIsRecorded()
        {
            var first = CreateCouncilWorld(88); var second = CreateCouncilWorld(88);
            first.VeilIncidents = new[] { DemoIncident() }; second.VeilIncidents = new[] { DemoIncident() };
            var command = new VeilIncidentActionCommand { IncidentId = "VEIL-TEST-0001", Action = VeilActionKind.Investigate };
            var firstResult = new WorldSimulation(first, ClearanceLevel.Level5).Tick(new ICommand[] { command });
            var secondResult = new WorldSimulation(second, ClearanceLevel.Level5).Tick(new ICommand[] { new VeilIncidentActionCommand { IncidentId = command.IncidentId, Action = command.Action } });
            Assert.That(first.VeilIncidents[0].LocationPrecision, Is.EqualTo(VeilLocationPrecision.Approximate));
            Assert.That(first.VeilIncidents[0].Dispositions, Has.Length.EqualTo(2));
            Assert.That(firstResult.Events.Any(item => item.Kind == DomainEventKind.VeilIncidentChanged), Is.True);
            Assert.That(JsonConvert.SerializeObject(first), Is.EqualTo(JsonConvert.SerializeObject(second)));
            Assert.That(secondResult.Events.Any(item => item.Kind == DomainEventKind.VeilIncidentChanged), Is.True);
        }

        [Test]
        public void VeilIncident_SaveRoundTripPreservesNodesTimelineAndPrecision()
        {
            var world = CreateCouncilWorld(89); world.VeilIncidents = new[] { DemoIncident() };
            var service = new SaveService(); var loaded = service.Deserialize(service.Serialize(new SaveFile { World = world, WorldFacts = world.Facts }));
            Assert.That(loaded.World.VeilIncidents[0].StableId, Is.EqualTo("VEIL-TEST-0001"));
            Assert.That(loaded.World.VeilIncidents[0].PropagationNodes[0].LocationPrecision, Is.EqualTo(VeilLocationPrecision.ContinentOnly));
            Assert.That(loaded.World.VeilIncidents[0].Dispositions, Has.Length.EqualTo(1));
        }

        private static VeilIncidentState DemoIncident() => new VeilIncidentState
        {
            StableId = "VEIL-TEST-0001", AnonymousTitle = "匿名测试事件", SourceCategory = "测试来源", OriginContinent = Continent.Asia,
            LocationPrecision = VeilLocationPrecision.ContinentOnly, Severity = 2000, CurrentStage = VeilIncidentStage.ClueBacklog,
            PropagationNodes = new[] { new VeilPropagationNode { StableId = "VEIL-TEST-0001-NODE-00", Continent = Continent.Asia, LocationPrecision = VeilLocationPrecision.ContinentOnly } },
            Dispositions = new[] { new VeilDispositionRecord { StableId = "VEIL-TEST-0001-REC-0000", Action = VeilActionKind.Monitor } }, NextRecordSequence = 1
        };

        private static WorldState CreateCouncilWorld(ulong seed)
        {
            var random = new DeterministicRandom(seed);
            var council = CouncilFactory.Create(ref random);
            return new WorldState
            {
                Funds = 10000000,
                Random = random,
                Council = council,
                EthicsScore = 20,
                // 中文：测试夹具使用最高稳定资金来源，使议会与 Alpha-1 测试不会被无关的默认财政赤字提前终止。
                // English: The fixture uses the highest stable funding source so council and Alpha-1 tests are not terminated by unrelated default fiscal deficits.
                Economy = new EconomyState
                {
                    FundingSource = FundingSource.GreyMarket,
                    Budget = new BudgetState
                    {
                        AlphaOne = EconomyRules.AlphaOneMaintenanceCost,
                        VeilOperations = Enumerable.Repeat(100000L, 7).ToArray()
                    }
                }
            };
        }

        private static ProposalState NewProposal(int id, ProposalThreshold threshold, AxisPosition position)
        {
            return new ProposalState
            {
                ProposalId = id,
                Kind = ProposalKind.Task,
                Threshold = threshold,
                Position = position,
                PlayerVote = VoteChoice.Support
            };
        }
    }
}
