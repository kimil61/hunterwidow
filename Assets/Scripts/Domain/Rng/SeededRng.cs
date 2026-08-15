using System;

namespace HunterWidow.Domain.Rng
{
    public sealed class SeededRng
    {
        private uint state;

        public SeededRng(uint seed)
        {
            state = seed == 0u ? 1u : seed;
        }

        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            }

            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % (uint)exclusiveMaximum);
        }

        public double NextUnit()
        {
            return NextInt(int.MaxValue) / (double)int.MaxValue;
        }
    }
}
