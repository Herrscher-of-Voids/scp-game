using System;

namespace Scp.Simulation
{
    public interface IWorld
    {
        TickResult Tick(ReadOnlySpan<ICommand> commands);
    }
}
