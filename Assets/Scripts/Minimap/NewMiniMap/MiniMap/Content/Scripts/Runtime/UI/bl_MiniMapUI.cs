using System;
using System.Collections;
using UnityEngine;

namespace System.MiniMap
{
    public class bl_MiniMapUI : MonoBehaviour
    {
        public RectTransform root;
        public CanvasGroup rootAlpha;
        public bl_MiniMapPlayerIcon playerIcon;
        public RectTransform iconsPanel;
        public MiniMapMaskHandler minimapMaskManager;
        public Animator hitAnimator;

        public float hitEffectSpeed = 1.5f;


        private bl_MiniMapSize _miniMapSize = null;
        public bl_MiniMapSize MiniMapSize
        {
            get
            {
                if(_miniMapSize == null)
                {
                    _miniMapSize = GetComponentInChildren<bl_MiniMapSize>(true);
                }
                return _miniMapSize;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="miniMap"></param>
        public void Setup(SystemMiniMap miniMap)
        {
            if (playerIcon != null) playerIcon.ApplyMiniMapSettings(miniMap);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="active"></param>
        public void SetActive(bool active)
        {
            root.gameObject.SetActive(active);
        }

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetIcon"></param>
        public void ConfigureWorldTarget(MiniMapEntityBase targetIcon)
        {
            if (playerIcon == null)
                return;

            MiniMapIconSettings settings = playerIcon.GetIconSettings();
            settings.Target = targetIcon.transform;
            settings.ItemEffect = ItemEffect.None;
            
            targetIcon.SetIconSettings(settings);
        }

        /// <summary>
        /// 
        /// </summary>
        public void DoHitEffect()
        {
            if (hitAnimator == null) return;

            hitAnimator.speed = hitEffectSpeed;
            hitAnimator.Play("HitEffect", 0, 0);
        }

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
        public RectTransform PlayerIconTransform
        {
            get => playerIcon.IconTransform;
        }
    }
}