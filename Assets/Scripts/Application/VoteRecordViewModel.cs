using System;
using Scp.Domain;

namespace Scp.Application
{
    public sealed class VoteRecordViewModel
    {
        public int ProposalId { get; set; }

        public ProposalKind Kind { get; set; }

        public ProposalThreshold Threshold { get; set; }

        public int Cycle { get; set; }

        public bool Passed { get; set; }

        public SeatVoteViewModel[] Votes { get; set; } = Array.Empty<SeatVoteViewModel>();
    }
}
