using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    public sealed class CommandLogEntry
    {
        public string Kind { get; set; } = string.Empty;

        public long SubmittedAtTick { get; set; }

        public long Amount { get; set; }

        public ClearanceLevel RequiredClearance { get; set; }

        /// <summary>中文：以下字段是无类型名的命令参数快照；只记录 M1 命令所需值。English: The following fields are untyped-name command argument snapshots and contain only values required by M1 commands.</summary>
        public BudgetState Budget { get; set; } = new BudgetState();
        public FundingSource Source { get; set; }
        public ProposalKind ProposalKind { get; set; }
        public ProposalThreshold Threshold { get; set; }
        public AxisPosition Position { get; set; }
        public int ProposalId { get; set; }
        public VoteChoice Choice { get; set; }
        public SeatId SeatId { get; set; }
        public int SupportBonus { get; set; }
        public bool ExchangeSupport { get; set; }
        public int PressureAmount { get; set; }
        public SiteId SiteId { get; set; }
        public long Cost { get; set; }
        public ProposalKind EmergencyAction { get; set; }
        public int Count { get; set; }

        /// <summary>中文：报告审批命令的稳定 ID、决定和严格条件原文，确保待执行命令恢复不丢参。English: Stable IDs, decision, and strict condition source for report approval so pending-command restoration loses no arguments.</summary>
        public string[] ReportIds { get; set; } = System.Array.Empty<string>();
        public ReportStatus ReportDecision { get; set; }
        public string Conditions { get; set; } = string.Empty;

        /// <summary>中文：财政抚恤命令的稳定事故/人员 ID 与处理状态；金额复用 Amount，确保待执行命令和永久历史无损恢复。English: Stable incident/person IDs and disposition for finance-compensation commands; Amount is reused so pending commands and permanent history restore losslessly.</summary>
        public string IncidentId { get; set; } = string.Empty;
        public string PersonnelId { get; set; } = string.Empty;
        public CompensationStatus CompensationDecision { get; set; }

        /// <summary>中文：帷幕处置命令使用匿名事件稳定 ID 与动作枚举，保证待执行队列和永久日志无损恢复。English: Veil response commands use an anonymous incident stable ID and action enum for lossless pending-queue and permanent-log restoration.</summary>
        public string VeilIncidentId { get; set; } = string.Empty;
        public VeilActionKind VeilAction { get; set; }
    }
}
