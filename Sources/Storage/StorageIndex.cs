using System.Collections;
using System.Collections.Generic;

namespace DigitalStorage.Storage
{
    internal class StorageIndex<T> : IReadOnlyList<T>
    {
        private readonly List<(T Item, ulong TagMask)> _list = new();
        private readonly IComparer<(T Item, ulong TagMask)> comparer;

        public StorageIndex(IComparer<T> comparer)
        {
            this.comparer = Comparer<(T Item, ulong TagMask)>.Create(
                (a, b) => comparer.Compare(a.Item, b.Item));
        }

        public T this[int index] => _list[index].Item;

        public ulong GetTagMask(int index) => _list[index].TagMask;

        public bool SetMask(int index, ulong mask)
        {
            var entry = _list[index];
            if (entry.TagMask == mask) return false;
            _list[index] = (entry.Item, mask);
            return true;
        }

        public bool ClearMask(int index, ulong mask) =>
            SetMask(index, _list[index].TagMask & ~mask);

        public ulong GetMask(int index) => _list[index].TagMask;

        public bool MatchRequiredMask(int index, ulong requiredMask)
        {
            return (_list[index].TagMask & requiredMask) == requiredMask;
        }

        public bool MatchAnyMask(int index, ulong anyMask)
        {
            return (_list[index].TagMask & anyMask) != 0;
        }

        public bool MatchExcludedMask(int index, ulong excludedMask)
        {
            return (_list[index].TagMask & excludedMask) == 0;
        }

        public int Count => _list.Count;

        public void Add(T item, ulong tagMask = 0)
        {
            var entry = (Item: item, TagMask: tagMask);
            int position = _list.BinarySearch(entry, comparer);
            _list.Insert(position < 0 ? ~position : position, entry);
        }

        public bool Remove(T item)
        {
            int index = _list.FindIndex(entry => EqualityComparer<T>.Default.Equals(entry.Item, item));
            if (index < 0) return false;
            _list.RemoveAt(index);
            return true;
        }

        public void Refresh() => _list.Sort(comparer);

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var entry in _list) yield return entry.Item;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
