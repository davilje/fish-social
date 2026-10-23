using System;
using System.Collections.Generic;

namespace FishSocialOverlay
{
    /// <summary>
    /// UI-thread-owned, byte-budgeted LRU. Resource references protect every decoded
    /// size of a live resource; inactive entries are retained until budget pressure.
    /// Live resources may exceed the budget and are never evicted to meet it.
    /// </summary>
    internal sealed class OverlayResourceCache<T> where T : class
    {
        sealed class Entry
        {
            public string Key;
            public string Resource;
            public T Value;
            public long Bytes;
        }

        readonly Dictionary<string, LinkedListNode<Entry>> _entries =
            new Dictionary<string, LinkedListNode<Entry>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, int> _references =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly LinkedList<Entry> _lru = new LinkedList<Entry>();
        public long BudgetBytes { get; }
        public long ResidentBytes { get; private set; }
        public int Count => _entries.Count;
        public int ReferencedResources => _references.Count;

        public OverlayResourceCache(long budgetBytes)
        {
            if (budgetBytes < 0) throw new ArgumentOutOfRangeException(nameof(budgetBytes));
            BudgetBytes = budgetBytes;
        }

        public bool TryGet(string key, out T value)
        {
            if (!_entries.TryGetValue(key, out var node))
            {
                value = null;
                return false;
            }
            _lru.Remove(node);
            _lru.AddLast(node);
            value = node.Value.Value;
            return true;
        }

        public void Add(string key, string resource, T value, long bytes)
        {
            if (key == null || resource == null || value == null || bytes < 0)
                throw new ArgumentException("Cache entry requires a key, resource, value and non-negative byte size.");
            if (_entries.TryGetValue(key, out var old)) Remove(old);
            var node = _lru.AddLast(new Entry { Key = key, Resource = resource, Value = value, Bytes = bytes });
            _entries.Add(key, node);
            ResidentBytes += bytes;
            Trim();
        }

        public void Retain(IEnumerable<string> resources)
        {
            foreach (var resource in resources)
            {
                _references.TryGetValue(resource, out var count);
                _references[resource] = count + 1;
            }
        }

        public void Release(IEnumerable<string> resources)
        {
            foreach (var resource in resources)
            {
                if (!_references.TryGetValue(resource, out var count)) continue;
                if (count == 1) _references.Remove(resource);
                else _references[resource] = count - 1;
            }
            Trim();
        }

        public void Trim()
        {
            var node = _lru.First;
            while (ResidentBytes > BudgetBytes && node != null)
            {
                var next = node.Next;
                if (!_references.ContainsKey(node.Value.Resource)) Remove(node);
                node = next;
            }
        }

        void Remove(LinkedListNode<Entry> node)
        {
            ResidentBytes -= node.Value.Bytes;
            _entries.Remove(node.Value.Key);
            _lru.Remove(node);
        }
    }
}
