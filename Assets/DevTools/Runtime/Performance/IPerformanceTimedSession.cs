using System;

namespace RPGKarawara.DevTools.Performance
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
    public interface IPerformanceTimedSession : IDisposable
    {
        bool IsRunning { get; }
        int OwnedRecorderCount { get; }
        bool Qualified { get; }
        bool TimingQualified { get; }
        bool PopulationVerified { get; }
        object EvidenceObject { get; }
        void Poll();
    }
#endif
}
