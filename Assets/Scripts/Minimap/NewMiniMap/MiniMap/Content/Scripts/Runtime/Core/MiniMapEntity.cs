using UnityEngine;
using UnityEngine.UI;
using System.MiniMap;
using System;


public class MiniMapEntity : MiniMapEntityBase
{

    [Serializable]
    public enum IconFaceDirection
    {
        UseGlobalSetting,
        AlwaysUp,
        TargetDirection
    }
    #region Public members
    [Header("ALVO")]
    [Tooltip("Transform que o ícone da interface seguirá")]
    public Transform Target = null;
    [Tooltip("Posição personalizada em relação à posição do alvo")]
    public Vector3 OffSet = Vector3.zero;

    [Header("ÍCONE")]
    public IconDataMap iconData;

    [Header("CONFIGURAÇÕES")]
    [Tooltip("O ícone pode ser exibido quando está fora da tela?")]
    public bool OffScreen = true;
    public bool opacityBasedDistance = false;
    public float maxDistance = 250;
    public IconFaceDirection iconFaceDirection = IconFaceDirection.UseGlobalSetting;
    public bool DestroyWithObject = true;
    [Range(0, 5)] public float BorderOffScreen = 0.01f;
    [Tooltip("Tempo antes de renderizar/exibir o item no minimapa após a instância")]
    [Range(0, 3)] public float RenderDelay = 0.3f;
    public ItemEffect m_Effect = ItemEffect.None;

    #endregion

    #region Public properties
    public IconBaseMiniMap IconInstance { get; private set; }

   

    public override Transform GetTarget
    {
        get => Target;
    }
    #endregion

    #region Private members
    private Image Graphic = null;
    private RectTransform GraphicRect;
    private RectTransform RectRoot;
    private GameObject cacheItem = null;
    private RectTransform CircleAreaRect = null;
    private Vector2 position;
    private bool clampedY, clampedX = false;
    private Vector2 viewPortSize, viewPortFullSize;
    private Vector2 borderMarging = Vector2.zero;
    private Vector2 targetSize = Vector2.one;
    private IconDataMap _instance;
    private float targetOpacity;
    #endregion

    /// <summary>
    /// Get all required component in start
    /// </summary>
    void Start()
    {
        if (MiniMapOwner != null)
        {
            CreateIcon();
            MiniMapOwner.RegisterItem(this);
        }
        else { Debug.Log("You need a MiniMap in scene for use MiniMap Items."); }
    }

    /// <summary>
    /// 
    /// </summary>
    void CreateIcon()
    {
        if (MiniMapOwner.hasError || !this.enabled) return;
        //Instantiate UI in canvas
        GameObject g = MiniMapDataBD.Instance.IconPrefab;
        cacheItem = Instantiate(g) as GameObject;
        cacheItem.name = $"Icon ({gameObject.name})";
        RectRoot = OffScreen ? MiniMapOwner.MiniMapUI.root : MiniMapOwner.MiniMapUI.iconsPanel;
        //SetUp Icon UI
        IconInstance = cacheItem.GetComponent<IconBaseMiniMap>();
        IconInstance.SetUp(this);
        Graphic = IconInstance.GetImage;
        GraphicRect = Graphic.GetComponent<RectTransform>();
        if (Icon != null) { Graphic.sprite = Icon; Graphic.color = IconColor; }
        cacheItem.transform.SetParent(RectRoot.transform, false);
        GraphicRect.anchoredPosition = Vector2.zero;
        if (Target == null) { Target = transform; }
        StartEffect();
        IconInstance.SpawnedDelayed(RenderDelay);
        viewPortFullSize = RectRoot.rect.size;
        viewPortSize = viewPortFullSize * 0.5f;
        borderMarging = Vector2.one * BorderOffScreen;
    }


    public override void ChangeMiniMapOwner(SystemMiniMap newOwner)
    {
        _minimap = newOwner;
        RectRoot = OffScreen ? MiniMapOwner.MiniMapUI.root : MiniMapOwner.MiniMapUI.iconsPanel;
        cacheItem.transform.SetParent(RectRoot.transform, false);
    }


    public override void OnUpdateItem()
    {
        //If a component missing, return for avoid bugs.
        if (Target == null || MiniMapOwner == null)
            return;
        if (Graphic == null)
            return;

        if (Time.frameCount % 30 == 0 || MiniMapOwner.HighPrecisionMode) OnFrameUpdate();
        IconControl();
        OpacityControl();
    }


