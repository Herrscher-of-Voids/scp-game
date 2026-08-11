using Scp.Domain;

namespace Scp.Simulation
{
    public interface ITraitHandler
    {
        ScpTrait Trait { get; }

        void Tick(in TraitContext context, TraitInstance instance, IEventSink events);
    }
}
