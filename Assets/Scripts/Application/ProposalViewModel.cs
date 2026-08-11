using Scp.Domain;

namespace Scp.Application
{
    public sealed class ProposalViewModel
    {
        public int ProposalId { get; set; }

        public ProposalKind Kind { get; set; }

        public ProposalThreshold Threshold { get; set; }

        public SeatId SubmittedBy { get; set; }

        public int SubmittedCycle { get; set; }

        public int ResolveCycle { get; set; }

        public VoteChoice PlayerVote { get; set; }

        public bool IsResolved { get; set; }

        public bool Passed { get; set; }

        /// <summary>
        /// 中文：失败议案最早可原样重提的公开议会周期；0 表示没有冷却。单位为周期，不包含任何 NPC 隐藏状态。
        /// English: Public council cycle when a rejected proposal may next be resubmitted unchanged; zero means no cooldown. Measured in cycles and contains no hidden NPC state.
        /// </summary>
        public int ResubmitAvailableCycle { get; set; }
    }
}
