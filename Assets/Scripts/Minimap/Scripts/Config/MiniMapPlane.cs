﻿using UnityEngine;

namespace System.MiniMap
{
    public sealed class MiniMapPlane : MiniMapPlaneBase
    {
        public GameObject mapPlane;
        public GameObject gridPlane;
        public Material planeMaterial, planeMobileMaterial;

        private Transform m_Transform;
        private SystemMiniMap m_Minimap;
        private Vector3 currentPosition;
        private Vector3 worldPosition;
        private float defaultYCameraPosition;

   
        public override void Setup(SystemMiniMap minimap)
        {
            m_Transform = transform;
            m_Minimap = minimap;
            
            Vector3 pos = minimap.mapBounds.position;
            
            Vector3 size = minimap.mapBounds.size;
            if (minimap.renderType == SystemMiniMap.RenderType.Picture)
            {
                
                if (minimap.mapRender.IsSingle())
                {
                    PlaneRender.material = CreateMaterial(minimap.mapRender.GetSingle());
                }
            }
           
            m_Transform.localPosition = pos;
            
            m_Transform.localScale = (new Vector3(size.x, 10, size.y) / 10);

            m_Transform.rotation = minimap.mapBounds.rotation;
            var eulers = m_Transform.eulerAngles;
            eulers.x -= 90;
            m_Transform.eulerAngles = eulers;

            worldPosition = minimap.mapBounds.position;

            mapPlane.layer = minimap.MiniMapLayer;
            gridPlane.layer = minimap.MiniMapLayer;
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            gameObject.name = $"Minimap Plane ({minimap.gameObject.name})";
            mapPlane.SetActive(minimap.renderType == SystemMiniMap.RenderType.Picture);
           
            

            if (minimap.renderType == SystemMiniMap.RenderType.Picture)
            {
               
                if (!minimap.mapRender.IsSingle())
                {
                    SetupMultiRender();
                }
            }

            Invoke(nameof(DelayPositionInvoke), 1);
        }

       
        private void SetupMultiRender()
        {
            var mapRender = m_Minimap.mapRender;
            var planes = new GameObject[mapRender.GetNumberOfDivisions() * mapRender.GetNumberOfDivisions()];

            float size = PlaneRender.transform.localScale.x;
            Vector3 originPos = PlaneRender.transform.localPosition;
            float div = (float)mapRender.renderDivisions;
            float divSize = size / div;
            float halfDiv = div / 2;

            for (int i = 0; i < planes.Length; i++)
            {
                planes[i] = Instantiate(PlaneRender.gameObject);
                planes[i].transform.parent = PlaneRender.transform.parent;
                planes[i].transform.localRotation = PlaneRender.transform.localRotation;
                planes[i].transform.localScale = Vector3.one * divSize;
                planes[i].GetComponent<Renderer>().material = CreateMaterial(mapRender.GetSnapshot(i));
            }

            divSize = ((size * 10) / div) * 0.5f;

            Vector3 divPos = originPos;
            divPos.x -= ((divSize * 2) * halfDiv) - divSize;
            divPos.z -= ((divSize * 2) * halfDiv) - divSize;
            float initX = divPos.x;

            int pId = 0;
            for (int i = 0; i < div; i++)
            {
                for (int x = 0; x < div; x++)
                {
                    planes[pId].transform.localPosition = divPos;
                    divPos.x += (divSize * 2);
                    pId++;
                }
                divPos.z += (divSize * 2);
                divPos.x = initX;
            }

            foreach (var p in planes)
            {
                p.transform.parent = PlaneRender.transform;
            }

            PlaneRender.enabled = false;
        }

       
        public void OnUpdate()
        {
            currentPosition = m_Transform.localPosition;
           
            float ydif = defaultYCameraPosition - m_Minimap.miniMapCamera.transform.position.y;
            currentPosition.y = currentPosition.y - ydif;
            m_Transform.position = currentPosition;
        }

        void DelayPositionInvoke() { defaultYCameraPosition = m_Minimap.miniMapCamera.transform.position.y; }


        public override void SetMapTexture(Texture2D newTexture)
        {
            PlaneRender.material.mainTexture = newTexture;
        }

       
        public override void SetGridSize(float size)
        {
            gridPlane.GetComponent<Renderer>().material.SetTextureScale("_MainTex", Vector2.one * size);
        }

       
        public override void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

       
        public override void SetActiveGrid(bool active)
        {
            if (gridPlane == null) return;
            gridPlane.SetActive(active);
        }

        
        public Material CreateMaterial(Texture2D texture)
        {
            Material mat = new Material(planeMaterial);

            mat.mainTexture = texture;
            mat.SetFloat("_Power", m_Minimap.planeSaturation);
            return mat;
        }

        public Renderer PlaneRender
        {
            get { return mapPlane.GetComponent<Renderer>(); }
        }
    }
}