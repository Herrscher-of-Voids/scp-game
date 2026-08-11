using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class CastPlayerVoteCommand : ICommand
    {
        public int ProposalId { get; set; }

        public VoteChoice Choice { get; set; }

        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            var access = O5CommandValidation.Validate(world);
            if (!access.IsValid)
            {
                return access;
            }

            return world.HasOpenProposal(ProposalId)
                ? ValidationResult.Success()
                : ValidationResult.Failure("Proposal is not open.");
        }

        public void Apply(WorldState world, IEventSink events)
        {
            foreach (var proposal in world.Council.Proposals)
            {
                if (proposal.ProposalId == ProposalId && !proposal.IsResolved)
                {
                    proposal.PlayerVote = Choice;
                    break;
                }
            }
        }
    }
}
