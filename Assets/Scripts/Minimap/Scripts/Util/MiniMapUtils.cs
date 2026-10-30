using UnityEngine;

public static class MiniMapUtils  {


    public static Vector3 CalculateMiniMapPosition(Vector3 viewPoint,RectTransform maxAnchor)
    {
        viewPoint = new Vector2((viewPoint.x * maxAnchor.sizeDelta.x) - (maxAnchor.sizeDelta.x * 0.5f),
            (viewPoint.y * maxAnchor.sizeDelta.y) - (maxAnchor.sizeDelta.y * 0.5f));

        return viewPoint;
    }

    public static SystemMiniMap GetMiniMap(int id = 0)
    {
        if (SystemMiniMap.ActiveMiniMap != null) return SystemMiniMap.ActiveMiniMap;

        SystemMiniMap[] allmm = GameObject.FindObjectsOfType<SystemMiniMap>();
        if (id > allmm.Length - 1) return null;
        return allmm[id];
    }

    public static bool IsInLayerMask(int layer, LayerMask layermask)
    {
        return layermask == (layermask | (1 << layer));
    }


    public static float ClampBorders(float value, float min, float max, out bool isClamped)
    {
        if (value < min)
        {
            value = min;
            isClamped = true;
        }
        else
        {
            if (value > max)
            {
                isClamped = true;
                value = max;
            }
            else
            {
                isClamped = false;
            }
        }
        return value;
    }

    private static Camera _renderCamera = null;
    public static Camera RenderCamera
    {
        get
        {
            if(_renderCamera == null)
            {
                _renderCamera = Camera.main;
                if(_renderCamera == null) { _renderCamera = Camera.current; }
            }
            return _renderCamera;
        }
    }
}