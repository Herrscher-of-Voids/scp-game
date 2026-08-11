using Scp.Domain;

namespace Scp.Simulation
{
    public static class CouncilFactory
    {
        public static CouncilState Create(ref DeterministicRandom random)
        {
            var seats = new CouncilSeatState[13];
            var playerIndex = random.NextInt(0, seats.Length);
            for (var index = 0; index < seats.Length; index++)
            {
                seats[index] = new CouncilSeatState
                {
                    Id = new SeatId(index + 1),
                    IsOccupied = true,
                    IsPlayer = index == playerIndex,
                    Position = new AxisPosition(
                        random.NextInt(-100, 101),
                        random.NextInt(-100, 101),
                        random.NextInt(-100, 101)),
                    Relationship = 0
                };
            }

            return new CouncilState
            {
                Seats = seats,
                PlayerSeatId = seats[playerIndex].Id
            };
        }
    }
}
