using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;

namespace System.MiniMap
{

    public sealed class IconBaseMiniMapIcon : IconBaseMiniMap
    {

        #region Public members
        [Header("CONFIGURAÇÕES")]
        public float DestroyIn = 5f;
        [Header("REFERÊNCIA")]
        public Image TargetGraphic;
        public Sprite DeathIcon = null;
        public CanvasGroup m_CanvasGroup;
        #endregion

        #region Private members
        private Animator Anim;
        private float delay = 0.1f;
        private MiniMapMaskHandler MaskHelper = null;
        private MiniMapEntityBase miniMapItem;
 
        private float maxOpacity = 1;
        #endregion

        #region Public properties

        public override Image GetImage => TargetGraphic;

        public override float Opacity
        {
            get => m_CanvasGroup.alpha; set
            {
                m_CanvasGroup.alpha = value * maxOpacity;
            }
        }
        #endregion

        void Awake()
        {
          
            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = GetComponent<CanvasGroup>();
            }
            if (GetComponent<Animator>() != null)
            {
                Anim = GetComponent<Animator>();
            }
            if (Anim != null) { Anim.enabled = false; }
           
            m_CanvasGroup.alpha = 0;
        }


        public override void SetUp(MiniMapEntityBase item)
        {
            miniMapItem = item;
        }
        
        public override void DestroyIcon(bool inmediate, Sprite death)
        {
            if (inmediate)
            {
                Destroy(gameObject);
            }
            else
            {
              
                TargetGraphic.sprite = death == null ? DeathIcon : death;
                Destroy(gameObject, DestroyIn);
            }
        }



       
        public override void SetIcon(Sprite ico)
        {
            TargetGraphic.sprite = ico;
        }

       
        public override void SetColor(Color newColor)
        {
            TargetGraphic.color = newColor;
        }

       
        public override void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }


       
        public override void SetOpacity(float opacity)
        {
            if (m_CanvasGroup == null) return;

            maxOpacity = opacity;
            m_CanvasGroup.alpha = opacity;
        }


        
        IEnumerator FadeIcon()
        {
            yield return new WaitForSeconds(delay);
            float d = 0;
            while (d < 1)
            {
                d += Time.deltaTime * 2;
                m_CanvasGroup.alpha = maxOpacity * d;
                yield return null;
            }
            if (Anim != null) { Anim.enabled = true; }
        }

        public void OnInteract(bool open)
        {
            StopCoroutine("FadeInfo");
            StartCoroutine("FadeInfo", !open);
        }

        

        public override void SpawnedDelayed(float v) { delay = v; StartCoroutine(FadeIcon()); }
    }
}