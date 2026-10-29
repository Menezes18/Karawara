using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGKarawara{
    
    public class Tooltip : MonoBehaviour{
        public TextMeshProUGUI headerField;
        public TextMeshProUGUI contentFields;

        public LayoutElement layoutElement;

        public int chracterWrapLimit;
        public RectTransform rectTransform;

        private void Awake(){
            rectTransform = GetComponent<RectTransform>();
        }

        public void SetText(string content, string header = ""){
            if (string.IsNullOrEmpty(header)){
                headerField.gameObject.SetActive(false);
            }
            else{
                headerField.gameObject.SetActive(true);
                headerField.text = header;
            }
            
            contentFields.text = content;
            int headerLength = headerField.text.Length;
            int contentLength = contentFields.text.Length;

            layoutElement.enabled =
                (headerLength > chracterWrapLimit || contentLength > chracterWrapLimit) ? true : false;
        }
        private void Update(){
            if (Application.isEditor){
                int headerLength = headerField.text.Length;
                int contentLength = contentFields.text.Length;

                layoutElement.enabled =
                    (headerLength > chracterWrapLimit || contentLength > chracterWrapLimit) ? true : false;
            }
            
            Vector2 position = Mouse.current.position.ReadValue();
            
            float pivotX = position.x / Screen.width;
            float pivotY = position.y / Screen.height;
            
            rectTransform.pivot = new Vector2(pivotX - 0.4f, pivotY);
            transform.position = position;

        }
    }
}