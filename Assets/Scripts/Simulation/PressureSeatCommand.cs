using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class PressureSeatCommand : ICommand
    {
        public SeatId SeatId { get; set; }

        public int PressureAmount { get; set; } = 100;

        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            var access = O5CommandValidation.Validate(world);
            if (!access.IsValid)
            {
                return access;
            }

            return world.IsNpcSeat(SeatId) && PressureAmount > 0
                ? ValidationResult.Success()
                : ValidationResult.Failure("An occupied NPC seat is required.");
        }

        public void Apply(WorldState world, IEventSink events)
        {
            foreach (var seat in world.Council.Seats)
            {
                if (seat.Id == SeatId)
                {
                    seat.Pressure += PressureAmount;
                    seat.Relationship -= 20;
                    break;
                }
            }

            world.Failure.HiddenEthicsRemovalRisk += 2;
        }
    }
}
