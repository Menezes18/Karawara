using UnityEngine;

namespace System.MiniMap
{
    public class bl_MiniMapOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject content = null;

       
        private void Awake()
        {
            SetActive(false);
            transform.SetAsFirstSibling();
        }

      
        private void OnDisable()
        {
            instance = null;
        }

       
        public void SetActive(bool active)
        {
            if (content != null)
            {
                content.SetActive(active);
            }
        }
        
        private static bl_MiniMapOverlay instance;
        public static bl_MiniMapOverlay Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<bl_MiniMapOverlay>();
                }
                return instance;
            }
        }
    }
}