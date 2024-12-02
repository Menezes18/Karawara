using UnityEngine;
using UnityEngine.UI;

namespace System.MiniMap
{
    public abstract class IconBaseMiniMap : MonoBehaviour
    {

        public abstract Image GetImage
        {
            get;
        }

    
        public abstract float Opacity
        {
            get;
            set;
        }

     
        public abstract void SetUp(MiniMapEntityBase entity);

        
        public virtual void SetIcon(Sprite newIcon)
        {
            GetImage.sprite = newIcon;
        }

       

       
        public virtual void SetColor(Color newColor)
        {
            GetImage.color = newColor;
        }
        
        public abstract void SetOpacity(float opacity);
        public abstract void SetActive(bool active);
     
        public virtual void DestroyIcon(bool inmediate, Sprite overrideDeathIcon = null)
        {
            Destroy(gameObject);
        }

        
        public abstract void SpawnedDelayed(float delay);


    }
}