namespace Scp.Simulation
{
    /// <summary>
    /// 中文：财政纵向切片的集中临时平衡配置；所有金额为待平衡的 64 位整数货币值，不代表最终世界观定稿。
    /// English: Central temporary balance configuration for the finance vertical slice; all amounts are provisional 64-bit integer currency values and are not final lore balance.
    /// </summary>
    public static class EconomyRules
    {
        public const long LegacyDemoStartingAvailableCash = 8_000_000L;
        public const long LegacyDemoPrimaryBudgetTotal = 900_000L;
        public const long TemporaryStartingAvailableCash = 800_000_000_000L;
        public const long TemporaryStartingTotalAssets = 8_000_000_000_000L;
        public const long MobileTaskForceUnitCost = 1_000_000_000L;
        public const long AlphaOneMaintenanceCost = MobileTaskForceUnitCost * 10;

        /// <summary>
        /// 中文：构造全球基金会临时月度预算，九项一级科目合计 1,180 亿元；固定返回全新对象以保证确定性隔离。English: Creates the provisional global monthly budget with nine primary categories totaling 118 billion; a fresh object preserves deterministic isolation.
        /// </summary>
        public static BudgetState CreateTemporaryPrimaryBudget() => new BudgetState
        {
            SiteOperations = 18_000_000_000L,
            ContainmentMaintenance = 21_000_000_000L,
            Research = 14_000_000_000L,
            Security = 12_000_000_000L,
            MobileTaskForces = 16_000_000_000L,
            AlphaOne = AlphaOneMaintenanceCost,
            VeilAndCover = 11_000_000_000L,
            AdministrationAndIntelligence = 7_000_000_000L,
            PersonnelAndEthics = 9_000_000_000L,
            ResearchDetail = new ResearchBudgetDetail { BasicResearch=4_000_000_000L, PriorityProjects=3_500_000_000L, ContainmentTechnology=4_000_000_000L, AnomalousApplications=2_500_000_000L },
            SecurityDetail = new SecurityBudgetDetail { SiteSecurity=12_000_000_000L, MtfHeadquarters=3_000_000_000L, MtfTeamCount=240, MtfTeamMaintenance=11_000_000_000L, MtfDeployment=2_000_000_000L, AlphaOne=AlphaOneMaintenanceCost },
            VeilOperations = new[] { 1_900_000_000L, 1_400_000_000L, 1_800_000_000L, 2_300_000_000L, 1_300_000_000L, 1_100_000_000L, 1_200_000_000L }
        };

        /// <summary>中文：构造四类并行收入临时值；固定顺序保证结算、存档和 UI 一致。English: Creates provisional values for all four concurrent income channels; fixed order keeps settlement, saves, and UI deterministic.</summary>
        public static FundingChannelState[] CreateTemporaryFundingChannels() => new[]
        {
            new FundingChannelState { Key="CorporateFronts", DisplayName="前台公司", Income=46_000_000_000L, FixedCost=9_000_000_000L, Risk=2400, Relationship=7200, CycleChange=1_200_000_000L },
            new FundingChannelState { Key="GovernmentSupport", DisplayName="政府与国际支持", Income=38_000_000_000L, FixedCost=4_000_000_000L, Risk=3100, Relationship=6800, CycleChange=-800_000_000L },
            new FundingChannelState { Key="TechnologyTransfer", DisplayName="技术专利与转化", Income=29_000_000_000L, FixedCost=7_000_000_000L, Risk=4200, Relationship=6100, CycleChange=2_100_000_000L },
            new FundingChannelState { Key="AssetInvestment", DisplayName="资产没收与投资", Income=34_000_000_000L, FixedCost=6_000_000_000L, Risk=4800, Relationship=5700, CycleChange=600_000_000L }
        };

        /// <summary>中文：四渠道净收入并行求和，使用 checked 防止大额静默溢出。English: Sums net income from all four channels concurrently under checked arithmetic to prevent silent overflow.</summary>
        public static long ParallelNetIncome(FundingChannelState[] channels)
        {
            long total = 0;
            foreach (FundingChannelState channel in channels ?? System.Array.Empty<FundingChannelState>()) total = checked(total + channel.NetIncome);
            return total;
        }
    }
}
