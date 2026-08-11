using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Scp.Domain;

namespace Scp.Simulation
{
    /// <summary>
    /// 中文：审批一份或一批报告；Validate 完整检查后 Apply 才一次性提交，保证批量失败时零状态变更。
    /// English: Decides one or a batch of reports; Apply commits only after full Validate succeeds, guaranteeing zero mutation when a batch fails.
    /// </summary>
    public sealed class ReportApprovalCommand : ICommand
    {
        public string[] ReportIds { get; set; } = Array.Empty<string>();
        public ReportStatus Decision { get; set; }
        public string Conditions { get; set; } = string.Empty;
        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery query)
        {
            var access = O5CommandValidation.Validate(query);
            if (!access.IsValid) return access;
            if (ReportIds == null || ReportIds.Length == 0 || ReportIds.Any(string.IsNullOrWhiteSpace)) return ValidationResult.Failure("At least one report ID is required.");
            if (ReportIds.Distinct(StringComparer.Ordinal).Count() != ReportIds.Length) return ValidationResult.Failure("Duplicate report IDs are not allowed.");
            if (Decision != ReportStatus.Approved && Decision != ReportStatus.Rejected && Decision != ReportStatus.Returned && Decision != ReportStatus.ConditionallyApproved) return ValidationResult.Failure("Unsupported report decision.");

            var reports = new List<ReportState>();
            foreach (string id in ReportIds)
            {
                ReportState? report = query.FindReport(id);
                if (report == null) return ValidationResult.Failure("Report not found: " + id);
                if (report.Status != ReportStatus.Pending) return ValidationResult.Failure("Report is not pending: " + id);
                reports.Add(report);
            }

            if (reports.Count > 1 && (reports.Any(item => item.Risk != ReportRisk.Low || !item.AllowsBatch) || reports.Any(item => item.Category != reports[0].Category)))
                return ValidationResult.Failure("Batch approval requires pending, batch-enabled, low-risk reports of one category.");

            ParsedReportConditions parsed;
            string error;
            if (!ReportConditionParser.TryParse(Conditions, out parsed, out error)) return ValidationResult.Failure(error);
            if (Decision == ReportStatus.ConditionallyApproved && parsed.Count == 0) return ValidationResult.Failure("Conditional approval requires at least one condition.");
            if (Decision != ReportStatus.ConditionallyApproved && parsed.Count > 0) return ValidationResult.Failure("Conditions are only valid for conditional approval.");
            return ValidationResult.Success();
        }

        public void Apply(WorldState world, IEventSink events)
        {
            ParsedReportConditions parsed;
            string ignored;
            ReportConditionParser.TryParse(Conditions, out parsed, out ignored);
            foreach (ReportState report in world.Reports.Where(item => ReportIds.Contains(item.Id, StringComparer.Ordinal))) report.Status = Decision;
            var records = new List<ReportApprovalRecord>(world.ReportApprovals)
            {
                new ReportApprovalRecord
                {
                    Id = "APR-" + world.Tick.ToString("D10", CultureInfo.InvariantCulture) + "-" + (world.ReportApprovals.Length + 1).ToString("D6", CultureInfo.InvariantCulture),
                    ReportIds = (string[])ReportIds.Clone(), Decision = Decision, DecidedTick = world.Tick,
                    Conditions = parsed.Canonical, BudgetCap = parsed.BudgetCap, DeadlineCycles = parsed.DeadlineCycles, AuditRequired = parsed.AuditRequired
                }
            };
            world.ReportApprovals = records.ToArray();
            events.Emit(new DomainEvent { Kind = DomainEventKind.ReportDecision, Tick = world.Tick, Detail = Decision + ":" + string.Join(",", ReportIds) });
        }
    }

    /// <summary>中文：严格解析受支持的 key=value 分号条件。English: Strictly parses supported semicolon-delimited key=value conditions.</summary>
    public static class ReportConditionParser
    {
        public static bool TryParse(string? text, out ParsedReportConditions result, out string error)
        {
            result = new ParsedReportConditions(); error = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string raw in text.Split(';'))
            {
                string part = raw.Trim();
                int separator = part.IndexOf('=');
                if (part.Length == 0 || separator <= 0 || separator != part.LastIndexOf('=') || separator == part.Length - 1) { error = "Conditions must use key=value entries separated by semicolons."; return false; }
                string key = part.Substring(0, separator).Trim(); string value = part.Substring(separator + 1).Trim();
                if (!seen.Add(key)) { error = "Duplicate condition key: " + key; return false; }
                switch (key)
                {
                    case "budget_cap": if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long cap) || cap < 0) { error = "budget_cap must be a non-negative integer."; return false; } result.BudgetCap = cap; break;
                    case "deadline_cycles": if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int cycles) || cycles <= 0) { error = "deadline_cycles must be a positive integer."; return false; } result.DeadlineCycles = cycles; break;
                    case "audit_required": if (value != "true" && value != "false") { error = "audit_required must be true or false."; return false; } result.AuditRequired = value == "true"; break;
                    default: error = "Unsupported condition key: " + key; return false;
                }
            }
            result.Count = seen.Count;
            result.Canonical = string.Join(";", new[] { result.BudgetCap.HasValue ? "budget_cap=" + result.BudgetCap.Value.ToString(CultureInfo.InvariantCulture) : null, result.DeadlineCycles.HasValue ? "deadline_cycles=" + result.DeadlineCycles.Value.ToString(CultureInfo.InvariantCulture) : null, result.AuditRequired.HasValue ? "audit_required=" + result.AuditRequired.Value.ToString().ToLowerInvariant() : null }.Where(item => item != null));
            return true;
        }
    }

    public sealed class ParsedReportConditions
    {
        public int Count { get; set; }
        public string Canonical { get; set; } = string.Empty;
        public long? BudgetCap { get; set; }
        public int? DeadlineCycles { get; set; }
        public bool? AuditRequired { get; set; }
    }
}
