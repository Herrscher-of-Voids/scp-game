using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class AdjustFundsCommand : ICommand
    {
        public long Amount { get; set; }

        public ClearanceLevel RequiredClearance { get; set; } = ClearanceLevel.Level4;

        public ValidationResult Validate(IWorldQuery world)
        {
            if (world.CurrentClearance < RequiredClearance)
            {
                return ValidationResult.Failure("Insufficient clearance.");
            }

            return ValidationResult.Success();
        }

        public void Apply(WorldState world, IEventSink events)
        {
            world.Funds += Amount;
            events.Emit(new DomainEvent
            {
                Kind = DomainEventKind.FundsAdjusted,
                Tick = world.Tick,
                Amount = Amount
            });
        }
    }
}
