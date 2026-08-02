using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.ECS;

/// <summary>
/// Zero-allocation entity filter. Works with foreach via duck-typed struct enumerator.
/// Implements IEnumerable{Entity} for LINQ compatibility (boxes on that path only).
/// </summary>
public readonly struct MaskFilter : IEnumerable<Entity>
{
    private readonly BitMask128[] _masks;
    private readonly BitMask128 _query;
    private readonly int _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal MaskFilter(BitMask128[] masks, BitMask128 query, int count)
    {
        _masks = masks;
        _query = query;
        _count = count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_masks, _query, _count);

    IEnumerator<Entity> IEnumerable<Entity>.GetEnumerator() => new Enumerator(_masks, _query, _count);
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_masks, _query, _count);

    public struct Enumerator : IEnumerator<Entity>
    {
        private readonly BitMask128[] _masks;
        private readonly BitMask128 _query;
        private readonly int _count;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(BitMask128[] masks, BitMask128 query, int count)
        {
            _masks = masks;
            _query = query;
            _count = count;
            _index = -1;
        }

        public Entity Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_index);
        }

        object IEnumerator.Current => Current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            var masks = _masks;
            var query = _query;
            int i = _index;
            while (++i < _count)
            {
                if (masks[i].HasAll(query))
                {
                    _index = i;
                    return true;
                }
            }
            _index = i;
            return false;
        }

        public void Reset() => _index = -1;
        public void Dispose() { }
    }
}
