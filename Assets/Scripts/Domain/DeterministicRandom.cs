using System;

namespace Scp.Domain
{
    public struct DeterministicRandom
    {
        public DeterministicRandom(ulong seed)
        {
            State0 = SplitMix64(ref seed);
            State1 = SplitMix64(ref seed);
            EnsureNonZeroState();
        }

        public DeterministicRandom(ulong state0, ulong state1)
        {
            State0 = state0;
            State1 = state1;
            EnsureNonZeroState();
        }

        public ulong State0 { get; set; }

        public ulong State1 { get; set; }

        public ulong NextUInt64()
        {
            var state0 = State0;
            var state1 = State1;
            var result = state0 + state1;
            state1 ^= state0;
            State0 = RotateLeft(state0, 55) ^ state1 ^ (state1 << 14);
            State1 = RotateLeft(state1, 36);
            return result;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            var range = (ulong)((long)maxExclusive - minInclusive);
            var threshold = unchecked((0UL - range) % range);
            ulong value;
            do
            {
                value = NextUInt64();
            }
            while (value < threshold);

            return (int)(minInclusive + (long)(value % range));
        }

        public bool Chance(int perTenThousand)
        {
            if (perTenThousand is < 0 or > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(perTenThousand));
            }

            return perTenThousand == 10000 ||
                (perTenThousand != 0 && NextInt(0, 10000) < perTenThousand);
        }

        private static ulong RotateLeft(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }

        private static ulong SplitMix64(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            var value = state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }

        private void EnsureNonZeroState()
        {
            if (State0 == 0 && State1 == 0)
            {
                State1 = 1;
            }
        }
    }
}
