using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Scp.Application;
using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application.Tests
{
    public sealed class M1ApplicationTests
    {
        [Test]
        public void ContentLoader_LoadsTwentyUniqueValidDefinitionsAndCapabilitiesFromData()
        {
            var directory = ContentPathResolver.FindScpDirectory(TestContext.CurrentContext.TestDirectory);
            var definitions = new ScpContentLoader().LoadDirectory(directory);

            Assert.That(definitions, Has.Length.EqualTo(20));
            Assert.That(definitions.Select(definition => definition.Id).Distinct().Count(), Is.EqualTo(20));
            Assert.That(definitions.Count(definition => definition.Capabilities.Contains(StrategicCapability.WorldReconstruction)), Is.EqualTo(1));
            Assert.That(definitions.Count(definition => definition.Capabilities.Contains(StrategicCapability.AmnesticSupply)), Is.EqualTo(1));
        }

        [Test]
        public void OverseerProjection_DoesNotExposeHiddenAxesRelationshipsOrPressure()
        {
            var world = CreateWorld();
            var view = new OverseerPerspective().Project<OverseerViewModel>(world);
            var json = JsonConvert.SerializeObject(view);

            Assert.That(view.Seats, Has.Length.EqualTo(13));
            Assert.That(json, Does.Not.Contain("Position"));
            // 中文：财政渠道公开投影合法包含关系和渠道名称；这里仅继续禁止 O5 席位的隐藏压力与立场字段。English: Public finance channels legitimately contain relationship and channel names; this assertion continues to forbid only hidden O5-seat pressure and axis fields.
            Assert.That(json, Does.Not.Contain("Pressure"));
        }

        [Test]
        public void OverseerProjection_IsDetachedFromWorldState()
        {
            var world = CreateWorld();
            world.Economy.Budget = new BudgetState
            {
                SiteOperations = 10,
                Research = 20,
                Security = 30,
                MobileTaskForces = 40,
                AlphaOne = 50,
                EmergencyReserve = 60,
                VeilOperations = new long[] { 1, 2, 3, 4, 5, 6, 7 }
            };
            world.Council.VoteRecords = new[]
            {
                new VoteRecord
                {
                    ProposalId = 11,
                    Kind = ProposalKind.Budget,
                    Threshold = ProposalThreshold.SimpleMajority,
                    Cycle = 2,
                    Passed = true,
                    Votes = new[]
                    {
                        new SeatVoteRecord { SeatId = new SeatId(1), Choice = VoteChoice.Support }
                    }
                }
            };
            world.Council.Proposals = new[]
            {
                new ProposalState
                {
                    ProposalId = 12,
                    Kind = ProposalKind.Experiment,
                    Threshold = ProposalThreshold.TwoThirds,
                    Position = new AxisPosition(1, 2, 3),
                    SubmittedBy = new SeatId(2),
                    SubmittedCycle = 3,
                    ResolveCycle = 4,
                    PlayerVote = VoteChoice.Oppose,
                    IsResolved = true,
                    Passed = false
                }
            };
            world.Council.AlphaOne.IsActive = true;
            world.Council.AlphaOne.LastResult = "ready";
            world.Failure.IsEnded = false;
            world.Failure.EndReason = GameEndReason.None;
            world.Failure.HiddenEthicsRemovalRisk = 77;

            var view = new OverseerPerspective().Project<OverseerViewModel>(world);

            view.Budget.SiteOperations = 999;
            view.Budget.VeilOperations[0] = 999;
            view.VoteRecords[0].Passed = false;
            view.VoteRecords[0].Votes[0].Choice = VoteChoice.Oppose;
            view.Proposals[0].Passed = true;
            view.Proposals[0].PlayerVote = VoteChoice.Support;
            view.AlphaOne.IsActive = false;
            view.AlphaOne.LastResult = "changed";
            view.Failure.IsEnded = true;
            view.Failure.EndReason = GameEndReason.EthicsRemoval;

            Assert.That(world.Economy.Budget.SiteOperations, Is.EqualTo(10));
            Assert.That(world.Economy.Budget.VeilOperations[0], Is.EqualTo(1));
            Assert.That(world.Council.VoteRecords[0].Passed, Is.True);
            Assert.That(world.Council.VoteRecords[0].Votes[0].Choice, Is.EqualTo(VoteChoice.Support));
            Assert.That(world.Council.Proposals[0].Passed, Is.False);
            Assert.That(world.Council.Proposals[0].PlayerVote, Is.EqualTo(VoteChoice.Oppose));
            Assert.That(world.Council.AlphaOne.IsActive, Is.True);
            Assert.That(world.Council.AlphaOne.LastResult, Is.EqualTo("ready"));
            Assert.That(world.Failure.IsEnded, Is.False);
            Assert.That(world.Failure.EndReason, Is.EqualTo(GameEndReason.None));
            Assert.That(world.Failure.HiddenEthicsRemovalRisk, Is.EqualTo(77));
        }

        [Test]
        public void OverseerProjection_ViewModelsDoNotExposeSensitiveState()
        {
            var publicTypes = new[]
            {
                typeof(OverseerViewModel),
                typeof(BudgetViewModel),
                typeof(VoteRecordViewModel),
                typeof(SeatVoteViewModel),
                typeof(ProposalViewModel),
                typeof(AlphaOneViewModel),
                typeof(FailureViewModel),
                typeof(CouncilSeatViewModel)
            };
            var propertyNames = publicTypes
                .SelectMany(type => type.GetProperties())
                .Select(property => property.Name)
                .ToArray();

            Assert.That(propertyNames, Does.Not.Contain("Position"));
            Assert.That(propertyNames, Does.Not.Contain("Relationship"));
            Assert.That(propertyNames, Does.Not.Contain("Pressure"));
            Assert.That(propertyNames, Does.Not.Contain("HiddenEthicsRemovalRisk"));
            // Tick 与周期属于公开信息，允许出现在 ViewModel 上，但必须是裸值类型而非世界状态引用。
            Assert.That(typeof(OverseerViewModel).GetProperty(nameof(OverseerViewModel.Tick))!.PropertyType, Is.EqualTo(typeof(long)));
            Assert.That(typeof(OverseerViewModel).GetProperty(nameof(OverseerViewModel.CurrentCycle))!.PropertyType, Is.EqualTo(typeof(int)));
            Assert.That(typeof(OverseerViewModel).GetProperty(nameof(OverseerViewModel.Budget))!.PropertyType, Is.EqualTo(typeof(BudgetViewModel)));
            Assert.That(typeof(OverseerViewModel).GetProperty(nameof(OverseerViewModel.VoteRecords))!.PropertyType, Is.EqualTo(typeof(VoteRecordViewModel[])));
            Assert.That(typeof(OverseerViewModel).GetProperty(nameof(OverseerViewModel.Proposals))!.PropertyType, Is.EqualTo(typeof(ProposalViewModel[])));
            Assert.That(typeof(OverseerViewModel).GetProperty(nameof(OverseerViewModel.AlphaOne))!.PropertyType, Is.EqualTo(typeof(AlphaOneViewModel)));
            Assert.That(typeof(OverseerViewModel).GetProperty(nameof(OverseerViewModel.Failure))!.PropertyType, Is.EqualTo(typeof(FailureViewModel)));
        }

        [Test]
        public void SaveV6_RoundTripPreservesM1State()
        {
            var service = new SaveService();
            var world = CreateWorld();
            world.Council.VoteRecords = new[]
            {
                new VoteRecord
                {
                    ProposalId = 1,
                    Kind = ProposalKind.Budget,
                    Threshold = ProposalThreshold.SimpleMajority,
                    Votes = new[] { new SeatVoteRecord { SeatId = new SeatId(1), Choice = VoteChoice.Support } }
                }
            };
            var json = service.Serialize(new SaveFile { World = world, WorldFacts = world.Facts });
            var loaded = service.Deserialize(json);

            Assert.That(loaded.SchemaVersion, Is.EqualTo(7));
            Assert.That(loaded.World.Council.Seats, Has.Length.EqualTo(13));
            Assert.That(loaded.World.Council.VoteRecords.Single().Votes.Single().Choice, Is.EqualTo(VoteChoice.Support));
            Assert.That(loaded.World.Veil.ByContinent, Is.EqualTo(world.Veil.ByContinent));
            Assert.That(json, Does.Not.Contain("$type"));
        }

        [Test]
        public void SaveV2_MigratesToV6AndOldM0ShapeLoads()
        {
            var json = "{\"schemaVersion\":2,\"mode\":\"Standalone\",\"world\":{\"schemaVersion\":2,\"funds\":10},\"worldFacts\":{\"knownFactKeys\":[\"legacy\"]},\"commandLog\":[]}";
            var loaded = new SaveService().Deserialize(json);

            Assert.That(loaded.SchemaVersion, Is.EqualTo(7));
            Assert.That(loaded.World.SchemaVersion, Is.EqualTo(7));
            Assert.That(loaded.BriefingAcknowledged, Is.True);
            Assert.That(loaded.Difficulty, Is.EqualTo(GameDifficulty.Unknown));
            Assert.That(loaded.SaveKind, Is.EqualTo(SaveKind.Unknown));
            Assert.That(loaded.World.Facts.KnownFactKeys, Is.EqualTo(new[] { "legacy" }));
            Assert.That(loaded.World.Economy, Is.Not.Null);
            Assert.That(loaded.World.Failure, Is.Not.Null);
        }

        [Test]
        public void EndedSession_CannotAdvanceAndEpilogueIsNotEmpty()
        {
            var world = CreateWorld();
            world.Failure.IsEnded = true;
            world.Failure.EndReason = GameEndReason.VeilCollapse;
            var session = new GameSession(world, new OverseerPerspective());

            Assert.Throws<InvalidOperationException>(() => session.Advance(1));
            Assert.That(new EpilogueService().Create(world), Is.Not.Empty);
        }

        [Test]
        public void AuditProjection_ShowsTruthWhileNormalReportUsesDistortion()
        {
            var world = CreateWorld();
            var site = world.Sites[0];
            site.TrueStability = 4000;
            site.ReportedStability = 7000;
            var perspective = new OverseerPerspective();
            Assert.That(perspective.Project<OverseerViewModel>(world).Sites[0].Stability, Is.EqualTo(7000));
            site.AuditCyclesRemaining = 1;
            Assert.That(perspective.Project<OverseerViewModel>(world).Sites[0].Stability, Is.EqualTo(4000));
        }

        [Test]
        public void CreateDemoWorld_WithSameDefinitions_ProducesDeterministicallyIdenticalWorld()
        {
            var directory = ContentPathResolver.FindScpDirectory(TestContext.CurrentContext.TestDirectory);
            var definitions = new ScpContentLoader().LoadDirectory(directory);
            var facilities = LoadFacilities();

            var first = OverseerScenarioFactory.CreateDemoWorld(definitions, facilities);
            var second = OverseerScenarioFactory.CreateDemoWorld(definitions, facilities);

            Assert.That(second.Tick, Is.EqualTo(first.Tick));
            Assert.That(second.Funds, Is.EqualTo(first.Funds));
            Assert.That(second.Random.State0, Is.EqualTo(first.Random.State0));
            Assert.That(second.Random.State1, Is.EqualTo(first.Random.State1));
            Assert.That(first.Council.Seats, Has.Length.EqualTo(13));
            Assert.That(first.Council.Seats.Count(seat => seat.IsPlayer), Is.EqualTo(1));
            Assert.That(second.Council.PlayerSeatId, Is.EqualTo(first.Council.PlayerSeatId));
            Assert.That(second.Sites.Select(site => site.Id), Is.EqualTo(first.Sites.Select(site => site.Id)));
            Assert.That(second.Anomalies, Has.Length.EqualTo(first.Anomalies.Length));
            Assert.That(
                second.Economy.Budget.TotalSpending(),
                Is.EqualTo(OverseerScenarioFactory.CreateSustainableBudget().TotalSpending()));
            // 中文：正式开局必须使用全部 89 项设施，并且构建顺序完全确定，因此两次结果的内部稳定 ID 序列必须逐项相同。
            // English: The official start must use all 89 facilities in a fully deterministic order, so both runs must produce the identical internal stable ID sequence.
            Assert.That(first.Sites, Has.Length.EqualTo(FacilityDataLoader.ExpectedFacilityCount));
            Assert.That(second.Sites.Select(site => site.InternalStableId), Is.EqualTo(first.Sites.Select(site => site.InternalStableId)));
        }

        /// <summary>
        /// 中文：验证正式 O5 开局构建的设施规模、SITE-45 双版本共享 canonical id、内部 ID 唯一、C 类排除项缺席，以及每项都带非空官方来源 URL。
        /// English: Verifies the official O5 start builds the confirmed facility scale, the SITE-45 pair sharing one canonical id, unique internal IDs, absence of the excluded C-section entries, and a non-empty official source URL on every entity.
        /// </summary>
        [Test]
        public void OfficialStart_BuildsConfirmedFacilityScaleWithSourceIntegrity()
        {
            var directory = ContentPathResolver.FindScpDirectory(TestContext.CurrentContext.TestDirectory);
            var definitions = new ScpContentLoader().LoadDirectory(directory);
            var world = OverseerScenarioFactory.CreateDemoWorld(definitions, LoadFacilities());

            Assert.That(world.Sites, Has.Length.EqualTo(89));
            Assert.That(world.Sites.Select(site => site.InternalStableId).Distinct().Count(), Is.EqualTo(89));
            Assert.That(world.Sites.Select(site => site.Id.Value).Distinct().Count(), Is.EqualTo(89));
            Assert.That(world.Sites.Count(site => site.CanonicalId == "SITE-45"), Is.EqualTo(2));
            Assert.That(
                world.Sites.Where(site => site.CanonicalId == "SITE-45").Select(site => site.Code).OrderBy(code => code, StringComparer.Ordinal),
                Is.EqualTo(new[] { "SITE-45-AU", "SITE-45-US" }));
            // 中文：C 类排除项（SITE-0、SITE-5、SITE-418、SITE-⌘）不得出现在正式世界。
            // English: The excluded C-section entries (SITE-0, SITE-5, SITE-418, SITE-⌘) must never appear in the official world.
            Assert.That(world.Sites.Select(site => site.CanonicalId), Is.Not.AnyOf("SITE-0", "SITE-5", "SITE-418", "SITE-⌘"));
            Assert.That(world.Sites.All(site => site.EnUrl.Length > 0), Is.True);
            Assert.That(world.Sites.All(site => site.DisplayLabel.Length > 0 && site.Code.Length > 0 && site.FacilityType.Length > 0), Is.True);
        }

        /// <summary>
        /// 中文：验证位置精度分级不会被地图坐标伪造。保密、未知与非地球设施必须没有地图点，落图设施必须标记为项目级近似。
        /// English: Verifies precision tiers are never faked by map coordinates. Redacted, unknown, and non-terrestrial facilities must carry no map point, and every mapped facility must be flagged as a project approximation.
        /// </summary>
        [Test]
        public void OfficialStart_KeepsLocationPrecisionHonestOnTheMap()
        {
            var directory = ContentPathResolver.FindScpDirectory(TestContext.CurrentContext.TestDirectory);
            var definitions = new ScpContentLoader().LoadDirectory(directory);
            var world = OverseerScenarioFactory.CreateDemoWorld(definitions, LoadFacilities());

            foreach (var site in world.Sites)
            {
                bool mapped = site.MapX > 0 && site.MapY > 0;
                Assert.That(mapped, Is.EqualTo(site.IsMapApproximate), site.InternalStableId);
                if (site.LocationPrecision is SiteLocationPrecision.Unknown or SiteLocationPrecision.Deleted or SiteLocationPrecision.NonTerrestrial)
                {
                    Assert.That(mapped, Is.False, site.InternalStableId);
                }

                Assert.That(site.LocationPrecision, Is.Not.EqualTo(SiteLocationPrecision.Exact), site.InternalStableId);
                Assert.That(site.IsNonTerrestrial, Is.EqualTo(site.LocationPrecision == SiteLocationPrecision.NonTerrestrial), site.InternalStableId);
            }

            Assert.That(world.Sites.Count(site => site.IsNonTerrestrial), Is.EqualTo(2));
        }

        /// <summary>
        /// 中文：验证 89 项设施在存档往返后保持 SiteId、内部稳定 ID、canonical id 与位置精度，且审计命令仍能按 SiteId 唯一定位到 SITE-45 的两个版本。
        /// English: Verifies all 89 facilities survive a save round trip with SiteId, internal stable ID, canonical id, and precision intact, and that audit commands still resolve each SITE-45 version uniquely by SiteId.
        /// </summary>
        [Test]
        public void OfficialStart_SurvivesSaveRoundTripWithoutIdCollision()
        {
            var directory = ContentPathResolver.FindScpDirectory(TestContext.CurrentContext.TestDirectory);
            var definitions = new ScpContentLoader().LoadDirectory(directory);
            var world = OverseerScenarioFactory.CreateDemoWorld(definitions, LoadFacilities());
            var service = new SaveService();

            var loaded = service.Deserialize(service.Serialize(new SaveFile { World = world, WorldFacts = world.Facts }));

            Assert.That(loaded.World.Sites, Has.Length.EqualTo(89));
            Assert.That(loaded.World.Sites.Select(site => site.Id.Value), Is.EqualTo(world.Sites.Select(site => site.Id.Value)));
            Assert.That(loaded.World.Sites.Select(site => site.InternalStableId), Is.EqualTo(world.Sites.Select(site => site.InternalStableId)));
            Assert.That(loaded.World.Sites.Select(site => site.CanonicalId), Is.EqualTo(world.Sites.Select(site => site.CanonicalId)));
            Assert.That(loaded.World.Sites.Select(site => site.LocationPrecision), Is.EqualTo(world.Sites.Select(site => site.LocationPrecision)));

            var query = new WorldQuery(loaded.World, ClearanceLevel.Level5);
            foreach (var site in loaded.World.Sites.Where(site => site.CanonicalId == "SITE-45"))
            {
                Assert.That(new AuditSiteCommand { SiteId = site.Id }.Validate(query).IsValid, Is.True, site.InternalStableId);
                Assert.That(loaded.World.Sites.Count(other => other.Id == site.Id), Is.EqualTo(1), site.InternalStableId);
            }
        }

        /// <summary>中文：从仓库唯一正式设施目录加载并验证 89 项数据，供各测试共用同一份输入。English: Loads and validates the 89 entities from the repository's single official facility catalogue so all tests share one input.</summary>
        private static FacilityDefinition[] LoadFacilities()
        {
            return new FacilityDataLoader().LoadFile(ContentPathResolver.FindFacilityFile(TestContext.CurrentContext.TestDirectory));
        }

        /// <summary>
        /// 中文：验证 schema v6 从 v5 确定性补齐待执行队列、检查点和尾声，而不伪造终局内容。
        /// English: Verifies deterministic v5-to-v6 migration adds pending commands, checkpoint and epilogue without fabricating an ending.
        /// </summary>
        [Test]
        public void SaveV5_MigratesToV6SessionLoopDefaults()
        {
            var source = Newtonsoft.Json.Linq.JObject.Parse("{\"schemaVersion\":5,\"world\":{\"schemaVersion\":5},\"worldFacts\":{},\"commandLog\":[]}");
            var migrated = new SaveMigrationV5ToV6().Migrate(source);

            Assert.That(migrated.Value<int>("schemaVersion"), Is.EqualTo(6));
            Assert.That(migrated["pendingCommands"], Is.TypeOf<Newtonsoft.Json.Linq.JArray>());
            Assert.That(migrated["checkpoint"]!["reason"]!.ToObject<string>(), Is.EqualTo("None"));
            Assert.That(migrated["epilogue"]!["isAvailable"]!.ToObject<bool>(), Is.False);
        }

        /// <summary>
        /// 中文：覆盖当前全部 M1 命令的参数往返，防止新增命令降级为类型名占位或恢复时丢参。
        /// English: Covers parameter round trips for every current M1 command so no command degrades to a type-name placeholder or loses arguments during restoration.
        /// </summary>
        [Test]
        public void CommandLogCodec_RoundTripsAllCurrentM1Commands()
        {
            ICommand[] commands =
            {
                new AdjustFundsCommand { Amount = 17, RequiredClearance = ClearanceLevel.Level4 },
                new AllocateBudgetCommand { Budget = new BudgetState { SiteOperations = 11, VeilOperations = new long[] { 1, 2, 3, 4, 5, 6, 7 } } },
                new SelectFundingSourceCommand { Source = FundingSource.GreyMarket },
                new SubmitProposalCommand { Kind = ProposalKind.Experiment, Threshold = ProposalThreshold.TwoThirds, Position = new AxisPosition(1, 2, 3) },
                new CastPlayerVoteCommand { ProposalId = 9, Choice = VoteChoice.Oppose },
                new LobbySeatCommand { SeatId = new SeatId(2), SupportBonus = 33, ExchangeSupport = true },
                new PressureSeatCommand { SeatId = new SeatId(3), PressureAmount = 44 },
                new AuditSiteCommand { SiteId = new SiteId(7), Cost = 55 },
                new DirectAnomalyContactCommand(),
                new UsePrivilegeCommand { EmergencyAction = ProposalKind.AlphaOneDeployment },
                new TerminatePersonnelCommand { Count = 6 },
                new ReportApprovalCommand { ReportIds = new[] { "RPT-000001", "RPT-000002" }, Decision = ReportStatus.ConditionallyApproved, Conditions = "budget_cap=50" }
            };

            CommandLogEntry[] entries = commands.Select(command => CommandLogCodec.Encode(command, 12)).ToArray();
            ICommand[] decoded = entries.Select(CommandLogCodec.Decode).ToArray();

            Assert.That(entries.Select(entry => entry.Kind).Distinct().Count(), Is.EqualTo(commands.Length));
            Assert.That(decoded.Select(command => command.GetType()), Is.EqualTo(commands.Select(command => command.GetType())));
            Assert.That(((AllocateBudgetCommand)decoded[1]).Budget.VeilOperations, Is.EqualTo(new long[] { 1, 2, 3, 4, 5, 6, 7 }));
            Assert.That(((SubmitProposalCommand)decoded[3]).Position, Is.EqualTo(new AxisPosition(1, 2, 3)));
            Assert.That(((LobbySeatCommand)decoded[5]).ExchangeSupport, Is.True);
            Assert.That(((AuditSiteCommand)decoded[7]).Cost, Is.EqualTo(55));
            Assert.That(((ReportApprovalCommand)decoded[11]).ReportIds, Is.EqualTo(new[] { "RPT-000001", "RPT-000002" }));
            Assert.That(((ReportApprovalCommand)decoded[11]).Conditions, Is.EqualTo("budget_cap=50"));
        }

        /// <summary>
        /// 中文：验证帷幕提交边界只拒绝同事件、同动作、同一世界 Tick 的第二次点击；不同动作和 Tick 推进后的同动作仍按原命令链路进入队列并各执行一次。
        /// English: Verifies the veil submission boundary rejects only a second click with the same incident, action, and world tick; a different action and the same action after tick advancement still enter the existing command path and execute once each.
        /// </summary>
        [Test]
        public void VeilIncidentSubmission_DeduplicatesOnlySameActionAtSameTick()
        {
            WorldState world = CreateWorld();
            world.VeilIncidents = new[] { CreateVeilIncident("VEIL-DEDUP", severity: 1000, loss: 0, recovery: 0) };
            var session = new GameSession(world, new OverseerPerspective());

            Assert.That(session.TrySubmit(new VeilIncidentActionCommand { IncidentId = "VEIL-DEDUP", Action = VeilActionKind.Investigate }).IsValid, Is.True);
            ValidationResult duplicate = session.TrySubmit(new VeilIncidentActionCommand { IncidentId = "VEIL-DEDUP", Action = VeilActionKind.Investigate });
            Assert.That(duplicate.IsValid, Is.False);
            Assert.That(duplicate.Error, Does.Contain("重复操作"));
            Assert.That(session.TrySubmit(new VeilIncidentActionCommand { IncidentId = "VEIL-DEDUP", Action = VeilActionKind.Monitor }).IsValid, Is.True);

            session.Advance(1);

            Assert.That(world.VeilIncidents[0].Dispositions, Has.Length.EqualTo(2));
            Assert.That(session.TrySubmit(new VeilIncidentActionCommand { IncidentId = "VEIL-DEDUP", Action = VeilActionKind.Investigate }).IsValid, Is.True);
            session.Advance(1);
            Assert.That(world.VeilIncidents[0].Dispositions, Has.Length.EqualTo(3));
        }

        /// <summary>
        /// 中文：调查或监测将严重度降低但不产生恢复时，0 损失/0 恢复事件必须保持 Active；存在正恢复且恢复追平损失时才进入 Recovering。
        /// English: When investigation or monitoring lowers severity but creates no recovery, a zero-loss/zero-recovery incident must remain Active; Recovering begins only with positive recovery that catches up to loss.
        /// </summary>
        [Test]
        public void VeilIncidentAction_StatusRequiresPositiveRecoveryToEnterRecovering()
        {
            WorldState activeWorld = CreateWorld();
            activeWorld.VeilIncidents = new[] { CreateVeilIncident("VEIL-ACTIVE", severity: 1000, loss: 0, recovery: 0) };
            new VeilIncidentActionCommand { IncidentId = "VEIL-ACTIVE", Action = VeilActionKind.Investigate }.Apply(activeWorld, new EventBuffer());
            Assert.That(activeWorld.VeilIncidents[0].Status, Is.EqualTo(VeilIncidentStatus.Active));

            WorldState recoveringWorld = CreateWorld();
            recoveringWorld.VeilIncidents = new[] { CreateVeilIncident("VEIL-RECOVER", severity: 1000, loss: 450, recovery: 0) };
            new VeilIncidentActionCommand { IncidentId = "VEIL-RECOVER", Action = VeilActionKind.SuppressPublicity }.Apply(recoveringWorld, new EventBuffer());
            Assert.That(recoveringWorld.VeilIncidents[0].Recovery, Is.GreaterThanOrEqualTo(recoveringWorld.VeilIncidents[0].Loss));
            Assert.That(recoveringWorld.VeilIncidents[0].Status, Is.EqualTo(VeilIncidentStatus.Recovering));
        }

        /// <summary>
        /// 中文：验证独立模式历法使用 1998-01-01 00:00 与每 Tick 一小时的权威定义；负 Tick 夹到纪元，跨日与 30 天自然月均按真实公历格式化且不输出内部 T 编号。
        /// English: Verifies the standalone calendar uses the authoritative 1998-01-01 00:00 epoch and one hour per tick; negative ticks clamp to the epoch, day and 30-day transitions use real Gregorian formatting, and internal T identifiers are absent.
        /// </summary>
        [Test]
        public void FoundationCalendar_FormatsStandaloneTickAsExactDateTime()
        {
            Assert.That(FoundationCalendar.FormatStandaloneDateTime(-1), Is.EqualTo("1998-01-01 00:00"));
            Assert.That(FoundationCalendar.FormatStandaloneDateTime(25), Is.EqualTo("1998-01-02 01:00"));
            Assert.That(FoundationCalendar.FormatStandaloneDateTime(720), Is.EqualTo("1998-01-31 00:00"));
            Assert.That(FoundationCalendar.FormatStandaloneDateTime(25), Does.Not.Contain("T"));
        }

        /// <summary>
        /// 中文：验证人数估算只依赖严重度、阶段、节点暴露与真实涉及洲集合；重复调用结果一致，越界比例被夹取，跨洲倍率按去重洲数计算且不修改输入事件。
        /// English: Verifies the population estimate depends only on severity, stage, node exposure, and the actual involved-continent set; repeated calls match, ratios clamp, duplicate continents are deduplicated, and the input incident is not mutated.
        /// </summary>
        [Test]
        public void VeilEstimate_IsDeterministicClampedAndReadOnly()
        {
            VeilIncidentState incident = CreateVeilIncident("VEIL-ESTIMATE", severity: 12000, loss: 0, recovery: 0);
            incident.CurrentStage = VeilIncidentStage.CrossRegionMediaSpread;
            incident.PropagationNodes = new[]
            {
                new VeilPropagationNode { Continent = Continent.Asia, Exposure = -50 },
                new VeilPropagationNode { Continent = Continent.Europe, Exposure = 12000 }
            };

            long first = VeilOverviewProjection.EstimateAffectedPeople(incident);
            long second = VeilOverviewProjection.EstimateAffectedPeople(incident);

            Assert.That(first, Is.EqualTo(30_000_000));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(incident.Severity, Is.EqualTo(12000));
            Assert.That(incident.PropagationNodes[0].Exposure, Is.EqualTo(-50));
            Assert.That(VeilOverviewProjection.ResolveInvolvedContinents(incident), Is.EqualTo(new[] { Continent.Europe, Continent.Asia }));
        }

        /// <summary>
        /// 中文：验证十一项总览固定齐全，跨洲事件的全球计数和估算人数只累计一次；洲级损失按起源归属、暴露按节点归属、人数确定性拆分、预算复制真实七洲数组，待执行处置保持零且说明不可归属。
        /// English: Verifies all eleven metrics exist, with cross-continent global incident counts and population estimates accumulated once; loss belongs to origins, exposure to nodes, people split deterministically, budgets copy real continent values, and pending actions remain explicitly unassigned zero.
        /// </summary>
        [Test]
        public void VeilOverview_UsesNonDuplicatedGlobalAndTruthfulContinentTotals()
        {
            WorldState world = CreateWorld();
            world.Veil.ByContinent = new[] { 1000, 2000, 3000, 4000, 5000, 6000, 7000 };
            world.Veil.RecalculateGlobal();
            world.Economy.Budget.VeilOperations = new long[] { 11, 22, 33, 44, 55, 66, 77 };
            VeilIncidentState incident = CreateVeilIncident("VEIL-SUMMARY", severity: 1000, loss: 300, recovery: 100);
            incident.OriginContinent = Continent.Asia;
            incident.CurrentStage = VeilIncidentStage.LocalPublicAwareness;
            incident.PropagationNodes = new[]
            {
                new VeilPropagationNode { Continent = Continent.Asia, Exposure = 100 },
                new VeilPropagationNode { Continent = Continent.Europe, Exposure = 200 }
            };
            world.VeilIncidents = new[] { incident };

            VeilOverviewMetricViewModel[] metrics = VeilOverviewProjection.Project(world);
            VeilOverviewMetricViewModel active = metrics.Single(item => item.Key == "active");
            VeilOverviewMetricViewModel people = metrics.Single(item => item.Key == "people");
            VeilOverviewMetricViewModel budget = metrics.Single(item => item.Key == "budget");
            VeilOverviewMetricViewModel pending = metrics.Single(item => item.Key == "pending");

            Assert.That(metrics, Has.Length.EqualTo(11));
            Assert.That(active.Value, Is.EqualTo(1));
            Assert.That(active.ByContinent.Sum(), Is.EqualTo(1));
            Assert.That(people.ByContinent.Sum(), Is.EqualTo(people.Value));
            Assert.That(metrics.Single(item => item.Key == "loss").ByContinent[(int)Continent.Asia], Is.EqualTo(300));
            Assert.That(metrics.Single(item => item.Key == "exposure").ByContinent[(int)Continent.Europe], Is.EqualTo(200));
            Assert.That(budget.Value, Is.EqualTo(308));
            Assert.That(budget.ByContinent, Is.EqualTo(new long[] { 11, 22, 33, 44, 55, 66, 77 }));
            Assert.That(pending.Value, Is.Zero);
            Assert.That(pending.ByContinent, Is.All.Zero);
            Assert.That(pending.TooltipNote, Does.Contain("暂无可归属"));

            metrics[0].ByContinent[0] = 999;
            Assert.That(world.Veil.ByContinent[0], Is.EqualTo(1000));
            Assert.That(world.Economy.Budget.VeilOperations[0], Is.EqualTo(11));
        }

        /// <summary>
        /// 中文：报告和审批历史经投影与 JSON 存档保持完整且与世界数组分离。English: Reports and approval history survive projection and JSON save while remaining detached from world arrays.
        /// </summary>
        [Test]
        public void Reports_ProjectAndPersistWithoutSharingMutableArrays()
        {
            var world = CreateWorld();
            world.Reports = ReportGenerationService.CreateInitial(0);
            world.NextReportSequence = world.Reports.Length;
            var session = new GameSession(world, new OverseerPerspective());
            Assert.That(session.TrySubmit(new ReportApprovalCommand { ReportIds = new[] { "RPT-000001" }, Decision = ReportStatus.Approved }).IsValid, Is.True);
            session.Advance(1);

            OverseerViewModel view = new OverseerPerspective().Project<OverseerViewModel>(world);
            view.Reports[0].Title = "changed";
            view.ReportApprovals[0].ReportIds[0] = "changed";
            SaveFile loaded = new SaveService().Deserialize(new SaveService().Serialize(session.CreateSave()));

            Assert.That(world.Reports[0].Title, Is.Not.EqualTo("changed"));
            Assert.That(world.ReportApprovals[0].ReportIds[0], Is.EqualTo("RPT-000001"));
            Assert.That(loaded.World.Reports[0].Status, Is.EqualTo(ReportStatus.Approved));
            Assert.That(loaded.World.ReportApprovals.Single().Decision, Is.EqualTo(ReportStatus.Approved));
        }

        /// <summary>
        /// 中文：验证三个自动槽分别保留自身上一版本为 bak，且槽选择严格由单调检查点序号决定。
        /// English: Verifies all three autosave slots independently retain their previous version as bak and slot selection is driven strictly by the monotonic checkpoint sequence.
        /// </summary>
        [Test]
        public void SaveRepository_AutoSlotsRotateIndependentTmpAndBakFiles()
        {
            string directory = Path.Combine(Path.GetTempPath(), "scp-auto-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var repository = new SaveRepository(directory);
                SaveFile save = CreateSave("auto");
                for (var sequence = 0; sequence < 6; sequence++)
                {
                    save.Checkpoint.CheckpointSequence = sequence;
                    save.World.Funds = 100 + sequence;
                    repository.SaveAutoCheckpoint(save);
                }

                for (var slot = 0; slot < 3; slot++)
                {
                    Assert.That(File.Exists(Path.Combine(directory, "auto", "auto-" + slot + ".json")), Is.True);
                    Assert.That(File.Exists(Path.Combine(directory, "auto", "auto-" + slot + ".bak")), Is.True);
                    Assert.That(repository.LoadAutoCheckpoint("auto", slot).World.Funds, Is.EqualTo(103 + slot));
                    Assert.That(repository.LoadAutoCheckpoint("auto", slot, true).World.Funds, Is.EqualTo(100 + slot));
                    Assert.That(File.Exists(Path.Combine(directory, "auto", "auto-" + slot + ".tmp")), Is.False);
                }
                Assert.That(repository.LoadLatestForViewing().World.Funds, Is.EqualTo(105));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// 中文：比较不中断推进与中途序列化恢复后的世界 JSON，包含随机流和全部并行新增模拟状态。
        /// English: Compares uninterrupted advancement with serialize-and-restore advancement using world JSON, including the random stream and all concurrently added simulation state.
        /// </summary>
        [Test]
        public void GameSession_RestoreContinuesDeterministicallyWithPendingCommands()
        {
            SaveFile baselineSave = CreateSave("baseline");
            baselineSave.World = CreateWorld();
            baselineSave.World.Random = new DeterministicRandom(777);
            string initialJson = new SaveService().Serialize(baselineSave);
            var uninterrupted = GameSession.Restore(new SaveService().Deserialize(initialJson), new OverseerPerspective());
            var interrupted = GameSession.Restore(new SaveService().Deserialize(initialJson), new OverseerPerspective());
            uninterrupted.Submit(new AdjustFundsCommand { Amount = 123, RequiredClearance = ClearanceLevel.Level4 });
            interrupted.Submit(new AdjustFundsCommand { Amount = 123, RequiredClearance = ClearanceLevel.Level4 });

            uninterrupted.Advance(20);
            SaveFile checkpoint = new SaveService().Deserialize(new SaveService().Serialize(interrupted.CreateSave(CheckpointReason.Exit)));
            var restored = GameSession.Restore(checkpoint, new OverseerPerspective());
            restored.Advance(20);

            string uninterruptedWorld = JsonConvert.SerializeObject(uninterrupted.World);
            string restoredWorld = JsonConvert.SerializeObject(restored.World);
            Assert.That(restoredWorld, Is.EqualTo(uninterruptedWorld));
            Assert.That(restored.CommandLog.Single().Amount, Is.EqualTo(123));
        }

        /// <summary>
        /// 中文：终局档可由查看入口载入并提供固定三部分尾声，但恢复为会话后仍拒绝推进和提交。
        /// English: An ended save loads through the viewing entry and exposes exactly three epilogue sections, while a restored session still rejects advancement and submission.
        /// </summary>
        [Test]
        public void EndedSave_IsViewableWithThreePartEpilogueButCannotContinue()
        {
            string directory = Path.Combine(Path.GetTempPath(), "scp-ended-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                SaveFile save = CreateSave("ended-view");
                save.World.Failure.IsEnded = true;
                save.World.Failure.EndReason = GameEndReason.VeilCollapse;
                save.Checkpoint.CheckpointSequence = 1;
                save.Epilogue = new EpilogueService().CreateReport(save.World);
                var repository = new SaveRepository(directory);
                repository.SaveAutoCheckpoint(save);

                Assert.That(repository.ProbeLatest().Status, Is.EqualTo(SaveProbeStatus.Ended));
                SaveFile viewed = repository.LoadLatestForViewing();
                Assert.That(viewed.Epilogue.Sections.Select(section => section.Kind), Is.EqualTo(new[] { EpilogueSectionKind.Outcome, EpilogueSectionKind.Legacy, EpilogueSectionKind.Archive }));
                var session = GameSession.Restore(viewed, new OverseerPerspective());
                Assert.Throws<InvalidOperationException>(() => session.Advance(1));
                Assert.Throws<InvalidOperationException>(() => session.Submit(new AdjustFundsCommand()));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static SaveFile CreateSave(string saveId)
        {
            return new SaveFile
            {
                SaveId = saveId,
                DisplayName = saveId,
                Identity = IdentityRole.Overseer,
                Difficulty = GameDifficulty.Normal,
                Seed = "M1-SEED",
                GameVersion = "test",
                SaveKind = SaveKind.Manual,
                World = CreateWorld()
            };
        }

        /// <summary>中文：财政历史与草案通过现有存档 JSON 无损往返，包含用于具体状态文本的确定性 Tick/周期。English: Fiscal history and drafts round-trip losslessly through existing save JSON, including deterministic tick/cycle metadata used by concrete status text.</summary>
        [Test]
        public void FinanceHistoryAndDraft_RoundTripThroughSave()
        {
            WorldState world=CreateWorld();world.Economy=new EconomyState{TotalAssets=8_000_000_000_000L,FundingChannels=EconomyRules.CreateTemporaryFundingChannels(),Budget=new BudgetState{SiteOperations=10},BudgetDraft=new BudgetState{SiteOperations=20},IsDraftRecorded=true,DraftRecordedTick=24,DraftRecordedCycle=2,FiscalHistory=new[]{new FiscalHistoryRecord{Kind="BudgetSigned",SubjectId="cycle-1",Amount=10,Decision="Signed"}},CycleHistory=new[]{new FiscalCycleSnapshot{Cycle=1,Income=100,Expenses=80,NetCashFlow=20,ClosingCash=500}}};
            var service=new SaveService();string json=service.Serialize(new SaveFile{World=world,WorldFacts=world.Facts});SaveFile restored=service.Deserialize(json);
            Assert.That(restored.World.Economy.BudgetDraft!.SiteOperations,Is.EqualTo(20));Assert.That(restored.World.Economy.DraftRecordedTick,Is.EqualTo(24));Assert.That(restored.World.Economy.DraftRecordedCycle,Is.EqualTo(2));Assert.That(restored.World.Economy.FiscalHistory[0].Decision,Is.EqualTo("Signed"));Assert.That(restored.World.Economy.CycleHistory[0].ClosingCash,Is.EqualTo(500));
        }

        /// <summary>中文：新局独立储备等于三个月必要支出，存档往返保留余额、总资产和可用现金。English: A new world starts with three necessary months in the independent reserve, and save round-trip preserves the reserve, total assets, and available cash.</summary>
        [Test]
        public void NewWorld_ReserveStartsAtThreeNecessaryMonthsAndRoundTrips()
        {
            ScpDefinition[] definitions=new ScpContentLoader().LoadDirectory(ContentPathResolver.FindScpDirectory(TestContext.CurrentContext.TestDirectory));WorldState world=OverseerScenarioFactory.CreateWorld(definitions,LoadFacilities(),123UL);long necessary=world.Economy.Budget.NecessaryMonthlySpending();var service=new SaveService();WorldState restored=service.Deserialize(service.Serialize(new SaveFile{World=world,WorldFacts=world.Facts})).World;
            Assert.Multiple(()=>{Assert.That(necessary,Is.EqualTo(104_000_000_000L));Assert.That(world.Economy.EmergencyReserveBalance,Is.EqualTo(necessary*3));Assert.That(world.Funds,Is.EqualTo(800_000_000_000L));Assert.That(world.Economy.TotalAssets,Is.EqualTo(8_000_000_000_000L));Assert.That(restored.Economy.EmergencyReserveBalance,Is.EqualTo(312_000_000_000L));});
        }

        /// <summary>中文：v6→v7 严格区分字段缺失与显式零；精确临时基线变为 3120 亿元，自定义旧值原样成为独立余额，正式草案旧科目均被清除。English: v6-to-v7 distinguishes a missing field from explicit zero; the exact provisional baseline becomes 312 billion, a custom legacy value becomes the independent balance unchanged, and legacy enacted/draft categories are removed.</summary>
        [Test]
        public void SaveMigrationV6ToV7_HandlesBaselineExplicitZeroAndCustomReserve()
        {
            JObject baselineBudget=JObject.FromObject(EconomyRules.CreateTemporaryPrimaryBudget());baselineBudget["EmergencyReserve"]=20_000_000_000L;JObject baseline=CreateV6FinanceMigrationRoot(baselineBudget,null);JObject zero=CreateV6FinanceMigrationRoot((JObject)baselineBudget.DeepClone(),0);JObject customBudget=(JObject)baselineBudget.DeepClone();customBudget["EmergencyReserve"]=7_000_000_000L;JObject custom=CreateV6FinanceMigrationRoot(customBudget,null);var migration=new SaveMigrationV6ToV7();
            migration.Migrate(baseline);migration.Migrate(zero);migration.Migrate(custom);
            Assert.Multiple(()=>{Assert.That(((JObject)baseline["world"]!["economy"]!).Property("EmergencyReserveBalance",StringComparison.OrdinalIgnoreCase)!.Value.Value<long>(),Is.EqualTo(312_000_000_000L));Assert.That(((JObject)zero["world"]!["economy"]!).Property("EmergencyReserveBalance",StringComparison.OrdinalIgnoreCase)!.Value.Value<long>(),Is.Zero);Assert.That(((JObject)custom["world"]!["economy"]!).Property("EmergencyReserveBalance",StringComparison.OrdinalIgnoreCase)!.Value.Value<long>(),Is.EqualTo(7_000_000_000L));Assert.That(((JObject)baseline["world"]!["economy"]!["budget"]!).Property("EmergencyReserve",StringComparison.OrdinalIgnoreCase),Is.Null);Assert.That(((JObject)baseline["world"]!["economy"]!["budgetDraft"]!).Property("EmergencyReserve",StringComparison.OrdinalIgnoreCase),Is.Null);});
        }

        private static JObject CreateV6FinanceMigrationRoot(JObject budget,long? independent)
        {
            var economy=new JObject{{"budget",budget},{"budgetDraft",(JObject)budget.DeepClone()},{"totalAssets",8_000_000_000_000L}};if(independent.HasValue)economy["emergencyReserveBalance"]=independent.Value;return new JObject{{"schemaVersion",6},{"world",new JObject{{"schemaVersion",6},{"economy",economy}}}};
        }

        /// <summary>中文：旧演示财政迁移只在旧结构且现金精确为 8,000,000 时发生；其他现金与新格式中碰巧相同的自定义现金必须原样保留。English: Legacy demo finance migration occurs only for the old shape with cash exactly 8,000,000; other cash and a new-format custom save that happens to have the same amount must remain unchanged.</summary>
        [Test]
        public void LegacyDemoCashMigration_IsExactAndDoesNotRewriteCustomCash()
        {
            var service=new SaveService();
            SaveFile migrated=service.Deserialize(CreateLegacyFinanceJson(EconomyRules.LegacyDemoStartingAvailableCash,CreateLegacyDemoBudget()));
            SaveFile custom=service.Deserialize(CreateLegacyFinanceJson(8_000_001L,CreateLegacyDemoBudget()));
            WorldState current=CreateWorld();current.Funds=EconomyRules.LegacyDemoStartingAvailableCash;current.Economy.TotalAssets=EconomyRules.TemporaryStartingTotalAssets;current.Economy.FundingChannels=EconomyRules.CreateTemporaryFundingChannels();string currentJson=service.Serialize(new SaveFile{World=current,WorldFacts=current.Facts});
            Assert.That(migrated.World.Funds,Is.EqualTo(EconomyRules.TemporaryStartingAvailableCash));
            Assert.That(custom.World.Funds,Is.EqualTo(8_000_001L));
            Assert.That(service.Deserialize(currentJson).World.Funds,Is.EqualTo(EconomyRules.LegacyDemoStartingAvailableCash));
        }

        /// <summary>中文：旧十项预算只有精确合计 900,000 的演示签名才升级为集中亿元级基线；玩家改动一单位后保持原预算，旧档也不注入演示事故。English: Only the exact legacy ten-category 900,000 demo signature upgrades to the centralized global-scale baseline; changing one unit preserves the player's budget, and old saves never receive a demo incident.</summary>
        [Test]
        public void LegacyDemoBudgetMigration_IsExactAndDoesNotInjectIncident()
        {
            JObject signature=CreateLegacyDemoBudget();JObject modified=(JObject)signature.DeepClone();modified["siteOperations"]=modified["siteOperations"]!.Value<long>()+1;
            SaveFile migrated=new SaveService().Deserialize(CreateLegacyFinanceJson(8_000_000L,signature));SaveFile custom=new SaveService().Deserialize(CreateLegacyFinanceJson(8_000_000L,modified));
            Assert.That(migrated.World.Economy.Budget.TotalSpending(),Is.EqualTo(EconomyRules.CreateTemporaryPrimaryBudget().TotalSpending()));
            Assert.That(custom.World.Economy.Budget.TotalSpending(),Is.EqualTo(EconomyRules.LegacyDemoPrimaryBudgetTotal-80_000L+1));
            Assert.That(migrated.World.Economy.CompensationIncidents,Is.Empty);
        }

        /// <summary>中文：自动金额格式按中文数量级输出两位小数，Tooltip 始终保留完整千位分隔整数货币值，并安全处理负数。English: Automatic money formatting emits two-decimal Chinese magnitudes, while tooltips retain the complete grouped integer currency value and negatives remain safe.</summary>
        [Test]
        public void FinanceAmountFormatter_UsesAutomaticUnitsAndFullTooltips()
        {
            Assert.That(FinanceAmountFormatter.Format(800_000_000_000L),Is.EqualTo("8000.00 亿"));
            Assert.That(FinanceAmountFormatter.Format(121_000_000_000L),Is.EqualTo("1210.00 亿"));
            Assert.That(FinanceAmountFormatter.Format(50_000_000L),Is.EqualTo("5000.00 万"));
            Assert.That(FinanceAmountFormatter.FormatSigned(-30_000_000_000L),Is.EqualTo("-300.00 亿"));
            Assert.That(FinanceAmountFormatter.FormatAbsolute(long.MinValue),Is.EqualTo("9223372.04 万亿"));
            Assert.That(FinanceAmountFormatter.FormatFull(800_000_000_000L),Is.EqualTo("800,000,000,000 货币单位"));
        }

        /// <summary>中文：正式新局包含唯一、明确标记为项目演示的通用殉职事故，而普通旧存档反序列化不会被污染。English: A formal new world contains one clearly project-marked generic casualty incident, while deserializing an ordinary legacy save does not contaminate it.</summary>
        [Test]
        public void NewWorld_HasProjectDemoIncidentWhileLegacySaveRemainsClean()
        {
            ScpDefinition[] definitions=new ScpContentLoader().LoadDirectory(ContentPathResolver.FindScpDirectory(TestContext.CurrentContext.TestDirectory));WorldState world=OverseerScenarioFactory.CreateDemoWorld(definitions,LoadFacilities());
            Assert.That(world.Economy.CompensationIncidents,Has.Length.EqualTo(1));Assert.That(world.Economy.CompensationIncidents[0].FacilityLabel,Does.Contain("DEMO-FAC-01"));
            SaveFile legacy=new SaveService().Deserialize(CreateLegacyFinanceJson(123L,null));Assert.That(legacy.World.Economy.CompensationIncidents,Is.Empty);
        }

        /// <summary>中文：覆盖正式新建路径的 CreateWorld→存档往返→GameSession.Restore→OverseerPerspective 链路，而非只断言工厂对象；UI 最终读取的投影必须仍含 DEMO-FAC-01。English: Covers the formal CreateWorld-to-save-round-trip-to-GameSession.Restore-to-OverseerPerspective path instead of asserting only a factory object; the projection actually consumed by UI must retain DEMO-FAC-01.</summary>
        [Test]
        public void FormalOverseerSession_NewWorldProjectsDemoIncidentThroughSavedSession()
        {
            ScpDefinition[] definitions=new ScpContentLoader().LoadDirectory(ContentPathResolver.FindScpDirectory(TestContext.CurrentContext.TestDirectory));WorldState created=OverseerScenarioFactory.CreateWorld(definitions,LoadFacilities(),987654321UL);var service=new SaveService();SaveFile disk=service.Deserialize(service.Serialize(new SaveFile{World=created,WorldFacts=created.Facts}));var session=GameSession.Restore(disk,new OverseerPerspective());OverseerViewModel view=session.Perspective.Project<OverseerViewModel>(session.World);
            Assert.That(view.Finance.CompensationIncidents,Has.Length.EqualTo(1));Assert.That(view.Finance.CompensationIncidents[0].Facility,Does.Contain("DEMO-FAC-01"));Assert.That(view.Finance.RiskSummary.PendingIncidentCount,Is.EqualTo(1));
        }

        /// <summary>中文：六列表格投影固定提供十行所需科目、上月、草案、变化、最低线与比例字段；新局变化明确相对预算基准，空决定不得移除右栏摘要。English: The six-column projection always supplies category, prior, draft, change, minimum, and ratio fields for ten rows; new-game changes explicitly use the budget baseline, and empty decisions cannot remove the right summary.</summary>
        [Test]
        public void FinanceProjection_ProvidesCompleteSixColumnRowsAndNewGameBaselineChanges()
        {
            WorldState world=CreateWorld();world.Funds=EconomyRules.TemporaryStartingAvailableCash;world.Economy.TotalAssets=EconomyRules.TemporaryStartingTotalAssets;world.Economy.FundingChannels=EconomyRules.CreateTemporaryFundingChannels();world.Economy.Budget=EconomyRules.CreateTemporaryPrimaryBudget();world.Economy.BudgetDraft=world.Economy.Budget.Clone();world.Economy.BudgetDraft.SiteOperations+=1_000_000_000L;world.Economy.CompensationIncidents=Array.Empty<CompensationIncidentState>();world.Economy.FiscalHistory=Array.Empty<FiscalHistoryRecord>();OverseerViewModel view=new OverseerPerspective().Project<OverseerViewModel>(world);BudgetLineViewModel site=view.Finance.BudgetLines[0];
            Assert.That(view.Finance.BudgetLines,Has.Length.EqualTo(9));Assert.That(view.Finance.BudgetLines.All(line=>line.Key.Length>0&&line.BaselineAmount>0&&line.DraftAmount>0&&line.MinimumLine>0&&line.RatioPercent>0&&line.ChangeBasis.Length>0),Is.True);Assert.Multiple(()=>{Assert.That(site.PreviousAmount,Is.Null);Assert.That(site.ChangeBasis,Is.EqualTo("相对预算基准"));Assert.That(site.ChangeAmount,Is.EqualTo(1_000_000_000L));Assert.That(site.ChangePercent,Is.EqualTo(5.6m));});Assert.That(view.Finance.RecentDecisions,Is.Empty);Assert.That(view.Finance.RiskSummary.PendingIncidentCount,Is.Zero);Assert.That(view.Finance.RiskSummary.UnpaidObligations,Is.Zero);
        }

        /// <summary>中文：有真实逐科目月结快照时变化必须相对上月实绩；上月基数为零时百分比保持 null，避免除零并允许界面显示“新增”。English: With a real per-category settlement snapshot, changes must compare against prior actuals; a zero prior basis keeps percent null, avoiding division by zero and allowing the UI to say “new”.</summary>
        [Test]
        public void FinanceProjection_WithHistoryUsesPriorActualAndAvoidsZeroDivision()
        {
            WorldState world=CreateWorld();world.Economy.FundingChannels=EconomyRules.CreateTemporaryFundingChannels();world.Economy.Budget=EconomyRules.CreateTemporaryPrimaryBudget();world.Economy.BudgetDraft=world.Economy.Budget.Clone();world.Economy.BudgetDraft.SiteOperations=30_000_000_000L;world.Economy.BudgetDraft.AlphaOne=5_000_000_000L;var previous=world.Economy.Budget.Clone();previous.SiteOperations=20_000_000_000L;previous.AlphaOne=0;world.Economy.CycleHistory=new[]{new FiscalCycleSnapshot{Cycle=1,SettledBudget=previous}};OverseerViewModel view=new OverseerPerspective().Project<OverseerViewModel>(world);BudgetLineViewModel site=view.Finance.BudgetLines[0];BudgetLineViewModel alpha=view.Finance.BudgetLines[5];
            Assert.Multiple(()=>{Assert.That(site.PreviousAmount,Is.EqualTo(20_000_000_000L));Assert.That(site.ChangeBasis,Is.EqualTo("较上月"));Assert.That(site.ChangeAmount,Is.EqualTo(10_000_000_000L));Assert.That(site.ChangePercent,Is.EqualTo(50m));Assert.That(alpha.PreviousAmount,Is.Zero);Assert.That(alpha.ChangeAmount,Is.EqualTo(5_000_000_000L));Assert.That(alpha.ChangePercent,Is.Null);});
        }

        /// <summary>中文：亿元输入使用 decimal 精确乘以一亿；接受零到两位小数，拒绝空、负数、三位小数和超出 long 的值。调用者只在 true 时替换草案，因此所有拒绝路径均保持上一个有效值。English: Hundred-million input uses exact decimal multiplication by 100,000,000; zero-to-two decimals are accepted while blank, negative, three-decimal, and long-overflow values are rejected. Callers replace drafts only on true, so every rejection preserves the prior valid value.</summary>
        [Test]
        public void FinanceBudgetAmountParser_ParsesYiExactlyAndRejectsInvalidWithoutReplacement()
        {
            long valid=12_300_000_000L;Assert.That(FinanceBudgetAmountParser.TryParseYi("385.00",out long parsed,out _),Is.True);Assert.That(parsed,Is.EqualTo(38_500_000_000L));Assert.That(FinanceBudgetAmountParser.TryParseYi("0.01",out parsed,out _),Is.True);Assert.That(parsed,Is.EqualTo(1_000_000L));Assert.That(FinanceBudgetAmountParser.FormatYi(parsed),Is.EqualTo("0.01"));
            foreach(string invalid in new[]{"","-1","1.001","92233720368.55"}){if(FinanceBudgetAmountParser.TryParseYi(invalid,out long candidate,out _))valid=candidate;}Assert.That(valid,Is.EqualTo(12_300_000_000L));
        }

        /// <summary>中文：格式化后的十项亿元文本必须原子往返为完全相同的 long 金额，且显式 0.00 是合法预算而非缺失值。English: All ten formatted yi texts must atomically round-trip to identical long amounts, and explicit 0.00 is a valid budget rather than a missing value.</summary>
        [Test]
        public void FinanceBudgetDraftAssembler_FormattedTenFieldsRoundTripAndAcceptZero()
        {
            var source=new BudgetViewModel{SiteOperations=18_000_000_000L,ContainmentMaintenance=10_000_000_000L,Research=7_000_000_000L,Security=5_000_000_000L,MobileTaskForces=2_000_000_000L,AlphaOne=10_000_000_000L,VeilAndCover=21_000_000_000L,AdministrationAndIntelligence=5_000_000_000L,PersonnelAndEthics=4_000_000_000L,VeilOperations=new long[7]};var texts=FinanceTexts(source);texts["Alpha-1"]="0.00";
            Assert.That(FinanceBudgetDraftAssembler.TryAssemble(texts,source,out BudgetState? assembled,out string error),Is.True,error);Assert.That(assembled,Is.Not.Null);Assert.Multiple(()=>{Assert.That(assembled!.SiteOperations,Is.EqualTo(source.SiteOperations));Assert.That(assembled.ContainmentMaintenance,Is.EqualTo(source.ContainmentMaintenance));Assert.That(assembled.Research,Is.EqualTo(source.Research));Assert.That(assembled.Security,Is.EqualTo(source.Security));Assert.That(assembled.MobileTaskForces,Is.EqualTo(source.MobileTaskForces));Assert.That(assembled.AlphaOne,Is.Zero);Assert.That(assembled.VeilAndCover,Is.EqualTo(source.VeilAndCover));Assert.That(assembled.AdministrationAndIntelligence,Is.EqualTo(source.AdministrationAndIntelligence));Assert.That(assembled.PersonnelAndEthics,Is.EqualTo(source.PersonnelAndEthics));});
        }

        /// <summary>中文：十项组装中任一无效或缺失都必须整体拒绝并保持调用者已有有效草案引用和值不变，禁止部分更新或以零替代。English: Any invalid or missing field must reject the whole ten-field assembly and preserve the caller's existing valid draft reference and values, forbidding partial updates or zero substitution.</summary>
        [Test]
        public void FinanceBudgetDraftAssembler_RejectsAnyInvalidWithoutReplacingValidDraft()
        {
            var source=new BudgetViewModel{SiteOperations=18_000_000_000L,ContainmentMaintenance=10_000_000_000L,Research=7_000_000_000L,Security=5_000_000_000L,MobileTaskForces=2_000_000_000L,AlphaOne=10_000_000_000L,VeilAndCover=21_000_000_000L,AdministrationAndIntelligence=5_000_000_000L,PersonnelAndEthics=4_000_000_000L,VeilOperations=new long[7]};BudgetState valid=new BudgetState{SiteOperations=123};var texts=FinanceTexts(source);texts["研究与实验"]="7.001";
            if(FinanceBudgetDraftAssembler.TryAssemble(texts,source,out BudgetState? candidate,out _)&&candidate!=null)valid=candidate;Assert.That(valid.SiteOperations,Is.EqualTo(123));Assert.That(candidate,Is.Null);texts=FinanceTexts(source);texts.Remove("人员与伦理保障");Assert.That(FinanceBudgetDraftAssembler.TryAssemble(texts,source,out candidate,out string error),Is.False);Assert.That(candidate,Is.Null);Assert.That(error,Does.StartWith("人员与伦理保障："));
        }

        private static Dictionary<string,string> FinanceTexts(BudgetViewModel source)=>new Dictionary<string,string>{{"设施运营",FinanceBudgetAmountParser.FormatYi(source.SiteOperations)},{"收容维护",FinanceBudgetAmountParser.FormatYi(source.ContainmentMaintenance)},{"研究与实验",FinanceBudgetAmountParser.FormatYi(source.Research)},{"安保",FinanceBudgetAmountParser.FormatYi(source.Security)},{"普通 MTF",FinanceBudgetAmountParser.FormatYi(source.MobileTaskForces)},{"Alpha-1",FinanceBudgetAmountParser.FormatYi(source.AlphaOne)},{"帷幕与掩盖",FinanceBudgetAmountParser.FormatYi(source.VeilAndCover)},{"行政与情报",FinanceBudgetAmountParser.FormatYi(source.AdministrationAndIntelligence)},{"人员与伦理保障",FinanceBudgetAmountParser.FormatYi(source.PersonnelAndEthics)}};

        /// <summary>中文：底部折叠策略只遵循显式控件状态，普通和事故明细都可在不依赖像素的情况下展开或收起。English: The bottom disclosure policy follows only explicit control state, allowing both ordinary and incident details to expand or collapse without pixel assumptions.</summary>
        [Test]
        public void FinanceDetailLayoutPolicy_RespectsExplicitDisclosureState()
        {
            Assert.That(FinanceDetailLayoutPolicy.IsExpanded(string.Empty,false),Is.False);Assert.That(FinanceDetailLayoutPolicy.IsExpanded(string.Empty,true),Is.True);Assert.That(FinanceDetailLayoutPolicy.IsExpanded("INC-DEMO-0001",true),Is.True);Assert.That(FinanceDetailLayoutPolicy.IsExpanded("INC-DEMO-0001",false),Is.False);
        }

        private static string CreateLegacyFinanceJson(long funds,JObject? budget)
        {
            var root=new JObject{{"schemaVersion",6},{"world",new JObject{{"schemaVersion",6},{"funds",funds},{"economy",new JObject{{"budget",budget}}}}},{"worldFacts",new JObject()}};
            return root.ToString(Formatting.None);
        }

        /// <summary>中文：重建第一版截图对应的旧十项演示预算签名，合计必须精确为 900,000；仅用于迁移边界测试。English: Reconstructs the legacy ten-category demo signature seen in the first-version screenshot, totaling exactly 900,000 and used only for migration-boundary tests.</summary>
        private static JObject CreateLegacyDemoBudget()=>new JObject{{"siteOperations",180_000L},{"containmentMaintenance",100_000L},{"research",70_000L},{"security",50_000L},{"mobileTaskForces",20_000L},{"alphaOne",100_000L},{"veilAndCover",210_000L},{"administrationAndIntelligence",50_000L},{"personnelAndEthics",40_000L},{"emergencyReserve",80_000L}};

        /// <summary>
        /// 中文：为帷幕提交与状态机测试构造最小有效事件；参数 severity、loss、recovery 均为 0-10000 的整数比例单位，初始状态固定为 Active。
        /// English: Builds the smallest valid incident for veil-submission and state-machine tests; severity, loss, and recovery are integer ratio units from 0 to 10000, with an Active initial state.
        /// </summary>
        private static VeilIncidentState CreateVeilIncident(string id, int severity, int loss, int recovery)
        {
            return new VeilIncidentState
            {
                StableId = id,
                AnonymousTitle = "测试帷幕事件",
                SourceCategory = "测试",
                OriginContinent = Continent.Asia,
                Severity = severity,
                Loss = loss,
                Recovery = recovery,
                Status = VeilIncidentStatus.Active,
                PropagationNodes = Array.Empty<VeilPropagationNode>(),
                Dispositions = Array.Empty<VeilDispositionRecord>()
            };
        }

        private static WorldState CreateWorld()
        {
            var random = new DeterministicRandom(42);
            return new WorldState
            {
                Funds = 5000000,
                Random = random,
                Council = CouncilFactory.Create(ref random),
                Sites = new[] { new SiteState { Id = new SiteId(1), Continent = Continent.Asia } }
            };
        }
    }
}
