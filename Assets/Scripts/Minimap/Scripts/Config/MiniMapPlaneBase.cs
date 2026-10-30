using UnityEngine;

namespace System.MiniMap
{
    public abstract class MiniMapPlaneBase : MonoBehaviour
    {
        
        public abstract void Setup(SystemMiniMap minimap);

        
        public abstract void SetActive(bool active);

        
        public virtual void SetActiveGrid(bool active) { }

        
        public virtual void SetMapTexture(Texture2D newTexture) { }

        
        public virtual void SetGridSize(float size) { }
    }
}