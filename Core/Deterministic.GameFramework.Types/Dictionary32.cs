using System;
using System.Collections.Generic;

namespace Deterministic.GameFramework.Types;

public struct Dictionary32<TKey, TValue> : IParam, IEquatable<Dictionary32<TKey, TValue>>
    where TKey : struct, IEquatable<TKey>
    where TValue : struct, IEquatable<TValue>
{
    public List32<TKey> Keys;
    public List32<TValue> Values;

    public int Count => Keys.Count;

    public TValue this[TKey key]
    {
        get
        {
            if (TryGetValue(key, out var value))
            {
                return value;
            }
            throw new KeyNotFoundException($"Key {key} not found in Dictionary32");
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
            throw new ArgumentException($"Key {key} already exists in Dictionary32");
        }
        
        if (Keys.Count >= List32<TKey>.Capacity)
        {
            throw new InvalidOperationException("Dictionary32 is full");
        }

        Keys.Add(key);
        Values.Add(value);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
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

    public bool Equals(Dictionary32<TKey, TValue> other)
    {
        return Keys.Equals(other.Keys) && Values.Equals(other.Values);
    }

    public override bool Equals(object? obj)
    {
        return obj is Dictionary32<TKey, TValue> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Keys, Values);
    }

    public static bool operator ==(Dictionary32<TKey, TValue> a, Dictionary32<TKey, TValue> b) => a.Equals(b);
    public static bool operator !=(Dictionary32<TKey, TValue> a, Dictionary32<TKey, TValue> b) => !a.Equals(b);
}
