//------------------------------------------------------------------------------
// <auto-generated>
//     This code was auto-generated by com.unity.inputsystem:InputActionCodeGenerator
//     version 1.7.0
//     from Assets/Vídeo/PlayerControls.inputactions
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public partial class @PlayerControls: IInputActionCollection2, IDisposable
{
    public InputActionAsset asset { get; }
    public @PlayerControls()
    {
        asset = InputActionAsset.FromJson(@"{
    ""name"": ""PlayerControls"",
    ""maps"": [
        {
            ""name"": ""Player Movement"",
            ""id"": ""64d3e07c-4c25-4468-828a-8c646eb5be7d"",
            ""actions"": [
                {
                    ""name"": ""Movement"",
                    ""type"": ""PassThrough"",
                    ""id"": ""04286049-d856-4dfb-919c-5e9970705fd4"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""2D Vector"",
                    ""id"": ""57ae3a4d-00ee-4138-a644-0dc7f4e2937f"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""82f7b64c-76a1-4f99-893d-2b0ae365cad3"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""5dfdc354-d4ab-4c9d-9bc4-599918cae8e2"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""4cfc3024-6b9d-44e4-9fde-ee22ba15c539"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""3725826f-ab36-44a2-8fd0-e9664dfff278"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                }
            ]
        },
        {
            ""name"": ""Player Actions"",
            ""id"": ""8f5139c1-d485-499f-8110-9e347e073605"",
            ""actions"": [
                {
                    ""name"": ""Dodge"",
                    ""type"": ""Button"",
                    ""id"": ""c83453e9-51b0-48b2-93df-217c3367c993"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": ""Tap"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Jump"",
                    ""type"": ""Button"",
                    ""id"": ""db19f866-8fe1-46e8-af72-eecd596a70f4"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""RB"",
                    ""type"": ""Button"",
                    ""id"": ""c39effda-0895-44ca-ba10-7fdffbca2f09"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""RT"",
                    ""type"": ""Button"",
                    ""id"": ""1200d899-abd7-4784-b003-bc393917b9a9"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Hold RT"",
                    ""type"": ""PassThrough"",
                    ""id"": ""ba25bc3f-5619-4c46-ba77-05ffb946923e"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": ""Hold(pressPoint=0.1)"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Sprint"",
                    ""type"": ""PassThrough"",
                    ""id"": ""507a9bd5-33b2-4142-a2f2-89f44aba1b5c"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": ""Hold"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Lock On"",
                    ""type"": ""Button"",
                    ""id"": ""e1ed310f-35a8-406f-815c-3616f959dfac"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Seek Left Lock On Target"",
                    ""type"": ""Button"",
                    ""id"": ""50afc21d-520d-4775-ae71-da3edb862ae6"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Seek Right Lock On Target"",
                    ""type"": ""Button"",
                    ""id"": ""1fbba268-60a6-496b-8fa4-fb80c5dc8a55"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Switch Right Weapon"",
                    ""type"": ""Button"",
                    ""id"": ""c2440bbb-6b28-4945-85f4-d0524448b5fb"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Switch Left Weapon"",
                    ""type"": ""Button"",
                    ""id"": ""8b5c7c4d-6114-4b0d-bd0a-0fa128329645"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""90ef3c6c-7cf9-4445-a445-ab6baa1f4416"",
                    ""path"": ""<Keyboard>/ctrl"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Dodge"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""1d252085-ac22-49a9-912b-967b0c8b9a9e"",
                    ""path"": ""<Keyboard>/shift"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Sprint"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""29abbe0c-2725-4278-a952-c0e8b9a0bb0a"",
                    ""path"": ""<Keyboard>/space"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Jump"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""56cd4076-afd6-4afe-a2f7-aae87edde51a"",
                    ""path"": ""<Mouse>/leftButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""RB"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""6f0f5105-d5c5-4848-911f-b8639abeaca1"",
                    ""path"": ""<Mouse>/middleButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Lock On"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""20c9fd86-e3ca-498a-a689-dbb061b1cd83"",
                    ""path"": ""<Keyboard>/leftArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Seek Left Lock On Target"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""ebe07ce3-181d-4984-811e-6648a30778fa"",
                    ""path"": ""<Keyboard>/rightArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Seek Right Lock On Target"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""df5981cf-f627-4872-a861-6c576631e42c"",
                    ""path"": ""<Mouse>/rightButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""RT"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""87104f32-1ee1-41ab-982b-7621e6ecf1f9"",
                    ""path"": ""<Mouse>/rightButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Hold RT"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""47ffcfb5-e9ce-4356-80ef-2a32ddc73feb"",
                    ""path"": ""<Keyboard>/1"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Switch Right Weapon"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""d1a076a2-69bc-4733-894c-cb2b4151c4ef"",
                    ""path"": ""<Keyboard>/2"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Switch Left Weapon"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        },
        {
            ""name"": ""Player Camera"",
            ""id"": ""18b43deb-ac9b-49b9-9ba6-419fa5ae1f3f"",
            ""actions"": [
                {
                    ""name"": ""Movement"",
                    ""type"": ""PassThrough"",
                    ""id"": ""1eea247d-96e5-4837-a48f-6cc3fd861c04"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""2D Vector"",
                    ""id"": ""9f41ac80-9e83-49b3-8645-c1cf084709fa"",
                    ""path"": ""2DVector(mode=2)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""d890b648-6b27-43d6-8764-87b64e5390a2"",
                    ""path"": ""<Mouse>/delta/up"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""f38840f3-86fb-41ac-a8e7-cb1c2cd5f5cc"",
                    ""path"": ""<Mouse>/delta/down"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""b1b1eb77-27e6-4a93-ac08-ebbaeb409ac0"",
                    ""path"": ""<Mouse>/delta/left"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""b8a2487b-3af8-4758-b461-efba3913feeb"",
                    ""path"": ""<Mouse>/delta/right"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                }
            ]
        },
        {
            ""name"": ""UI"",
            ""id"": ""4f7703d9-e503-4517-ad0a-c0372682612f"",
            ""actions"": [
                {
                    ""name"": ""X"",
                    ""type"": ""Button"",
                    ""id"": ""f0e3cc88-13d1-4bba-96e9-04baca9c7a2b"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": ""Tap"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""PauseBack"",
                    ""type"": ""Button"",
                    ""id"": ""b6183ee7-4250-4bf3-a052-19f6abd32624"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": ""Tap"",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""7f9a5300-3753-4244-a63a-84eb0b3189c6"",
                    ""path"": ""<Keyboard>/backspace"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""X"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""34cc99ec-b028-4ecb-a87e-3831dbf33623"",
                    ""path"": ""<Keyboard>/escape"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""PauseBack"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
        // Player Movement
        m_PlayerMovement = asset.FindActionMap("Player Movement", throwIfNotFound: true);
        m_PlayerMovement_Movement = m_PlayerMovement.FindAction("Movement", throwIfNotFound: true);
        // Player Actions
        m_PlayerActions = asset.FindActionMap("Player Actions", throwIfNotFound: true);
        m_PlayerActions_Dodge = m_PlayerActions.FindAction("Dodge", throwIfNotFound: true);
        m_PlayerActions_Jump = m_PlayerActions.FindAction("Jump", throwIfNotFound: true);
        m_PlayerActions_RB = m_PlayerActions.FindAction("RB", throwIfNotFound: true);
        m_PlayerActions_RT = m_PlayerActions.FindAction("RT", throwIfNotFound: true);
        m_PlayerActions_HoldRT = m_PlayerActions.FindAction("Hold RT", throwIfNotFound: true);
        m_PlayerActions_Sprint = m_PlayerActions.FindAction("Sprint", throwIfNotFound: true);
        m_PlayerActions_LockOn = m_PlayerActions.FindAction("Lock On", throwIfNotFound: true);
        m_PlayerActions_SeekLeftLockOnTarget = m_PlayerActions.FindAction("Seek Left Lock On Target", throwIfNotFound: true);
        m_PlayerActions_SeekRightLockOnTarget = m_PlayerActions.FindAction("Seek Right Lock On Target", throwIfNotFound: true);
        m_PlayerActions_SwitchRightWeapon = m_PlayerActions.FindAction("Switch Right Weapon", throwIfNotFound: true);
        m_PlayerActions_SwitchLeftWeapon = m_PlayerActions.FindAction("Switch Left Weapon", throwIfNotFound: true);
        // Player Camera
        m_PlayerCamera = asset.FindActionMap("Player Camera", throwIfNotFound: true);
        m_PlayerCamera_Movement = m_PlayerCamera.FindAction("Movement", throwIfNotFound: true);
        // UI
        m_UI = asset.FindActionMap("UI", throwIfNotFound: true);
        m_UI_X = m_UI.FindAction("X", throwIfNotFound: true);
        m_UI_PauseBack = m_UI.FindAction("PauseBack", throwIfNotFound: true);
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    public bool Contains(InputAction action)
    {
        return asset.Contains(action);
    }

    public IEnumerator<InputAction> GetEnumerator()
    {
        return asset.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Enable()
    {
        asset.Enable();
    }

    public void Disable()
    {
        asset.Disable();
    }

    public IEnumerable<InputBinding> bindings => asset.bindings;

    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
    {
        return asset.FindAction(actionNameOrId, throwIfNotFound);
    }

    public int FindBinding(InputBinding bindingMask, out InputAction action)
    {
        return asset.FindBinding(bindingMask, out action);
    }

    // Player Movement
    private readonly InputActionMap m_PlayerMovement;
    private List<IPlayerMovementActions> m_PlayerMovementActionsCallbackInterfaces = new List<IPlayerMovementActions>();
    private readonly InputAction m_PlayerMovement_Movement;
    public struct PlayerMovementActions
    {
        private @PlayerControls m_Wrapper;
        public PlayerMovementActions(@PlayerControls wrapper) { m_Wrapper = wrapper; }
        public InputAction @Movement => m_Wrapper.m_PlayerMovement_Movement;
        public InputActionMap Get() { return m_Wrapper.m_PlayerMovement; }
        public void Enable() { Get().Enable(); }
        public void Disable() { Get().Disable(); }
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(PlayerMovementActions set) { return set.Get(); }
        public void AddCallbacks(IPlayerMovementActions instance)
        {
            if (instance == null || m_Wrapper.m_PlayerMovementActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_PlayerMovementActionsCallbackInterfaces.Add(instance);
            @Movement.started += instance.OnMovement;
            @Movement.performed += instance.OnMovement;
            @Movement.canceled += instance.OnMovement;
        }

        private void UnregisterCallbacks(IPlayerMovementActions instance)
        {
            @Movement.started -= instance.OnMovement;
            @Movement.performed -= instance.OnMovement;
            @Movement.canceled -= instance.OnMovement;
        }

        public void RemoveCallbacks(IPlayerMovementActions instance)
        {
            if (m_Wrapper.m_PlayerMovementActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        public void SetCallbacks(IPlayerMovementActions instance)
        {
            foreach (var item in m_Wrapper.m_PlayerMovementActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_PlayerMovementActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    public PlayerMovementActions @PlayerMovement => new PlayerMovementActions(this);

    // Player Actions
    private readonly InputActionMap m_PlayerActions;
    private List<IPlayerActionsActions> m_PlayerActionsActionsCallbackInterfaces = new List<IPlayerActionsActions>();
    private readonly InputAction m_PlayerActions_Dodge;
    private readonly InputAction m_PlayerActions_Jump;
    private readonly InputAction m_PlayerActions_RB;
    private readonly InputAction m_PlayerActions_RT;
    private readonly InputAction m_PlayerActions_HoldRT;
    private readonly InputAction m_PlayerActions_Sprint;
    private readonly InputAction m_PlayerActions_LockOn;
    private readonly InputAction m_PlayerActions_SeekLeftLockOnTarget;
    private readonly InputAction m_PlayerActions_SeekRightLockOnTarget;
    private readonly InputAction m_PlayerActions_SwitchRightWeapon;
    private readonly InputAction m_PlayerActions_SwitchLeftWeapon;
    public struct PlayerActionsActions
    {
        private @PlayerControls m_Wrapper;
        public PlayerActionsActions(@PlayerControls wrapper) { m_Wrapper = wrapper; }
        public InputAction @Dodge => m_Wrapper.m_PlayerActions_Dodge;
        public InputAction @Jump => m_Wrapper.m_PlayerActions_Jump;
        public InputAction @RB => m_Wrapper.m_PlayerActions_RB;
        public InputAction @RT => m_Wrapper.m_PlayerActions_RT;
        public InputAction @HoldRT => m_Wrapper.m_PlayerActions_HoldRT;
        public InputAction @Sprint => m_Wrapper.m_PlayerActions_Sprint;
        public InputAction @LockOn => m_Wrapper.m_PlayerActions_LockOn;
        public InputAction @SeekLeftLockOnTarget => m_Wrapper.m_PlayerActions_SeekLeftLockOnTarget;
        public InputAction @SeekRightLockOnTarget => m_Wrapper.m_PlayerActions_SeekRightLockOnTarget;
        public InputAction @SwitchRightWeapon => m_Wrapper.m_PlayerActions_SwitchRightWeapon;
        public InputAction @SwitchLeftWeapon => m_Wrapper.m_PlayerActions_SwitchLeftWeapon;
        public InputActionMap Get() { return m_Wrapper.m_PlayerActions; }
        public void Enable() { Get().Enable(); }
        public void Disable() { Get().Disable(); }
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(PlayerActionsActions set) { return set.Get(); }
        public void AddCallbacks(IPlayerActionsActions instance)
        {
            if (instance == null || m_Wrapper.m_PlayerActionsActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_PlayerActionsActionsCallbackInterfaces.Add(instance);
            @Dodge.started += instance.OnDodge;
            @Dodge.performed += instance.OnDodge;
            @Dodge.canceled += instance.OnDodge;
            @Jump.started += instance.OnJump;
            @Jump.performed += instance.OnJump;
            @Jump.canceled += instance.OnJump;
            @RB.started += instance.OnRB;
            @RB.performed += instance.OnRB;
            @RB.canceled += instance.OnRB;
            @RT.started += instance.OnRT;
            @RT.performed += instance.OnRT;
            @RT.canceled += instance.OnRT;
            @HoldRT.started += instance.OnHoldRT;
            @HoldRT.performed += instance.OnHoldRT;
            @HoldRT.canceled += instance.OnHoldRT;
            @Sprint.started += instance.OnSprint;
            @Sprint.performed += instance.OnSprint;
            @Sprint.canceled += instance.OnSprint;
            @LockOn.started += instance.OnLockOn;
            @LockOn.performed += instance.OnLockOn;
            @LockOn.canceled += instance.OnLockOn;
            @SeekLeftLockOnTarget.started += instance.OnSeekLeftLockOnTarget;
            @SeekLeftLockOnTarget.performed += instance.OnSeekLeftLockOnTarget;
            @SeekLeftLockOnTarget.canceled += instance.OnSeekLeftLockOnTarget;
            @SeekRightLockOnTarget.started += instance.OnSeekRightLockOnTarget;
            @SeekRightLockOnTarget.performed += instance.OnSeekRightLockOnTarget;
            @SeekRightLockOnTarget.canceled += instance.OnSeekRightLockOnTarget;
            @SwitchRightWeapon.started += instance.OnSwitchRightWeapon;
            @SwitchRightWeapon.performed += instance.OnSwitchRightWeapon;
            @SwitchRightWeapon.canceled += instance.OnSwitchRightWeapon;
            @SwitchLeftWeapon.started += instance.OnSwitchLeftWeapon;
            @SwitchLeftWeapon.performed += instance.OnSwitchLeftWeapon;
            @SwitchLeftWeapon.canceled += instance.OnSwitchLeftWeapon;
        }

        private void UnregisterCallbacks(IPlayerActionsActions instance)
        {
            @Dodge.started -= instance.OnDodge;
            @Dodge.performed -= instance.OnDodge;
            @Dodge.canceled -= instance.OnDodge;
            @Jump.started -= instance.OnJump;
            @Jump.performed -= instance.OnJump;
            @Jump.canceled -= instance.OnJump;
            @RB.started -= instance.OnRB;
            @RB.performed -= instance.OnRB;
            @RB.canceled -= instance.OnRB;
            @RT.started -= instance.OnRT;
            @RT.performed -= instance.OnRT;
            @RT.canceled -= instance.OnRT;
            @HoldRT.started -= instance.OnHoldRT;
            @HoldRT.performed -= instance.OnHoldRT;
            @HoldRT.canceled -= instance.OnHoldRT;
            @Sprint.started -= instance.OnSprint;
            @Sprint.performed -= instance.OnSprint;
            @Sprint.canceled -= instance.OnSprint;
            @LockOn.started -= instance.OnLockOn;
            @LockOn.performed -= instance.OnLockOn;
            @LockOn.canceled -= instance.OnLockOn;
            @SeekLeftLockOnTarget.started -= instance.OnSeekLeftLockOnTarget;
            @SeekLeftLockOnTarget.performed -= instance.OnSeekLeftLockOnTarget;
            @SeekLeftLockOnTarget.canceled -= instance.OnSeekLeftLockOnTarget;
            @SeekRightLockOnTarget.started -= instance.OnSeekRightLockOnTarget;
            @SeekRightLockOnTarget.performed -= instance.OnSeekRightLockOnTarget;
            @SeekRightLockOnTarget.canceled -= instance.OnSeekRightLockOnTarget;
            @SwitchRightWeapon.started -= instance.OnSwitchRightWeapon;
            @SwitchRightWeapon.performed -= instance.OnSwitchRightWeapon;
            @SwitchRightWeapon.canceled -= instance.OnSwitchRightWeapon;
            @SwitchLeftWeapon.started -= instance.OnSwitchLeftWeapon;
            @SwitchLeftWeapon.performed -= instance.OnSwitchLeftWeapon;
            @SwitchLeftWeapon.canceled -= instance.OnSwitchLeftWeapon;
        }

        public void RemoveCallbacks(IPlayerActionsActions instance)
        {
            if (m_Wrapper.m_PlayerActionsActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        public void SetCallbacks(IPlayerActionsActions instance)
        {
            foreach (var item in m_Wrapper.m_PlayerActionsActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_PlayerActionsActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    public PlayerActionsActions @PlayerActions => new PlayerActionsActions(this);

    // Player Camera
    private readonly InputActionMap m_PlayerCamera;
    private List<IPlayerCameraActions> m_PlayerCameraActionsCallbackInterfaces = new List<IPlayerCameraActions>();
    private readonly InputAction m_PlayerCamera_Movement;
    public struct PlayerCameraActions
    {
        private @PlayerControls m_Wrapper;
        public PlayerCameraActions(@PlayerControls wrapper) { m_Wrapper = wrapper; }
        public InputAction @Movement => m_Wrapper.m_PlayerCamera_Movement;
        public InputActionMap Get() { return m_Wrapper.m_PlayerCamera; }
        public void Enable() { Get().Enable(); }
        public void Disable() { Get().Disable(); }
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(PlayerCameraActions set) { return set.Get(); }
        public void AddCallbacks(IPlayerCameraActions instance)
        {
            if (instance == null || m_Wrapper.m_PlayerCameraActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_PlayerCameraActionsCallbackInterfaces.Add(instance);
            @Movement.started += instance.OnMovement;
            @Movement.performed += instance.OnMovement;
            @Movement.canceled += instance.OnMovement;
        }

        private void UnregisterCallbacks(IPlayerCameraActions instance)
        {
            @Movement.started -= instance.OnMovement;
            @Movement.performed -= instance.OnMovement;
            @Movement.canceled -= instance.OnMovement;
        }

        public void RemoveCallbacks(IPlayerCameraActions instance)
        {
            if (m_Wrapper.m_PlayerCameraActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        public void SetCallbacks(IPlayerCameraActions instance)
        {
            foreach (var item in m_Wrapper.m_PlayerCameraActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_PlayerCameraActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    public PlayerCameraActions @PlayerCamera => new PlayerCameraActions(this);

    // UI
    private readonly InputActionMap m_UI;
    private List<IUIActions> m_UIActionsCallbackInterfaces = new List<IUIActions>();
    private readonly InputAction m_UI_X;
    private readonly InputAction m_UI_PauseBack;
    public struct UIActions
    {
        private @PlayerControls m_Wrapper;
        public UIActions(@PlayerControls wrapper) { m_Wrapper = wrapper; }
        public InputAction @X => m_Wrapper.m_UI_X;
        public InputAction @PauseBack => m_Wrapper.m_UI_PauseBack;
        public InputActionMap Get() { return m_Wrapper.m_UI; }
        public void Enable() { Get().Enable(); }
        public void Disable() { Get().Disable(); }
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(UIActions set) { return set.Get(); }
        public void AddCallbacks(IUIActions instance)
        {
            if (instance == null || m_Wrapper.m_UIActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_UIActionsCallbackInterfaces.Add(instance);
            @X.started += instance.OnX;
            @X.performed += instance.OnX;
            @X.canceled += instance.OnX;
            @PauseBack.started += instance.OnPauseBack;
            @PauseBack.performed += instance.OnPauseBack;
            @PauseBack.canceled += instance.OnPauseBack;
        }

        private void UnregisterCallbacks(IUIActions instance)
        {
            @X.started -= instance.OnX;
            @X.performed -= instance.OnX;
            @X.canceled -= instance.OnX;
            @PauseBack.started -= instance.OnPauseBack;
            @PauseBack.performed -= instance.OnPauseBack;
            @PauseBack.canceled -= instance.OnPauseBack;
        }

        public void RemoveCallbacks(IUIActions instance)
        {
            if (m_Wrapper.m_UIActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        public void SetCallbacks(IUIActions instance)
        {
            foreach (var item in m_Wrapper.m_UIActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_UIActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    public UIActions @UI => new UIActions(this);
    public interface IPlayerMovementActions
    {
        void OnMovement(InputAction.CallbackContext context);
    }
    public interface IPlayerActionsActions
    {
        void OnDodge(InputAction.CallbackContext context);
        void OnJump(InputAction.CallbackContext context);
        void OnRB(InputAction.CallbackContext context);
        void OnRT(InputAction.CallbackContext context);
        void OnHoldRT(InputAction.CallbackContext context);
        void OnSprint(InputAction.CallbackContext context);
        void OnLockOn(InputAction.CallbackContext context);
        void OnSeekLeftLockOnTarget(InputAction.CallbackContext context);
        void OnSeekRightLockOnTarget(InputAction.CallbackContext context);
        void OnSwitchRightWeapon(InputAction.CallbackContext context);
        void OnSwitchLeftWeapon(InputAction.CallbackContext context);
    }
    public interface IPlayerCameraActions
    {
        void OnMovement(InputAction.CallbackContext context);
    }
    public interface IUIActions
    {
        void OnX(InputAction.CallbackContext context);
        void OnPauseBack(InputAction.CallbackContext context);
    }
}
