using UnityEngine;
using UnityEngine.UI;

namespace System.MiniMap
{
    public class bl_MiniMapPlayerIcon : MonoBehaviour
    {
        [SerializeField] private RectTransform root = null;
        [SerializeField] private Image iconImage = null;
        [SerializeField] private GameObject viewAreaUI = null;

        private Sprite defaultIcon;

        
        public void SetActive(bool active)
        {
            if (root == null) return;

            root.gameObject.SetActive(active);
        }

        
        public void SetActiveVisibleArea(bool active)
        {
            if (viewAreaUI == null) return;

            viewAreaUI.SetActive(active);
        }

        
        public void SetColor(Color color)
        {
            if (iconImage == null) return;

            iconImage.color = color;
        }

        
        public void SetIcon(Sprite newIcon, bool overrideDefault = false)
        {
            if (iconImage == null) return;

            if (newIcon == null)
            {
                if (defaultIcon == null && iconImage.sprite != null) return;
                
                iconImage.sprite = defaultIcon;
                return;
            }

            if (defaultIcon == null && !overrideDefault) defaultIcon = iconImage.sprite;
            iconImage.sprite = newIcon;
        }

       
        public void SetSize(float size)
        {
            if (root == null) return;

            Vector2 newSize = Vector2.one * size;
            root.sizeDelta = newSize;
        }

       
        public void ApplyMiniMapSettings(SystemMiniMap miniMap)
        {
            SetIcon(miniMap.PlayerIconSprite, true);
            SetColor(miniMap.playerColor);
            SetActive(miniMap.mapMode == SystemMiniMap.MapType.Local);
        }

       
        public RectTransform IconTransform
        {
            get => root;
        }

        
        public Vector2 Position
        {
            get => root.anchoredPosition;
            set => root.anchoredPosition = value;
        }

        
        public MiniMapIconSettings GetIconSettings()
        {
            return new MiniMapIconSettings()
            {
                Icon = iconImage.sprite,
                Color = iconImage.color,
                Size = root.sizeDelta.x + 2,               
            };
        }
    }
}