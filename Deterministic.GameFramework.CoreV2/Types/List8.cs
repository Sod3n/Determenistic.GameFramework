using System;
using System.Collections;
using System.Collections.Generic;

namespace Deterministic.GameFramework.CoreV2;

public struct List8<T> : IParam, IEnumerable<T> where T : struct
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
    
    public void Clear()
    {
        Count = 0;
        // We don't strictly need to clear the values for correctness, 
        // but for deterministic serialization of the whole struct, it might be safer to zero them out 
        // if we serialize the whole struct bytes. 
        // However, usually we only serialize valid data.
        // For simplicity and perf, we just reset Count.
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
}
