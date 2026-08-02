using System;
using System.Collections.Generic;

namespace Deterministic.GameFramework.Detour
{
    public class DtSortedQueue<T>
    {
        private bool _dirty;
        private readonly List<T> _items;
        private readonly Comparison<T> _comparison;

        public DtSortedQueue(Comparison<T> comp)
        {
            _items = new List<T>();
            _comparison = (x, y) => comp(x, y) * -1;
        }

        public int Count()
        {
            return _items.Count;
        }

        public bool IsEmpty()
        {
            return 0 == _items.Count;
        }

        public void Clear()
        {
            _items.Clear();
            _dirty = false;
        }

        private void Balance()
        {
            if (_dirty)
            {
                _items.Sort(_comparison); // reverse
                _dirty = false;
            }
        }

        public T Peek()
        {
            Balance();
            return _items[^1];
        }

        public T Dequeue()
        {
            var node = Peek();
            _items.RemoveAt(_items.Count - 1);
            return node;
        }

        public void Enqueue(T item)
        {
            if (null == item)
                return;

            _items.Add(item);
            _dirty = true;
        }

        public bool Remove(T item)
        {
            if (null == item)
                return false;

            int idx = _items.LastIndexOf(item);
            if (0 > idx)
                return false;

            _items.RemoveAt(idx);
            return true;
        }

        public List<T> ToList()
        {
            Balance();
            var temp = new List<T>(_items);
            temp.Reverse();
            return temp;
        }
    }
}
