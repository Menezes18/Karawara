using System.MiniMap;
using UnityEngine;

namespace RPGKarawara.DevTools.Performance
{
    // Attached to the actual icon prefab, including future active instances.
    [DisallowMultipleComponent]
    public sealed class PerformanceMinimapIconWitness : MonoBehaviour
    {
        public IconBaseMiniMapIcon icon;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void Awake() => PerformanceMinimapContext.Notify(this, PerformanceMinimapEvent.Created);
        void OnEnable() => PerformanceMinimapContext.Notify(this, PerformanceMinimapEvent.Enabled);
        void OnDisable() => PerformanceMinimapContext.Notify(this, PerformanceMinimapEvent.Disabled);
        void OnDestroy() => PerformanceMinimapContext.Notify(this, PerformanceMinimapEvent.Destroyed);
        void OnTransformParentChanged() => PerformanceMinimapContext.Notify(this, PerformanceMinimapEvent.ParentChanged);
#endif
    }
}
