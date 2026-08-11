using System;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>中文：报告页只读投影，保留审批所需公开字段且不暴露 WorldState 引用。English: Read-only report-page projection retaining public approval fields without exposing WorldState references.</summary>
    public sealed class ReportViewModel
    {
        public string Id { get; set; } = string.Empty;
        public ReportCategory Category { get; set; }
        public ReportRisk Risk { get; set; }
        public ReportStatus Status { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public long CreatedTick { get; set; }
        public string Source { get; set; } = string.Empty;
        public bool AllowsBatch { get; set; }
    }

    /// <summary>中文：公开审批历史投影，数组与条件值均从世界快照复制。English: Public approval-history projection whose arrays and condition values are copied from the world snapshot.</summary>
    public sealed class ReportApprovalViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string[] ReportIds { get; set; } = Array.Empty<string>();
        public ReportStatus Decision { get; set; }
        public long DecidedTick { get; set; }
        public string Conditions { get; set; } = string.Empty;
    }
}
