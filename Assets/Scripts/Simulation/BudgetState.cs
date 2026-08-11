using System;

namespace Scp.Simulation
{
    /// <summary>
    /// 中文：九个互不合并的一级月度预算，全部金额为非负 64 位整数货币单位；研究、安全与七洲帷幕数组仅是明细，TotalSpending 只累计一级科目。
    /// English: Nine independent primary monthly budgets, all non-negative 64-bit integer currency units; research, security, and continent arrays are detail only, while TotalSpending sums primary categories exactly once.
    /// </summary>
    public sealed class BudgetState
    {
        public const int AlphaOneRebuildCyclesRequired = 3;
        public long SiteOperations { get; set; }
        public long ContainmentMaintenance { get; set; }
        public long Research { get; set; }
        public long Security { get; set; }
        public long MobileTaskForces { get; set; }
        public long AlphaOne { get; set; }
        public long VeilAndCover { get; set; }
        public long AdministrationAndIntelligence { get; set; }
        public long PersonnelAndEthics { get; set; }
        [Obsolete("Legacy save compatibility only; emergency reserve is persisted on EconomyState.")]
        public long EmergencyReserve { get; set; }
        public ResearchBudgetDetail ResearchDetail { get; set; } = new ResearchBudgetDetail();
        public SecurityBudgetDetail SecurityDetail { get; set; } = new SecurityBudgetDetail();
        public long[] VeilOperations { get; set; } = new long[7];

        /// <summary>中文：使用 checked 汇总九个一级科目，金额单位为每月最小货币单位；大额越界明确失败，应急储备不再是支出。English: Sums nine primary categories in checked arithmetic, in smallest currency units per month; overflow fails explicitly and emergency reserve is not spending.</summary>
        public long TotalSpending() => checked(SiteOperations + ContainmentMaintenance + Research + Security + MobileTaskForces + AlphaOne + VeilAndCover + AdministrationAndIntelligence + PersonnelAndEthics);

        /// <summary>中文：返回九项月度业务支出，与 TotalSpending 同口径；异常成本在月结时另行加入。English: Returns nine-category monthly business spending using the same basis as TotalSpending; anomaly costs are added separately during settlement.</summary>
        public long MonthlySpending() => TotalSpending();

        /// <summary>中文：返回必要月度支出，排除研究且不加入异常成本；金额单位为每月最小货币单位。English: Returns necessary monthly spending excluding research and anomaly costs; amount is measured in smallest currency units per month.</summary>
        public long NecessaryMonthlySpending() => checked(SiteOperations + ContainmentMaintenance + Security + MobileTaskForces + AlphaOne + VeilAndCover + AdministrationAndIntelligence + PersonnelAndEthics);

        /// <summary>中文：创建深拷贝供草案持久化，防止 UI 草案修改正式预算或数组明细。English: Creates a deep copy for persisted drafts so UI edits cannot mutate the enacted budget or its array details.</summary>
        public BudgetState Clone() => new BudgetState
        {
            SiteOperations = SiteOperations, ContainmentMaintenance = ContainmentMaintenance, Research = Research, Security = Security,
            MobileTaskForces = MobileTaskForces, AlphaOne = AlphaOne, VeilAndCover = VeilAndCover,
            AdministrationAndIntelligence = AdministrationAndIntelligence, PersonnelAndEthics = PersonnelAndEthics,
            ResearchDetail = new ResearchBudgetDetail { BasicResearch = ResearchDetail.BasicResearch, PriorityProjects = ResearchDetail.PriorityProjects, ContainmentTechnology = ResearchDetail.ContainmentTechnology, AnomalousApplications = ResearchDetail.AnomalousApplications },
            SecurityDetail = new SecurityBudgetDetail { SiteSecurity = SecurityDetail.SiteSecurity, MtfHeadquarters = SecurityDetail.MtfHeadquarters, MtfTeamCount = SecurityDetail.MtfTeamCount, MtfTeamMaintenance = SecurityDetail.MtfTeamMaintenance, MtfDeployment = SecurityDetail.MtfDeployment, AlphaOne = SecurityDetail.AlphaOne },
            VeilOperations = (long[])(VeilOperations ?? Array.Empty<long>()).Clone()
        };
    }
}
