using System;

namespace Scp.Simulation
{
    public sealed class VeilState
    {
        private static readonly int[] Weights = { 22, 8, 22, 25, 10, 8, 5 };

        public int Global { get; set; } = 10000;

        public int[] ByContinent { get; set; } = CreateFullIntegrity();

        public void RecalculateGlobal()
        {
            long weighted = 0;
            var totalWeight = 0;
            for (var index = 0; index < ByContinent.Length && index < Weights.Length; index++)
            {
                ByContinent[index] = Clamp(ByContinent[index]);
                weighted += (long)ByContinent[index] * Weights[index];
                totalWeight += Weights[index];
            }

            Global = totalWeight == 0 ? 0 : (int)(weighted / totalWeight);
        }

        public bool HasFailed()
        {
            var critical = 0;
            foreach (var value in ByContinent)
            {
                if (value < 2000)
                {
                    critical++;
                }
            }

            return Global == 0 || critical >= 3;
        }

        private static int[] CreateFullIntegrity()
        {
            var values = new int[7];
            Array.Fill(values, 10000);
            return values;
        }

        private static int Clamp(int value)
        {
            return value < 0 ? 0 : value > 10000 ? 10000 : value;
        }
    }
}
