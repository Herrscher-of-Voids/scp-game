using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class DirectAnomalyContactCommand : ICommand
    {
        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            var access = O5CommandValidation.Validate(world);
            if (!access.IsValid)
            {
                return access;
            }

            return world.ContactRestrictionActive
                ? ValidationResult.Failure("Direct anomaly contact is restricted.")
                : ValidationResult.Success();
        }

        public void Apply(WorldState world, IEventSink events)
        {
            world.EthicsScore -= 3;
            world.Failure.HiddenEthicsRemovalRisk += 6;
            foreach (var site in world.Sites)
            {
                site.AuditCyclesRemaining = site.AuditCyclesRemaining < 1 ? 1 : site.AuditCyclesRemaining;
            }

            // 中文：M1 将认知污染、异常影响与本人被收容合并为 10% 的确定性种子后果；概率不是免费按钮，且必须推进世界随机流。
            // English: M1 abstracts cognitive contamination, anomalous influence, and containment of the Overseer into a seeded 10% consequence; the action is not free and must advance the world random stream.
            if (world.RandomChance(1000))
            {
                world.Failure.IsEnded = true;
                world.Failure.EndReason = GameEndReason.ContainedOverseer;
            }
        }
    }
}
