using System;
using System.Collections;
using UnityEngine;

namespace System.MiniMap
{
    public class MiniMapUI : MonoBehaviour
    {
        public RectTransform root;
        public CanvasGroup rootAlpha;
        public MiniMapPlayerIcon playerIcon;
        public RectTransform iconsPanel;
        public MiniMapMaskHandler minimapMaskManager;
        public Animator hitAnimator;

        public float hitEffectSpeed = 1.5f;


        private MiniMapSize _miniMapSize = null;
        public MiniMapSize MiniMapSize
        {
            get
            {
                if(_miniMapSize == null)
                {
                    _miniMapSize = GetComponentInChildren<MiniMapSize>(true);
                }
                return _miniMapSize;
            }
        }


        public void Setup(SystemMiniMap miniMap)
        {
            if (playerIcon != null) playerIcon.ApplyMiniMapSettings(miniMap);
        }


        public void SetActive(bool active)
        {
            root.gameObject.SetActive(active);
        }


        public void DoStartFade(float delay, Action callback)
        {
            if (rootAlpha == null) return;

            StopAllCoroutines();
            StartCoroutine(Fade());

            IEnumerator Fade()
            {
                rootAlpha.alpha = 0;
                yield return new WaitForSeconds(delay);
                while (rootAlpha.alpha < Minimap.overallOpacity)
                {
                    rootAlpha.alpha += Time.deltaTime;
                    yield return null;
                }
                rootAlpha.alpha = Minimap.overallOpacity;
                callback?.Invoke();
            }
        }


        public void ConfigureWorldTarget(MiniMapEntityBase targetIcon)
        {
            if (playerIcon == null)
                return;

            MiniMapIconSettings settings = playerIcon.GetIconSettings();
            settings.Target = targetIcon.transform;
            settings.ItemEffect = ItemEffect.None;
            
            targetIcon.SetIconSettings(settings);
        }


        public void DoHitEffect()
        {
            if (hitAnimator == null) return;

            hitAnimator.speed = hitEffectSpeed;
            hitAnimator.Play("HitEffect", 0, 0);
        }


        public void OnZoomClick(bool zoomIn)
        {
            Minimap.ChangeZoom(zoomIn);
        }

 

        private SystemMiniMap _minimap;
        public SystemMiniMap Minimap
        {
            get
            {
                if(_minimap == null)
                {
                    _minimap = GetComponentInParent<SystemMiniMap>();
                }
                return _minimap;
            }
        }

        public RectTransform PlayerIconTransform
        {
            get => playerIcon.IconTransform;
        }
    }
}