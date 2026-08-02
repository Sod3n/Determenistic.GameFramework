using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour.Crowd
{
    public class DtCrowdTelemetry
    {
        public const int TIMING_SAMPLES = 10;
        private Float _maxTimeToEnqueueRequest;
        private Float _maxTimeToFindPath;

        private readonly Dictionary<DtCrowdTimerLabel, long> _executionTimings = new Dictionary<DtCrowdTimerLabel, long>();
        private readonly Dictionary<DtCrowdTimerLabel, Queue<long>> _executionTimingSamples = new Dictionary<DtCrowdTimerLabel, Queue<long>>();

        public Float MaxTimeToEnqueueRequest()
        {
            return _maxTimeToEnqueueRequest;
        }

        public Float MaxTimeToFindPath()
        {
            return _maxTimeToFindPath;
        }

        public List<(string Label, long Ticks)> ToExecutionTimings()
        {
            return _executionTimings
                .Select(e => (e.Key.Label, e.Value))
                .OrderByDescending(x => x.Value)
                .ToList();
        }

        public void Start()
        {
            _maxTimeToEnqueueRequest = 0;
            _maxTimeToFindPath = 0;
            _executionTimings.Clear();
        }

        public void RecordMaxTimeToEnqueueRequest(Float time)
        {
            _maxTimeToEnqueueRequest = Float.Max(_maxTimeToEnqueueRequest, time);
        }

        public void RecordMaxTimeToFindPath(Float time)
        {
            _maxTimeToFindPath = Float.Max(_maxTimeToFindPath, time);
        }

        internal DtCrowdScopedTimer ScopedTimer(DtCrowdTimerLabel label)
        {
            return new DtCrowdScopedTimer(this, label);
        }

        internal void Start(DtCrowdTimerLabel name)
        {
            _executionTimings.Add(name, Stopwatch.GetTimestamp());
        }

        internal void Stop(DtCrowdTimerLabel name)
        {
            long duration = Stopwatch.GetTimestamp() - _executionTimings[name];
            if (!_executionTimingSamples.TryGetValue(name, out var cb))
            {
                cb = new Queue<long>(TIMING_SAMPLES);
                _executionTimingSamples.Add(name, cb);
            }

            if (cb.Count >= TIMING_SAMPLES)
                cb.Dequeue();
            cb.Enqueue(duration);
            _executionTimings[name] = (long)cb.Average();
        }
    }
}
