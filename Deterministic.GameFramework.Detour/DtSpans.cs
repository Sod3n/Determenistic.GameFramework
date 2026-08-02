using System;

namespace Deterministic.GameFramework.Detour
{
    public static class DtSpans
    {
        public static void Copy<T>(ReadOnlySpan<T> src, Span<T> dst)
        {
            src.CopyTo(dst);
        }

        public static void Copy<T>(ReadOnlySpan<T> src, int srcIdx, Span<T> dst, int dstIdx, int length)
        {
            var slicedSrc = src.Slice(srcIdx, length);
            var slicedDst = dst.Slice(dstIdx);
            slicedSrc.CopyTo(slicedDst);
        }

        public static void Move<T>(Span<T> src, int srcIdx, int dstIdx, int length)
        {
            var slicedSrc = src.Slice(srcIdx, length);
            var slicedDst = src.Slice(dstIdx, length);
            slicedSrc.CopyTo(slicedDst);
        }

        public static void Fill<T>(Span<T> span, T value, int start, int count)
        {
            span.Slice(start, count).Fill(value);
        }
    }
}
