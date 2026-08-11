using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class WorldQuery : IWorldQuery
    {
        private readonly WorldState _world;

        public WorldQuery(WorldState world, ClearanceLevel currentClearance)
        {
            _world = world;
            CurrentClearance = currentClearance;
        }

        public ClearanceLevel CurrentClearance { get; }

        public long Tick => _world.Tick;

        public long Funds => _world.Funds;

        /// <summary>中文：仅供同一模拟程序集内需要结构化财政状态的命令验证使用；外部界面仍必须经 Perspective 投影。English: Exposes the world only to command validation inside the simulation assembly when structured finance state is required; presentation must still use a Perspective projection.</summary>
        internal WorldState World => _world;

        public bool IsEnded => _world.Failure.IsEnded;

        public bool PrivilegeUsedThisCycle => _world.Council.PrivilegeUsedThisCycle;

        public bool IsAlphaOneAvailable => _world.Council.AlphaOne.IsActive && !_world.Council.AlphaOne.IsDeployed;

        public bool ContactRestrictionActive => _world.Council.ContactRestrictionActive;

        public SiteState? FindSite(SiteId siteId)
        {
            foreach (var site in _world.Sites)
            {
                if (site.Id == siteId)
                {
                    return site;
                }
            }

            return null;
        }

        /// <summary>中文：按序扫描持久化报告并使用序号比较 ID，避免区域设置影响命令验证。English: Scans persisted reports using ordinal ID comparison so command validation is culture-independent.</summary>
        public ReportState? FindReport(string reportId)
        {
            foreach (var report in _world.Reports)
            {
                if (string.Equals(report.Id, reportId, System.StringComparison.Ordinal)) return report;
            }
            return null;
        }

        public bool HasCapability(StrategicCapability capability)
        {
            return StrategicCapabilityService.IsAvailable(_world, capability);
        }

        public bool HasOpenProposal(int proposalId)
        {
            foreach (var proposal in _world.Council.Proposals)
            {
                if (proposal.ProposalId == proposalId && !proposal.IsResolved)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 中文：查询同类同坐标的失败议案冷却；只读取世界状态，不推进时间或随机流。当前周期达到可重提周期时返回 false。
        /// English: Queries cooldown for a rejected proposal with the same kind and axes without advancing time or randomness. Returns false once the current cycle reaches the resubmission cycle.
        /// </summary>
        public bool IsProposalCoolingDown(ProposalKind kind, AxisPosition position)
        {
            var clamped = position.Clamp();
            foreach (var proposal in _world.Council.Proposals)
            {
                if (proposal.IsResolved && !proposal.Passed && proposal.Kind == kind &&
                    proposal.Position == clamped && _world.Council.CurrentCycle < proposal.ResubmitAvailableCycle)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsNpcSeat(SeatId seatId)
        {
            foreach (var seat in _world.Council.Seats)
            {
                if (seat.Id == seatId)
                {
                    return seat.IsOccupied && !seat.IsPlayer;
                }
            }

            return false;
        }
    }
}
