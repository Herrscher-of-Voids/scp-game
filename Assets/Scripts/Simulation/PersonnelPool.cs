using System;

namespace Scp.Simulation
{
    public sealed class PersonnelPool
    {
        public PersonnelState[] Members { get; set; } = Array.Empty<PersonnelState>();
    }
}
