using System;
using System.Diagnostics;

namespace RPGKarawara.DevTools.Performance
{
    // Two bounded observations only; reusable by a lightweight route driver.
    [Serializable]
    public sealed class PerformanceExternalClock
    {
        [Serializable] public sealed class Observation
        {
            public long qpcBefore, qpcAfter;
            public double unitySeconds;
        }
        public int schemaVersion = 1, processId;
        public long frequency;
        public bool isHighResolution;
        public Observation started, ended;

        static Observation Observe(Func<double> unscaledClock)
        {
            if (unscaledClock == null) throw new ArgumentNullException(nameof(unscaledClock));
            long before = Stopwatch.GetTimestamp();
            double seconds = unscaledClock();
            long after = Stopwatch.GetTimestamp();
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || after < before)
                throw new InvalidOperationException("Finite unscaled clock and monotonic Stopwatch observation required.");
            return new Observation { qpcBefore = before, unitySeconds = seconds, qpcAfter = after };
        }
        public static PerformanceExternalClock Begin(Func<double> unscaledClock)
        {
            using (var process = Process.GetCurrentProcess())
                return new PerformanceExternalClock { processId = process.Id, frequency = Stopwatch.Frequency, isHighResolution = Stopwatch.IsHighResolution, started = Observe(unscaledClock) };
        }
        public void End(Func<double> unscaledClock)
        {
            if (ended != null) return;
            ended = Observe(unscaledClock);
        }
    }
}
