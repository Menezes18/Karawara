using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class SliderSound : MonoBehaviour
    {
        public Slider volumeSlider;
        private AudioController boundAudio;

        void OnEnable()
        {
            if (volumeSlider == null) volumeSlider = GetComponent<Slider>();
            BindAudio();
        }

        void Update()
        {
            if (boundAudio != AudioController.AudioInstance) BindAudio();
        }

        void BindAudio()
        {
            if (volumeSlider == null) return;
            volumeSlider.onValueChanged.RemoveListener(SetVolume);
            boundAudio = AudioController.AudioInstance;
            if (boundAudio == null || boundAudio.audioSourceMusicaDeFundo == null) return;
            volumeSlider.SetValueWithoutNotify(boundAudio.audioSourceMusicaDeFundo.volume);
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        void SetVolume(float value)
        {
            if (boundAudio != null && boundAudio.audioSourceMusicaDeFundo != null)
                boundAudio.SetVolum(Mathf.Clamp01(value));
        }

        void OnDisable()
        {
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(SetVolume);
            boundAudio = null;
        }
    }
}
