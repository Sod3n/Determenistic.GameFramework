// Ported from CDT C++ library (detail namespace in Triangulation.h and portable_nth_element.hpp)
// Deterministic utilities for triangulation: PRNG, shuffle, iota, and nth_element.

using System;
using System.Collections.Generic;

namespace Deterministic.GameFramework.CDT
{
    /// <summary>
    /// SplitMix64 pseudo-random number generator.
    /// Produces identical output for identical seeds on all platforms.
    /// </summary>
    internal struct SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed)
        {
            _state = seed;
        }

        /// <summary>
        /// Advance the state and return the next pseudo-random 64-bit value.
        /// </summary>
        public ulong Next()
        {
            ulong z = (_state += 0x9e3779b97f4a7c15UL);
            z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
            z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>
    /// Fisher-Yates shuffle and iota helper, using <see cref="SplitMix64"/> for determinism.
    /// </summary>
    internal static class ShuffleHelper
    {
        /// <summary>
        /// In-place Fisher-Yates shuffle of <paramref name="list"/> using the given PRNG.
        /// </summary>
        public static void Shuffle<T>(List<T> list, ref SplitMix64 rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = (int)(rng.Next() % (ulong)(i + 1));
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>
        /// Create an array [startValue, startValue+1, ..., startValue+count-1].
        /// </summary>
        public static int[] Iota(int count, int startValue)
        {
            int[] result = new int[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = startValue + i;
            }
            return result;
        }
    }

    /// <summary>
    /// Portable nth_element (partial sort via introselect / quickselect).
    /// Rearranges <c>array[left..right)</c> so that <c>array[nth]</c> is in its
    /// correct sorted position, with all elements before it &lt;= and all after &gt;=.
    /// Ported from LLVM libc++ portable_nth_element.hpp used by CDT.
    /// </summary>
    internal static class NthElement
    {
        /// <summary>
        /// Partial sort so that array[nth] holds the value it would hold if the
        /// range [left, right) were fully sorted according to <paramref name="comparison"/>.
        /// </summary>
        public static void Perform<T>(T[] array, int left, int nth, int right, Comparison<T> comparison)
        {
            // Direct port of LLVM libc++ nth_element with selection_sort fallback.
            while (true)
            {
                if (nth >= right)
                    return;

                int len = right - left;
                switch (len)
                {
                    case 0:
                    case 1:
                        return;
                    case 2:
                        if (comparison(array[right - 1], array[left]) < 0)
                            Swap(array, left, right - 1);
                        return;
                    case 3:
                        Sort3(array, left, left + 1, right - 1, comparison);
                        return;
                }

                const int limit = 7;
                if (len <= limit)
                {
                    SelectionSort(array, left, right, comparison);
                    return;
                }

                // len > limit >= 3
                int m = left + len / 2;
                int lm1 = right - 1;
                int nSwaps = Sort3(array, left, m, lm1, comparison);

                // Partition around *m
                int i = left;
                int j = lm1;

                if (comparison(array[i], array[m]) >= 0) // *first >= *m
                {
                    while (true)
                    {
                        --j;
                        if (i == j)
                        {
                            // *first == *m, *m <= all other elements
                            ++i; // first + 1
                            j = right;
                            --j;
                            if (comparison(array[left], array[j]) >= 0) // *first >= *(last-1)
                            {
                                while (true)
                                {
                                    if (i == j)
                                        return;
                                    if (comparison(array[left], array[i]) < 0)
                                    {
                                        Swap(array, i, j);
                                        ++nSwaps;
                                        ++i;
                                        break;
                                    }
                                    ++i;
                                }
                            }

                            if (i == j)
                                return;

                            while (true)
                            {
                                while (comparison(array[left], array[i]) >= 0)
                                    ++i;
                                while (comparison(array[left], array[--j]) < 0)
                                    ;
                                if (i >= j)
                                    break;
                                Swap(array, i, j);
                                ++nSwaps;
                                ++i;
                            }

                            if (nth < i)
                                return;

                            // nth_element the second part
                            left = i;
                            goto restart;
                        }

                        if (comparison(array[j], array[m]) < 0)
                        {
                            Swap(array, i, j);
                            ++nSwaps;
                            break;
                        }
                    }
                }

                ++i;

                if (i < j)
                {
                    while (true)
                    {
                        while (comparison(array[i], array[m]) < 0)
                            ++i;
                        while (comparison(array[--j], array[m]) >= 0)
                            ;
                        if (i >= j)
                            break;
                        Swap(array, i, j);
                        ++nSwaps;
                        if (m == i)
                            m = j;
                        ++i;
                    }
                }

                // Place pivot
                if (i != m && comparison(array[m], array[i]) < 0)
                {
                    Swap(array, i, m);
                    ++nSwaps;
                }

                if (nth == i)
                    return;

                if (nSwaps == 0)
                {
                    // Check if already sorted on the side containing nth
                    if (nth < i)
                    {
                        int jj = left;
                        int mm = left;
                        ++jj;
                        while (jj != i)
                        {
                            if (comparison(array[jj], array[mm]) < 0)
                                goto not_sorted;
                            mm = jj;
                            ++jj;
                        }
                        return;
                    }
                    else
                    {
                        int jj = i;
                        int mm = i;
                        ++jj;
                        while (jj != right)
                        {
                            if (comparison(array[jj], array[mm]) < 0)
                                goto not_sorted;
                            mm = jj;
                            ++jj;
                        }
                        return;
                    }
                }

                not_sorted:
                if (nth < i)
                {
                    right = i;
                }
                else
                {
                    left = i + 1;
                }

                restart:;
            }
        }

        private static void Swap<T>(T[] array, int a, int b)
        {
            T tmp = array[a];
            array[a] = array[b];
            array[b] = tmp;
        }

        /// <summary>
        /// Sort three elements. Returns the number of swaps performed (0-2).
        /// </summary>
        private static int Sort3<T>(T[] array, int x, int y, int z, Comparison<T> c)
        {
            int r = 0;
            if (c(array[y], array[x]) >= 0) // x <= y
            {
                if (c(array[z], array[y]) >= 0) // y <= z
                    return r;
                // x <= y && y > z
                Swap(array, y, z);
                r = 1;
                if (c(array[y], array[x]) < 0) // x > y
                {
                    Swap(array, x, y);
                    r = 2;
                }
                return r;
            }
            if (c(array[z], array[y]) < 0) // x > y && y > z
            {
                Swap(array, x, z);
                r = 1;
                return r;
            }
            Swap(array, x, y); // x > y && y <= z
            r = 1;
            if (c(array[z], array[y]) < 0) // y > z
            {
                Swap(array, y, z);
                r = 2;
            }
            return r;
        }

        /// <summary>
        /// Selection sort for small ranges.
        /// </summary>
        private static void SelectionSort<T>(T[] array, int first, int last, Comparison<T> comp)
        {
            for (int i = first; i < last - 1; i++)
            {
                int minIdx = i;
                for (int j = i + 1; j < last; j++)
                {
                    if (comp(array[j], array[minIdx]) < 0)
                        minIdx = j;
                }
                if (minIdx != i)
                    Swap(array, i, minIdx);
            }
        }
    }
}
