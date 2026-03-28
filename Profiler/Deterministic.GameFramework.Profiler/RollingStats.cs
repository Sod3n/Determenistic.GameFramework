namespace Deterministic.GameFramework.Profiler;

/// <summary>
/// Tracks min/max/average over a fixed-size rolling window. Zero-allocation after construction.
/// </summary>
public sealed class RollingStats
{
    private readonly double[] _samples;
    private int _head;
    private int _count;

    public int WindowSize => _samples.Length;

    public RollingStats(int windowSize)
    {
        _samples = new double[windowSize];
    }

    public void Record(double value)
    {
        _samples[_head] = value;
        _head = (_head + 1) % _samples.Length;
        if (_count < _samples.Length) _count++;
    }

    public double Min
    {
        get
        {
            if (_count == 0) return 0;
            double min = double.MaxValue;
            for (int i = 0; i < _count; i++)
                if (_samples[i] < min) min = _samples[i];
            return min;
        }
    }

    public double Max
    {
        get
        {
            if (_count == 0) return 0;
            double max = 0;
            for (int i = 0; i < _count; i++)
                if (_samples[i] > max) max = _samples[i];
            return max;
        }
    }

    public double Average
    {
        get
        {
            if (_count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < _count; i++)
                sum += _samples[i];
            return sum / _count;
        }
    }

    public int Count => _count;

    public void Reset()
    {
        _head = 0;
        _count = 0;
    }
}
