using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.Types;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct List8<T> : IParam, IEnumerable<T>, IEquatable<List8<T>> where T : struct, IEquatable<T>
{
    public T Item0;
    public T Item1;
    public T Item2;
    public T Item3;
    public T Item4;
    public T Item5;
    public T Item6;
    public T Item7;
    
    public byte Count;
    public const int Capacity = 8;

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= Count) throw new IndexOutOfRangeException();
            return index switch
            {
                0 => Item0,
                1 => Item1,
                2 => Item2,
                3 => Item3,
                4 => Item4,
                5 => Item5,
                6 => Item6,
                7 => Item7,
                _ => default // Should not happen given check above
            };
        }
        set
        {
            if (index < 0 || index >= Count) throw new IndexOutOfRangeException();
            switch (index)
            {
                case 0: Item0 = value; break;
                case 1: Item1 = value; break;
                case 2: Item2 = value; break;
                case 3: Item3 = value; break;
                case 4: Item4 = value; break;
                case 5: Item5 = value; break;
                case 6: Item6 = value; break;
                case 7: Item7 = value; break;
            }
        }
    }

    public void Add(T item)
    {
        if (Count >= Capacity) throw new InvalidOperationException("List is full");
        
        switch (Count)
        {
            case 0: Item0 = item; break;
            case 1: Item1 = item; break;
            case 2: Item2 = item; break;
            case 3: Item3 = item; break;
            case 4: Item4 = item; break;
            case 5: Item5 = item; break;
            case 6: Item6 = item; break;
            case 7: Item7 = item; break;
        }
        Count++;
    }
    
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Count) throw new IndexOutOfRangeException();
        for (int i = index; i < Count - 1; i++)
            this[i] = this[i + 1];
        // Clear the last slot directly (can't use indexer since Count is about to shrink)
        int lastSlot = Count - 1;
        switch (lastSlot)
        {
            case 0: Item0 = default; break;
            case 1: Item1 = default; break;
            case 2: Item2 = default; break;
            case 3: Item3 = default; break;
            case 4: Item4 = default; break;
            case 5: Item5 = default; break;
            case 6: Item6 = default; break;
            case 7: Item7 = default; break;
        }
        Count--;
    }

    public void Clear()
    {
        Count = 0;
        Item0 = default;
        Item1 = default;
        Item2 = default;
        Item3 = default;
        Item4 = default;
        Item5 = default;
        Item6 = default;
        Item7 = default;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(List8<T> other)
    {
        if (Count != other.Count) return false;
        
        // Only compare elements up to Count
        for (int i = 0; i < Count; i++)
        {
            if (!this[i].Equals(other[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is List8<T> other && Equals(other);
    
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Count);
        for (int i = 0; i < Count; i++)
        {
            hash.Add(this[i]);
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(List8<T> a, List8<T> b) => a.Equals(b);
    public static bool operator !=(List8<T> a, List8<T> b) => !a.Equals(b);
}
