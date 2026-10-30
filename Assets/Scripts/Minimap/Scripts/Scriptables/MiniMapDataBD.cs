using UnityEngine;
using System.MiniMap;

public class MiniMapDataBD : ScriptableObject
{
    public GameObject IconPrefab;
    public InputBaseMiniMap inputHandler;
    public IconDataMap empty;
    public GameObject ScreenShotPrefab;
    public MiniMapPlaneBase mapPlane;

    public static MiniMapDataBD _instance;
    public static MiniMapDataBD Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<MiniMapDataBD>("MiniMapData") as MiniMapDataBD;
            }
            return _instance;
        }
    }
}