using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.MiniMap;

public sealed class SystemMiniMap : MonoBehaviour
{
    #region Enums
    [Serializable]
    public enum RenderType
    {
        RealTime,
        Picture,
    }

    [Serializable]
    public enum RenderMode
    {
        Mode2D,
    }

    [Serializable]
    public enum MapType
    {
        Local,
        Global,
    }

    [Serializable]
    public enum MapShape
    {
        Circle
    }
    #endregion

    #region Public members
    public GameObject m_Target;
    public int MiniMapLayer = 10;
    public bool useNonRenderLayer = false;
    public int nonRenderLayer = 10;
    public Camera miniMapCamera = null;
    public RenderType renderType = RenderType.Picture;
    public RenderMode canvasRenderMode = RenderMode.Mode2D;
    public MapType mapMode = MapType.Local;
    public bool Ortographic2D = false;
    public SystemMapRender mapRender = null;
    public int UpdateRate = 5;
    public Color playerColor = Color.white;
    [Range(0.05f, 2)] public float IconMultiplier = 1;
    [Range(1, 10)] public int scrollSensitivity = 3;
    public float DefaultHeight = 30;
    public bool saveZoomInRuntime = false;
    public float MaxZoom = 80;
    public float MinZoom = 5;
    public float LerpHeight = 8;
    public Sprite PlayerIconSprite;
    public MapShape mapShape = MapShape.Circle;
    public float CompassSize = 175f;
    public bool iconsAlwaysFacingUp = true;
    public bool DynamicRotation = true;
    public bool SmoothRotation = true;
    public float LerpRotation = 8;
    public float mapRotationOffset = 0;
    public bool AllowMapMarks = true;
    public bool AllowMultipleMarks = false;
    [Range(1, 20)] public float AreasSize = 4;
    public float gridOpacity = 0.7f;
    public float overallOpacity = 1;

    public MiniMapFullScreenMode fullScreenMode = MiniMapFullScreenMode.ScreenArea;
    public bool FadeOnFullScreen = false;
    public float fullScreenMargin = 10;
    public float sizeTransitionDuration = 0.5f;
    public AnimationCurve sizeTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool showCursorOnFullscreen = true;

    public bool lerpTrackingPosition = false;
    public Vector3 FullMapPosition = Vector2.zero;
    public Vector3 FullMapRotation = Vector3.zero;
    public Vector2 FullMapSize = Vector2.zero;
    public bool CanDragMiniMap = true;
    public bool DragOnlyOnFullScreen = true;
    public bool ResetOffSetOnChange = true;
    public Vector2 DragMovementSpeed = new Vector2(0.5f, 0.35f);
    public Vector2 MaxOffSetPosition = new Vector2(1000, 1000);
    public float planeSaturation = 1.4f;
    public MiniMapBounds mapBounds;
    public Canvas m_Canvas = null;
    public GameObject ItemPrefabSimple = null;
    public Transform minimapRig;
    #endregion

    #region Public properties
    public bool isFullScreen { get; set; }
    public bool hasError { get; set; }
    
    public float Zoom { get; set; }
    
    public bool HighPrecisionMode
    {
        get;
        set;
    } = false;
    
    public static SystemMiniMap ActiveMiniMap { get; private set; }

    private MiniMapUI _minimapUI = null;
    public MiniMapUI MiniMapUI
    {
        get
        {
            if (_minimapUI == null)
            {
                if (transform.parent != null)
                    _minimapUI = transform.parent.GetComponentInChildren<MiniMapUI>(true);
                else
                    _minimapUI = GetComponentInChildren<MiniMapUI>(true);
            }
            return _minimapUI;
        }
    }
    #endregion

