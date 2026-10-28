using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPGKarawara.DevTools.Performance
{
    // Never auto-attached or auto-started. Operational code is QA/development only.
    public sealed class PerformanceCaptureController : MonoBehaviour
    {
        public CaptureResult LastResult { get; private set; }
        public bool IsCapturing { get; private set; }
        public int OwnedRecorderCount => recorders.Count;
        public Func<int> EnemyCountProvider { get; set; }
        public Func<string> CameraIdProvider { get; set; }
        // Runs after sampling stops, before validity calculation and exclusive result export.
        public Action<CaptureResult> FinalValidation { get; set; }
        sealed class Counter
        {
            public string name, unit;
            public ProfilerRecorder recorder;
        }
        readonly List<Counter> recorders = new List<Counter>();
        readonly List<CaptureMetric> missing = new List<CaptureMetric>();
        CaptureResult result;
        internal double StartedTimestampSeconds => result?.startedTimestampSeconds ?? double.NaN;
        double previous;
        const int MaxFrames = 120000;
        double[] timestamps, frameTimes;
        bool[] focused;
        double?[,] counterValues;
        int sampledFrames, initialQualityIndex;
        PerformanceUpdateClock updateClock;
        bool lightControl;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        PerformanceGcCollectionCounter gcCollectionCounter;
#endif

        public bool Begin(CaptureConfig config)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (IsCapturing || config == null || !config.qaEnabled || config.warmupSeconds != 30 || config.captureSeconds != 120 ||
                string.IsNullOrWhiteSpace(config.outputRoot) || string.IsNullOrWhiteSpace(config.routeId) ||
                string.IsNullOrWhiteSpace(config.commit) || string.IsNullOrWhiteSpace(config.buildId) ||
                string.IsNullOrWhiteSpace(config.camera) || config.enemyCount < 0 ||
                (config.lightControl && config.counterNames != null && config.counterNames.Length != 0)) return false;
            try
            {
                var copy = JsonConvert.DeserializeObject<CaptureConfig>(JsonConvert.SerializeObject(config));
                lightControl = copy.lightControl;
                copy.runId = SafePrefix(copy.runId) + CaptureContract.NewRunId();
                copy.outputRoot = Path.GetFullPath(copy.outputRoot);
                Directory.CreateDirectory(copy.outputRoot);
                // Claim the run exclusively before allocating native handles; never reuse an existing run.
                using (var claim = new FileStream(Path.Combine(copy.outputRoot, copy.runId + ".run"), FileMode.CreateNew, FileAccess.Write)) { }
                result = new CaptureResult
                {
                    runId = copy.runId, conditions = copy, utc = DateTime.UtcNow.ToString("O"),
                    cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                    operatingSystem = SystemInfo.operatingSystem, unityVersion = Application.unityVersion,
                    graphicsApi = SystemInfo.graphicsDeviceType.ToString(), memoryMB = SystemInfo.systemMemorySize,
                    gpuMemoryMB = SystemInfo.graphicsMemorySize, width = Screen.width, height = Screen.height,
                    vsync = QualitySettings.vSyncCount, targetFrameRate = Application.targetFrameRate,
                    quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                    buildMode = Application.isEditor ? "Editor (fixture/complementary evidence)" : Debug.isDebugBuild ? "Development" : "QA Release",
                    startedTimestampSeconds = Time.realtimeSinceStartupAsDouble
                };
                try
                {
                    gcCollectionCounter = new PerformanceGcCollectionCounter();
                    result.gcCollections = gcCollectionCounter.Report;
                }
                catch (Exception error)
                {
                    gcCollectionCounter = null;
                    result.gcCollections = new PerformanceGcCollectionReport { unavailableReason = error.GetType().Name + ": " + error.Message };
                }
                copy.renderScale = ActualRenderScale();
                result.populationVerified = !lightControl && EnemyCountProvider != null;
                result.cameraVerified = !lightControl && CameraIdProvider != null;
                if (lightControl) result.validationUnavailableReasons.Add("Explicit light control: diagnostic Update pacing only; native counters and continuous context providers disabled.");
                result.jsonPath = Path.Combine(copy.outputRoot, copy.runId + ".json");
                result.csvPath = Path.Combine(copy.outputRoot, copy.runId + ".csv");
                result.inventoryPath = Path.Combine(copy.outputRoot, copy.runId + ".counters.json");
                if (!lightControl) Discover(copy.counterNames ?? Array.Empty<string>());
                else missing.Clear();
                result.requestedMetrics.AddRange(missing);
                foreach (var counter in recorders)
                    result.requestedMetrics.Add(new CaptureMetric { name = counter.name, unit = counter.unit, available = false, value = null, unavailableReason = "No captured frame sample." });
                initialQualityIndex = QualitySettings.GetQualityLevel();
                timestamps = new double[MaxFrames];
                frameTimes = new double[MaxFrames];
                focused = new bool[MaxFrames];
                counterValues = new double?[MaxFrames, recorders.Count];
                sampledFrames = 0;
                result.captureBufferBytesEstimate = MaxFrames * (25L + 16L * recorders.Count);
                result.externalClock = PerformanceExternalClock.Begin(() => Time.realtimeSinceStartupAsDouble);
                updateClock = PerformanceUpdateClock.Create(MaxFrames, result.externalClock);
                result.startedTimestampSeconds = result.externalClock.started.unitySeconds;
                previous = result.startedTimestampSeconds;
                IsCapturing = true;
                return true;
            }
            catch (Exception error)
            {
                DisposeRecorders();
                timestamps = null; frameTimes = null; focused = null; counterValues = null; updateClock = null;
                Debug.LogError("QA performance capture Begin failed: " + error.Message);
                return false;
            }
#else
            return false;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void Discover(string[] requested)
        {
            missing.Clear();
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var descriptors = new Dictionary<string, ProfilerRecorderHandle>(StringComparer.Ordinal);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                result.counterInventory.Add(new CaptureCounterDescriptor {
                    name = description.Name, category = description.Category.ToString(),
                    unit = description.UnitType.ToString(), flags = description.Flags.ToString()
                });
                // Duplicate names across categories are ambiguous; never guess which counter was requested.
                if (!descriptors.ContainsKey(description.Name)) descriptors.Add(description.Name, handle);
            }
            foreach (var name in requested.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(name) || !descriptors.TryGetValue(name, out var handle) ||
                    result.counterInventory.Count(d => d.name == name) != 1)
                {
                    missing.Add(new CaptureMetric { name = name, available = false, value = null, unavailableReason = "Not enumerated uniquely on this platform/build." });
                    continue;
                }
                var desc = ProfilerRecorderHandle.GetDescription(handle);
                ProfilerRecorder recorder;
                try { recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately); }
                catch (Exception error)
                {
                    missing.Add(new CaptureMetric { name = name, unit = desc.UnitType.ToString(), available = false, value = null, unavailableReason = "Recorder unavailable: " + error.Message });
                    continue;
                }
                if (!recorder.Valid)
                {
                    recorder.Dispose();
                    missing.Add(new CaptureMetric { name = name, unit = desc.UnitType.ToString(), available = false, value = null, unavailableReason = "Unity recorder invalid." });
                    continue;
                }
                recorders.Add(new Counter { name = name, unit = desc.UnitType.ToString(), recorder = recorder });
            }
        }
        void Update()
        {
            SampleAt(Time.realtimeSinceStartupAsDouble);
        }
        // Same sampling path for Update and deterministic Editor regression timestamps.
        void SampleAt(double now)
        {
            using (PerformanceProfilerAttribution.CaptureMarker.Auto()) SampleAtCore(now);
        }
        void SampleAtCore(double now)
        {
            if (!IsCapturing) return;
            double elapsed = now - result.startedTimestampSeconds;
            double startElapsed = previous - result.startedTimestampSeconds;
            try
            {
                if (double.IsNaN(now) || double.IsInfinity(now) || now <= previous)
                    throw new InvalidOperationException("Sampling clock must advance monotonically.");
                updateClock.Record();
                gcCollectionCounter?.Observe(elapsed);
                if (startElapsed < 30 && elapsed > 30)
                {
                    result.excludedWarmupBoundaryStartSeconds = startElapsed;
                    result.excludedWarmupBoundaryEndSeconds = elapsed;
                    result.excludedWarmupBoundaryOverlapSeconds = Math.Min(elapsed, 150) - 30;
                }
                if (CaptureContract.IncludesFrame(startElapsed, elapsed))
                {
                    if (sampledFrames >= MaxFrames)
                    {
                        result.errors.Add("Fixed QA sampling buffer exhausted; capture invalid.");
                        result.conditionsChanged = true;
                        Finish("buffer-exhausted", now);
                        return;
                    }
                    timestamps[sampledFrames] = now;
                    frameTimes[sampledFrames] = (now - previous) * 1000;
                    focused[sampledFrames] = Application.isFocused;
                    for (int index = 0; index < recorders.Count; index++)
                    {
                        var recorder = recorders[index].recorder;
                        bool available = recorder.Valid && recorder.Count > 0;
                        counterValues[sampledFrames, index] = available ? (double?)(recorder.DataType == ProfilerMarkerDataType.Double || recorder.DataType == ProfilerMarkerDataType.Float
                            ? recorder.LastValueAsDouble : recorder.LastValue) : null;
                    }
                    sampledFrames++;
                    if (!result.firstCapturedStartElapsedSeconds.HasValue) result.firstCapturedStartElapsedSeconds = startElapsed;
                    result.lastCapturedEndElapsedSeconds = elapsed;
                    result.capturedCoverageSeconds += Math.Min(elapsed, 150) - startElapsed;
                    result.terminalOverrunSeconds = Math.Max(0, elapsed - 150);
                }
                if (lightControl) result.conditionsChanged |= !Application.isFocused;
                else
                {
                    result.conditionsChanged |= Screen.width != result.width || Screen.height != result.height ||
                        QualitySettings.vSyncCount != result.vsync || Application.targetFrameRate != result.targetFrameRate ||
                        QualitySettings.GetQualityLevel() != initialQualityIndex ||
                        Math.Abs(ActualRenderScale() - result.conditions.renderScale) > 0.0001 || !Application.isFocused;
                    result.populationVerified &= EnemyCountProvider != null;
                    result.cameraVerified &= CameraIdProvider != null;
                    if (EnemyCountProvider != null) result.conditionsChanged |= EnemyCountProvider() != result.conditions.enemyCount;
                    if (CameraIdProvider != null) result.conditionsChanged |= CameraIdProvider() != result.conditions.camera;
                }
                previous = now;
                if (elapsed >= 150) Finish("window-completed", now);
            }
            catch (Exception error)
            {
                result.errors.Add("Sampling/provider failure: " + error.GetType().Name + ": " + error.Message);
                result.conditionsChanged = true;
                Finish("sampling-failed", now);
            }
        }
