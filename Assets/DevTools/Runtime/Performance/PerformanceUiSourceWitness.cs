using UnityEngine;
namespace RPGKarawara.DevTools.Performance
{
    [DisallowMultipleComponent]
    public sealed class PerformanceUiSourceWitness : MonoBehaviour
    {
        public Animator animator;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void Awake() => PerformanceUiSourceContext.Notify(this, PerformanceUiSourceEvent.Created);
        void OnEnable() => PerformanceUiSourceContext.Notify(this, PerformanceUiSourceEvent.Enabled);
        void OnDisable() => PerformanceUiSourceContext.Notify(this, PerformanceUiSourceEvent.Disabled);
        void OnDestroy() => PerformanceUiSourceContext.Notify(this, PerformanceUiSourceEvent.Destroyed);
        void OnTransformParentChanged() => PerformanceUiSourceContext.Notify(this, PerformanceUiSourceEvent.ParentChanged);
#endif
    }
}
