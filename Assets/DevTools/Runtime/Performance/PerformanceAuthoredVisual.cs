using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPGKarawara.DevTools.Performance
{
    // Serialized identity for reviewed scene visuals; no discovery by object name.
    // Presence does not grant creation closure or population certification.
    [DisallowMultipleComponent]
    public sealed class PerformanceAuthoredVisual : MonoBehaviour
    {
        public Animator visual;
        public MonoBehaviour[] reviewedComponents = System.Array.Empty<MonoBehaviour>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        internal bool IsValidSource()
        {
            if (visual == null || visual.gameObject != gameObject || GetComponent<Animator>() != visual ||
                GetComponentInChildren<SkinnedMeshRenderer>(true) == null || reviewedComponents == null || reviewedComponents.Length > 32) return false;
            for (int i = 0; i < reviewedComponents.Length; i++)
            {
                var child = reviewedComponents[i];
                if (child == null || !child.transform.IsChildOf(transform) ||
                    (child.GetType() != typeof(UndeadHandDamageCollider) && child.GetType() != typeof(LockOnTransform) && child.GetType() != typeof(UniversalAdditionalCameraData))) return false;
                if (child is UniversalAdditionalCameraData && (child.GetComponent<Camera>() == null || child.GetComponent<AudioListener>() == null || child.GetComponent<Animation>() == null)) return false;
                if (child is DamageCollider damage && (damage.QaDamageCollider == null || damage.QaDamageCollider.gameObject != child.gameObject)) return false;
                for (int j = 0; j < i; j++) if (reviewedComponents[j] == child) return false;
            }
            foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) return false;
                if (component == this || component is PerformanceActorLifetimeWitness) continue;
                bool declared = false;
                for (int i = 0; i < reviewedComponents.Length; i++) if (component == reviewedComponents[i]) { declared = true; break; }
                if (!declared) return false;
            }
            return true;
        }
        void OnEnable() => PerformanceActorLifecycleScope.Notify(this, PerformanceActorLifecycle.Enabled);
        void OnDisable() => PerformanceActorLifecycleScope.Notify(this, PerformanceActorLifecycle.Disabled);
        void OnDestroy() => PerformanceActorLifecycleScope.Notify(this, PerformanceActorLifecycle.Destroyed);
#endif
    }
}
