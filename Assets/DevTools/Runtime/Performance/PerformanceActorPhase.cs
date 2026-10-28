using System;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Actor-only diagnostic phase. UI/auxiliary/rendering and creation closure remain separate gates.
    public sealed class PerformanceActorPhase : IDisposable
    {
        [Serializable] public sealed class Report
        {
            public string boundary = "Separate actor source context only; no population, timing, UI or creation-closure acceptance";
            public double startedElapsed, endedElapsed;
            public bool completed, sourceFaulted;
            public long notifications;
            public PerformanceActorCensus.Report preflight, postflight;
            public CaptureResult previousValidation, validation;
            public PerformanceActorLifecycleScope.HistoryReport previousHistory, history;
            public int? previousViolationMask, violationMask;
        }
        public Report Evidence { get; private set; }
        PerformanceActorRosterContext context;
        PerformanceActorLifecycleScope scope;
        bool disposed;

        public static PerformanceActorPhase Begin(PerformanceActorCensus.Snapshot census,
            PerformanceActorRosterContext previousContext, PerformanceActorLifecycleScope previousScope, double elapsed)
        {
            if (census?.report == null || previousContext == null || previousScope == null || previousScope.IsDisposed ||
                double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0)
                throw new ArgumentException("Present census, live previous scope and finite phase boundary required.");
            var previous = new CaptureResult(); previousContext.Validate(previous);
            previousScope.Dispose(); // Never clear/rebind the previous context or its latched transition.
            var phase = new PerformanceActorPhase { Evidence = new Report {
                startedElapsed = elapsed, preflight = census.report, previousValidation = previous, previousHistory = previousScope.CaptureHistory(), previousViolationMask = previousContext.SourceViolationMask } };
            try
            {
                phase.context = census.CreateContext();
                phase.scope = PerformanceActorLifecycleScope.Begin(new[] { "-karawaraPerformanceRoster" }, phase.context);
                return phase;
            }
            catch { phase.Dispose(); throw; }
        }
        public void Complete(double elapsed)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PerformanceActorPhase));
            if (Evidence.completed) return;
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < Evidence.startedElapsed)
                throw new ArgumentException("Ordered finite phase end required.");
            Evidence.postflight = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" }).report;
            Evidence.validation = new CaptureResult(); context.Validate(Evidence.validation);
            Evidence.violationMask = context.SourceViolationMask;
            Evidence.notifications = scope.NotificationCount; Evidence.sourceFaulted = scope.NotificationFaulted;
            Evidence.history = scope.CaptureHistory();
            Evidence.endedElapsed = elapsed; Evidence.completed = true;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { scope?.Dispose(); } finally { context?.Dispose(); scope = null; context = null; }
        }
    }
#endif
}
