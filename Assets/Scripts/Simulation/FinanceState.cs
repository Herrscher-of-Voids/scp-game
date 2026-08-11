using System;

namespace Scp.Simulation
{
    /// <summary>
    /// 中文：四类长期并行资金渠道的确定性财务快照；金额单位为基金会货币整数，风险与关系为 0..10000 万分比。本周期变化不参与结算，只用于解释趋势。
    /// English: Deterministic financial snapshot for one of four concurrent long-running funding channels; money uses integer Foundation currency units, while risk and relationship use 0..10000 ten-thousandths. Cycle change is explanatory and is not settled again.
    /// </summary>
    public sealed class FundingChannelState
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public long Income { get; set; }
        public long FixedCost { get; set; }
        public int Risk { get; set; }
        public int Relationship { get; set; }
        public long CycleChange { get; set; }
        public long NetIncome => Income - FixedCost;
    }

    /// <summary>
    /// 中文：研究一级预算的四项二级分配；四项之和必须等于研究一级总额，避免结算重复计算。
    /// English: Four secondary allocations beneath the research primary budget; their sum must equal the primary research total so settlement cannot double-count detail rows.
    /// </summary>
    public sealed class ResearchBudgetDetail
    {
        public long BasicResearch { get; set; }
        public long PriorityProjects { get; set; }
        public long ContainmentTechnology { get; set; }
        public long AnomalousApplications { get; set; }
        public long Total() => checked(BasicResearch + PriorityProjects + ContainmentTechnology + AnomalousApplications);
    }

    /// <summary>
    /// 中文：安全力量的真实聚合明细；普通队伍只记录数量与维护/部署汇总，不伪造未经来源核验的具名 MTF 清单。
    /// English: Real aggregate detail for security forces; ordinary teams store only count and maintenance/deployment totals, avoiding fabricated named MTF rosters without verified sources.
    /// </summary>
    public sealed class SecurityBudgetDetail
    {
        public long SiteSecurity { get; set; }
        public long MtfHeadquarters { get; set; }
        public int MtfTeamCount { get; set; }
        public long MtfTeamMaintenance { get; set; }
        public long MtfDeployment { get; set; }
        public long AlphaOne { get; set; }
        public long OrdinaryMtfTotal() => checked(MtfHeadquarters + MtfTeamMaintenance + MtfDeployment);
    }

    public enum CompensationStatus
    {
        Pending,
        Paid,
        Delayed,
        Refused
    }

    /// <summary>
    /// 中文：单名殉职人员的待签补偿；金额由玩家填写，零表示尚未决定，系统不替玩家设置道德答案。
    /// English: Pending compensation for one fallen staff member; the player supplies the amount, and zero means undecided—the system never chooses a moral answer.
    /// </summary>
    public sealed class FallenPersonnelCompensation
    {
        public string PersonnelId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public long Amount { get; set; }
        public CompensationStatus Status { get; set; }
    }

    /// <summary>
    /// 中文：事故级抚恤责任，保存设施、报告 Tick、逐人金额和处理状态；数组顺序是确定性的显示与存档顺序。
    /// English: Incident-level compensation obligation containing facility, report tick, per-person amounts, and disposition; array order is deterministic for display and saves.
    /// </summary>
    public sealed class CompensationIncidentState
    {
        public string IncidentId { get; set; } = string.Empty;
        public string FacilityLabel { get; set; } = string.Empty;
        public long ReportedTick { get; set; }
        public CompensationStatus Status { get; set; }
        public int DelayCycles { get; set; }
        public FallenPersonnelCompensation[] Personnel { get; set; } = Array.Empty<FallenPersonnelCompensation>();
    }

    /// <summary>
    /// 中文：财政责任链的只追加记录；金额单位为整数货币，Tick 与周期用于确定性审计，不保存现实时间。
    /// English: Append-only fiscal accountability record; amount uses integer currency and deterministic tick/cycle replace wall-clock time for auditing.
    /// </summary>
    public sealed class FiscalHistoryRecord
    {
        public string Kind { get; set; } = string.Empty;
        public string SubjectId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public long Tick { get; set; }
        public int Cycle { get; set; }
        public string Decision { get; set; } = string.Empty;
    }

    /// <summary>
    /// 中文：单个已结算周期的紧凑趋势快照；新快照按九项预算比较，旧快照中的第十项兼容值不会进入现行支出。English: Compact settled-cycle snapshot; new comparisons use nine budgets and any legacy tenth-category compatibility value never enters current spending.
    /// </summary>
    public sealed class FiscalCycleSnapshot
    {
        public int Cycle { get; set; }
        public long Income { get; set; }
        public long Expenses { get; set; }
        public long NetCashFlow { get; set; }
        public long ClosingCash { get; set; }
        public BudgetState? SettledBudget { get; set; }
    }
}
