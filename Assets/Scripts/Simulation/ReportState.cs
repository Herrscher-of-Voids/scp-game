using System;

namespace Scp.Simulation
{
    /// <summary>
    /// 中文：报告类别固定覆盖设施、异常、人员和外部事务；枚举值参与存档与批量同类校验，不得按显示文本推断。
    /// English: Report categories cover facility, anomaly, personnel, and external affairs; enum values participate in saves and same-category batch validation and must not be inferred from display text.
    /// </summary>
    public enum ReportCategory { Facility, Anomaly, Personnel, External }

    /// <summary>中文：报告风险从低到危急，批量操作只接受 Low。English: Report risk ranges from low to critical, and batch operations accept Low only.</summary>
    public enum ReportRisk { Low, Medium, High, Critical }

    /// <summary>中文：Pending 是唯一可审批状态，其余四项是公开终态。English: Pending is the only actionable state; the other four values are public terminal decisions.</summary>
    public enum ReportStatus { Pending, Approved, Rejected, Returned, ConditionallyApproved }

    /// <summary>
    /// 中文：持久化一份 O5 可见报告；ID、标题、摘要、创建 Tick、来源和批量许可均由模拟层确定，UI 不得改写。
    /// English: Persists one O5-visible report; ID, title, summary, creation tick, source, and batch permission are determined by simulation and cannot be rewritten by UI.
    /// </summary>
    public sealed class ReportState
    {
        public string Id { get; set; } = string.Empty;
        public ReportCategory Category { get; set; }
        public ReportRisk Risk { get; set; }
        public ReportStatus Status { get; set; } = ReportStatus.Pending;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public long CreatedTick { get; set; }
        public string Source { get; set; } = string.Empty;
        public bool AllowsBatch { get; set; }
    }

    /// <summary>
    /// 中文：公开审批记录保存一次原子命令的全部报告 ID、决定、原始规范条件及解析值；金额单位为基金会货币单位，期限单位为结算周期。
    /// English: A public approval record stores all report IDs from one atomic command, its decision, canonical conditions, and parsed values; budget uses Foundation currency units and deadline uses settlement cycles.
    /// </summary>
    public sealed class ReportApprovalRecord
    {
        public string Id { get; set; } = string.Empty;
        public string[] ReportIds { get; set; } = Array.Empty<string>();
        public ReportStatus Decision { get; set; }
        public long DecidedTick { get; set; }
        public string Conditions { get; set; } = string.Empty;
        public long? BudgetCap { get; set; }
        public int? DeadlineCycles { get; set; }
        public bool? AuditRequired { get; set; }
    }
}
