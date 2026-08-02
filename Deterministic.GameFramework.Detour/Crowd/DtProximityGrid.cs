using System;
using System.Collections.Generic;
using System.Linq;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour.Crowd
{
    public class DtProximityGrid
    {
        private readonly Float _cellSize;
        private readonly Float _invCellSize;
        private readonly Dictionary<long, List<DtCrowdAgent>> _items;

        public DtProximityGrid(Float cellSize)
        {
            _cellSize = cellSize;
            _invCellSize = 1 / cellSize;
            _items = new Dictionary<long, List<DtCrowdAgent>>();
        }

        public static long CombineKey(int x, int y)
        {
            uint ux = (uint)x;
            uint uy = (uint)y;
            return ((long)ux << 32) | uy;
        }

        public static void DecomposeKey(long key, out int x, out int y)
        {
            uint ux = (uint)(key >> 32);
            uint uy = (uint)key;
            x = (int)ux;
            y = (int)uy;
        }

        public void Clear()
        {
            _items.Clear();
        }

        public void AddItem(DtCrowdAgent agent, Float minx, Float miny, Float maxx, Float maxy)
        {
            int iminx = (int)Float.Floor(minx * _invCellSize);
            int iminy = (int)Float.Floor(miny * _invCellSize);
            int imaxx = (int)Float.Floor(maxx * _invCellSize);
            int imaxy = (int)Float.Floor(maxy * _invCellSize);

            for (int y = iminy; y <= imaxy; ++y)
            {
                for (int x = iminx; x <= imaxx; ++x)
                {
                    long key = CombineKey(x, y);
                    if (!_items.TryGetValue(key, out var ids))
                    {
                        ids = new List<DtCrowdAgent>();
                        _items.Add(key, ids);
                    }

                    ids.Add(agent);
                }
            }
        }

        public int QueryItems(Float minx, Float miny, Float maxx, Float maxy, Span<int> ids, int maxIds)
        {
            int iminx = (int)Float.Floor(minx * _invCellSize);
            int iminy = (int)Float.Floor(miny * _invCellSize);
            int imaxx = (int)Float.Floor(maxx * _invCellSize);
            int imaxy = (int)Float.Floor(maxy * _invCellSize);

            int n = 0;

            for (int y = iminy; y <= imaxy; ++y)
            {
                for (int x = iminx; x <= imaxx; ++x)
                {
                    long key = CombineKey(x, y);
                    bool hasPool = _items.TryGetValue(key, out var pool);
                    if (!hasPool)
                    {
                        continue;
                    }

                    for (int idx = 0; idx < pool.Count; ++idx)
                    {
                        var item = pool[idx];

                        // Check if the id exists already.
                        int end = n;
                        int i = 0;
                        while (i != end && ids[i] != item.idx)
                        {
                            ++i;
                        }

                        // Item not found, add it.
                        if (i == n)
                        {
                            ids[n++] = item.idx;

                            if (n >= maxIds)
                                return n;
                        }
                    }
                }
            }

            return n;
        }

        public IEnumerable<(long, int)> GetItemCounts()
        {
            return _items
                .Where(e => e.Value.Count > 0)
                .Select(e => (e.Key, e.Value.Count));
        }

        public Float GetCellSize()
        {
            return _cellSize;
        }
    }
}
