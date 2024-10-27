using UnityEngine;

namespace System.MiniMap
{
    public class MiniMapOverlay : MonoBehaviour
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
        
        private static MiniMapOverlay instance;
        public static MiniMapOverlay Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<MiniMapOverlay>();
                }
                return instance;
            }
        }
    }
}