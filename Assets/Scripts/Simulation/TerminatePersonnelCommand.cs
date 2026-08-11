using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class TerminatePersonnelCommand : ICommand
    {
        public int Count { get; set; } = 1;

        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            var access = O5CommandValidation.Validate(world);
            if (!access.IsValid)
            {
                return access;
            }

            return Count > 0
                ? ValidationResult.Success()
                : ValidationResult.Failure("Termination count must be positive.");
        }

        public void Apply(WorldState world, IEventSink events)
        {
            world.Facts.PersonnelTerminated += Count;
            world.EthicsScore -= Count;
            world.Failure.HiddenEthicsRemovalRisk += Count * 4;
            foreach (var site in world.Sites)
            {
                site.ReportCredibility = site.ReportCredibility > 100 ? site.ReportCredibility - 100 : 0;
            }
        }
    }
}
