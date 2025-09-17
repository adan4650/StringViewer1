using System;
using System.Collections.Generic;

namespace StringViewer1
{
    public class LruCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>> _map;
        private readonly LinkedList<(TKey key, TValue value)> _list;
        private readonly object _lock = new object();

        public LruCache(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _map = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>();
            _list = new LinkedList<(TKey, TValue)>();
        }

        public bool TryGet(TKey key, out TValue value)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var node))
                {
                    _list.Remove(node);
                    _list.AddFirst(node);
                    value = node.Value.value;
                    return true;
                }
                value = default!;
                return false;
            }
        }

        public void Add(TKey key, TValue value)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var existing))
                {
                    _list.Remove(existing);
                    _map.Remove(key);
                }

                var node = new LinkedListNode<(TKey, TValue)>((key, value));
                _list.AddFirst(node);
                _map[key] = node;

                if (_map.Count > _capacity)
                {
                    var last = _list.Last!;
                    _map.Remove(last.Value.key);
                    _list.RemoveLast();
                }
            }
        }
    }
}