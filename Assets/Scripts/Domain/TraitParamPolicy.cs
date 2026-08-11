using System;

namespace Scp.Domain
{
    public static class TraitParamPolicy
    {
        public static bool IsValid(TraitParamKey key, int value)
        {
            return key switch
            {
                TraitParamKey.ObservationRange => value is >= 0 and <= 1000,
                TraitParamKey.RequiredObservers => value is >= 1 and <= 100,
                TraitParamKey.ReactionDelayTicks => value is >= 0 and <= 100000,
                TraitParamKey.MoralePerTick => value is >= -10000 and <= 10000,
                TraitParamKey.ResourcePerCycle => value is >= 0 and <= 1000000000,
                TraitParamKey.CycleTicks => value is >= 1 and <= 1000000,
                TraitParamKey.StabilityLossPerTick => value is >= 0 and <= 10000,
                _ => false
            };
        }

        public static void EnsureValid(TraitParam parameter)
        {
            if (!IsValid(parameter.Key, parameter.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(parameter));
            }
        }
    }
}
