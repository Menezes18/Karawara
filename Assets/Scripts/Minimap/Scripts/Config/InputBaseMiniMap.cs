using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace System.MiniMap
{

    public abstract class InputBaseMiniMap : ScriptableObject
    {
        
        public enum MiniMapInput
        {
            ZoomIn,
            ZoomOut,
            ScreenMode,
        }
        
        
        public abstract void Init();

       
        public abstract bool IsInputDown(MiniMapInput key);

    }
}