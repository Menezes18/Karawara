using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGKarawara
{
    public class ItemSlotSkill : MonoBehaviour, IDropHandler {
        public RectTransform itemSlotImage;
        public GameObject slotTransform;
        public int slotID;
        private RectTransform draggedRectTransform;
        private IconDragDrop dragDrop;
        private void Awake(){
            itemSlotImage = GetComponent<RectTransform>();
        }

        public void OnDrop(PointerEventData eventData) {
            Debug.Log("OnDrop");
            if (eventData.pointerDrag != null) {
               Debug.Log(eventData.pointerDrag.name); 
               draggedRectTransform = eventData.pointerDrag.GetComponent<RectTransform>();
                dragDrop = eventData.pointerDrag.GetComponent<IconDragDrop>();
                
               if (draggedRectTransform != null)
               {
                   // Defina o item como filho do slotTransform para garantir que ele esteja no contexto certo.
                   draggedRectTransform.SetParent(slotTransform.transform);

                   // Ajusta a posição ancorada dentro do slot.
                   draggedRectTransform.anchoredPosition = Vector2.zero; // Centraliza dentro do slot.
               }
               dragDrop.auxID = slotID;
               dragDrop.skillUISlot.OnSkillButtonClicked(slotID);
               dragDrop.isDragging = true;
            }
        }

        

    }
}
