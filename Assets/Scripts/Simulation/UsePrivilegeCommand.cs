using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class UsePrivilegeCommand : ICommand
    {
        public ProposalKind EmergencyAction { get; set; } = ProposalKind.Task;

        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            var access = O5CommandValidation.Validate(world);
            if (!access.IsValid)
            {
                return access;
            }

            if (world.PrivilegeUsedThisCycle)
            {
                return ValidationResult.Failure("Overseer privilege was already used this cycle.");
            }

            if (EmergencyAction == ProposalKind.AlphaOneDeployment && !world.IsAlphaOneAvailable)
            {
                return ValidationResult.Failure("Alpha-1 is unavailable.");
            }

            return ValidationResult.Success();
        }

        public void Apply(WorldState world, IEventSink events)
        {
            world.Council.PrivilegeUsedThisCycle = true;
            world.Council.PrivilegeUseCount++;
            world.Facts.PrivilegeUses++;
            world.Failure.HiddenEthicsRemovalRisk += 10;
            foreach (var seat in world.Council.Seats)
            {
                if (!seat.IsPlayer)
                {
                    seat.Relationship -= 3;
                }
            }

            if (EmergencyAction == ProposalKind.AlphaOneDeployment)
            {
                world.Council.AlphaOne.IsDeployed = true;
                world.Council.AlphaOne.Deployments++;
                world.Facts.AlphaOneDeployments++;
            }
        }
    }
}