    #region Private members
    private GameObject mapPointer;
    [HideInInspector] public Vector3 MiniMapPosition = Vector2.zero;
    [HideInInspector] public Vector3 MiniMapRotation = Vector3.zero;
    [HideInInspector] public Vector2 MiniMapSize = Vector2.zero;
    private Vector3 DragOffset = Vector3.zero;
    private bool DefaultRotationMode = false;
    private Vector3 DeafultMapRot = Vector3.zero;
    private MapShape defaultShape;
    const string MMHeightKey = "MinimapCameraHeight";
    private bool isAlphaComplete = false;
    private bool isPlanedCreated = false;
    private List<MiniMapEntityBase> miniMapItems = new List<MiniMapEntityBase>();
    private Vector3 playerPosition, targetPosition;
    private Vector3 playerRotation;
    private bool isUpdateFrame = false;
    private MiniMapPlaneBase miniMapPlane;
    [HideInInspector] public bool _isPreviewFullscreen = false;
    private bool m_initialized = false;
    private InputBaseMiniMap inputHandler;
    private bool wasCursorVisible = false;
    private CursorLockMode wasCursorMode = CursorLockMode.None;
    private Vector3 m_mapRotationOffsetVector = Vector3.zero;
    #endregion
    void Awake()
    {
        if (!m_initialized)
        {
            inputHandler = MiniMapDataBD.Instance.inputHandler;
            if (inputHandler != null) inputHandler.Init();

            MiniMapUI?.Setup(this);
            MiniMapUI.MiniMapSize?.Init(this);
            GetMiniMapSize();
            DefaultRotationMode = DynamicRotation;
            DeafultMapRot = minimapRig.eulerAngles;
            defaultShape = mapShape;
            m_mapRotationOffsetVector.Set(0, mapRotationOffset, 0);
            
            if (hasError) return;

            mapBounds?.Init();
            SetupMiniMapCamera();
            CreateMapPlane(renderType == RenderType.RealTime);

            if (mapMode == MapType.Local)
            {
                if (saveZoomInRuntime) Zoom = PlayerPrefs.GetFloat(MMHeightKey, DefaultHeight);
                else Zoom = DefaultHeight;
            }
            else
            {
                ConfigureWorldTarget();
                Zoom = DefaultHeight;
            }
        }

        MiniMapUI.DoStartFade(0, () => { isAlphaComplete = true; });
        m_initialized = true;
    }


    private void Start()
    {
        if (ActiveMiniMap == null) ActiveMiniMap = this;
        Target = TargetMinimapPlayer();
    }


    void OnEnable()
    {
        if (!isAlphaComplete) MiniMapUI.DoStartFade(0, () => { isAlphaComplete = true; });
    }

    void CreateMapPlane(bool realTime)
    {
        if (isPlanedCreated) return;
        if (mapRender == null && !realTime)
        {
            Debug.LogError("Não tem maprender");
            return;
        }
        isPlanedCreated = true;
    }

  
    private void SetupMiniMapCamera()
    {
        //Verify is MiniMap Layer Exist in Layer Mask List.
        string layer = LayerMask.LayerToName(MiniMapLayer);
        //If not exist.
        if (string.IsNullOrEmpty(layer))
        {
            int tryID = LayerMask.NameToLayer("MiniMap");
            if (tryID == -1)
            {
                Debug.LogError($"MiniMap Layer '{tryID}' is null, please assign it in the inspector.", gameObject);
                MiniMapUI.SetActive(false);
                hasError = true;
                enabled = false;
                return;
            }
            else
            {
                MiniMapLayer = tryID;
            }
        }
        

        if (renderType == RenderType.Picture)
        {
            miniMapCamera.cullingMask = 1 << MiniMapLayer;
        }

        if (useNonRenderLayer)
        {
            miniMapCamera.cullingMask = miniMapCamera.cullingMask & ~(1 << nonRenderLayer);
        }
    }

    private Transform TargetMinimapPlayer(){
        Transform t = GameObject.FindGameObjectWithTag("TargetPlayerMiniMap").transform;
        return t;
    }
    public void ConfigureWorldTarget()
    {
        if (m_Target == null)
            return;

        var mmi = m_Target.AddComponent<MiniMapEntity>();
        MiniMapUI.ConfigureWorldTarget(mmi);
    }


