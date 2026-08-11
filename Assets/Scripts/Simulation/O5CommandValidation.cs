using Scp.Domain;

namespace Scp.Simulation
{
    public static class O5CommandValidation
    {
        public static ValidationResult Validate(IWorldQuery world)
        {
            if (world.CurrentClearance < ClearanceLevel.Level5)
            {
                return ValidationResult.Failure("O5 clearance required.");
            }

            if (world.IsEnded)
            {
                return ValidationResult.Failure("The session has ended.");
            }

            return ValidationResult.Success();
        }
    }
}