#endif
        public CaptureResult End() => Finish("manual");
        CaptureResult Finish(string reason, double? sampledTimestamp = null)
        {
            if (!IsCapturing) return LastResult;
            IsCapturing = false;
            double endedTimestamp = sampledTimestamp ?? Time.realtimeSinceStartupAsDouble;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            gcCollectionCounter?.Finish(endedTimestamp - result.startedTimestampSeconds);
#endif
            try
            {
                result.externalClock?.End(() => Time.realtimeSinceStartupAsDouble);
                result.updateClock = updateClock?.Snapshot();
                FinalValidation?.Invoke(result);
                result.elapsedSeconds = endedTimestamp - result.startedTimestampSeconds;
                result.fullWindowCompleted = result.elapsedSeconds >= 150 && sampledFrames > 0 && result.errors.Count == 0 &&
                    result.firstCapturedStartElapsedSeconds.HasValue &&
                    result.lastCapturedEndElapsedSeconds >= 150 &&
                    result.firstCapturedStartElapsedSeconds.Value <= 30 + CaptureContract.MaximumExcludedWarmupOverlapSeconds &&
                    result.excludedWarmupBoundaryOverlapSeconds <= CaptureContract.MaximumExcludedWarmupOverlapSeconds &&
                    result.capturedCoverageSeconds >= 120 - CaptureContract.MaximumExcludedWarmupOverlapSeconds;
                result.endReason = result.fullWindowCompleted ? "window-completed" : reason == "window-completed" ? "window-incomplete" : reason;
                result.comparisonValid = result.fullWindowCompleted && !result.conditionsChanged && result.populationVerified && result.cameraVerified && !Application.isEditor;
                // DTO construction/serialization happens only after the measured interval has ended.
                for (int frameIndex = 0; frameIndex < sampledFrames; frameIndex++)
                {
                    var frame = new CaptureFrame { timestampSeconds = timestamps[frameIndex], frameTimeMs = frameTimes[frameIndex], focused = focused[frameIndex] };
                    for (int metricIndex = 0; metricIndex < recorders.Count; metricIndex++)
                    {
                        var counter = recorders[metricIndex];
                        double? value = counterValues[frameIndex, metricIndex];
                        frame.metrics.Add(new CaptureMetric { name = counter.name, unit = counter.unit, available = value.HasValue,
                            value = value, unavailableReason = value.HasValue ? null : "No completed recorder sample." });
                    }
                    frame.metrics.AddRange(missing);
                    result.frames.Add(frame);
                }
                if (result.frames.Count > 0)
                {
                    result.requestedMetrics = result.frames[result.frames.Count - 1].metrics;
                }
                if (result.frames.Count > 0)
                {
                    var times = result.frames.Select(f => f.frameTimeMs).ToArray();
                    result.medianFrameTimeMs = CaptureContract.Percentile(times, 0.5);
                    result.p95FrameTimeMs = CaptureContract.Percentile(times, 0.95);
                }
            }
            catch (Exception error)
            {
                result.fullWindowCompleted = false;
                result.comparisonValid = false;
                result.errors.Add("Result finalization failure: " + error.GetType().Name + ": " + error.Message);
            }
            finally
            {
                if (lightControl)
                {
                    result.populationVerified = false;
                    result.cameraVerified = false;
                    result.comparisonValid = false;
                    if (result.conditions != null) result.conditions.lightControl = true;
                }
                DisposeRecorders();
            }
            try
            {
                WriteNew(result.inventoryPath, JsonConvert.SerializeObject(result.counterInventory, Formatting.Indented));
                var csv = new StringBuilder("timestampSeconds,frameTimeMs,focused,metric,unit,available,value\n");
                foreach (var frame in result.frames)
                {
                    var metrics = frame.metrics.Count > 0 ? frame.metrics : new List<CaptureMetric> { new CaptureMetric { name = "none-requested" } };
                    foreach (var metric in metrics)
                        csv.Append(CaptureContract.Number(frame.timestampSeconds)).Append(',').Append(CaptureContract.Number(frame.frameTimeMs)).Append(',')
                            .Append(frame.focused ? "true" : "false").Append(',').Append(CaptureContract.CsvCell(metric.name)).Append(',')
                            .Append(CaptureContract.CsvCell(metric.unit)).Append(',').Append(metric.available ? "true" : "false").Append(',')
                            .Append(metric.value.HasValue ? CaptureContract.Number(metric.value.Value) : "").Append('\n');
                }
                WriteNew(result.csvPath, csv.ToString());
                WriteNew(result.jsonPath, JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            catch (Exception error)
            {
                result.comparisonValid = false;
                result.errors.Add("Output failure: " + error.Message);
                Debug.LogError("QA performance capture output failed: " + error.Message);
            }
            timestamps = null; frameTimes = null; focused = null; counterValues = null; updateClock = null;
            LastResult = result;
            return LastResult;
        }
        void DisposeRecorders()
        {
            foreach (var counter in recorders)
            {
                try { counter.recorder.Dispose(); }
                catch (Exception error)
                {
                    if (result != null) { result.errors.Add("Recorder disposal failure: " + error.Message); result.comparisonValid = false; }
                }
            }
            recorders.Clear();
            missing.Clear();
        }
        void OnDisable() { Finish("component-disabled"); DisposeRecorders(); }
        void OnDestroy() { Finish("component-destroyed"); DisposeRecorders(); }
        static double ActualRenderScale() => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp ? urp.renderScale : 1;
        static string SafePrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return "";
            return new string(prefix.Take(48).Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray()) + "-";
        }
        static void WriteNew(string path, string text)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text);
        }
    }
}






