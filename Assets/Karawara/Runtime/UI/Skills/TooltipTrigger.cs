using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGKarawara
{
    
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler{

        
        public string content;
        public string header;
        public void OnPointerEnter(PointerEventData eventData){
                TooltipSystem.current.Show(content, header);
        }
        
        public void OnPointerExit(PointerEventData eventData){
           // LeanTween.cancel(delay.uniqueId);
            TooltipSystem.current.Hide();
        }
    }
}
