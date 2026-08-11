using System;
using System.Collections.Generic;
using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class SubmitProposalCommand : ICommand
    {
        public ProposalKind Kind { get; set; }

        public ProposalThreshold Threshold { get; set; }

        public AxisPosition Position { get; set; }

        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            var access = O5CommandValidation.Validate(world);
            if (!access.IsValid)
            {
                return access;
            }

            if (Kind == ProposalKind.WorldRestart && !world.HasCapability(StrategicCapability.WorldReconstruction))
            {
                return ValidationResult.Failure("World reconstruction capability is unavailable.");
            }

            if (Kind == ProposalKind.WorldRestart && Threshold != ProposalThreshold.Unanimous)
            {
                return ValidationResult.Failure("World restart requires unanimous approval.");
            }

            // 中文：门槛由议题类型固定，避免玩家以普通 7 票提交重大议案；重启世界在上方单独固定为 13 票。
            // English: Thresholds are fixed by issue type so a player cannot submit a major issue under the ordinary seven-vote rule; world restart is separately fixed to thirteen votes above.
            var requiredThreshold = Kind == ProposalKind.Experiment || Kind == ProposalKind.Diplomacy || Kind == ProposalKind.Impeachment
                ? ProposalThreshold.TwoThirds
                : ProposalThreshold.SimpleMajority;
            if (Kind != ProposalKind.WorldRestart && Threshold != requiredThreshold)
            {
                return ValidationResult.Failure("Proposal threshold does not match its issue type.");
            }

            if (world.IsProposalCoolingDown(Kind, Position))
            {
                return ValidationResult.Failure("An identical rejected proposal is in its three-cycle cooldown.");
            }

            return ValidationResult.Success();
        }

        public void Apply(WorldState world, IEventSink events)
        {
            var proposals = new List<ProposalState>(world.Council.Proposals);
            proposals.Add(new ProposalState
            {
                ProposalId = proposals.Count == 0 ? 1 : proposals[proposals.Count - 1].ProposalId + 1,
                Kind = Kind,
                Threshold = Threshold,
                Position = Position.Clamp(),
                SubmittedBy = world.Council.PlayerSeatId,
                SubmittedCycle = world.Council.CurrentCycle,
                ResolveCycle = world.Council.CurrentCycle
            });
            world.Council.Proposals = proposals.ToArray();
        }
    }
}
