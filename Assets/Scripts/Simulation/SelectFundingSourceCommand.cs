using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class SelectFundingSourceCommand : ICommand
    {
        public FundingSource Source { get; set; }

        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            return O5CommandValidation.Validate(world);
        }

        public void Apply(WorldState world, IEventSink events)
        {
            world.Economy.FundingSource = Source;
        }
    }
}
