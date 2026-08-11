using System;
using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class CouncilState
    {
        public CouncilSeatState[] Seats { get; set; } = Array.Empty<CouncilSeatState>();

        public int CurrentCycle { get; set; }

        public VoteRecord[] VoteRecords { get; set; } = Array.Empty<VoteRecord>();

        public ProposalState[] Proposals { get; set; } = Array.Empty<ProposalState>();

        public SeatId PlayerSeatId { get; set; }

        public bool ContactRestrictionActive { get; set; } = true;

        public bool PrivilegeUsedThisCycle { get; set; }

        public int PrivilegeUseCount { get; set; }

        public bool ImpeachmentWarning { get; set; }

        public AlphaOneState AlphaOne { get; set; } = new AlphaOneState();
    }
}
