using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGKarawara
{
    
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler{

        private static LTDescr delay;
        public string content;
        public string header;
        public void OnPointerEnter(PointerEventData eventData){
                TooltipSystem.current.Show(content, header);
            delay = LeanTween.delayedCall(0.5f, () => {
                Debug.Log('a');
            });
        }
        
        public void OnPointerExit(PointerEventData eventData){
           // LeanTween.cancel(delay.uniqueId);
            TooltipSystem.current.Hide();
        }
    }
}
