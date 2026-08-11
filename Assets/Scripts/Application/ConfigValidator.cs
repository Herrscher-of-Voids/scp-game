using System;
using System.Collections.Generic;
using Scp.Domain;

namespace Scp.Application
{
    public static class ConfigValidator
    {
        public static void Validate(ScpDefinition[] definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var ids = new HashSet<int>();
            foreach (var definition in definitions)
            {
                if (definition.Id.Number <= 0 || !ids.Add(definition.Id.Number))
                {
                    throw new InvalidOperationException("SCP IDs must be positive and unique.");
                }

                if (definition.Traits == null || definition.Requirement == null ||
                    definition.ResearchValue == null || definition.Capabilities == null)
                {
                    throw new InvalidOperationException("SCP definition is missing required fields.");
                }

                if (definition.BaseBreachChance < 0 || definition.BaseBreachChance > 10000)
                {
                    throw new InvalidOperationException("Base breach chance must be fixed-point 0..10000.");
                }

                if (definition.Requirement.MinimumSecurityLevel < 0 ||
                    definition.Requirement.RequiredObserverCapacity < 0 ||
                    definition.Requirement.MonthlyCost < 0)
                {
                    throw new InvalidOperationException("Containment requirement values cannot be negative.");
                }

                foreach (var trait in definition.Traits)
                {
                    foreach (var parameter in trait.Params)
                    {
                        if (!TraitParamPolicy.IsValid(parameter.Key, parameter.Value))
                        {
                            throw new InvalidOperationException("Trait parameter is outside its legal range.");
                        }
                    }
                }
            }
        }
    }
}
