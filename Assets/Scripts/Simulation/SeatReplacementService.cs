using Scp.Domain;

namespace Scp.Simulation
{
    public static class SeatReplacementService
    {
        public static CouncilSeatState Replace(WorldState world, SeatId seatId, int recentFailures)
        {
            var containmentBias = -recentFailures * 12;
            var veilBias = world.Veil.Global < 4000 ? -35 : 0;
            var fiscalBias = world.Economy.LastCashFlow < 0 ? 25 : 0;
            var ethicsBias = world.EthicsScore < -30 ? -30 : 0;
            var mean = MeanPosition(world.Council);
            // 中文：三轴在世界状态与现有议会均值偏置后仍保留种子随机波动；每次取值经 WorldState 写回，保证连续补选不会重复同一随机片段。
            // English: Each axis retains seeded variation after world-state and council-mean biases; draws go through WorldState write-back so consecutive replacements cannot replay the same random fragment.
            var replacement = new CouncilSeatState
            {
                Id = seatId,
                IsOccupied = true,
                Position = new AxisPosition(
                    Clamp(world.NextRandomInt(-35, 36) + containmentBias - mean.Containment / 2),
                    Clamp(world.NextRandomInt(-35, 36) + fiscalBias + ethicsBias - mean.PersonnelEthics / 2),
                    Clamp(world.NextRandomInt(-35, 36) + veilBias - mean.VeilPolicy / 2))
            };

            for (var index = 0; index < world.Council.Seats.Length; index++)
            {
                if (world.Council.Seats[index].Id == seatId)
                {
                    world.Council.Seats[index] = replacement;
                    break;
                }
            }

            return replacement;
        }

        private static AxisPosition MeanPosition(CouncilState council)
        {
            var count = 0;
            var containment = 0;
            var ethics = 0;
            var veil = 0;
            foreach (var seat in council.Seats)
            {
                if (!seat.IsOccupied)
                {
                    continue;
                }

                count++;
                containment += seat.Position.Containment;
                ethics += seat.Position.PersonnelEthics;
                veil += seat.Position.VeilPolicy;
            }

            return count == 0 ? default : new AxisPosition(containment / count, ethics / count, veil / count);
        }

        private static int Clamp(int value)
        {
            return value < -100 ? -100 : value > 100 ? 100 : value;
        }
    }
}
