using System;
using System.Collections.Generic;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>中文：财政页单个并行渠道的只读投影，金额为 64 位整数，风险/关系为万分比。English: Read-only projection of one concurrent funding channel with 64-bit money and ten-thousandth risk/relationship.</summary>
    public sealed class FundingChannelViewModel { public string Key { get; set; }=string.Empty; public string Name { get; set; }=string.Empty; public long Income { get; set; } public long FixedCost { get; set; } public long NetIncome { get; set; } public int Risk { get; set; } public int Relationship { get; set; } public long CycleChange { get; set; } }

    /// <summary>中文：逐人抚恤投影不暴露其他人员私密状态，仅包含财政处理所需稳定 ID、显示称谓、金额和状态。English: Per-person compensation projection exposes no unrelated private personnel state, only stable ID, display label, amount, and fiscal disposition.</summary>
    public sealed class CompensationPersonViewModel { public string PersonnelId { get; set; }=string.Empty; public string Name { get; set; }=string.Empty; public long Amount { get; set; } public CompensationStatus Status { get; set; } }
    public sealed class CompensationIncidentViewModel { public string IncidentId { get; set; }=string.Empty; public string Facility { get; set; }=string.Empty; public long ReportedTick { get; set; } public CompensationStatus Status { get; set; } public int DelayCycles { get; set; } public CompensationPersonViewModel[] Personnel { get; set; }=Array.Empty<CompensationPersonViewModel>(); }

    /// <summary>中文：真实月结历史的只读投影，供底部紧凑趋势按当前在左显示。English: Read-only projection of real settled history for the compact newest-on-left trend strip.</summary>
    public sealed class FiscalCycleViewModel { public int Cycle { get; set; } public long Income { get; set; } public long Expenses { get; set; } public long NetCashFlow { get; set; } public long ClosingCash { get; set; } }

    /// <summary>中文：最近财政决定的公开只读摘要；金额为整数货币，Tick/周期为确定性模拟时间，不暴露隐藏状态。English: Public read-only summary of a recent fiscal decision; amount is integer currency and tick/cycle use deterministic simulation time without exposing hidden state.</summary>
    public sealed class FiscalDecisionViewModel { public string Kind { get; set; }=string.Empty; public string SubjectId { get; set; }=string.Empty; public string Decision { get; set; }=string.Empty; public long Amount { get; set; } public long Tick { get; set; } public int Cycle { get; set; } }

    /// <summary>中文：六列预算表的一行确定性投影；金额均为最小货币单位。PreviousAmount 为空表示新局，ChangeBasis 明确变化是相对集中预算基准还是上一结算周期实绩，避免 UI 自行猜测口径。English: Deterministic row projection for the six-column budget table; money uses the smallest currency unit. A null PreviousAmount denotes a new game, while ChangeBasis explicitly identifies the centralized budget baseline or prior settled actual so UI never guesses the comparison basis.</summary>
    public sealed class BudgetLineViewModel { public string Key { get; set; }=string.Empty; public long BaselineAmount { get; set; } public long? PreviousAmount { get; set; } public long DraftAmount { get; set; } public long ChangeAmount { get; set; } public decimal? ChangePercent { get; set; } public string ChangeBasis { get; set; }=string.Empty; public long MinimumLine { get; set; } public decimal RatioPercent { get; set; } }

    /// <summary>
    /// 中文：把玩家直接输入的“亿元”十进制文本精确转换为模型 long 最小货币单位。仅接受非负、最多两位小数的十进制，不接受空值、指数、千位分隔或溢出；全程使用 decimal 与 checked，确保 385.00 恒等于 38,500,000,000，绝不经过 double。
    /// English: Converts player-entered decimal hundred-million-unit text exactly into model long smallest-currency units. Only non-negative decimals with at most two fractional digits are accepted; blank, exponent, grouping, and overflow inputs are rejected. decimal and checked arithmetic guarantee 385.00 always equals 38,500,000,000 without double rounding.
    /// </summary>
    public static class FinanceBudgetAmountParser
    {
        public const long CurrencyUnitsPerYi=100_000_000L;
        public static bool TryParseYi(string text,out long amount,out string error)
        {
            amount=0;error=string.Empty;if(string.IsNullOrWhiteSpace(text)){error="请输入预算金额";return false;}
            string value=text.Trim();int point=value.IndexOf('.');if(point>=0&&(value.IndexOf('.',point+1)>=0||value.Length-point-1>2)){error="仅允许最多两位小数";return false;}
            if(!decimal.TryParse(value,System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out decimal yi)||yi<0){error="请输入非负十进制亿元金额";return false;}
            try{decimal currency=checked(yi*CurrencyUnitsPerYi);if(currency!=decimal.Truncate(currency)||currency>long.MaxValue){error="金额超出可保存范围";return false;}amount=decimal.ToInt64(currency);return true;}catch(OverflowException){error="金额超出可保存范围";return false;}
        }
        public static string FormatYi(long amount)=>(amount/(decimal)CurrencyUnitsPerYi).ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 中文：把财政页固定九项亿元文本作为不可分割事务组装成 BudgetState；缺失或无效值整体拒绝。English: Atomically assembles the finance page's fixed nine yi-denominated texts into a BudgetState; any missing or invalid value rejects the whole transaction.
    /// </summary>
    public static class FinanceBudgetDraftAssembler
    {
        public static bool TryAssemble(IReadOnlyDictionary<string,string> texts,BudgetViewModel source,out BudgetState? budget,out string error)
        {
            budget=null;error=string.Empty;
            if(!TryRead(texts,"设施运营",out long site,out error)||!TryRead(texts,"收容维护",out long containment,out error)||!TryRead(texts,"研究与实验",out long research,out error)||!TryRead(texts,"安保",out long security,out error)||!TryRead(texts,"普通 MTF",out long mtf,out error)||!TryRead(texts,"Alpha-1",out long alphaOne,out error)||!TryRead(texts,"帷幕与掩盖",out long veil,out error)||!TryRead(texts,"行政与情报",out long administration,out error)||!TryRead(texts,"人员与伦理保障",out long personnel,out error))return false;
            budget=new BudgetState{SiteOperations=site,ContainmentMaintenance=containment,Research=research,Security=security,MobileTaskForces=mtf,AlphaOne=alphaOne,VeilAndCover=veil,AdministrationAndIntelligence=administration,PersonnelAndEthics=personnel,ResearchDetail=new ResearchBudgetDetail{BasicResearch=source.ResearchDetail.BasicResearch,PriorityProjects=source.ResearchDetail.PriorityProjects,ContainmentTechnology=source.ResearchDetail.ContainmentTechnology,AnomalousApplications=source.ResearchDetail.AnomalousApplications},SecurityDetail=new SecurityBudgetDetail{SiteSecurity=source.SecurityDetail.SiteSecurity,MtfHeadquarters=source.SecurityDetail.MtfHeadquarters,MtfTeamCount=source.SecurityDetail.MtfTeamCount,MtfTeamMaintenance=source.SecurityDetail.MtfTeamMaintenance,MtfDeployment=source.SecurityDetail.MtfDeployment,AlphaOne=source.SecurityDetail.AlphaOne},VeilOperations=(long[])source.VeilOperations.Clone()};return true;
        }

        private static bool TryRead(IReadOnlyDictionary<string,string> texts,string key,out long amount,out string error)
        {
            amount=0;if(!texts.TryGetValue(key,out string? text)){error=key+"：缺少预算金额";return false;}if(!FinanceBudgetAmountParser.TryParseYi(text,out amount,out string detail)){error=key+"："+detail;return false;}error=string.Empty;return true;
        }
    }

    /// <summary>中文：右栏不依赖最近决定是否存在的定量摘要；金额单位为整数货币，事故数量为份，确保空决定不能吞掉风险、义务或事故。English: Quantitative right-column summary independent of recent-decision availability; money uses integer currency and incident count uses cases, ensuring empty decisions cannot suppress risks, obligations, or incidents.</summary>
    public sealed class FinanceRiskSummaryViewModel { public long CashFlow { get; set; } public decimal ReserveMonths { get; set; } public long LiquidityGap { get; set; } public long UnpaidObligations { get; set; } public int PendingIncidentCount { get; set; } }

    /// <summary>
    /// 中文：底部布局策略接收玩家显式展开状态与当前事故选择；事故被选中时默认展开，但玩家仍可收起。返回值只控制约 20%/35% 的确定性比例，不依赖像素或窗口尺寸。
    /// English: The bottom-layout policy accepts the player's explicit expansion state and current incident selection; selecting an incident defaults to expanded, while the player may still collapse it. The result controls only deterministic roughly 20%/35% ratios and never depends on pixels or window size.
    /// </summary>
    /// <param name="selectedIncidentId">中文：当前事故稳定 ID；空字符串表示普通科目。English: Stable id of the selected incident; empty means an ordinary category.</param>
    /// <param name="expandedByUser">中文：当前可折叠控件状态。English: Current state of the disclosure control.</param>
    /// <returns>中文：需要扩展底部明细时为 true。English: True when the bottom detail should use its expanded share.</returns>
    public static class FinanceDetailLayoutPolicy { public static bool IsExpanded(string selectedIncidentId,bool expandedByUser)=>expandedByUser; }

    /// <summary>中文：财政页六指标与编辑状态的完整公开投影；储备月数为 decimal，必要支出为零时固定为零而非无穷。English: Complete public projection of six finance indicators and edit state; reserve months uses decimal and is deterministically zero when necessary spending is zero rather than infinity.</summary>
    public sealed class FinanceViewModel
    {
        public long AvailableCash { get; set; }
        public long TotalAssets { get; set; }
        public long ReserveBalance { get; set; }
        public long NecessaryMonthlyExpenses { get; set; }
        public long MonthlyIncome { get; set; }
        public long MonthlyExpenses { get; set; }
        public long NetCashFlow { get; set; }
        public decimal ReserveMonths { get; set; }
        public long AnomalyCosts { get; set; }
        public bool IsDraftRecorded { get; set; }
        /// <summary>中文：最近一次草案保存的模拟 Tick 与周期；-1 表示无记录，单位分别为模拟小时 Tick 和财政周期。English: Simulation tick and fiscal cycle of the latest draft save; -1 means no record, measured respectively in simulation-hour ticks and fiscal cycles.</summary>
        public long DraftRecordedTick { get; set; } = -1;
        public int DraftRecordedCycle { get; set; } = -1;
        public bool IsBudgetSignedThisCycle { get; set; }
        public BudgetViewModel EnactedBudget { get; set; }=new BudgetViewModel();
        public BudgetViewModel DraftBudget { get; set; }=new BudgetViewModel();
        /// <summary>中文：预算表固定十行、六列所需的完整字段，不把变化口径或显示计算散落到容器尺寸敏感的 UI。English: Complete fields for the fixed ten-row, six-column budget table, keeping comparison semantics and display calculations out of size-sensitive UI containers.</summary>
        public BudgetLineViewModel[] BudgetLines { get; set; }=Array.Empty<BudgetLineViewModel>();
        /// <summary>中文：右栏定量摘要与决定数组相互独立，任何空决定边界都不得影响其存在。English: Quantitative right-column summary is independent from the decisions array, so an empty-decision boundary can never remove it.</summary>
        public FinanceRiskSummaryViewModel RiskSummary { get; set; }=new FinanceRiskSummaryViewModel();
        public FundingChannelViewModel[] Channels { get; set; }=Array.Empty<FundingChannelViewModel>();
        public CompensationIncidentViewModel[] CompensationIncidents { get; set; }=Array.Empty<CompensationIncidentViewModel>();
        public FiscalCycleViewModel[] CycleHistory { get; set; }=Array.Empty<FiscalCycleViewModel>();
        public FiscalDecisionViewModel[] RecentDecisions { get; set; }=Array.Empty<FiscalDecisionViewModel>();
    }
}