    void Update()
    {
        if (hasError) return;
        if (m_Target == null || miniMapCamera == null)
            return;
        isUpdateFrame = (Time.frameCount % UpdateRate) == 0;

        //Controlled inputs key for minimap
        Inputs();
        //controlled that minimap follow the target
        PositionControll();
        //Apply rotation settings
        RotationControll();
        //for minimap and world map control
        MapZoomControl();
        //update all items (icons)
        UpdateItems();
    }
    void PositionControll()
    {
        if (mapMode == MapType.Local)
        {
            if (isUpdateFrame)
            {
                playerPosition = minimapRig.position;
                targetPosition = Target.position;
                // Update the transformation of the camera as per the target's position.
                playerPosition.x = targetPosition.x;
                if (!Ortographic2D)
                {
                    playerPosition.z = targetPosition.z;
                }
                else
                {
                    playerPosition.y = targetPosition.y;
                }
                playerPosition += DragOffset;

                //Calculate player position
                if (Target != null)
                {
                    Vector3 pp = miniMapCamera.WorldToViewportPoint(TargetPosition);
                    MiniMapUI.PlayerIconTransform.anchoredPosition = MiniMapUtils.CalculateMiniMapPosition(pp, MiniMapUI.root);
                }

                // For this, we add the predefined (but variable, see below) height var.
                if (!Ortographic2D)
                {
                    playerPosition.y = Target.TransformPoint(Vector3.up * 200).y;
                }
                else
                {
                    playerPosition.z = (targetPosition.z * 2) - (MaxZoom + MinZoom * 0.5f);
                }
            }
            //Camera follow the target
            if (lerpTrackingPosition)
            {
                minimapRig.position = Vector3.Lerp(minimapRig.position, playerPosition, Time.deltaTime * 10);
            }
            else
            {
                minimapRig.position = playerPosition;
            }
        }
    }


    void RotationControll()
    {
        if (DynamicRotation && mapMode != MapType.Global)
        {
            if (isUpdateFrame)
            {
                //get local reference.
                playerRotation = minimapRig.eulerAngles;
                playerRotation.y = Target.eulerAngles.y;
            }
            if (SmoothRotation)
            {
                if (isUpdateFrame)
                {
                    if (canvasRenderMode == RenderMode.Mode2D)
                    {
                        //For 2D Mode
                        MiniMapUI.PlayerIconTransform.eulerAngles = Vector3.zero;
                    }
                    else
                    {
                        //For 3D Mode
                        MiniMapUI.PlayerIconTransform.localEulerAngles = Vector3.zero;
                    }
                }

                // Lerp rotation of map
                minimapRig.rotation = Quaternion.Slerp(minimapRig.rotation, Quaternion.Euler(playerRotation), Time.smoothDeltaTime * LerpRotation);
            }
            else
            {
                minimapRig.eulerAngles = playerRotation;
            }
        }
        else
        {
            m_mapRotationOffsetVector.y = mapRotationOffset;
            minimapRig.eulerAngles = DeafultMapRot + m_mapRotationOffsetVector;
            if (canvasRenderMode == RenderMode.Mode2D)
            {
                //When map rotation is static, only rotate the player icon
                Vector3 e = Vector3.zero;
                //get and fix the correct angle rotation of target
                e.z = -Target.eulerAngles.y + mapRotationOffset;
                MiniMapUI.PlayerIconTransform.eulerAngles = e;
            }
            else
            {
                //Use local rotation in 3D mode.
                Vector3 tr = Target.localEulerAngles;
                Vector3 r = Vector3.zero;
                r.z = -tr.y;
                MiniMapUI.PlayerIconTransform.localEulerAngles = r;
            }
        }
    }


    void UpdateItems()
    {
        if (!isUpdateFrame) return;
        if (miniMapItems == null || miniMapItems.Count <= 0) return;

        for (int i = miniMapItems.Count - 1; i >= 0; i--)
        {
            if (miniMapItems[i] == null) { miniMapItems.RemoveAt(i); continue; }
            miniMapItems[i].OnUpdateItem();
        }
    }
    
    void Inputs()
    {
        if (inputHandler == null) return;


        if (inputHandler.IsInputDown(InputBaseMiniMap.MiniMapInput.ScreenMode))
        {
            ToggleSize();
        }
        if (inputHandler.IsInputDown(InputBaseMiniMap.MiniMapInput.ZoomOut))
        {
            ChangeZoom(true);
        }
        if (inputHandler.IsInputDown(InputBaseMiniMap.MiniMapInput.ZoomIn))
        {
            ChangeZoom(false);
        }
    }


