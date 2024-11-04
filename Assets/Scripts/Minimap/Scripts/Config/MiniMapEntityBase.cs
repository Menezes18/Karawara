using System.MiniMap;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class MiniMapEntityBase : MonoBehaviour
{
  
    public abstract Transform GetTarget
    {
        get;
    }
    
   
    public abstract void OnUpdateItem();

  
    public abstract void SetIconSettings(MiniMapIconSettings iconSettings);

   
    public abstract void SetTarget(Transform newTarget);

    
    public abstract void SetIcon(Sprite newSprite);

    
    public abstract void SetIconColor(Color newIconColor);

    
    public abstract void SetActiveIcon(bool active);
    
    public abstract void DestroyIcon(bool inmediate = false);
    
    public abstract void ChangeMiniMapOwner(SystemMiniMap newOwner);
}