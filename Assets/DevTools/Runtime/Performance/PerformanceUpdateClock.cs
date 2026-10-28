using System;
using System.Diagnostics;

namespace RPGKarawara.DevTools.Performance
{
    // Update callback pacing only. This observer has no present/display semantics.
    // Caller owns the shared route clock, ends it before export, and records exactly
    // one endpoint per Update. Export/copies occur outside the measured interval.
    public sealed class PerformanceUpdateClock
    {
        public const int MaximumCapacity = 120000;
        [Serializable] public sealed class Report
        {
            public int schemaVersion = 1;
            public string clockSource = "Stopwatch.GetTimestamp / Update endpoints";
            public bool faulted, overflow;
            public readonly bool timingEligible = false, g1bAccepted = false;
            public PerformanceExternalClock clock;
            public long[] ticks;
        }
        readonly long[] ticks;
        readonly PerformanceExternalClock clock;
        int count;
        bool faulted, overflow;

        PerformanceUpdateClock(int capacity, PerformanceExternalClock clock)
        { ticks = new long[capacity]; this.clock = clock; }

        public static PerformanceUpdateClock Create(int capacity, PerformanceExternalClock clock)
        {
            if (capacity < 1 || capacity > MaximumCapacity) throw new ArgumentException("Bounded 1..120000 Update endpoint capacity required.");
            var start = clock?.started;
            if (clock == null || clock.schemaVersion != 1 || clock.processId <= 0 || clock.frequency <= 0 || !clock.isHighResolution || start == null ||
                start.qpcBefore < 0 || start.qpcAfter < start.qpcBefore || double.IsNaN(start.unitySeconds) || double.IsInfinity(start.unitySeconds) || start.unitySeconds < 0)
                throw new ArgumentException("Explicit valid high-resolution owned route clock required.");
            return new PerformanceUpdateClock(capacity, clock);
        }

        public void Record() => Record(Stopwatch.GetTimestamp());
        public void Record(long qpcEndpoint)
        {
            if (faulted) return;
            if (qpcEndpoint < clock.started.qpcBefore || (count > 0 && qpcEndpoint <= ticks[count - 1]) ||
                (clock.ended != null && qpcEndpoint > clock.ended.qpcAfter))
            { faulted = true; return; }
            if (count == ticks.Length) { overflow = true; faulted = true; return; }
            ticks[count++] = qpcEndpoint;
        }

        public Report Snapshot()
        {
            var copy = new long[count]; Array.Copy(ticks, copy, count);
            return new Report { ticks = copy, faulted = faulted, overflow = overflow,
                clock = new PerformanceExternalClock { schemaVersion = clock.schemaVersion, processId = clock.processId,
                    frequency = clock.frequency, isHighResolution = clock.isHighResolution,
                    started = Copy(clock.started), ended = Copy(clock.ended) } };
        }
        static PerformanceExternalClock.Observation Copy(PerformanceExternalClock.Observation observation)
            => observation == null ? null : new PerformanceExternalClock.Observation {
                qpcBefore = observation.qpcBefore, qpcAfter = observation.qpcAfter, unitySeconds = observation.unitySeconds };
    }
}
