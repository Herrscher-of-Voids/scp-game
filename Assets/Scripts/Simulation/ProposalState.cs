using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class ProposalState
    {
        public int ProposalId { get; set; }

        public ProposalKind Kind { get; set; }

        public ProposalThreshold Threshold { get; set; }

        public AxisPosition Position { get; set; }

        public SeatId SubmittedBy { get; set; }

        public int SubmittedCycle { get; set; }

        public int ResolveCycle { get; set; }

        public bool IsResolved { get; set; }

        public bool Passed { get; set; }

        /// <summary>
        /// 中文：被否决后同类同坐标议案允许再次提交的最早周期；单位为议会周期。通过议案与尚未结案议案保持 0。
        /// English: Earliest council cycle in which an identical kind-and-position proposal may be submitted after rejection, measured in council cycles. Passed and unresolved proposals keep zero.
        /// </summary>
        public int ResubmitAvailableCycle { get; set; }

        public VoteChoice PlayerVote { get; set; }
    }
}
