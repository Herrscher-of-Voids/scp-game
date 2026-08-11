using System;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>中文：九项一级预算及只读二级明细投影；所有数组和对象均从世界状态深拷贝。English: Projection of nine primary budgets plus read-only secondary detail; arrays and objects are deep-copied from world state.</summary>
    public sealed class BudgetViewModel
    {
        public long SiteOperations { get; set; }
        public long ContainmentMaintenance { get; set; }
        public long Research { get; set; }
        public long Security { get; set; }
        public long MobileTaskForces { get; set; }
        public long AlphaOne { get; set; }
        public long VeilAndCover { get; set; }
        public long AdministrationAndIntelligence { get; set; }
        public long PersonnelAndEthics { get; set; }
        public ResearchBudgetDetail ResearchDetail { get; set; } = new ResearchBudgetDetail();
        public SecurityBudgetDetail SecurityDetail { get; set; } = new SecurityBudgetDetail();
        public long[] VeilOperations { get; set; } = new long[7];
    }
}
