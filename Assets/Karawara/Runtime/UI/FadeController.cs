using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class FadeController : MonoBehaviour
    {
       public Animator animator;

       public void Awake(){
           FadeOut();
       }

       public void FadeOut(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceUiSourceContext.NotifyOwner(this, RPGKarawara.DevTools.Performance.PerformanceUiSourceEvent.FadeTrigger);
#endif
           animator.SetTrigger("FadeOut");
       }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void OnEnable() { RPGKarawara.DevTools.Performance.PerformanceUiSourceContext.NotifyOwner(this, RPGKarawara.DevTools.Performance.PerformanceUiSourceEvent.Enabled); }
        void OnDisable() { RPGKarawara.DevTools.Performance.PerformanceUiSourceContext.NotifyOwner(this, RPGKarawara.DevTools.Performance.PerformanceUiSourceEvent.Disabled); }
        void OnDestroy() { RPGKarawara.DevTools.Performance.PerformanceUiSourceContext.NotifyOwner(this, RPGKarawara.DevTools.Performance.PerformanceUiSourceEvent.Destroyed); }
#endif
}
}
