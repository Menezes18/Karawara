using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class LoadScene : MonoBehaviour{
        
        
        public Button _button;
        public GameObject image;

        


        public void LoadSceneGame(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceUiSourceContext.NotifyOwner(this, RPGKarawara.DevTools.Performance.PerformanceUiSourceEvent.LoadingRequest);
#endif
            image.SetActive(true);
            
            _button.enabled = false;
            
        }
        
        public void LoadGame(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceUiSourceContext.NotifyOwner(this, RPGKarawara.DevTools.Performance.PerformanceUiSourceEvent.LoadingRequest);
#endif
            image.SetActive(true);

        }

        public void desativado(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceUiSourceContext.NotifyOwner(this, RPGKarawara.DevTools.Performance.PerformanceUiSourceEvent.HiddenRequest);
#endif
            image.SetActive(false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        void OnEnable() { RPGKarawara.DevTools.Performance.PerformanceUiSourceContext.NotifyOwner(this, RPGKarawara.DevTools.Performance.PerformanceUiSourceEvent.Enabled); }
        void OnDisable() { RPGKarawara.DevTools.Performance.PerformanceUiSourceContext.NotifyOwner(this, RPGKarawara.DevTools.Performance.PerformanceUiSourceEvent.Disabled); }
        void OnDestroy() { RPGKarawara.DevTools.Performance.PerformanceUiSourceContext.NotifyOwner(this, RPGKarawara.DevTools.Performance.PerformanceUiSourceEvent.Destroyed); }
#endif
}
}
