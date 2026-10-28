using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class LockOnTransform : MonoBehaviour
    {
        //  THIS SCRIPT ALLOWS OUR COMBAT MANAGER TO FETCH THE LOCK ON TRANSFORM
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        private void OnEnable() => DevTools.Performance.PerformanceActorLifecycleScope.NotifyVisualChild(this, DevTools.Performance.PerformanceActorLifecycle.Enabled);
        private void OnDisable() => DevTools.Performance.PerformanceActorLifecycleScope.NotifyVisualChild(this, DevTools.Performance.PerformanceActorLifecycle.Disabled);
        private void OnDestroy() => DevTools.Performance.PerformanceActorLifecycleScope.NotifyVisualChild(this, DevTools.Performance.PerformanceActorLifecycle.Destroyed);
#endif
    }
}
