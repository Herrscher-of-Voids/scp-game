namespace Scp.Simulation
{
    public enum DomainEventKind
    {
        CommandRejected,
        FundsAdjusted,
        ObservationLocked,
        AnomalyActed,
        ResourceYielded,

        /// <summary>中文：M1 概率抽象判定出的收容失效战略事件。English: Strategic containment-breach event produced by the M1 probability abstraction.</summary>
        ContainmentBreach,

        MonthlySettlement,

        /// <summary>中文：公开报告审批业务事件，Detail 包含决定和稳定 ID。English: Public report-decision business event whose Detail contains the decision and stable IDs.</summary>
        ReportDecision,

        /// <summary>中文：帷幕事件阶段或处置发生变化，Detail 仅含匿名稳定 ID 与公开动作。English: A veil incident stage or disposition changed; Detail contains only an anonymous stable ID and public action.</summary>
        VeilIncidentChanged
    }
}
