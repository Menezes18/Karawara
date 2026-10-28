using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace RPGKarawara.DevTools.Performance
{
    [Serializable]
    public sealed class CaptureConfig
    {
        public bool qaEnabled;
        // Explicit diagnostic control: no native recorders or per-frame context providers.
        public bool lightControl;
        public double warmupSeconds = 30, captureSeconds = 120;
        public string routeId, runId, outputRoot, commit, buildId, camera;
        public int enemyCount;
        public double renderScale = 1;
        // Exact names selected from the exported Unity descriptor inventory. No guessed counter aliases.
        public string[] counterNames = Array.Empty<string>();
    }
    [Serializable]
    public sealed class CaptureMetric
    {
        public string name, unit, unavailableReason;
        public bool available;
        public double? value;
    }
    [Serializable]
    public sealed class CaptureCounterDescriptor
    {
        public string name, category, unit, flags;
    }
    [Serializable]
    public sealed class CaptureFrame
    {
        public double timestampSeconds, frameTimeMs;
        public bool focused;
        public List<CaptureMetric> metrics = new List<CaptureMetric>();
    }
    [Serializable]
    public sealed class CaptureResult
    {
        public int schemaVersion = 1;
        public string runId, utc, endReason, jsonPath, csvPath, inventoryPath;
        public CaptureConfig conditions;
        public string cpu, gpu, operatingSystem, unityVersion, quality, buildMode, graphicsApi;
        public int memoryMB, gpuMemoryMB, width, height, vsync, targetFrameRate;
        public long captureBufferBytesEstimate;
        public double startedTimestampSeconds, elapsedSeconds;
        public PerformanceExternalClock externalClock;
        public PerformanceUpdateClock.Report updateClock;
        public PerformanceGcCollectionReport gcCollections;
        public double capturedCoverageSeconds, terminalOverrunSeconds;
        public double? firstCapturedStartElapsedSeconds, lastCapturedEndElapsedSeconds, excludedWarmupBoundaryStartSeconds, excludedWarmupBoundaryEndSeconds;
        public double excludedWarmupBoundaryOverlapSeconds;
        public bool fullWindowCompleted, conditionsChanged, populationVerified, cameraVerified, comparisonValid;
        public double? medianFrameTimeMs, p95FrameTimeMs;
        public List<CaptureFrame> frames = new List<CaptureFrame>();
        public List<CaptureMetric> requestedMetrics = new List<CaptureMetric>();
        public List<CaptureCounterDescriptor> counterInventory = new List<CaptureCounterDescriptor>();
        public List<string> errors = new List<string>();
        public List<string> validationUnavailableReasons = new List<string>();
    }
    public static class CaptureContract
    {
        // A normal warm-up boundary frame remains excluded; larger missing coverage fails closed.
        public const double MaximumExcludedWarmupOverlapSeconds = 0.1;
        public static bool IncludesFrame(double startElapsed, double endElapsed)
        {
            return startElapsed >= 30 && startElapsed < 150 && endElapsed > startElapsed;
        }
        public static double Percentile(double[] values, double fraction)
        {
            if (values == null || values.Length == 0 || fraction <= 0 || fraction > 1)
                throw new ArgumentException("A non-empty finite series and fraction in (0,1] are required.");
            if (values.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v < 0))
                throw new ArgumentException("Frame times must be finite and non-negative.");
            var sorted = values.OrderBy(v => v).ToArray();
            return sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * fraction) - 1)];
        }
        public static string NewRunId() => Guid.NewGuid().ToString("N");
        public static string MetricJson(string name, string unit, bool available, double value)
            => JsonConvert.SerializeObject(new CaptureMetric { name = name, unit = unit, available = available, value = available ? (double?)value : null });
        public static string CsvCell(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}



