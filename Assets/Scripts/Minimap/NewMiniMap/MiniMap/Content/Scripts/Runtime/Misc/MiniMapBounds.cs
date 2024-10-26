using UnityEngine;
using System.Collections.Generic;


namespace System.MiniMap
{
    public class MiniMapBounds : MonoBehaviour
    {

        [Header("Use UI editor Tool for scale the wordSpace")]
        public Color GizmoColor = new Color(1, 1, 1, 0.75f);
        public bool alwaysShow = false;

        private RectTransform m_rectTransform;
        public RectTransform BoundTransform
        {
            get
            {
                if (m_rectTransform == null) { m_rectTransform = GetComponent<RectTransform>(); }
                return m_rectTransform;
            }
        }

        
        public Vector3 position
        {
            get => BoundTransform.position;
        }

       
        public Vector3 size
        {
            get => BoundTransform.sizeDelta;
            set => BoundTransform.sizeDelta = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public Quaternion rotation
        {
            get => BoundTransform.rotation;
        }

        
        public void Init()
        {
            Vector2 originalSize = size;
            if (size.x > size.y)
            {
                originalSize.y = size.x;
            }
            else if (size.x < size.y)
            {
                originalSize.x = size.y;
            }
            size = originalSize;
        }
        
        
        private void OnDrawGizmos()
        {
            if (!alwaysShow) return;

            Draw();
        }

        
        void OnDrawGizmosSelected()
        {
            if (alwaysShow) return;

            Draw();
        }

        
        void Draw()
        {
            if (m_rectTransform == null) m_rectTransform = this.GetComponent<RectTransform>();

            Vector3 v = m_rectTransform.sizeDelta;

            var matrix = Gizmos.matrix;

            Gizmos.matrix = Matrix4x4.TRS(m_rectTransform.position, m_rectTransform.rotation, Vector3.one);

            Gizmos.color = GizmoColor;
            Gizmos.DrawCube(Vector3.zero, new Vector3(v.x, v.y, 2));
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(v.x, v.y, 2));

            Gizmos.matrix = matrix;
        }
    }
}