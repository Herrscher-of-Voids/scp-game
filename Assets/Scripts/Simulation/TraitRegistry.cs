using System;

using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class TraitRegistry
    {
        private readonly ITraitHandler?[] _handlers = new ITraitHandler[Enum.GetValues(typeof(ScpTrait)).Length];

        public void Register(ITraitHandler handler)
        {
            _handlers[(int)handler.Trait] = handler;
        }

        public ITraitHandler? Resolve(ScpTrait trait)
        {
            return _handlers[(int)trait];
        }

        public static TraitRegistry CreateDefault()
        {
            var registry = new TraitRegistry();
            registry.Register(new ActObservationLockedHandler());
            registry.Register(new YieldResourceHandler());
            return registry;
        }
    }
}
