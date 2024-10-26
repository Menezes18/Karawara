using UnityEngine;

namespace System.MiniMap
{
    public sealed class PointerBaseMapPointer : PointerBaseMap
    {

        public string PlayerTag = "Player";
        public AudioClip SpawnSound;
        public MeshRenderer m_Render;
        private AudioSource ASource;

       
        private void OnEnable()
        {
            if (SpawnSound != null)
            {
                ASource = GetComponent<AudioSource>();
                ASource.clip = SpawnSound;
                ASource.Play();
            }
        }

        
        public override void SetColor(Color c)
        {
            if (m_Render == null) return;

            GetComponent<MiniMapEntityBase>().SetIconColor(c);
            c.a = 0.25f;
            m_Render.material.SetColor("_TintColor", c);
        }

       

    }
}