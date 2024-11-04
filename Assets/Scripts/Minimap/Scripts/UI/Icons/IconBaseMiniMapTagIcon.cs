using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace System.MiniMap
{
    public sealed class IconBaseMiniMapTagIcon : IconBaseMiniMap
    {
        [SerializeField] private Image tagImg = null;
        [SerializeField] private UnityEngine.UI.Text tagText = null;
        [SerializeField] private CanvasGroup rootGroup = null;

        private float maxOpacity = 1;
        private float delay = 0;
        public override Image GetImage => tagImg;

 
        public override float Opacity
        {
            get => rootGroup.alpha; set
            {
                rootGroup.alpha = value * maxOpacity;
            }
        }


        public override void SetUp(MiniMapEntityBase entity)
        {

        }


        public override void SetActive(bool active)
        {
           gameObject.SetActive(active);
        }

        public override void SetOpacity(float opacity)
        {
            rootGroup.alpha = opacity;
        }

        
        public override void SpawnedDelayed(float wdelay)
        {
            delay = wdelay;
            StartCoroutine(FadeIcon());
        }

        
        IEnumerator FadeIcon()
        {
            yield return new WaitForSeconds(delay);
            float d = 0;
            while (d < 1)
            {
                d += Time.deltaTime * 2;
                rootGroup.alpha = maxOpacity * d;
                yield return null;
            }
        }
    }
}