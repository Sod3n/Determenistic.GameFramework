using System;
using System.Collections;
using System.Collections.Generic;

namespace Deterministic.GameFramework.Types;

public struct List32<T> : IParam, IEnumerable<T>, IEquatable<List32<T>> where T : struct, IEquatable<T>
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
    public T Item16;
    public T Item17;
    public T Item18;
    public T Item19;
    public T Item20;
    public T Item21;
    public T Item22;
    public T Item23;
    public T Item24;
    public T Item25;
    public T Item26;
    public T Item27;
    public T Item28;
    public T Item29;
    public T Item30;
    public T Item31;
    
    public byte Count;
    public const int Capacity = 32;

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
                16 => Item16,
                17 => Item17,
                18 => Item18,
                19 => Item19,
                20 => Item20,
                21 => Item21,
                22 => Item22,
                23 => Item23,
                24 => Item24,
                25 => Item25,
                26 => Item26,
                27 => Item27,
                28 => Item28,
                29 => Item29,
                30 => Item30,
                31 => Item31,
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
                case 16: Item16 = value; break;
                case 17: Item17 = value; break;
                case 18: Item18 = value; break;
                case 19: Item19 = value; break;
                case 20: Item20 = value; break;
                case 21: Item21 = value; break;
                case 22: Item22 = value; break;
                case 23: Item23 = value; break;
                case 24: Item24 = value; break;
                case 25: Item25 = value; break;
                case 26: Item26 = value; break;
                case 27: Item27 = value; break;
                case 28: Item28 = value; break;
                case 29: Item29 = value; break;
                case 30: Item30 = value; break;
                case 31: Item31 = value; break;
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
            case 16: Item16 = item; break;
            case 17: Item17 = item; break;
            case 18: Item18 = item; break;
            case 19: Item19 = item; break;
            case 20: Item20 = item; break;
            case 21: Item21 = item; break;
            case 22: Item22 = item; break;
            case 23: Item23 = item; break;
            case 24: Item24 = item; break;
            case 25: Item25 = item; break;
            case 26: Item26 = item; break;
            case 27: Item27 = item; break;
            case 28: Item28 = item; break;
            case 29: Item29 = item; break;
            case 30: Item30 = item; break;
            case 31: Item31 = item; break;
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
        Item16 = default;
        Item17 = default;
        Item18 = default;
        Item19 = default;
        Item20 = default;
        Item21 = default;
        Item22 = default;
        Item23 = default;
        Item24 = default;
        Item25 = default;
        Item26 = default;
        Item27 = default;
        Item28 = default;
        Item29 = default;
        Item30 = default;
        Item31 = default;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(List32<T> other)
    {
        if (Count != other.Count) return false;
        
        // Only compare elements up to Count
        for (int i = 0; i < Count; i++)
        {
            if (!this[i].Equals(other[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is List32<T> other && Equals(other);
    
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

    public static bool operator ==(List32<T> a, List32<T> b) => a.Equals(b);
    public static bool operator !=(List32<T> a, List32<T> b) => !a.Equals(b);
}