    void MapZoomControl()
    {
        float delta = Time.deltaTime;
        float zoom = Mathf.Lerp(miniMapCamera.orthographicSize, Zoom, delta * LerpHeight);
        zoom = Mathf.Max(1, zoom);
        miniMapCamera.orthographicSize = zoom;
    }


    void ToggleSize()
    {
        isFullScreen = !isFullScreen;
        MiniMapEntity[] items = FindObjectsOfType<MiniMapEntity>();
        foreach (MiniMapEntity item in items)
        {
            item.OffScreen = !isFullScreen; 
        }
        if (isFullScreen) SetToFullscreenSize();
        else SetToMiniMapSize();
    }

    public void SetToMiniMapSize()
    {
        isFullScreen = false;
        if (FadeOnFullScreen) { MiniMapUI.DoStartFade(0.35f, null); }
        if (mapMode != MapType.Global)
        {
            //when return of full screen, return to current height
            Zoom = PlayerPrefs.GetFloat(MMHeightKey, DefaultHeight);
        }
        if (mapShape != defaultShape) { mapShape = defaultShape; }
        MiniMapUI.minimapMaskManager?.ChangeMaskType(false);
        if (DynamicRotation != DefaultRotationMode) { DynamicRotation = DefaultRotationMode; }

        if (showCursorOnFullscreen)
        {
            Cursor.visible = wasCursorVisible;
            Cursor.lockState = wasCursorMode;
        }

        MiniMapOverlay.Instance?.SetActive(isFullScreen);
        

        MiniMapUI.MiniMapSize?.DoTransition();
    }


    public void SetToFullscreenSize()
    {
        isFullScreen = true;
        if (FadeOnFullScreen) { MiniMapUI.DoStartFade(0.35f, null); }
        if (mapMode != MapType.Global)
        {
            //when change to full screen, the height is the max
            Zoom = MaxZoom;
        }
        mapShape = MapShape.Circle;
        MiniMapUI.minimapMaskManager?.ChangeMaskType(true);
        if (DynamicRotation) { DynamicRotation = false; ResetMapRotation(); }
        

        MiniMapOverlay.Instance?.SetActive(isFullScreen);
        //reset offset position 
        if (ResetOffSetOnChange) { GoToTarget(); }

        MiniMapUI.MiniMapSize?.DoTransition();
    }
    public void GoToTarget()
    {
        StopCoroutine("ResetOffset");
        StartCoroutine("ResetOffset");
    }
    
    public void SetAsActiveMiniMap()
    {
        if (ActiveMiniMap == this) return;

        var othersMinimaps = FindObjectsOfType<SystemMiniMap>();
        for (int i = 0; i < othersMinimaps.Length; i++)
        {
            othersMinimaps[i].SetActive(false);
        }

        SetActive(true);
        if (ActiveMiniMap != null)
        {
            ActiveMiniMap.TransferIconsTo(this);
        }
        ActiveMiniMap = this;
        MiniMapEvents.onActiveMiniMapChanged?.Invoke(this);
    }


    public void TransferIconsTo(SystemMiniMap otherMinimap)
    {
        foreach (var item in miniMapItems)
        {
            if (item == null) continue;

            item.ChangeMiniMapOwner(otherMinimap);

            if (!otherMinimap.miniMapItems.Contains(item))
            {
                otherMinimap.miniMapItems.Add(item);
            }
        }
    }


    public void SetActive(bool active, bool onlyUI = false)
    {
        if (!onlyUI)
        {
            gameObject.SetActive(active);
            if (miniMapPlane != null) miniMapPlane.SetActive(active);
        }
        else MiniMapUI.SetActive(active);
    }


