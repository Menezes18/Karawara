using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace RPGKarawara.DevTools.Performance
{
    [DisallowMultipleComponent]
    public sealed class PerformanceAuxiliarySource : MonoBehaviour
    {
        public Animator visual;
        public Component source;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        internal bool IsValidSource()
        {
            if (!enabled || visual == null || source == null || visual.gameObject != gameObject || source.gameObject != gameObject || GetComponent<Animator>() != visual) return false;
            bool particle = source.GetType() == typeof(ParticleSystem), spawner = source.GetType() == typeof(AICharacterSpawner), camera = source.GetType() == typeof(DestroyAfterTime);
            if (!particle && !spawner && !camera || particle && GetComponent<ParticleSystemRenderer>() == null || camera && (GetComponent<Camera>() == null || GetComponent<AudioListener>() == null || GetComponent<UniversalAdditionalCameraData>() == null)) return false;
            // Native mixed-role roots require their own reviewed contract.
            if (!camera && GetComponent<Camera>() != null || !particle && GetComponent<ParticleSystem>() != null) return false;
            foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
                if (component == null || component != this && component != source && !(camera && component.gameObject == gameObject && component.GetType() == typeof(UniversalAdditionalCameraData))) return false;
            return true;
        }
        void Awake() => PerformanceAuxiliaryContext.Notify(this, PerformanceAuxiliaryEvent.Created);
        void OnEnable() => PerformanceAuxiliaryContext.Notify(this, PerformanceAuxiliaryEvent.Enabled);
        void OnDisable() => PerformanceAuxiliaryContext.Notify(this, PerformanceAuxiliaryEvent.Disabled);
        void OnDestroy() => PerformanceAuxiliaryContext.Notify(this, PerformanceAuxiliaryEvent.Destroyed);
        void OnTransformParentChanged() => PerformanceAuxiliaryContext.Notify(this, PerformanceAuxiliaryEvent.ParentChanged);
#endif
    }
}
