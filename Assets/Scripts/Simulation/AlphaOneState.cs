namespace Scp.Simulation
{
    public sealed class AlphaOneState
    {
        public bool IsActive { get; set; } = true;

        public bool IsDeployed { get; set; }

        public int RebuildCycles { get; set; }

        public int Deployments { get; set; }

        public string LastResult { get; set; } = string.Empty;
    }
}
