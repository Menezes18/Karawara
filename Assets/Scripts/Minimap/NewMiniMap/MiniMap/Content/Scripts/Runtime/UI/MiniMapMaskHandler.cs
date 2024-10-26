using UnityEngine;
using UnityEngine.UI;

namespace System.MiniMap
{
    public class MiniMapMaskHandler : MonoBehaviour
    {
        [Header("Mask")]
        public Sprite MiniMapMask = null;
        public Sprite WorldMapMask = null;

        [Header("References")]
        [SerializeField] private Material maskMaterial = null;
        [SerializeField] private Image[] maskImages = null;
        [SerializeField] private Image Background = null;
        [SerializeField] private Sprite MiniMapBackGround = null;
        [SerializeField] private Sprite WorldMapBackGround = null;
        [SerializeField] private RectTransform MaskIconRoot = null;
        public GameObject[] OnFullScreenDisable;

       
        void Start()
        {
            ChangeMaskSprite(false);
            if (maskMaterial != null)
            {
                maskMaterial.SetTexture("_Mask", MiniMapMask.texture);
            }
        }

       
        private void OnDestroy()
        {
            if (maskMaterial != null)
            {
                maskMaterial.SetTexture("_Mask", MiniMapMask.texture);
            }
        }

        
        public void ChangeMaskType(bool fullscren = false)
        {
            if (fullscren)
            {
                ChangeMaskSprite(true);
                if (Background != null) { Background.sprite = WorldMapBackGround; }
                if (maskMaterial != null) maskMaterial.SetTexture("_Mask", WorldMapMask.texture);
            }
            else
            {
                ChangeMaskSprite(false);
                if (Background != null) { Background.sprite = MiniMapBackGround; }
                if (maskMaterial != null) maskMaterial.SetTexture("_Mask", MiniMapMask.texture);
            }

            foreach (var item in OnFullScreenDisable)
            {
                if (item != null)
                    item.SetActive(!fullscren);
            }
        }

       
        public void ChangeMaskSprite(bool toWorldMask)
        {
            foreach (var mask in maskImages)
            {
                if (mask == null) continue;

                mask.sprite = toWorldMask ? WorldMapMask : MiniMapMask;
            }
        }

        
        public void SetMaskedIcon(RectTransform trans)
        {
            trans.SetParent(MaskIconRoot);
        }
    }
}