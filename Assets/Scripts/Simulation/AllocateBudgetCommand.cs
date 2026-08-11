using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class AllocateBudgetCommand : ICommand
    {
        public BudgetState Budget { get; set; } = new BudgetState();

        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            var access = O5CommandValidation.Validate(world);
            if (!access.IsValid)
            {
                return access;
            }

            if (Budget.SiteOperations < 0 || Budget.ContainmentMaintenance < 0 || Budget.Research < 0 || Budget.Security < 0 ||
                Budget.MobileTaskForces < 0 || Budget.AlphaOne < 0 || Budget.VeilAndCover < 0 || Budget.AdministrationAndIntelligence < 0 || Budget.PersonnelAndEthics < 0)
            {
                return ValidationResult.Failure("Budget values cannot be negative.");
            }

            foreach (var amount in Budget.VeilOperations)
            {
                if (amount < 0)
                {
                    return ValidationResult.Failure("Budget values cannot be negative.");
                }
            }

            return ValidationResult.Success();
        }

        public void Apply(WorldState world, IEventSink events)
        {
            world.Economy.Budget = Budget;
        }
    }
}
