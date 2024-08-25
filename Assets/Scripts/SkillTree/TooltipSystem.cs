using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace RPGKarawara
{
    public class TooltipSystem : MonoBehaviour
    {
       public static TooltipSystem current;

       public Tooltip tooltip;
       private void Awake(){
           current = this;
       }

       public void Restart()
       {
           // Começa uma Coroutine para adicionar um atraso antes de reativar o tooltip
           StartCoroutine(RestartWithDelay(0.2f));
       }

       private IEnumerator RestartWithDelay(float delay)
       {
           // Desativa o tooltip imediatamente
           tooltip.gameObject.SetActive(false);

           // Aguarda o tempo especificado
           yield return new WaitForSeconds(delay);

           // Reativa o tooltip
           tooltip.gameObject.SetActive(true);
       }
       public void Show(string content, string head = ""){
           tooltip.SetText(content, head);
           tooltip.gameObject.SetActive(true);
       }

       public void Hide(){
           tooltip.gameObject.SetActive(false);
       }
    }
}
