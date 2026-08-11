using Scp.Domain;

namespace Scp.Simulation
{
    public interface ICommand
    {
        ClearanceLevel RequiredClearance { get; }

        ValidationResult Validate(IWorldQuery world);

        void Apply(WorldState world, IEventSink events);
    }
}
