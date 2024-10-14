using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGKarawara
{
    public class IconDragDrop :  MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
    {
        public Canvas canvas;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        public RectTransform startPosition; 
        private Vector3 storedPosition;
        public Transform parentTransform;
        
        public SkillUISlot skillUISlot;

        
        public bool auxPoint;
        public bool isDragging;
        private void Awake(){   
            parentTransform = transform.parent;
            startPosition = this.transform as RectTransform;

            skillUISlot = transform.parent.GetComponent<SkillUISlot>();
            storedPosition = startPosition.position;
            rectTransform = GetComponent<RectTransform>();
            canvas = GameObject.Find("SkillMenu").GetComponent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
           
        }

        public void OnPointerDown(PointerEventData eventData){

            if (auxPoint){
                transform.SetParent(parentTransform);
                startPosition.position = storedPosition;
                auxPoint = !auxPoint;
                skillUISlot.OnUnlockButtonClicked();
                isDragging = false;
            }
        }

        public void OnBeginDrag(PointerEventData eventData) {

            canvasGroup.alpha = .6f;
            canvasGroup.blocksRaycasts = false;
        }
        
        public void OnEndDrag(PointerEventData eventData) {

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            auxPoint = !auxPoint;
            if (!isDragging){
                startPosition.position = storedPosition;
            }
        }
        public void OnDrag(PointerEventData eventData){
            if (!isDragging){
                rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
            }
        }
    }
}
