using Scp.Domain;

namespace Scp.Application
{
    public sealed class CommandDescriptor
    {
        public string Kind { get; set; } = string.Empty;

        public ClearanceLevel RequiredClearance { get; set; }
    }
}
