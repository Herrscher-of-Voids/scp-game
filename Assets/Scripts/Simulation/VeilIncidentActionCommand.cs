using System;
using Scp.Domain;

namespace Scp.Simulation
{
    /// <summary>
    /// 中文：对一个稳定 ID 帷幕事件执行最小事件级处置。命令要求 5 级权限；Action 决定固定、无随机的渐进效果，避免 UI 直接改世界。暂停与撤销只改变事件状态，不删除节点或历史；返回验证结果，Apply 通过事件槽写入业务历史。
    /// English: Performs a minimal incident-level response on one stable-ID veil incident. The command requires level-5 clearance; Action selects fixed, non-random gradual effects so UI never edits the world directly. Pause and withdrawal change status without deleting nodes or history; validation returns a result and Apply emits business history through the event sink.
    /// </summary>
    public sealed class VeilIncidentActionCommand : ICommand
    {
        public string IncidentId { get; set; } = string.Empty;
        public VeilActionKind Action { get; set; }
        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            if (world.IsEnded) return ValidationResult.Failure("The session has ended.");
            if (world.CurrentClearance < RequiredClearance) return ValidationResult.Failure("Level 5 clearance is required.");
            if (world is not WorldQuery query || VeilIncidentService.Find(query.World, IncidentId) is not VeilIncidentState incident) return ValidationResult.Failure("Veil incident was not found.");
            if (incident.Status is VeilIncidentStatus.Resolved or VeilIncidentStatus.Withdrawn) return ValidationResult.Failure("Veil incident is closed.");
            return ValidationResult.Success();
        }

        public void Apply(WorldState world, IEventSink events)
        {
            VeilIncidentState incident = VeilIncidentService.Find(world, IncidentId) ?? throw new InvalidOperationException("Validated veil incident is missing.");
            string effect;
            switch (Action)
            {
                case VeilActionKind.Pause: incident.Status = incident.Status == VeilIncidentStatus.Paused ? VeilIncidentStatus.Active : VeilIncidentStatus.Paused; effect = incident.Status == VeilIncidentStatus.Paused ? "事件推进已暂停，传播记录继续保留。" : "事件推进已恢复，继续接受确定性监测。"; break;
                case VeilActionKind.Withdraw: incident.Status = VeilIncidentStatus.Withdrawn; effect = "专项处置已撤销，事件档案封存。"; break;
                case VeilActionKind.Monitor: incident.Severity = Clamp(incident.Severity - 100); effect = "监测覆盖已加强，严重度小幅下降。"; break;
                case VeilActionKind.Investigate: incident.Severity = Clamp(incident.Severity - 180); incident.LocationPrecision = PromoteOnce(incident.LocationPrecision); effect = "调查已核验部分线索，位置精度提升一级。"; break;
                case VeilActionKind.SuppressPublicity: incident.Recovery = Clamp(incident.Recovery + 450); incident.Loss = Clamp(incident.Loss - 260); effect = "舆情扩散受到压制，损失开始恢复。"; break;
                case VeilActionKind.CoordinateInstitutions: incident.Recovery = Clamp(incident.Recovery + 350); incident.Severity = Clamp(incident.Severity - 220); effect = "公共机构协调已建立，严重度下降。"; break;
                case VeilActionKind.AssessWitnessDisposition: incident.Recovery = Clamp(incident.Recovery + 180); effect = "证人处置评估已完成，仅记录评估而未虚构执行结果。"; break;
                default: incident.Recovery = Clamp(incident.Recovery + 700); incident.Loss = Clamp(incident.Loss - 500); incident.Severity = Clamp(incident.Severity - 400); effect = "紧急专项已启动，传播损失与严重度逐步下降。"; break;
            }
            if (Action != VeilActionKind.Pause && Action != VeilActionKind.Withdraw)
            {
                // 中文：0/0 表示本次调查或监测没有损失、也没有恢复；事件必须保持 Active，不能因相等关系误进入 Recovering。
                // English: 0/0 means this investigation or monitoring caused neither loss nor recovery; the incident must remain Active instead of entering Recovering merely because the values are equal.
                if (incident.Recovery > 0 && incident.Recovery >= incident.Loss)
                    incident.Status = VeilIncidentStatus.Recovering;
                else
                    incident.Status = VeilIncidentStatus.Active;
            }
            // 中文：只有已进入 Recovering 且严重度完全降至 0 才能解决；Severity 使用 0-10000 整数单位。
            // English: Resolution is allowed only after Recovering has been reached and severity is exactly 0; Severity uses integer units from 0 to 10000.
            if (incident.Status == VeilIncidentStatus.Recovering && incident.Severity == 0) incident.Status = VeilIncidentStatus.Resolved;
            VeilIncidentService.AppendRecord(incident, world.Tick, Action, effect);
            events.Emit(new DomainEvent { Kind = DomainEventKind.VeilIncidentChanged, Tick = world.Tick, Detail = IncidentId + ":" + Action });
        }

        private static VeilLocationPrecision PromoteOnce(VeilLocationPrecision value) => value == VeilLocationPrecision.ContinentOnly ? VeilLocationPrecision.Approximate : value;
        private static int Clamp(int value) => value < 0 ? 0 : value > 10000 ? 10000 : value;
    }
}
