using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class SliderSound : MonoBehaviour
    {
        public Slider volumeSlider;
        private float volum = 1f;
        void Start()
        {
            volumeSlider.value = volum;
            volumeSlider.onValueChanged.AddListener(AudioController.AudioInstance.SetVolum);
        }

    
        
    }
}
