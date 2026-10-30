using System;
using UnityEngine;

public static class MiniMapEvents 
{

    public static Action<bool> onSizeChanged;


    public static Action<SystemMiniMap> onActiveMiniMapChanged;

    
    public static Action<Vector3> onSelectPosition;
}