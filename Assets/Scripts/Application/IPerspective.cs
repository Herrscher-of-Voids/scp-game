using System.Collections.Generic;

using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    public interface IPerspective
    {
        IdentityRole Role { get; }

        ClearanceLevel Clearance { get; }

        TViewModel Project<TViewModel>(WorldState world);

        IReadOnlyList<CommandDescriptor> AvailableCommands(WorldState world);
    }
}
