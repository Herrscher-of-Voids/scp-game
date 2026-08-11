using System.Linq;
using System.Text;

using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// M1 演示世界的唯一构建入口。
    /// 控制台宿主（--m1）与 Unity Presentation 层共用同一份构建逻辑，
    /// 避免两处各自维护初始化代码导致确定性漂移。
    /// </summary>
    public static class OverseerScenarioFactory
    {
        /// <summary>
        /// 演示世界的随机种子。数值变更即视为破坏性变更：会改变既有存档与验收输出。
        /// </summary>
        public const int DemoSeed = 20260805;

        /// <summary>
        /// 中文：新局可用现金临时平衡值，单位为整数货币；集中引用 EconomyRules，待后续平衡复核。
        /// English: Provisional new-game available cash in integer currency units, centrally sourced from EconomyRules pending later balance review.
        /// </summary>
        public const long DemoStartingFunds = EconomyRules.TemporaryStartingAvailableCash;

        /// <summary>
        /// 起始伦理评分。
        /// </summary>
        public const int DemoEthicsScore = 15;

        /// <summary>中文：全部正式设施统一的初始安保等级；来源资料未给出逐设施安保数值，因此使用既有统一基线，不为个别设施编造数值。English: Uniform initial security level for every official facility; source material provides no per-facility security value, so the existing shared baseline is used instead of invented numbers.</summary>
        public const int BaselineSecurityLevel = 5;

        /// <summary>中文：全部正式设施统一的初始可用观察员编制，单位为人。English: Uniform initial observer headcount available at every official facility, in persons.</summary>
        public const int BaselineObservers = 4;

        /// <summary>中文：全部正式设施统一的初始真实稳定度，万分比定点数；同时作为初始自报值，因为开局尚无失真历史。English: Uniform initial true stability for every official facility in ten-thousandths; it also seeds the reported value because no distortion history exists at start.</summary>
        public const int BaselineStability = 8000;

        /// <summary>中文：全部正式设施统一的初始报告可信度，万分比定点数；决定未审计时自报值的失真幅度。English: Uniform initial report credibility for every official facility in ten-thousandths, which governs distortion magnitude while unaudited.</summary>
        public const int BaselineReportCredibility = 7000;

        /// <summary>
        /// 中文：按固定种子构建 M1 世界；设施目录与 SCP 定义顺序相同时必然产出完全相同的世界。
        /// English: Builds the M1 world from the fixed seed; identical facility catalogue and SCP definition order always produce the same world.
        /// </summary>
        /// <param name="definitions">中文：已验证的 SCP 内容定义。English: Validated SCP content definitions.</param>
        /// <param name="facilities">中文：已验证的 89 项正式设施目录。English: Validated catalogue of the 89 official facilities.</param>
        public static WorldState CreateDemoWorld(ScpDefinition[] definitions, FacilityDefinition[] facilities)
        {
            return CreateWorld(definitions, facilities, DemoSeed);
        }

        /// <summary>
        /// 中文：按玩家种子的稳定 64 位数值创建 O5 世界；相同定义顺序、相同设施目录和相同种子必然得到相同初始快照。
        /// English: Creates an Overseer world from the player's stable 64-bit seed; identical definition order, facility catalogue, and seed always produce the same initial snapshot.
        /// </summary>
        /// <param name="definitions">中文：已验证的 SCP 内容定义。English: Validated SCP content definitions.</param>
        /// <param name="facilities">中文：已验证的正式设施目录；顺序由 FacilityDataLoader 按 siteId 固定。English: Validated official facility catalogue whose order is fixed by FacilityDataLoader on siteId.</param>
        /// <param name="seed">中文：玩家种子的稳定 64 位数值。English: Stable 64-bit value of the player seed.</param>
        public static WorldState CreateWorld(ScpDefinition[] definitions, FacilityDefinition[] facilities, ulong seed)
        {
            var random = new DeterministicRandom(seed);
            // 中文：CouncilFactory 会推进随机状态，因此建席后才把状态写入世界快照。
            // English: CouncilFactory advances random state, so the state is stored in the snapshot only after council creation.
            var council = CouncilFactory.Create(ref random);
            SiteState[] sites = CreateSites(facilities);
            var anomalies = definitions.Select((definition, index) => new AnomalyInstance
            {
                Definition = definition,
                SiteId = sites[index % sites.Length].Id,
                IsObserved = true,
                ObserverCount = 2
            }).ToArray();
            ReportState[] reports = ReportGenerationService.CreateInitial(0);
            return new WorldState
            {
                Funds = DemoStartingFunds,
                Reports = reports,
                NextReportSequence = reports.Length,
                Random = random,
                Council = council,
                Sites = sites,
                Anomalies = anomalies,
                EthicsScore = DemoEthicsScore,
                // 中文：新局加入一个明确标记为项目演示的匿名帷幕事件，只使用洲级位置且不引用官方设施；旧存档不会经过场景工厂，因此自然保持空数组且不受污染。
                // English: New worlds receive one explicitly project-demo anonymous veil incident using only continent-level location and no official facility reference; legacy saves bypass this factory and naturally retain an empty array without contamination.
                VeilIncidents = CreateDemoVeilIncidents(),
                Economy = new EconomyState
                {
                    TotalAssets = EconomyRules.TemporaryStartingTotalAssets,
                    EmergencyReserveBalance = EconomyRules.CreateTemporaryPrimaryBudget().NecessaryMonthlySpending() * 3,
                    FundingChannels = EconomyRules.CreateTemporaryFundingChannels(),
                    Budget = CreateSustainableBudget(),
                    // 中文：确定性演示事故只使用本项目通用编号与角色，不引入或伪造任何官方人物/设施设定；用于验证逐人抚恤完整链路。
                    // English: The deterministic demo incident uses project-generic identifiers and roles only, introducing no fabricated official person or facility lore; it exists to verify the complete per-person compensation path.
                    CompensationIncidents = new[]
                    {
                        new CompensationIncidentState
                        {
                            IncidentId = "INC-DEMO-0001", FacilityLabel = "项目演示设施 DEMO-FAC-01", ReportedTick = 0, Status = CompensationStatus.Pending,
                            Personnel = new[]
                            {
                                new FallenPersonnelCompensation { PersonnelId = "STAFF-001", DisplayName = "值勤安保员 A", Status = CompensationStatus.Pending },
                                new FallenPersonnelCompensation { PersonnelId = "STAFF-002", DisplayName = "收容技术员 B", Status = CompensationStatus.Pending }
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 中文：创建固定匿名演示事件；位置仅确认到亚洲洲级，节点坐标保持 0，标题和来源明确为项目演示，不冒充 SCP 官方设施或组织。返回新数组供每个新世界独立持久化。
        /// English: Creates the fixed anonymous demo incident with Asia-only continent precision and zero node coordinates; title and source explicitly identify project demo content and impersonate no official SCP facility or organisation. A fresh array is returned for independent persistence in each new world.
        /// </summary>
        private static VeilIncidentState[] CreateDemoVeilIncidents()
        {
            return new[]
            {
                new VeilIncidentState
                {
                    StableId = "VEIL-DEMO-0001", AnonymousTitle = "匿名传播链｜项目演示", SourceCategory = "匿名公众影像线索（项目演示）",
                    CreatedTick = 0, DiscoveredTick = 0, OriginContinent = Continent.Asia, LocationPrecision = VeilLocationPrecision.ContinentOnly,
                    Severity = 3200, CurrentStage = VeilIncidentStage.ClueBacklog, Status = VeilIncidentStatus.Active, LastProgressTick = 0, NextRecordSequence = 1,
                    PropagationNodes = new[] { new VeilPropagationNode { StableId = "VEIL-DEMO-0001-NODE-00", Continent = Continent.Asia, FirstObservedTick = 0, LocationPrecision = VeilLocationPrecision.ContinentOnly, Exposure = 1800 } },
                    Dispositions = new[] { new VeilDispositionRecord { StableId = "VEIL-DEMO-0001-REC-0000", Tick = 0, Action = VeilActionKind.Monitor, Effect = "监测系统发现匿名洲级传播线索。" } }
                }
            };
        }

        /// <summary>
        /// 中文：从种子文本与已创建世界只读派生任命交接元数据。局部 FNV-1a 选择有限通用模板，不读取或推进 World.Random，因此不会改变模拟后续随机序列。
        /// English: Derives appointment metadata read-only from seed text and the created world. A local FNV-1a hash selects finite generic templates without reading or advancing World.Random, so later simulation randomness is unchanged.
        /// </summary>
        /// <param name="seedText">中文：持久化的原始种子文本，按 UTF-8 字节稳定映射。English: Persisted original seed text, stably mapped as UTF-8 bytes.</param>
        /// <param name="world">中文：已创建世界，只读取玩家席位编号与基础规模。English: Created world, read only for the player seat designation and baseline scale.</param>
        /// <returns>中文：可直接持久化并重复显示的内部开发交接摘要。English: Internal-development handover metadata ready for persistence and repeat display.</returns>
        public static OverseerBriefingMetadata CreateBriefing(string seedText, WorldState world)
        {
            ulong hash = StableTextHash(seedText);
            string[] departures =
            {
                "前任监督者因机密原因离席；相关细节不在本任命文件中披露。",
                "前任监督者已按内部程序离席；后续审查由既有保密渠道处理。",
                "前任监督者因未公开的内部原因终止履职；本席位即刻补任。"
            };
            string[][] briefs =
            {
                new[] { "核对全球设施的运行摘要与报告可信度。", "复核当前拨款结构与紧急储备是否可持续。", "确认全球帷幕与收容警报的首轮观察重点。" },
                new[] { "审阅设施稳定度差异并标记需要复核的报告。", "检查收容资源与安保支出的当前平衡。", "确认监督委员会未结提案与后续议程入口。" },
                new[] { "建立全球设施网络的首轮状态基线。", "复核财政、收容与帷幕摘要中的风险信号。", "确认进入总览后的首个观察周期安排。" }
            };
            string[] legacies =
            {
                "遗留政策：维持现有基础拨款。既有承诺：不得在未经复核时扩大长期支出。未结事项：设施报告与风险优先级仍待新任监督者确认。",
                "遗留政策：优先保持设施连续运作。既有承诺：保留紧急储备。未结事项：部分摘要仍需在总览中持续观察。",
                "遗留政策：暂不改变当前设施配置。既有承诺：先核对信息再采取不可逆行动。未结事项：首轮周期的资源重点尚未裁定。"
            };
            int template = (int)(hash % (ulong)briefs.Length);
            string seat = world.Council?.PlayerSeatId.Number > 0 ? "O5-" + world.Council.PlayerSeatId.Number : "O5-UNASSIGNED";
            // 中文：设施规模直接取世界实际设施数量，不写死数字，避免摘要与正式目录规模脱节。
            // English: The facility scale is read from the actual world site count rather than hard-coded, so the summary cannot drift from the official catalogue size.
            int siteCount = world.Sites?.Length ?? 0;
            return new OverseerBriefingMetadata
            {
                SeatDesignation = seat,
                PredecessorDepartureCategory = departures[template],
                FoundationStatusSummary = "当前世界包含 " + siteCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 处正式设施实体，来源为 SCP-EN/CN 主站全球设施资料；部分设施位置保密、仅有大区级信息或不在地球，界面按位置精度分级显示。",
                PriorityBriefs = briefs[template].Select((text, index) => "PRIORITY-0" + (index + 1) + "｜" + text).ToArray(),
                PredecessorLegacy = legacies[template]
            };
        }

        /// <summary>
        /// 中文：UTF-8 FNV-1a 64 位稳定哈希，仅用于本地确定性模板选择；相同文本跨进程得到相同值。
        /// English: Stable 64-bit UTF-8 FNV-1a used only for local deterministic template selection; identical text yields the same value across processes.
        /// </summary>
        private static ulong StableTextHash(string text)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (byte value in Encoding.UTF8.GetBytes(text ?? string.Empty))
            {
                hash ^= value;
                hash *= prime;
            }
            return hash;
        }

        /// <summary>
        /// 中文：财政纵向切片的九项初始预算临时值；二级明细严格加总到对应一级科目。English: Provisional nine-category opening budget for the finance vertical slice; secondary details sum exactly to their primary categories.
        /// </summary>
        public static BudgetState CreateSustainableBudget()
        {
            // 中文：场景工厂只引用集中临时基线，确保新局、迁移与测试使用完全相同的九项金额和二级明细。English: The scenario factory delegates to the central baseline so new games, migrations, and tests use identical nine-category amounts and detail.
            return EconomyRules.CreateTemporaryPrimaryBudget();
        }

        /// <summary>
        /// 中文：把已验证的正式设施目录逐条转成运行时设施状态。顺序完全沿用目录顺序（FacilityDataLoader 已按 siteId 升序固定），不做任何随机化，因此不会读取或推进 World.Random。
        /// English: Converts each validated official facility definition into runtime site state. Order follows the catalogue exactly (already fixed ascending by siteId in FacilityDataLoader) with no randomisation, so World.Random is never read or advanced.
        /// </summary>
        /// <param name="facilities">中文：已通过 FacilityDataLoader 验证的设施目录。English: Facility catalogue already validated by FacilityDataLoader.</param>
        /// <returns>中文：与目录同序、同长度的设施运行状态数组。English: Site-state array matching the catalogue in order and length.</returns>
        private static SiteState[] CreateSites(FacilityDefinition[] facilities)
        {
            var sites = new SiteState[facilities.Length];
            for (int index = 0; index < facilities.Length; index++)
            {
                FacilityDefinition facility = facilities[index];
                bool nonTerrestrial = facility.LocationPrecision == SiteLocationPrecision.NonTerrestrial;
                // 中文：只有目录给出成对近似坐标时才换算地图万分比；保密、未知、多地点和非地球设施保持 0，由表现层按精度降级显示，绝不伪造坐标。
                // English: Map ten-thousandths are computed only when the catalogue supplies an approximate coordinate pair; redacted, unknown, multi-location, and non-terrestrial facilities stay at zero so presentation degrades by precision instead of fabricating a position.
                bool mapped = facility.Latitude.HasValue && facility.Longitude.HasValue && !nonTerrestrial;
                sites[index] = new SiteState
                {
                    Id = new SiteId(facility.SiteId),
                    InternalStableId = facility.InternalStableId,
                    CanonicalId = facility.CanonicalId,
                    Code = facility.DisplayCode,
                    DisplayLabel = facility.DisplayName,
                    FacilityType = facility.FacilityType,
                    LocationText = facility.Region,
                    LocationPrecision = facility.LocationPrecision,
                    Country = facility.Country ?? string.Empty,
                    MapX = mapped ? LongitudeToMapX(facility.Longitude!.Value) : 0,
                    MapY = mapped ? LatitudeToMapY(facility.Latitude!.Value) : 0,
                    IsMapApproximate = mapped,
                    IsNonTerrestrial = nonTerrestrial,
                    EnUrl = facility.EnUrl,
                    CnUrl = facility.CnUrl ?? string.Empty,
                    SourceCanon = facility.SourceCanon,
                    ProjectDistinctionNote = facility.ProjectNotes,
                    // 中文：洲别只在目录可确认时写入；不可确认时保留枚举默认值，落图由 MapX/MapY 与精度标记控制。
                    // English: Continent is written only when the catalogue confirms it; otherwise the enum default remains and mapping is governed by MapX/MapY plus precision flags.
                    Continent = facility.Continent ?? default,
                    // 中文：以下运行参数是全设施统一的既有基线默认值，不为个别设施编造独有剧情数值。
                    // English: The following runtime parameters are the existing uniform baseline defaults; no facility receives invented bespoke narrative values.
                    SecurityLevel = BaselineSecurityLevel,
                    AvailableObservers = BaselineObservers,
                    TrueStability = BaselineStability,
                    ReportedStability = BaselineStability,
                    ReportCredibility = BaselineReportCredibility
                };
            }

            return sites;
        }

        /// <summary>
        /// 中文：把十进制度经度换算为等距圆柱投影下的万分比横坐标；-180° → 0，+180° → 10000。返回值最小为 1，因为 0 在 SiteState 中表示“不落普通地球地图”。
        /// English: Converts decimal-degree longitude to a ten-thousandths X on the equirectangular projection; -180° maps to 0 and +180° to 10000. The result is clamped to at least 1 because zero means "not placed on the ordinary Earth map" in SiteState.
        /// </summary>
        /// <param name="longitude">中文：十进制度经度，范围 -180..180。English: Longitude in decimal degrees, -180..180.</param>
        /// <returns>中文：1..10000 的万分比横坐标。English: X coordinate in ten-thousandths, 1..10000.</returns>
        private static int LongitudeToMapX(double longitude)
        {
            return ClampMapUnit((int)System.Math.Round((longitude + 180.0) / 360.0 * 10000.0));
        }

        /// <summary>
        /// 中文：把十进制度纬度换算为等距圆柱投影下的万分比纵坐标；+90° → 0（地图上边缘），-90° → 10000。返回值最小为 1，语义同经度换算。
        /// English: Converts decimal-degree latitude to a ten-thousandths Y on the equirectangular projection; +90° maps to 0 (map top) and -90° to 10000. The result is clamped to at least 1 with the same semantics as longitude.
        /// </summary>
        /// <param name="latitude">中文：十进制度纬度，范围 -90..90。English: Latitude in decimal degrees, -90..90.</param>
        /// <returns>中文：1..10000 的万分比纵坐标。English: Y coordinate in ten-thousandths, 1..10000.</returns>
        private static int LatitudeToMapY(double latitude)
        {
            return ClampMapUnit((int)System.Math.Round((90.0 - latitude) / 180.0 * 10000.0));
        }

        /// <summary>中文：把万分比坐标夹进 1..10000，保留 0 作为“无地图点”的唯一含义。English: Clamps ten-thousandths coordinates into 1..10000 so zero keeps its single meaning of "no map point".</summary>
        private static int ClampMapUnit(int value)
        {
            return value < 1 ? 1 : value > 10000 ? 10000 : value;
        }
    }
}
