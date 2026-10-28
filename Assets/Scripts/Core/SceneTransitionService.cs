using System;
using System.Threading;
using System.Threading.Tasks;
using Karawara.Core.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RPGKarawara
{
    public sealed class SceneTransitionService : ISceneTransitionService
    {
        private bool loading;
        public async Task<SessionResult> LoadAsync(string scenePath, CancellationToken cancellationToken)
        {
            if (loading) return SessionResult.Busy;
            loading = true;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(scenePath) || !Application.CanStreamedLevelBeLoaded(scenePath)) return SessionResult.Failed;
                // Activation must remain enabled: Unity cannot cancel LoadSceneAsync. Always drain
                // the operation before releasing the transition lock, even after cancellation.
                var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
                if (operation == null) return SessionResult.Failed;
                while (!operation.isDone) await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                var context = SceneContext.GetOrCreate(SceneManager.GetActiveScene());
                float deadline = Time.realtimeSinceStartup + 30f;
                while (!context.IsReady)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!context.TryPrepare() && Time.realtimeSinceStartup >= deadline) return SessionResult.Failed;
                    await Task.Yield();
                }
                return SessionResult.Success;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Local scene transition failed: " + exception.Message);
                return SessionResult.Failed;
            }
            finally { loading = false; }
        }
    }
}