    public void SetDragPosition(Vector3 pos)
    {
        if (DragOnlyOnFullScreen)
        {
            if (!isFullScreen)
                return;
        }

        DragOffset.x += ((-pos.x) * DragMovementSpeed.x);
        DragOffset.z += ((-pos.y) * DragMovementSpeed.y);

        DragOffset.x = Mathf.Clamp(DragOffset.x, -MaxOffSetPosition.x, MaxOffSetPosition.x);
        DragOffset.z = Mathf.Clamp(DragOffset.z, -MaxOffSetPosition.y, MaxOffSetPosition.y);
    }
    

    
    IEnumerator ResetOffset()
    {
        while (Vector3.Distance(DragOffset, Vector3.zero) > 0.2f)
        {
            DragOffset = Vector3.Lerp(DragOffset, Vector3.zero, Time.deltaTime * 12);
            yield return null;
        }
        DragOffset = Vector3.zero;
    }

    
    public void ChangeZoom(bool zoomIn)
    {
        if (mapMode == MapType.Global)
            return;

        if (zoomIn) Zoom += scrollSensitivity;
        else Zoom -= scrollSensitivity;

        Zoom = Mathf.Clamp(Zoom, MinZoom, MaxZoom);
        if (saveZoomInRuntime) PlayerPrefs.SetFloat(MMHeightKey, Zoom);
    }

    
    public void DoHitEffect() => MiniMapUI?.DoHitEffect();

    
    public MiniMapEntityBase CreateNewItem(MiniMapIconSettings item)
    {
        if (hasError) return null;

        GameObject newItem = Instantiate(ItemPrefabSimple, item.Position, Quaternion.identity) as GameObject;
        var mmItem = newItem.GetComponent<MiniMapEntityBase>();

        mmItem.SetIconSettings(item);

        return mmItem;
    }

    
    void ResetMapRotation() { minimapRig.eulerAngles = new Vector3(90, 0, 0); }

    
    public void ChangeMapSize(bool fullscreen)
    {
        isFullScreen = fullscreen;
    }

    
    public void SetTarget(GameObject t)
    {
        m_Target = t;
    }

   
    public void SetMapTexture(Texture2D newTexture)
    {
        if (renderType != RenderType.Picture)
        {
            Debug.LogWarning("You only can set texture in Picture Mode");
            return;
        }
        miniMapPlane.SetMapTexture(newTexture);
    }

#if UNITY_EDITOR
    public void OnValidate()
    {
        if (miniMapCamera != null)
        {
            miniMapCamera.orthographicSize = DefaultHeight;
        }
        if (MiniMapUI != null && MiniMapUI.playerIcon != null)
        {
            MiniMapUI.playerIcon.SetIcon(PlayerIconSprite, true);
            MiniMapUI.playerIcon.SetColor(playerColor);
        }

        if (MiniMapUI != null)
        {
            if (MiniMapUI.rootAlpha != null) MiniMapUI.rootAlpha.alpha = overallOpacity;
        }
    }
#endif


    
    public void SetActiveGrid(bool active)
    {
        if (miniMapPlane == null) return;

        miniMapPlane.SetActiveGrid(active);
    }

   
    public void SetMapRotationMode(bool dynamic)
    {
        if (isFullScreen) return; 

        DynamicRotation = dynamic;
        DefaultRotationMode = dynamic;
    }

   
    public void GetMiniMapSize()
    {
        var root = MiniMapUI.root;
        MiniMapSize = root.sizeDelta;
        MiniMapPosition = root.anchoredPosition;
        MiniMapRotation = root.eulerAngles;
    }

    
    public void RegisterItem(MiniMapEntityBase item)
    {
        if (miniMapItems.Contains(item)) return;

        miniMapItems.Add(item);
    }

    
    public void RemoveItem(MiniMapEntityBase item)
    {
        miniMapItems.Remove(item);
    }

  
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnSceneLoad()
    {
        ActiveMiniMap = null;
    }

    
    public Transform Target
    {
        get
        {
            if (m_Target != null)
            {
                return m_Target.GetComponent<Transform>();
            }
            return this.GetComponent<Transform>();
        }
        set
        {
            m_Target = value.gameObject;
        }
    }

    
    public Vector3 TargetPosition
    {
        get
        {
            Vector3 v = Vector3.zero;
            if (m_Target != null)
            {
                v = m_Target.transform.position;
            }
            return v;
        }
    }

    
    public bool HasTarget() => m_Target != null;
}