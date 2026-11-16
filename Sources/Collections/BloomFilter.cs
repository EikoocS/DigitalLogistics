using System;

namespace DigitalStorage.Collections
{
    public sealed class BloomFilter
    {
        private const int BitsPerWord = sizeof(ulong) * 8;
        private readonly ulong[] bits;
        private readonly int hashCount;
        private readonly int bitCount;

        /// <param name="expectedCount">Expected distinct value count, greater than zero.</param>
        /// <param name="falsePositiveProbability">Target false-positive probability, between zero and one.</param>
        public BloomFilter(int expectedCount = 1024, double falsePositiveProbability = 0.01)
        {
            double logTwo = Math.Log(2);
            int requiredBits = (int)Math.Ceiling(-expectedCount * Math.Log(falsePositiveProbability) / (logTwo * logTwo));
            bits = new ulong[(requiredBits + BitsPerWord - 1) / BitsPerWord];
            bitCount = bits.Length * BitsPerWord;
            hashCount = Math.Max(1, (int)Math.Round((double)requiredBits / expectedCount * logTwo));
        }

        public void Add(int hashCode)
        {
            GetHashes(hashCode, out uint hash, out uint step);
            for (int i = 0; i < hashCount; i++)
            {
                int bit = (int)(hash % (uint)bitCount);
                bits[bit / BitsPerWord] |= 1UL << (bit % BitsPerWord);
                hash = unchecked(hash + step);
            }
        }

        /// <summary>False proves absence; true means the hash may be present.</summary>
        public bool MayContain(int hashCode)
        {
            GetHashes(hashCode, out uint hash, out uint step);
            for (int i = 0; i < hashCount; i++)
            {
                int bit = (int)(hash % (uint)bitCount);
                if ((bits[bit / BitsPerWord] & (1UL << (bit % BitsPerWord))) == 0) return false;
                hash = unchecked(hash + step);
            }
            return true;
        }

        public void Clear() => Array.Clear(bits, 0, bits.Length);

        private static void GetHashes(int hashCode, out uint hash, out uint step)
        {
            hash = unchecked((uint)HashCode.Combine(hashCode, 0));
            step = unchecked((uint)HashCode.Combine(hashCode, 1)) | 1U;
        }
    }
}