    void IconControl()
    {
        //Setting the modify position
        Vector3 CorrectPosition = TargetPosition + OffSet;
        //Convert the position of target in ViewPortPoint
        Vector2 wvp = MiniMapOwner.miniMapCamera.WorldToViewportPoint(CorrectPosition);
        //Calculate the position of target and convert into position of screen
        position.Set((wvp.x * viewPortFullSize.x) - viewPortSize.x, (wvp.y * viewPortFullSize.y) - viewPortSize.y);
        Vector2 UnClampPosition = position;

        //calculate the position of UI again, determine if off screen
        //if off screen reduce the size
        float Iconsize = Size;
        if (OffScreen)
        {
            if (MiniMapOwner.mapShape == SystemMiniMap.MapShape.Circle)
            {
                // Clamp the icon position inside the circle area
                if (position.sqrMagnitude > (MiniMapOwner.CompassSize * MiniMapOwner.CompassSize))
                {
                    position = position.normalized * MiniMapOwner.CompassSize;
                    Iconsize = IconOffScreenSize;
                }
                else
                {
                    Iconsize = Size;
                }
            }
            else
            {
                Vector2 border = viewPortSize + borderMarging;
                position.x = MiniMapUtils.ClampBorders(position.x, -border.x, border.x, out clampedX);
                position.y = MiniMapUtils.ClampBorders(position.y, -border.y, border.y, out clampedY);
                //check if the icon is out of the minimap bounds
                if (clampedX || clampedY)
                {
                    Iconsize = IconOffScreenSize;
                }
                else
                {
                    Iconsize = Size;
                }
            }
        }


        GraphicRect.anchoredPosition = position;
        if (CircleAreaRect != null) { CircleAreaRect.anchoredPosition = UnClampPosition; }
        //Change size with smooth transition
        targetSize = Vector2.one * (Iconsize * MiniMapOwner.IconMultiplier);
        GraphicRect.sizeDelta = Vector2.Lerp(GraphicRect.sizeDelta, targetSize, Time.deltaTime * 8);

        if (GetFaceDirection() == IconFaceDirection.AlwaysUp)
        {
            //with this the icon rotation always will facing up
            if (MiniMapOwner.canvasRenderMode == SystemMiniMap.RenderMode.Mode2D) { GraphicRect.up = Vector3.up; }
            else
            {
                Quaternion r = Quaternion.identity;
                r.x = Target.rotation.x;
                GraphicRect.localRotation = r;
            }
        }
        else
        {

            Vector3 vre = MiniMapOwner.minimapRig.eulerAngles;
            Vector3 re = MiniMapOwner.canvasRenderMode == SystemMiniMap.RenderMode.Mode2D ? Vector3.zero : GraphicRect.eulerAngles;
            //Fix player rotation for apply to el icon.
            re.z = ((-Target.rotation.eulerAngles.y) + vre.y);
            Quaternion q = Quaternion.Euler(re);
            GraphicRect.rotation = q;
            
        }
    }


    void OnFrameUpdate()
    {
        viewPortFullSize = RectRoot.rect.size;
        viewPortSize = viewPortFullSize * 0.5f;

        if (opacityBasedDistance && MiniMapOwner != null && MiniMapOwner.HasTarget())
        {
            if (MiniMapOwner.isFullScreen)
            {
                targetOpacity = 1;
            }
            else
            {
                float targetDistance = Vector3.Distance(TargetPosition, MiniMapOwner.Target.position);
                float percentage = targetDistance / maxDistance;
                percentage = Math.Min(1, percentage);
                if (percentage < 0.2f) percentage = 0;
                targetOpacity = 1 - percentage;
            }
        }
    }


    private void OpacityControl()
    {
        if (!opacityBasedDistance || IconInstance == null) return;

        IconInstance.Opacity = Mathf.Lerp(IconInstance.Opacity, targetOpacity, Time.deltaTime * 8);
    }

    /// <summary>
    /// 
    /// </summary>
    void StartEffect()
    {
        Animator a = Graphic.GetComponent<Animator>();
        if(a == null) return;

        if (m_Effect == ItemEffect.Pulsing)
        {
            a.SetInteger("Type", 2);
        }
        else if (m_Effect == ItemEffect.Fade)
        {
            a.SetInteger("Type", 1);
        }
        else
        {
            Destroy(a);
        }
    }
    
