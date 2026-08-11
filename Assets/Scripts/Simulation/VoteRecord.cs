using System;
using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class VoteRecord
    {
        public int ProposalId { get; set; }

        public ProposalKind Kind { get; set; }

        public ProposalThreshold Threshold { get; set; }

        public int Cycle { get; set; }

        public bool Passed { get; set; }

        public SeatVoteRecord[] Votes { get; set; } = Array.Empty<SeatVoteRecord>();
    }
}
