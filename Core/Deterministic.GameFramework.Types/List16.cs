using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.Types;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct List16<T> : IParam, IEnumerable<T>, IEquatable<List16<T>> where T : struct, IEquatable<T>
{
    public T Item0;
    public T Item1;
    public T Item2;
    public T Item3;
    public T Item4;
    public T Item5;
    public T Item6;
    public T Item7;
    public T Item8;
    public T Item9;
    public T Item10;
    public T Item11;
    public T Item12;
    public T Item13;
    public T Item14;
    public T Item15;
    
    public byte Count;
    public const int Capacity = 16;

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
                8 => Item8,
                9 => Item9,
                10 => Item10,
                11 => Item11,
                12 => Item12,
                13 => Item13,
                14 => Item14,
                15 => Item15,
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
                case 8: Item8 = value; break;
                case 9: Item9 = value; break;
                case 10: Item10 = value; break;
                case 11: Item11 = value; break;
                case 12: Item12 = value; break;
                case 13: Item13 = value; break;
                case 14: Item14 = value; break;
                case 15: Item15 = value; break;
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
            case 8: Item8 = item; break;
            case 9: Item9 = item; break;
            case 10: Item10 = item; break;
            case 11: Item11 = item; break;
            case 12: Item12 = item; break;
            case 13: Item13 = item; break;
            case 14: Item14 = item; break;
            case 15: Item15 = item; break;
        }
        Count++;
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
        Item8 = default;
        Item9 = default;
        Item10 = default;
        Item11 = default;
        Item12 = default;
        Item13 = default;
        Item14 = default;
        Item15 = default;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(List16<T> other)
    {
        if (Count != other.Count) return false;
        
        // Only compare elements up to Count
        for (int i = 0; i < Count; i++)
        {
            if (!this[i].Equals(other[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is List16<T> other && Equals(other);
    
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

    public static bool operator ==(List16<T> a, List16<T> b) => a.Equals(b);
    public static bool operator !=(List16<T> a, List16<T> b) => !a.Equals(b);
}
