using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Separate visual diagnostic. Never certifies the timing sampled by its route.
    public sealed class PerformanceCheckpointImages
    {
        static readonly double[] Times = { 20, 45, 75, 105, 135, 150 };
        [Serializable] public sealed class Request
        {
            public string path, scene;
            public int requestedFrame;
            public double expectedElapsed, requestedElapsed, requestedRealtime;
            public bool focused;
            public float[] playerPosition, cameraPosition, cameraRotation;
        }
        [Serializable] public sealed class Report
        {
            public readonly List<Request> requests = new List<Request>(6);
            public bool completed;
            public double filesObservedReadyRealtime;
            public readonly bool timingEligible = false;
            public readonly string boundary = "Internal Unity ScreenCapture requests at route checkpoints. Request frame/time and numeric poses only; no exact present frame, deterministic pixels or continuous visual parity. PNG headers checked at runtime; decoding/appearance inspection is separate. Never baseline timing.";
        }
        public readonly Report Evidence = new Report();
        readonly string outputRoot, identity = Guid.NewGuid().ToString("N");
        bool failed;
        public bool AwaitingFiles => !failed && !Evidence.completed && Evidence.requests.Count == Times.Length &&
            Time.realtimeSinceStartupAsDouble <= Evidence.requests[Times.Length - 1].requestedRealtime + 5;

        public static bool ValidateRequest(string mode, string arm, JToken flag, bool profilerRequested)
        {
            if (flag == null) return false;
            if (flag.Type != JTokenType.Boolean) throw new ArgumentException("Checkpoint image flag must be a typed boolean.");
            if (!(bool)flag) return false;
            if (mode != "tutorial-certified-route" && mode != "tutorial-ui-certified-route" || arm != null && arm != "full" || profilerRequested)
                throw new ArgumentException("Checkpoint images require a separate full Tutorial exploration/UI visual diagnostic, without LIGHT or profiler recording.");
            return true;
        }
        public static int DueIndex(int next, double elapsed)
        {
            if (next < 0 || next > Times.Length || double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0)
                throw new ArgumentException("Invalid image checkpoint state/time.");
            if (next == Times.Length || elapsed < Times[next]) return -1;
            if (elapsed > Times[next] + .25) throw new ArgumentException("Image checkpoint was skipped: " + Times[next]);
            return next;
        }
        public PerformanceCheckpointImages(string root)
        {
            if (Application.isEditor || !Path.IsPathFullyQualified(root) || !Directory.Exists(root))
                throw new ArgumentException("Actual standalone image diagnostic output required.");
            outputRoot = Path.GetFullPath(root);
        }
        public void Tick(double elapsed, Transform player, Transform camera)
        {
            int next = DueIndex(Evidence.requests.Count, elapsed);
            if (next < 0) return;
            if (player == null || camera == null || !Application.isFocused || Screen.width != 1920 || Screen.height != 1080)
                throw new InvalidOperationException("Image checkpoint owners/focus/resolution changed.");
            string path = Path.Combine(outputRoot, "checkpoint-image-" + identity + "-" + ((int)Times[next]).ToString("D3") + ".png");
            if (File.Exists(path)) throw new IOException("Refuse replacement of checkpoint image.");
            ScreenCapture.CaptureScreenshot(path);
            Evidence.requests.Add(new Request { path = path, scene = SceneManager.GetActiveScene().name,
                requestedFrame = Time.frameCount, expectedElapsed = Times[next], requestedElapsed = elapsed,
                requestedRealtime = Time.realtimeSinceStartupAsDouble, focused = Application.isFocused,
                playerPosition = new[] { player.position.x, player.position.y, player.position.z },
                cameraPosition = new[] { camera.position.x, camera.position.y, camera.position.z },
                cameraRotation = new[] { camera.rotation.x, camera.rotation.y, camera.rotation.z, camera.rotation.w } });
        }
        public void CompletePending()
        {
            if (Evidence.completed || failed) return;
            if (Evidence.requests.Count != Times.Length) { failed = true; throw new InvalidOperationException("Image requests are incomplete."); }
            bool headersReady = Evidence.requests.All(r => ValidPngHeader(r.path));
            double observedAt = Time.realtimeSinceStartupAsDouble;
            if (FilesReadyWithinDeadline(headersReady, Evidence.requests[Times.Length - 1].requestedRealtime, observedAt))
            {
                Evidence.completed = true; Evidence.filesObservedReadyRealtime = observedAt; return;
            }
            if (!AwaitingFiles) { failed = true; throw new InvalidOperationException("Checkpoint image files are incomplete or invalid."); }
        }
        public static bool FilesReadyWithinDeadline(bool headersReady, double requestedAt, double observedAt)
        {
            if (double.IsNaN(requestedAt) || double.IsInfinity(requestedAt) || double.IsNaN(observedAt) || double.IsInfinity(observedAt) ||
                requestedAt < 0 || observedAt < requestedAt || observedAt > requestedAt + 5)
                throw new ArgumentException("Image file observation is outside its five-second deadline.");
            return headersReady;
        }
        static bool ValidPngHeader(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var b = new byte[24]; if (stream.Length <= 24 || stream.Read(b, 0, b.Length) != b.Length) return false;
                    if (!b.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return false;
                    int width = b[16] << 24 | b[17] << 16 | b[18] << 8 | b[19];
                    int height = b[20] << 24 | b[21] << 16 | b[22] << 8 | b[23];
                    return width == 1920 && height == 1080;
                }
            }
            catch (IOException) { return false; }
        }
    }
#endif
}
