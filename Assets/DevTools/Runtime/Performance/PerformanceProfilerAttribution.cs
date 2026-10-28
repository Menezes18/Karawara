using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Bounded local diagnostic recording only. This session can never certify timing.
    public sealed class PerformanceProfilerAttribution : IDisposable
    {
        public static readonly ProfilerMarker CaptureMarker = new ProfilerMarker("Karawara.QA.CaptureSample");
        public static readonly ProfilerMarker PollMarker = new ProfilerMarker("Karawara.QA.RoutePoll");
        public static readonly ProfilerMarker InputMarker = new ProfilerMarker("Karawara.QA.ReplayInput");
        public static readonly ProfilerMarker GuardMarker = new ProfilerMarker("Karawara.QA.OwnerGuard");
        public static readonly ProfilerMarker LifecycleMarker = new ProfilerMarker("Karawara.QA.ActorLifecycle");
        public static readonly ProfilerMarker ProcessMarker = new ProfilerMarker("Karawara.QA.ProcessUpdate");
        public static readonly ProfilerMarker HistoryMarker = new ProfilerMarker("Karawara.QA.AiHistoryEntry");
        public static readonly ProfilerMarker UiMarker = new ProfilerMarker("Karawara.QA.UiRouteGuard");
        public static readonly ProfilerMarker LogMarker = new ProfilerMarker("Karawara.QA.LogCallback");
        [Serializable] public sealed class Report
        {
            public string path;
            public bool started, completed, restored, allocationCallstacks, ownershipLost;
            public readonly bool timingEligible = false;
            public double requestedStartSeconds, requestedDurationSeconds, actualStartSeconds, actualEndSeconds;
            public int startFrame, endFrame;
            public long bytes;
        }
        public readonly Report Evidence;
        readonly bool priorCpu, priorMemory, priorCallstacks, priorBinary;
        readonly string priorLog;
        bool recording, disposed;
        public static void ValidateWindow(double start, double duration)
        {
            if (double.IsNaN(start) || double.IsInfinity(start) || double.IsNaN(duration) || double.IsInfinity(duration) || start < 3 || duration < 1 || duration > 8 || start + duration > 145)
                throw new ArgumentException("Diagnostic profiler window must be finite, 1-8 seconds, between route seconds3 and145.");
        }
        public PerformanceProfilerAttribution(ProfilerAttributionConfig config, string outputRoot)
        {
            if (Application.isEditor || config == null) throw new ArgumentException("Explicit standalone profiler attribution required.");
            ValidateWindow(config.startSeconds, config.durationSeconds);
            if (Profiler.enabled || Profiler.enableAllocationCallstacks || !string.IsNullOrEmpty(Profiler.logFile))
                throw new InvalidOperationException("Existing profiler session/settings cannot be taken over by QA attribution.");
            priorCpu = Profiler.GetAreaEnabled(ProfilerArea.CPU); priorMemory = Profiler.GetAreaEnabled(ProfilerArea.Memory);
            priorCallstacks = Profiler.enableAllocationCallstacks; priorBinary = Profiler.enableBinaryLog; priorLog = Profiler.logFile;
            string path = Path.Combine(Path.GetFullPath(outputRoot), "attribution-" + Guid.NewGuid().ToString("N") + ".raw");
            using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
            Evidence = new Report { path = path, requestedStartSeconds = config.startSeconds, requestedDurationSeconds = config.durationSeconds, allocationCallstacks = config.allocationCallstacks };
        }
        public void Tick(double elapsed)
        {
            if (disposed || Evidence.completed) return;
            if (recording && !OwnsRecording())
            {
                AbandonChangedOwner();
                throw new InvalidOperationException("Profiler recording owner/settings changed during attribution; partial artifact is invalid.");
            }
            if (!recording && elapsed >= Evidence.requestedStartSeconds)
            {
                // Reject skipped windows rather than pretending the requested phase was recorded.
                if (elapsed >= Evidence.requestedStartSeconds + Evidence.requestedDurationSeconds) throw new InvalidOperationException("Profiler attribution window was skipped.");
                if (Profiler.enabled || Profiler.logFile != priorLog || Profiler.enableAllocationCallstacks != priorCallstacks || Profiler.enableBinaryLog != priorBinary)
                    throw new InvalidOperationException("Profiler ownership changed before the requested attribution window.");
                recording = true;
                Profiler.SetAreaEnabled(ProfilerArea.CPU, true); Profiler.SetAreaEnabled(ProfilerArea.Memory, true);
                Profiler.logFile = Evidence.path; Profiler.enableBinaryLog = true;
                Profiler.enableAllocationCallstacks = Evidence.allocationCallstacks; Profiler.enabled = true;
                Evidence.started = true; Evidence.actualStartSeconds = elapsed; Evidence.startFrame = Time.frameCount;
            }
            if (recording && elapsed >= Evidence.requestedStartSeconds + Evidence.requestedDurationSeconds)
            {
                Evidence.actualEndSeconds = elapsed; Evidence.endFrame = Time.frameCount;
                Restore();
                Evidence.bytes = new FileInfo(Evidence.path).Length;
                Evidence.completed = Evidence.started && Evidence.bytes > 0 && Evidence.endFrame > Evidence.startFrame;
                if (!Evidence.completed) throw new InvalidOperationException("Profiler attribution did not produce a nonempty multi-frame artifact.");
            }
        }
        public static bool SettingsMatch(bool enabled, string path, string ownedPath, bool binary, bool callstacks, bool expectedCallstacks, bool cpu, bool memory)
            => enabled && SamePath(path, ownedPath) && binary && callstacks == expectedCallstacks && cpu && memory;
        static bool SamePath(string actual, string expected)
        {
            if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected)) return false;
            try { return string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected), Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal); }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
        }
        bool OwnsRecording() => SettingsMatch(Profiler.enabled, Profiler.logFile, Evidence.path, Profiler.enableBinaryLog, Profiler.enableAllocationCallstacks, Evidence.allocationCallstacks,
            Profiler.GetAreaEnabled(ProfilerArea.CPU), Profiler.GetAreaEnabled(ProfilerArea.Memory));
        void AbandonChangedOwner()
        {
            Evidence.ownershipLost = true; Evidence.restored = false; Evidence.completed = false;
            // Only close our unique output. A foreign destination/session remains untouched.
            if (SamePath(Profiler.logFile, Evidence.path))
            {
                Profiler.enabled = false; Profiler.logFile = priorLog;
                if (Profiler.enableBinaryLog) Profiler.enableBinaryLog = priorBinary;
                if (Profiler.enableAllocationCallstacks == Evidence.allocationCallstacks) Profiler.enableAllocationCallstacks = priorCallstacks;
                if (Profiler.GetAreaEnabled(ProfilerArea.CPU)) Profiler.SetAreaEnabled(ProfilerArea.CPU, priorCpu);
                if (Profiler.GetAreaEnabled(ProfilerArea.Memory)) Profiler.SetAreaEnabled(ProfilerArea.Memory, priorMemory);
            }
            recording = false;
        }
        void Restore()
        {
            Profiler.enabled = false;
            // Clearing logFile closes the stream so the Editor can load the local artifact.
            Profiler.logFile = ""; Profiler.enableAllocationCallstacks = priorCallstacks;
            Profiler.enableBinaryLog = priorBinary; Profiler.logFile = priorLog;
            Profiler.SetAreaEnabled(ProfilerArea.CPU, priorCpu); Profiler.SetAreaEnabled(ProfilerArea.Memory, priorMemory);
            recording = false;
            Evidence.restored = !Profiler.enabled && Profiler.logFile == priorLog && Profiler.enableBinaryLog == priorBinary && Profiler.enableAllocationCallstacks == priorCallstacks &&
                Profiler.GetAreaEnabled(ProfilerArea.CPU) == priorCpu && Profiler.GetAreaEnabled(ProfilerArea.Memory) == priorMemory;
            if (!Evidence.restored) throw new InvalidOperationException("Owned profiler settings were not restored.");
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (recording) { if (OwnsRecording()) Restore(); else AbandonChangedOwner(); }
        }
    }
#endif
}
