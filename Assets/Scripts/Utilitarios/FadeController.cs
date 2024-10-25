using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class FadeController : MonoBehaviour
    {
        public Image uiImage;
        public float fadeDuration = 1f;

        private Color originalColor;

        void Start()
        {
            originalColor = uiImage.color;
            FadeOut();
        }

        public void FadeIn()
        {
            StartCoroutine(Fade(0f, 1f));
        }

        public void FadeOut()
        {
            StartCoroutine(Fade(1f, 0f));
        }

        private IEnumerator Fade(float startAlpha, float endAlpha)
        {
            float elapsedTime = 0f;
            Color color = uiImage.color;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeDuration);
                color.a = alpha;
                uiImage.color = color;

                yield return null;
            }

            color.a = endAlpha;
            uiImage.color = color;
        }
    }
}
