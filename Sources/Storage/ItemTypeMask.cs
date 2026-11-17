using System;
using System.Collections.Generic;

namespace DigitalLogistics.Storage
{
    public sealed class ItemTypeMask : IEquatable<ItemTypeMask>
    {
        private const int BitsPerWord = sizeof(ulong) * 8;
        private readonly ulong[] mask;

        public ItemTypeMask()
        {
            int minCapacity = TypeHelper.ALL_TYPES_LIST.Count;
            mask = new ulong[(int)(((ulong)minCapacity + BitsPerWord - 1) / BitsPerWord)];
        }

        public ItemTypeMask(int minCapacity)
        {
            mask = new ulong[(int)(((ulong)minCapacity + BitsPerWord - 1) / BitsPerWord)];
        }

        private ItemTypeMask(ulong[] mask)
        {
            this.mask = mask;
        }

        public int Capacity => mask.Length * BitsPerWord;

        public bool this[int index]
        {
            get => (mask[index / BitsPerWord] & (1UL << (index % BitsPerWord))) != 0;
            set
            {
                int word = index / BitsPerWord;
                ulong bit = 1UL << (index % BitsPerWord);
                if (value)
                    mask[word] |= bit;
                else
                    mask[word] &= ~bit;
            }
        }

        public bool IsEmpty
        {
            get
            {
                foreach (ulong word in mask)
                    if (word != 0)
                        return false;

                return true;
            }
        }

        public void Clear() => Array.Clear(mask, 0, mask.Length);

        public ItemTypeMask Clone() => new ItemTypeMask((ulong[])mask.Clone());

        public static ItemTypeMask operator &(ItemTypeMask a, ItemTypeMask b)
        {
            var result = new ulong[a.mask.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = a.mask[i] & b.mask[i];

            return new ItemTypeMask(result);
        }

        public static ItemTypeMask operator |(ItemTypeMask a, ItemTypeMask b)
        {
            var result = new ulong[a.mask.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = a.mask[i] | b.mask[i];

            return new ItemTypeMask(result);
        }

        public static ItemTypeMask operator ^(ItemTypeMask a, ItemTypeMask b)
        {
            var result = new ulong[a.mask.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = a.mask[i] ^ b.mask[i];

            return new ItemTypeMask(result);
        }

        public static ItemTypeMask operator ~(ItemTypeMask value)
        {
            var result = new ulong[value.mask.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = ~value.mask[i];

            return new ItemTypeMask(result);
        }

        public static ItemTypeMask operator <<(ItemTypeMask value, int count)
        {
            var result = new ulong[value.mask.Length];
            int wordShift = count / BitsPerWord;
            int bitShift = count % BitsPerWord;

            for (int i = wordShift; i < result.Length; i++)
            {
                int source = i - wordShift;
                result[i] = value.mask[source] << bitShift;
                if (bitShift != 0 && source > 0)
                    result[i] |= value.mask[source - 1] >> (BitsPerWord - bitShift);
            }

            return new ItemTypeMask(result);
        }

        public static ItemTypeMask operator >>(ItemTypeMask value, int count)
        {
            var result = new ulong[value.mask.Length];
            int wordShift = count / BitsPerWord;
            int bitShift = count % BitsPerWord;

            for (int i = 0; i < result.Length - wordShift; i++)
            {
                int source = i + wordShift;
                result[i] = value.mask[source] >> bitShift;
                if (bitShift != 0 && source + 1 < result.Length)
                    result[i] |= value.mask[source + 1] << (BitsPerWord - bitShift);
            }

            return new ItemTypeMask(result);
        }

        public bool Equals(ItemTypeMask other)
        {
            if (other is null || mask.Length != other.mask.Length)
                return false;

            for (int i = 0; i < mask.Length; i++)
                if (mask[i] != other.mask[i])
                    return false;

            return true;
        }

        public override bool Equals(object obj) => obj is ItemTypeMask other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(mask.Length);
            foreach (ulong word in mask)
                hash.Add(word);

            return hash.ToHashCode();
        }

        public static bool operator ==(ItemTypeMask a, ItemTypeMask b) =>
            ReferenceEquals(a, b) || (a is not null && a.Equals(b));

        public static bool operator !=(ItemTypeMask a, ItemTypeMask b) => !(a == b);

        public static int MatchIndexFromString(string tag) => TypeHelper.ALL_TYPES_LIST.IndexOf(tag);
        public static string MatchStringFromIndex(int index) => TypeHelper.ALL_TYPES_LIST[index];
        public void SetBitByTags(List<string> tags)
        {
            foreach(var tag in tags) this[MatchIndexFromString(tag)] = true;
        }
    }
}
