using Scp.Domain;

namespace Scp.Simulation
{
    public interface IWorldQuery
    {
        ClearanceLevel CurrentClearance { get; }

        long Tick { get; }

        long Funds { get; }

        bool IsEnded { get; }

        bool PrivilegeUsedThisCycle { get; }

        bool IsAlphaOneAvailable { get; }

        bool ContactRestrictionActive { get; }

        SiteState? FindSite(SiteId siteId);

        /// <summary>中文：按稳定 ID 只读查找报告；找不到时返回 null，不改变报告或随机状态。English: Finds a report read-only by stable ID; returns null when absent without changing reports or random state.</summary>
        ReportState? FindReport(string reportId);

        bool HasCapability(StrategicCapability capability);

        bool HasOpenProposal(int proposalId);

        /// <summary>
        /// 中文：判断同类且三轴完全相同的议案是否仍处于三周期重提冷却。提案种类与坐标无单位；返回 true 时命令必须拒绝，坐标变化代表实质修改。
        /// English: Determines whether a proposal with the same kind and identical axes remains in its three-cycle resubmission cooldown. Kind and axes are dimensionless; true requires command rejection, while changed axes represent a material amendment.
        /// </summary>
        bool IsProposalCoolingDown(ProposalKind kind, AxisPosition position);

        bool IsNpcSeat(SeatId seatId);
    }
}