    public override void DestroyIcon(bool inmediate)
    {
        if (IconInstance == null) return;

        if (DeathIcon == null || inmediate)
        {
            IconInstance.DestroyIcon(inmediate);
        }
        else
        {
            IconInstance.DestroyIcon(inmediate, DeathIcon);
        }
    }
    public override void SetTarget(Transform newTarget)
    {
        Target = newTarget;
    }

    public override void SetIcon(Sprite newIcon)
    {
        if (IconInstance == null) return;

        IconInstance.SetIcon(newIcon);
    }

    public override void SetIconColor(Color newIconColor)
    {
        IconColor = newIconColor;

        if (IconInstance == null) return;

        IconInstance.SetColor(newIconColor);
    }

    public override void SetActiveIcon(bool active)
    {
        if (active) ShowItem();
        else HideItem();
    }
    
    public void HideItem()
    {
        if (cacheItem != null)
        {
            cacheItem.SetActive(false);
        }
        else
        {
            Debug.Log("There is no item to disable.");
        }
    }


    public void ShowItem()
    {
        if (IconInstance != null)
        {
            cacheItem.SetActive(true);
            IconInstance.SetOpacity(1);
        }
        else
        {
            Debug.Log("There is no item to active.");
        }
    }
    public override void SetIconSettings(MiniMapIconSettings iconSettings)
    {
        Icon = iconSettings.Icon;
        IconColor = iconSettings.Color;
        Target = iconSettings.Target;
        Size = iconSettings.Size;
        m_Effect = iconSettings.ItemEffect;
    }


    void OnDestroy()
    {
        if (MiniMapOwner != null) MiniMapOwner.RemoveItem(this);
        if (DestroyWithObject)
        {
            DestroyIcon(true);
        }
    }

    public Vector3 TargetPosition
    {
        get
        {
            if (Target == null)
            {
                return Vector3.zero;
            }

            return new Vector3(Target.position.x, 0, Target.position.z);
        }
    }


    private IconFaceDirection GetFaceDirection()
    {
        if (iconFaceDirection == IconFaceDirection.UseGlobalSetting)
        {
            if (MiniMapOwner.iconsAlwaysFacingUp) return IconFaceDirection.AlwaysUp;
            return IconFaceDirection.TargetDirection;
        }
        else return iconFaceDirection;
    }

    public Sprite Icon
    {
        get
        {
            if (_instance != null) return _instance.Icon;

            if (iconData == null) return null;
            return iconData.Icon;
        }
        set
        {
            if (iconData == null)
            {
                iconData = MiniMapDataBD.Instance.empty;
            }

            if (_instance == null) _instance = Instantiate(iconData);
            _instance.Icon = value;
        }
    }

    public Sprite DeathIcon
    {
        get
        {
            if (_instance != null) return _instance.DeathIcon;

            if (iconData == null) return null;
            return iconData.DeathIcon;
        }
        set
        {
            if (iconData == null)
            {
                iconData = MiniMapDataBD.Instance.empty;
            }

            if (_instance == null) _instance = Instantiate(iconData);
            _instance.DeathIcon = value;
        }
    }


    public Color IconColor
    {
        get
        {
            if (_instance != null) return _instance.IconColor;

            if (iconData == null) return Color.white;
            return iconData.IconColor;
        }
        set
        {
            if (iconData == null)
            {
                iconData = MiniMapDataBD.Instance.empty;
            }

            if (_instance == null) _instance = Instantiate(iconData);
            _instance.IconColor = value;
        }
    }


    public float Size
    {
        get
        {
            if (_instance != null) return _instance.Size;

            if (iconData == null) return 0;
            return iconData.Size;
        }
        set
        {
            if (iconData == null)
            {
                iconData = MiniMapDataBD.Instance.empty;
            }

            if (_instance == null) _instance = Instantiate(iconData);
            _instance.Size = value;
        }
    }

    public float IconOffScreenSize
    {
        get
        {
            if (_instance != null) return _instance.OffScreenSize;

            if (iconData == null) return 0;
            return iconData.OffScreenSize;
        }
        set
        {
            if (iconData == null)
            {
                iconData = MiniMapDataBD.Instance.empty;
            }

            if (_instance == null) _instance = Instantiate(iconData);
            _instance.OffScreenSize = value;
        }
    }

    private SystemMiniMap _minimap = null;
    private SystemMiniMap MiniMapOwner
    {
        get
        {
            if (_minimap == null)
            {
                _minimap = MiniMapUtils.GetMiniMap();
            }
            return _minimap;
        }
    }
}