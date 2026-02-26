namespace Deterministic.GameFramework.Network.NetworkState;

public struct List8<T> where T : struct
{
    public T Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7;
    public byte Count;
    
    public T this[int index] {
        get {
            switch(index) {
                case 0: return Item0;
                case 1: return Item1;
                case 2: return Item2;
                case 3: return Item3;
                case 4: return Item4;
                case 5: return Item5;
                case 6: return Item6;
                case 7: return Item7;
                default: throw new IndexOutOfRangeException();
            }
        }
        set {
            switch(index) {
                case 0: Item0 = value; break;
                case 1: Item1 = value; break;
                case 2: Item2 = value; break;
                case 3: Item3 = value; break;
                case 4: Item4 = value; break;
                case 5: Item5 = value; break;
                case 6: Item6 = value; break;
                case 7: Item7 = value; break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }
}