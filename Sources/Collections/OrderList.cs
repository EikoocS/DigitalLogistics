using System;
using System.Collections;
using System.Collections.Generic;

namespace DigitalStorage.Collections
{
    public class OrderList<T> : IReadOnlyList<T>
    {
        private readonly List<T> list = new();
        private readonly IComparer<T> comparer;
        private readonly Func<T, bool> filter = (_) => true;


        public OrderList(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }

        public OrderList(IComparer<T> comparer, Func<T, bool> filter)
        {
            this.comparer = comparer;
            this.filter = filter;
        }

        public void Add(T item)
        {
            if (!filter(item)) return;

            int position = list.BinarySearch(item, comparer);
            list.Insert(position < 0 ? ~position : position, item);
        }

        public bool Remove(T item)
        {
            if (!filter(item)) return false;

            int index = list.BinarySearch(item, comparer);
            if (index < 0)
                return false;

            while (index > 0 && comparer.Compare(list[index - 1], item) == 0)
                index--;

            for (; index < list.Count; index++)
            {
                T current = list[index];

                if (comparer.Compare(current, item) != 0)
                    break;

                if (ReferenceEquals(current, item))
                {
                    list.RemoveAt(index);
                    return true;
                }
            }

            return false;
        }

        public T Pop(bool bottom = false)
        {
            int index = bottom ? 0 : list.Count - 1;
            T item = list[index];
            list.RemoveAt(index);
            return item;
        }

        public void Clear()
        {
            list.Clear();
        }

        // TODO re filter
        public void Sort()
        {
            list.Sort(comparer);
        }

        public T this[int index] => list[index];

        public int Count => list.Count;

        public IEnumerator<T> GetEnumerator(bool reverse)
        {
            if (reverse) return GetReverseEnumerator();
            else return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            var copy = list.ToArray();
            return ((IEnumerable<T>)copy).GetEnumerator();
        }

        public IEnumerable<T> AsReverseEnumerable()
        {
            var copy = list.ToArray();
            for (int i = copy.Length - 1; i >= 0; i--)
                yield return copy[i];
        }

        public IEnumerator<T> GetReverseEnumerator() => AsReverseEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        

        public T[] ToArray() => list.ToArray();

        public static OrderList<A> BuildByGetterAndFilter<A, TKey>(Func<A, TKey> getter){
            var comparer = ComparerHelper.ComparedBy(getter);
            Func<A, bool> filter = (a) => getter(a) != null ;
            return new OrderList<A>(comparer, filter);
        }
    }
}
