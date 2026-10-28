using System;

namespace RPGKarawara.DevTools.Performance
{
    [Serializable]
    public sealed class PerformanceGcCollectionReport
    {
        public bool available;
        public int maximumGeneration;
        public double? startElapsedSeconds, endElapsedSeconds;
        public int[] before, after, deltas;
        public string unavailableReason;
        public string source = "System.GC.CollectionCount(generation)";
        public string boundary = "Endpoint deltas per runtime generation, excluding warmup and post-window export. Generation counts overlap and are never summed. No timing or global retention acceptance.";
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    public sealed class PerformanceGcCollectionCounter
    {
        readonly Func<int, int> read;
        readonly int[] before, after;
        bool started, finished, failed;
        public PerformanceGcCollectionReport Report { get; }

        public PerformanceGcCollectionCounter() : this(GC.MaxGeneration, GC.CollectionCount) { }

        public PerformanceGcCollectionCounter(int maximumGeneration, Func<int, int> readCount)
        {
            if (maximumGeneration < 0 || maximumGeneration > 32) throw new ArgumentOutOfRangeException(nameof(maximumGeneration));
            read = readCount ?? throw new ArgumentNullException(nameof(readCount));
            before = new int[maximumGeneration + 1]; after = new int[maximumGeneration + 1];
            Report = new PerformanceGcCollectionReport { maximumGeneration = maximumGeneration };
        }

        public void Observe(double elapsedSeconds)
        {
            if (finished || failed || started || elapsedSeconds < 30) return;
            try
            {
                if (!Finite(elapsedSeconds) || elapsedSeconds > 30 + CaptureContract.MaximumExcludedWarmupOverlapSeconds)
                    throw new InvalidOperationException("GC baseline missed the post-warmup boundary.");
                Read(before); started = true;
                Report.startElapsedSeconds = elapsedSeconds; Report.before = before;
            }
            catch (Exception error) { Unavailable(error.GetType().Name + ": " + error.Message); }
        }

        public void Finish(double elapsedSeconds)
        {
            if (finished) return;
            finished = true;
            if (failed) return;
            if (!started) { Unavailable("Measured GC window never started."); return; }
            try
            {
                if (!Finite(elapsedSeconds) || elapsedSeconds <= Report.startElapsedSeconds.Value)
                    throw new InvalidOperationException("GC endpoint clock did not advance.");
                // Read before allocating the exported delta array or serializing any DTO.
                Read(after);
                var deltas = new int[before.Length];
                for (int i = 0; i < deltas.Length; i++)
                {
                    if (after[i] < before[i]) throw new InvalidOperationException("GC generation count decreased/reset.");
                    deltas[i] = after[i] - before[i];
                }
                Report.after = after; Report.deltas = deltas;
                Report.endElapsedSeconds = elapsedSeconds; Report.available = true;
            }
            catch (Exception error) { Unavailable(error.GetType().Name + ": " + error.Message); }
        }

        void Read(int[] destination)
        {
            for (int i = 0; i < destination.Length; i++)
            {
                int value = read(i);
                if (value < 0) throw new InvalidOperationException("GC generation count is negative.");
                destination[i] = value;
            }
        }
        void Unavailable(string reason)
        {
            failed = true; Report.available = false; Report.unavailableReason = reason;
            Report.before = null; Report.after = null; Report.deltas = null;
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
#endif
}
