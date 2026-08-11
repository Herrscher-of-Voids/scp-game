using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class AuditSiteCommand : ICommand
    {
        public SiteId SiteId { get; set; }

        public long Cost { get; set; } = 25000;

        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            var access = O5CommandValidation.Validate(world);
            if (!access.IsValid)
            {
                return access;
            }

            if (world.FindSite(SiteId) == null)
            {
                return ValidationResult.Failure("Site not found.");
            }

            return world.Funds >= Cost
                ? ValidationResult.Success()
                : ValidationResult.Failure("Insufficient funds.");
        }

        public void Apply(WorldState world, IEventSink events)
        {
            world.Funds -= Cost;
            foreach (var site in world.Sites)
            {
                if (site.Id == SiteId)
                {
                    site.AuditCyclesRemaining = 2;
                    site.ReportedStability = site.TrueStability;
                    site.ReportedCasualties = site.TrueCasualties;
                    site.ReportedResearchOutput = site.TrueResearchOutput;
                    break;
                }
            }
        }
    }
}
