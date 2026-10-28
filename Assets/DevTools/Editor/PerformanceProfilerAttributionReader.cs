using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.Profiling;

namespace RPGKarawara.DevTools.Editor
{
    // Offline inspection of a local Player profiler log. Never runs during timing.
    public static class PerformanceProfilerAttributionReader
    {
        sealed class Allocation
        {
            public string thread, stack;
            public long samples, knownBytes, unknownByteSamples;
            public long? bytes => unknownByteSamples == 0 ? (long?)knownBytes : null;
            public bool hasCallstack, hasResolvedMethod;
        }
        sealed class Frame
        {
            public int index;
            public double frameMs, markedToolingMs;
        }
        public static string Inspect(string rawPath, string outputPath)
        {
            if (Profiler.enabled) throw new InvalidOperationException("Stop the Editor profiler before offline import.");
            rawPath = Path.GetFullPath(rawPath); outputPath = Path.GetFullPath(outputPath);
            if (!File.Exists(rawPath) || new FileInfo(rawPath).Length == 0 || File.Exists(outputPath)) throw new ArgumentException("Existing nonempty raw profile and exclusive output required.");
            string backup = null;
            if (ProfilerDriver.firstFrameIndex >= 0)
            {
                backup = Path.Combine(Path.GetDirectoryName(outputPath), "prior-editor-profile-" + Guid.NewGuid().ToString("N") + ".raw");
                if (!ProfilerDriver.SaveProfile(backup)) throw new InvalidOperationException("Cannot preserve existing Editor profiler data.");
            }
            try
            {
                if (!ProfilerDriver.LoadProfile(rawPath, false)) throw new InvalidOperationException("Unity rejected local profiler log.");
                var frames = new List<Frame>();
                var markers = new Dictionary<string, List<double>>(StringComparer.Ordinal);
                var allocations = new Dictionary<string, Allocation>(StringComparer.Ordinal);
                var addresses = new List<ulong>();
                int loadedFrames = 0, mainFrames = 0; long gcSamples = 0, knownGcBytes = 0, unknownByteSamples = 0, stackSamples = 0, resolvedSamples = 0;
                int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
                if (first < 0 || last < first) throw new InvalidOperationException("Imported profile has no frames.");
                for (int frame = first; frame <= last; frame = ProfilerDriver.GetNextFrameIndex(frame))
                {
                    loadedFrames++;
                    for (int thread = 0; thread < 256; thread++)
                    using (var view = ProfilerDriver.GetRawFrameDataView(frame, thread))
                    {
                        if (!view.valid) break;
                        bool main = view.threadName == "Main Thread";
                        var row = main ? new Frame { index = frame, frameMs = view.frameTimeMs } : null;
                        if (main) mainFrames++;
                        int coveredThrough = -1;
                        for (int sample = 0; sample < view.sampleCount; sample++)
                        {
                            string name = view.GetSampleName(sample);
                            if (main && name.StartsWith("Karawara.QA.", StringComparison.Ordinal))
                            {
                                double ms = view.GetSampleTimeMs(sample);
                                if (!markers.TryGetValue(name, out var values)) markers.Add(name, values = new List<double>());
                                values.Add(ms);
                                // Raw samples are depth-first; only the outer owned region contributes
                                // to the combined value, so nested OwnerGuard is not counted twice.
                                if (sample > coveredThrough)
                                {
                                    row.markedToolingMs += ms;
                                    coveredThrough = sample + view.GetSampleChildrenCountRecursive(sample);
                                }
                            }
                            if (name != "GC.Alloc") continue;
                            int metadata = view.GetSampleMetadataCount(sample);
                            long? bytes = ReadAllocationBytes(metadata, metadata > 0 ? view.GetSampleMetadataAsLong(sample, 0) : 0);
                            addresses.Clear(); view.GetSampleCallstack(sample, addresses);
                            bool resolved = false;
                            var methods = new List<string>();
                            foreach (ulong address in addresses)
                            {
                                var method = view.ResolveMethodInfo(address);
                                bool known = !string.IsNullOrWhiteSpace(method.methodName);
                                resolved |= known;
                                methods.Add(known ? method.methodName + " @ " + method.sourceFileName + ":" + method.sourceFileLine : "0x" + address.ToString("X"));
                            }
                            string stack = methods.Count > 0 ? string.Join("\n", methods) : "<callstack unavailable>";
                            string key = view.threadName + "\n" + stack;
                            if (!allocations.TryGetValue(key, out var allocation)) allocations.Add(key, allocation = new Allocation { thread = view.threadName, stack = stack, hasCallstack = addresses.Count > 0, hasResolvedMethod = resolved });
                            allocation.samples++; gcSamples++;
                            if (bytes.HasValue) { allocation.knownBytes += bytes.Value; knownGcBytes += bytes.Value; }
                            else { allocation.unknownByteSamples++; unknownByteSamples++; }
                            if (addresses.Count > 0) stackSamples++;
                            if (resolved) resolvedSamples++;
                        }
                        if (main) frames.Add(row);
                    }
                    if (frame == last) break;
                    int next = ProfilerDriver.GetNextFrameIndex(frame);
                    if (next <= frame) throw new InvalidOperationException("Imported frame chronology is invalid.");
                }
                string hash; using (var sha = SHA256.Create()) using (var stream = File.OpenRead(rawPath)) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                var markerStats = markers.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new { name = p.Key, samples = p.Value.Count, totalMs = p.Value.Sum(), medianMs = Percentile(p.Value, .5), p95Ms = Percentile(p.Value, .95), maximumMs = p.Value.Max() }).ToArray();
                var report = new { utc = DateTime.UtcNow.ToString("O"), rawPath, rawSha256 = hash, rawBytes = new FileInfo(rawPath).Length,
                    firstLoadedFrame = first, lastLoadedFrame = last, loadedFrames, mainFrames, frames, markerStats, gcSamples, gcBytes = unknownByteSamples == 0 ? (long?)knownGcBytes : null, knownGcBytes, unknownByteSamples, stackSamples, resolvedSamples,
                    allocationStacks = allocations.Values.OrderByDescending(a => a.knownBytes).ToArray(),
                    g1bAccepted = false, timingEligible = false,
                    boundary = "Separate attribution with profiler/callstack overhead. Marked inclusive regions exclude nested double-counting, but do not prove total harness cost or timing A/B equivalence. Loaded frame history may be smaller than the archived raw file. Bytes are GC.Alloc metadata; missing callstacks stay unavailable." };
                using (var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented));
                return outputPath;
            }
            finally
            {
                if (backup != null)
                {
                    if (!ProfilerDriver.LoadProfile(backup, false)) throw new InvalidOperationException("Prior profiler data could not be restored; backup preserved: " + backup);
                }
                else ProfilerDriver.ClearAllFrames();
            }
        }
        public static long? ReadAllocationBytes(int metadataCount, long rawValue)
        {
            if (metadataCount < 0 || (metadataCount > 0 && rawValue < 0)) throw new ArgumentException("Invalid GC allocation metadata; byte attribution unsupported.");
            return metadataCount > 0 ? (long?)rawValue : null;
        }
        static double Percentile(List<double> values, double fraction)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            return sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * fraction) - 1)];
        }
    }
}
