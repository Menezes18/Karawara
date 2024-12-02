using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace System.MiniMap
{
    [CreateAssetMenu(fileName = "Map Render", menuName = "MiniMap/Render")]
    public class SystemMapRender : ScriptableObject
    {

        public enum RendersDivisions
        {
            Single = 1,
        }

        public RendersDivisions renderDivisions = RendersDivisions.Single;
        public List<Texture2D> snapshots;

        public Texture2D GetSingle()
        {
            return snapshots[0];
        }

       
        public Texture2D GetSnapshot(int index)
        {
            return snapshots[index];
        }

        public bool IsSingle()
        {
            return renderDivisions == RendersDivisions.Single;
        }

        public int GetNumberOfDivisions()
        {
            return (int)renderDivisions;
        }

        
  }

}