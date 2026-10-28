using System;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Explicit QA handoff only. Full creation/native/rendering closure is never implied.
    public sealed class PerformanceUiAuxiliaryPhase : IDisposable
    {
        [Serializable] public sealed class Report
        {
            public string boundary = "Separate minimap/UI/auxiliary source contexts only; no timing, population or rendering acceptance", beginError;
            public double startedElapsed, endedElapsed;
            public bool previousCaptured, started, completed;
            public PerformanceActorCensus.Report preflight, postflight;
            public PerformanceMinimapContext.Checkpoint previousMinimap, minimapPreflight, minimapPostflight;
            public PerformanceUiSourceContext.Checkpoint previousUi, uiPreflight, uiPostflight;
            public PerformanceAuxiliaryContext.Checkpoint previousAuxiliary, auxiliaryPreflight, auxiliaryPostflight;
            public CaptureResult previousMinimapValidation, previousUiValidation, previousAuxiliaryValidation;
            public CaptureResult minimapValidation, uiValidation, auxiliaryValidation;
        }
        public Report Evidence { get; } = new Report();
        PerformanceMinimapContext minimap;
        PerformanceUiSourceContext ui;
        PerformanceAuxiliaryContext auxiliary;
        bool attempted, disposed;

        public void Begin(PerformanceActorCensus.Snapshot census, PerformanceMinimapContext previousMinimap,
            PerformanceUiSourceContext previousUi, PerformanceAuxiliaryContext previousAuxiliary, double elapsed)
        {
            if (disposed || attempted || census?.report == null || previousMinimap == null || previousMinimap.IsDisposed ||
                previousUi == null || previousUi.IsDisposed || previousAuxiliary == null || previousAuxiliary.IsDisposed ||
                double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0)
                throw new ArgumentException("Fresh phase, present census and live previous contexts required.");
            attempted = true; Evidence.startedElapsed = elapsed; Evidence.preflight = census.report;
            try
            {
                Evidence.previousMinimap = previousMinimap.CaptureCheckpoint("previous-minimap-at-setup-boundary");
                Evidence.previousUi = previousUi.CaptureCheckpoint("previous-ui-at-setup-boundary");
                Evidence.previousAuxiliary = previousAuxiliary.CaptureCheckpoint("previous-auxiliary-at-setup-boundary");
                Evidence.previousMinimapValidation = new CaptureResult(); previousMinimap.Validate(Evidence.previousMinimapValidation);
                Evidence.previousUiValidation = new CaptureResult(); previousUi.Validate(Evidence.previousUiValidation);
                Evidence.previousAuxiliaryValidation = new CaptureResult(); previousAuxiliary.Validate(Evidence.previousAuxiliaryValidation);
                Evidence.previousCaptured = true;
                // Keep the captured rejection; release exclusive callback owners before creating fresh ones.
                previousMinimap.Dispose(); previousUi.Dispose(); previousAuxiliary.Dispose();
                minimap = census.CreateUiContext(); ui = census.CreateUiSourceContext(); auxiliary = census.CreateAuxiliaryContext();
                minimap.Freeze(); ui.Freeze(); auxiliary.Freeze();
                Evidence.minimapPreflight = minimap.CaptureCheckpoint("settled-minimap-phase-start");
                Evidence.uiPreflight = ui.CaptureCheckpoint("settled-ui-phase-start");
                Evidence.auxiliaryPreflight = auxiliary.CaptureCheckpoint("settled-auxiliary-phase-start");
                Evidence.started = true;
            }
            catch (Exception error) { Evidence.beginError = error.ToString(); Dispose(); throw; }
        }
        public void Poll()
        {
            if (disposed || !Evidence.started) throw new InvalidOperationException("Live started UI/auxiliary phase required.");
            minimap.Poll(); ui.Poll(); auxiliary.Poll();
        }
        public void Complete(double elapsed)
        {
            if (disposed || !Evidence.started) throw new InvalidOperationException("Live started UI/auxiliary phase required.");
            if (Evidence.completed) return;
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < Evidence.startedElapsed)
                throw new ArgumentException("Ordered finite phase end required.");
            Poll();
            Evidence.minimapPostflight = minimap.CaptureCheckpoint("settled-minimap-phase-end");
            Evidence.uiPostflight = ui.CaptureCheckpoint("settled-ui-phase-end");
            Evidence.auxiliaryPostflight = auxiliary.CaptureCheckpoint("settled-auxiliary-phase-end");
            Evidence.minimapValidation = new CaptureResult(); minimap.Validate(Evidence.minimapValidation);
            Evidence.uiValidation = new CaptureResult(); ui.Validate(Evidence.uiValidation);
            Evidence.auxiliaryValidation = new CaptureResult(); auxiliary.Validate(Evidence.auxiliaryValidation);
            Evidence.postflight = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" }).report;
            Evidence.endedElapsed = elapsed; Evidence.completed = true;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { minimap?.Dispose(); }
            finally { try { ui?.Dispose(); } finally { auxiliary?.Dispose(); minimap = null; ui = null; auxiliary = null; } }
        }
    }
#endif
}
