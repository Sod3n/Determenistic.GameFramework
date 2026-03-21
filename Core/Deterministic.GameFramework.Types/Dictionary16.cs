using System;
using System.Collections.Generic;

namespace Deterministic.GameFramework.Types;

public struct Dictionary16<TKey, TValue> : IParam, IEquatable<Dictionary16<TKey, TValue>>
    where TKey : struct, IEquatable<TKey>, IComparable<TKey>
    where TValue : struct, IEquatable<TValue>
{
    public List16<TKey> Keys;
    public List16<TValue> Values;

    public int Count => Keys.Count;

    public TValue this[TKey key]
    {
        get
        {
            if (TryGetValue(key, out var value))
            {
                return value;
            }
            throw new KeyNotFoundException($"Key {key} not found in Dictionary16");
        }
        set
        {
            var index = IndexOfKey(key);
            if (index != -1)
            {
                Values[index] = value;
            }
            else
            {
                Add(key, value);
            }
        }
    }

    public void Add(TKey key, TValue value)
    {
        if (IndexOfKey(key) != -1)
        {
            throw new ArgumentException($"Key {key} already exists in Dictionary16");
        }
        
        if (Keys.Count >= List16<TKey>.Capacity)
        {
            throw new InvalidOperationException("Dictionary16 is full");
        }

        // Find insertion point to maintain sorted order
        int insertIndex = 0;
        while (insertIndex < Keys.Count && Keys[insertIndex].CompareTo(key) < 0)
        {
            insertIndex++;
        }

        // Add dummy to increase count
        Keys.Add(default);
        Values.Add(default);
        
        // Shift elements to make room
        for (int i = Keys.Count - 1; i > insertIndex; i--)
        {
            Keys[i] = Keys[i - 1];
            Values[i] = Values[i - 1];
        }

        Keys[insertIndex] = key;
        Values[insertIndex] = value;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        // Since it's sorted, we could use BinarySearch, but for 16 elements linear scan is fine/fast.
        var index = IndexOfKey(key);
        if (index != -1)
        {
            value = Values[index];
            return true;
        }

        value = default;
        return false;
    }

    public bool ContainsKey(TKey key)
    {
        return IndexOfKey(key) != -1;
    }

    public void Clear()
    {
        Keys.Clear();
        Values.Clear();
    }

    private int IndexOfKey(TKey key)
    {
        for (int i = 0; i < Keys.Count; i++)
        {
            if (Keys[i].Equals(key))
            {
                return i;
            }
        }
        return -1;
    }

    public bool Equals(Dictionary16<TKey, TValue> other)
    {
        return Keys.Equals(other.Keys) && Values.Equals(other.Values);
    }

    public override bool Equals(object? obj)
    {
        return obj is Dictionary16<TKey, TValue> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Keys, Values);
    }

    public static bool operator ==(Dictionary16<TKey, TValue> a, Dictionary16<TKey, TValue> b) => a.Equals(b);
    public static bool operator !=(Dictionary16<TKey, TValue> a, Dictionary16<TKey, TValue> b) => !a.Equals(b);
}
