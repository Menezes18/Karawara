using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace RPGKarawara
{
    public enum Element
    {
        None,
        Fire,
        Water
    }

    public class ElementManager : MonoBehaviour
    {
        public Element currentElement = Element.None;

        public void SetElement(Element newElement)
        {
            currentElement = newElement;
        }

        private void Update(){
            
        }
    }
}