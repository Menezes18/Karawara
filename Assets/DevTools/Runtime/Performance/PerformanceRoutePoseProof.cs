using System;
using System.Linq;
using UnityEngine;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    // Numeric expectations and observations only. Never retains the Player/camera.
    public sealed class PerformanceRoutePoseProof
    {
        struct Expected
        {
            public double elapsed;
            public Vector3 position;
            public Quaternion rotation;
            public float positionTolerance, cameraTolerance;
        }
        [Serializable] public sealed class Observation
        {
            public bool observed;
            public int frame;
            public double expectedElapsed, actualElapsed;
            public float positionError, cameraAngleError;
            public float[] position = new float[3], cameraRotation = new float[4];
        }
        [Serializable] public sealed class Report
        {
            public bool completed, closurePassed, faulted;
            public int observedCount;
            public string error;
            public float? closureDistance, closureCameraAngle;
            public Observation[] observations;
            public readonly string boundary = "Observed frozen poses and actual30-to150s closure only; no population, rendering, timing or G1B acceptance.";
        }
        readonly Expected[] expected;
        readonly double maxLateness;
        readonly Report report;
        double lastElapsed = -1;
        int lastFrame = -1, index;
        bool anchorObserved, failed, completed;
        Vector3 anchorPosition;
        Quaternion anchorRotation;
        public Report Evidence => report;
        public bool IsComplete => completed && !failed && index == expected.Length && report.closurePassed;

        PerformanceRoutePoseProof(RehearsalPoseCheckpoint[] checkpoints, double lateness)
        {
            if (!Finite(lateness) || lateness <= 0 || lateness > .25 || checkpoints == null || checkpoints.Length < 6)
                throw new ArgumentException("Frozen checkpoints and at most0.25s lateness required.");
            PerformanceProcessHarness.BuildRehearsalCheckpoints(new TutorialRehearsalConfig { durationSeconds = 150, checkpoints = checkpoints });
            foreach (double at in new double[] { 0, 30, 60, 90, 120, 150 })
                if (!checkpoints.Any(c => c.elapsed == at)) throw new ArgumentException("Mandatory frozen route phase missing: " + at);
            if (checkpoints.Any(c => c.positionTolerance > 2 || c.cameraAngleTolerance > 10))
                throw new ArgumentException("Frozen pose bounds cannot exceed2m/10degrees.");
            maxLateness = lateness; expected = new Expected[checkpoints.Length];
            report = new Report { observations = new Observation[checkpoints.Length] };
            for (int i = 0; i < checkpoints.Length; i++)
            {
                var c = checkpoints[i];
                expected[i] = new Expected { elapsed = c.elapsed, position = new Vector3(c.position[0], c.position[1], c.position[2]),
                    rotation = new Quaternion(c.cameraRotation[0], c.cameraRotation[1], c.cameraRotation[2], c.cameraRotation[3]),
                    positionTolerance = c.positionTolerance, cameraTolerance = c.cameraAngleTolerance };
                report.observations[i] = new Observation { expectedElapsed = c.elapsed };
            }
        }
        public static PerformanceRoutePoseProof Create(RehearsalPoseCheckpoint[] checkpoints, double maxLatenessSeconds)
            => new PerformanceRoutePoseProof(checkpoints, maxLatenessSeconds);

        public void Observe(double elapsed, Vector3 position, Quaternion rotation, int frame)
        {
            if (failed || completed) throw new InvalidOperationException("Route pose proof has already ended or faulted.");
            double norm = (double)rotation.x * rotation.x + (double)rotation.y * rotation.y + (double)rotation.z * rotation.z + (double)rotation.w * rotation.w;
            Check(Finite(elapsed) && elapsed >= 0 && elapsed >= lastElapsed && frame >= 0 && frame >= lastFrame &&
                Finite(position.x) && Finite(position.y) && Finite(position.z) && Finite(norm) && Math.Abs(norm - 1) <= .01,
                "Finite monotonic actual pose/frame and normalized camera required.");
            lastElapsed = elapsed; lastFrame = frame;
            while (index < expected.Length && elapsed >= expected[index].elapsed)
            {
                var target = expected[index]; var row = report.observations[index];
                row.observed = true; row.frame = frame; row.actualElapsed = elapsed;
                row.position[0] = position.x; row.position[1] = position.y; row.position[2] = position.z;
                row.cameraRotation[0] = rotation.x; row.cameraRotation[1] = rotation.y; row.cameraRotation[2] = rotation.z; row.cameraRotation[3] = rotation.w;
                row.positionError = Vector3.Distance(position, target.position); row.cameraAngleError = Quaternion.Angle(rotation, target.rotation);
                Check(elapsed - target.elapsed <= maxLateness && row.positionError <= target.positionTolerance && row.cameraAngleError <= target.cameraTolerance,
                    "Frozen route checkpoint diverged or arrived late.");
                if (target.elapsed == 30) { anchorObserved = true; anchorPosition = position; anchorRotation = rotation; }
                if (target.elapsed == 150)
                {
                    Check(anchorObserved, "Actual measured-window anchor missing.");
                    report.closureDistance = Vector3.Distance(position, anchorPosition); report.closureCameraAngle = Quaternion.Angle(rotation, anchorRotation);
                    report.closurePassed = report.closureDistance <= 2 && report.closureCameraAngle <= 10;
                    Check(report.closurePassed, "Actual measured-window closure diverged.");
                }
                index++; report.observedCount = index;
            }
        }
        public void Complete()
        {
            Check(!failed && !completed && index == expected.Length && report.closurePassed, "Incomplete/faulted route poses cannot complete.");
            completed = true; report.completed = true;
        }
        void Check(bool condition, string error)
        {
            if (condition) return;
            failed = true; report.faulted = true; report.error = error;
            throw new InvalidOperationException(error);
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
#endif
}
