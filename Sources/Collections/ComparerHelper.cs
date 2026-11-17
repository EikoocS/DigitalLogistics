using System;
using System.Collections.Generic;

namespace DigitalLogistics.Collections
{
    public static class ComparerHelper
    {
        public static Comparer<T> ComparedBy<T,TKey>(Func<T, TKey> getter)
        {
            return Comparer<T>.Create((a, b) => Comparer<TKey>.Default.Compare(getter(a), getter(b)));
        }

        public static Comparer<T> ComparedBy<T, TKey>(Func<T, TKey> getter, Comparer<TKey> comparer)
        {
            return Comparer<T>.Create((a, b) => comparer.Compare(getter(a), getter(b)));
        }

        public static Comparer<T> LinkComparer<T>(this Comparer<T> first, Comparer<T> second)
        {
            return Comparer<T>.Create((a, b) =>
            {
                int result = first.Compare(a, b);
                return result != 0 ? result : second.Compare(a, b);
            });
        }
    }
}
