using System;

namespace Scp.Application
{
    /// <summary>
    /// O5 视角的完整投影结果。界面只读这个对象，绝不直接访问 WorldState
    /// （04-技术架构.md 第 5 节）。凡是 O5 不该看到的信息都不出现在此类中：
    /// NPC 真实立场、席位关系、压力值、隐藏伦理移除风险一律不投影。
    /// </summary>
    public sealed class OverseerViewModel
    {
        /// <summary>
        /// 世界推进的绝对 Tick 数（1 Tick = 游戏内 1 小时）。属于公开信息，不涉及权限过滤。
        /// </summary>
        public long Tick { get; set; }

        /// <summary>
        /// 当前议会周期序号（每 720 Tick 一个周期）。属于公开信息。
        /// </summary>
        public int CurrentCycle { get; set; }

        /// <summary>基金会历法年份。由 Tick 与存档模式的起始年月推出，见 FoundationCalendar。</summary>
        public int CalendarYear { get; set; }

        /// <summary>基金会历法月份，1–12。顶栏显示「当前年月」用。</summary>
        public int CalendarMonth { get; set; }

        /// <summary>当前周期内已经过的天数，0–29。用于顶栏的周期进度提示。</summary>
        public int DayOfCycle { get; set; }

        /// <summary>基金会当前可用总资金。顶栏「余额」。</summary>
        public long Funds { get; set; }

        /// <summary>
        /// 上一个周期结算出的现金流。由 Funds 与 Budget 推导而来，不含隐藏信息。
        /// 顶栏「本月净流量」。
        /// </summary>
        public long LastCashFlow { get; set; }

        /// <summary>上一周期的总收入。财政页「全局资金来源」用。</summary>
        public long LastIncome { get; set; }

        /// <summary>上一周期的总支出。财政页「支出分类」的合计校验值。</summary>
        public long LastExpenses { get; set; }

        /// <summary>当前资金来源渠道。决定基础收入水平。</summary>
        public Scp.Domain.FundingSource FundingSource { get; set; }

        /// <summary>连续赤字周期数。达到 3 且储备清零即财政崩溃。</summary>
        public int ConsecutiveDeficitCycles { get; set; }

        /// <summary>各科目正式拨款额度。财政页两级拨款的第一级。Enacted allocation by primary finance category.</summary>
        public BudgetViewModel Budget { get; set; } = new BudgetViewModel();

        /// <summary>中文：财政纵向切片的六指标、四渠道、草案与抚恤公开投影。English: Public finance-slice projection containing six indicators, four channels, draft state, and compensation obligations.</summary>
        public FinanceViewModel Finance { get; set; } = new FinanceViewModel();

        /// <summary>七大洲帷幕完整度，万分比定点数，索引对应 Continent 枚举。</summary>
        public int[] VeilByContinent { get; set; } = Array.Empty<int>();

        /// <summary>全球总帷幕数，为七洲加权汇总。</summary>
        public int GlobalVeil { get; set; }

        /// <summary>中文：帷幕纵向切片的匿名事件、传播节点、时间线和专属警报投影。English: Veil-slice projection for anonymous incidents, propagation nodes, timelines, and veil-only alerts.</summary>
        public VeilViewModel Veil { get; set; } = new VeilViewModel();

        /// <summary>各设施的可见数据。含结构性事实与可能失真的自报值。</summary>
        public SiteReportViewModel[] Sites { get; set; } = Array.Empty<SiteReportViewModel>();

        /// <summary>中文：四类业务报告的公开投影。English: Public projection of all four business-report categories.</summary>
        public ReportViewModel[] Reports { get; set; } = Array.Empty<ReportViewModel>();

        /// <summary>中文：公开审批历史，用于报告页审计显示。English: Public approval history for report-page audit display.</summary>
        public ReportApprovalViewModel[] ReportApprovals { get; set; } = Array.Empty<ReportApprovalViewModel>();

        /// <summary>议会席位。只有编号与占位状态，没有立场与关系。</summary>
        public CouncilSeatViewModel[] Seats { get; set; } = Array.Empty<CouncilSeatViewModel>();

        /// <summary>公开投票记录。玩家认识十二位 NPC 的唯一途径。</summary>
        public VoteRecordViewModel[] VoteRecords { get; set; } = Array.Empty<VoteRecordViewModel>();

        /// <summary>议案列表，含已决与待决。待决项构成左栏「待办任务」。</summary>
        public ProposalViewModel[] Proposals { get; set; } = Array.Empty<ProposalViewModel>();

        /// <summary>
        /// 由世界状态派生的全球警报，按危急优先排序。
        /// 派生逻辑见 OverseerAlertService，只使用 O5 可见的数据。
        /// </summary>
        public OverseerAlertViewModel[] Alerts { get; set; } = Array.Empty<OverseerAlertViewModel>();

        /// <summary>MTF Alpha-1 的状态。</summary>
        public AlphaOneViewModel AlphaOne { get; set; } = new AlphaOneViewModel();

        /// <summary>接触异常的限制是否仍然生效。</summary>
        public bool ContactRestrictionActive { get; set; }

        /// <summary>「重启世界」议案当前是否可提交。依赖对应战略能力是否存续。</summary>
        public bool CanSubmitWorldRestart { get; set; }

        /// <summary>记忆删除供应链当前是否可用。</summary>
        public bool AmnesticSupplyAvailable { get; set; }

        /// <summary>失败状态。IsEnded 为 true 时界面必须停止推进并转入文字尾声。</summary>
        public FailureViewModel Failure { get; set; } = new FailureViewModel();
    }
}
