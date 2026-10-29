using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class DestroyAfterTime : MonoBehaviour{
        [SerializeField] private float timeToDestroy = 5f;

        private void Start(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this);
            RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.DestroyScheduled);
#endif
            Destroy(gameObject, timeToDestroy);
        }
        private void Destroyer(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this);
            RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.DestroyScheduled);
#endif
            Destroy(gameObject, timeToDestroy);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        public float QaDestroyDelay => timeToDestroy;
        void OnEnable() { RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this); RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.Enabled); }
        void OnDisable() { RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this); RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.Disabled); }
        void OnDestroy() { RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this); RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.Destroyed); }
#endif
}
}
