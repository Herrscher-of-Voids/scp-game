using Scp.Domain;

namespace Scp.Application
{
    public sealed class CouncilSeatViewModel
    {
        public SeatId SeatId { get; set; }

        public bool IsOccupied { get; set; }

        public bool IsPlayer { get; set; }
    }
}
