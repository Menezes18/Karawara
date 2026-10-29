using UnityEngine;
using UnityEngine.InputSystem;
using RPGKarawara;
using RPGKarawara.SkillTree;
using Unity.VisualScripting;
using System.Collections.Generic;
using PlayerInputManager = RPGKarawara.PlayerInputManager;

public class PetAttackController : MonoBehaviour
{
    public PlayerManager Owner { get; private set; }
    private Transform orbit;
    private Animator petAnimator; 
    private PlayerNetworkManager _playerNetworkManager;
    public float damageExtra = 10f;
    private bool _isAttackPressed;
    public GameObject petPrefab; // Referência ao Prefab do pet
    public float duration = 10;
    static readonly Dictionary<PlayerNetworkManager, PetAttackController> powerOwners = new Dictionary<PlayerNetworkManager, PetAttackController>();
    GameObject ownedPet;
    PlayerSkillManager ownedSkillManager;
    float previousDamageSpirit;
    bool previousPower;

    private void Awake(){
        
        // The creating skill supplies the actual user before acquiring state.
    }

   
    
    private void Start()
    {
        // Inicializações necessárias
    }

    public void Init() => BindOwner(Owner != null ? Owner : GetComponentInParent<PlayerManager>());
    public void BindOwner(PlayerManager owner)
    {
        // Init can be repeated, and a new cast can replace an older controller.
        // Retire only this owner's resources before acquiring the player's lease.
        ReleaseOwnedPet();
        Owner = owner;
        if (Owner == null || petPrefab == null) return;
        _playerNetworkManager = Owner.playerNetworkManager != null ? Owner.playerNetworkManager : Owner.GetComponent<PlayerNetworkManager>();
        var skills = Owner.Context?.Skills ?? Owner.GetComponent<PlayerSkillManager>();
        orbit = null;
        foreach (var child in Owner.GetComponentsInChildren<Transform>(true)) if (child.CompareTag("Orbit")) { orbit = child; break; }
        if (_playerNetworkManager == null || skills == null || orbit == null) return;
        if (powerOwners.TryGetValue(_playerNetworkManager, out var previous) && previous != null && previous != this)
        {
            previous.ReleaseOwnedPet();
            Destroy(previous.gameObject);
        }
        ownedSkillManager = skills;
        previousDamageSpirit = ownedSkillManager.damageSpirit;
        previousPower = _playerNetworkManager.isPowerPet.Value;
        powerOwners[_playerNetworkManager] = this;
        _playerNetworkManager.isPowerPet.Value = true;
        InstantiatePet();
        Invoke(nameof(DesativarPower), duration);
    }

    private void DesativarPower()
    {
        ReleaseOwnedPet();
        Destroy(gameObject);
    }

    // The creating skill owns the effect lifetime. Bind after Instantiate/Awake,
    // before a prefab's legacy zero-delay callback can expire this cast.
    public void BindSkillDuration(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(seconds));
        duration = seconds;
        CancelInvoke(nameof(DesativarPower));
        Invoke(nameof(DesativarPower), duration);
    }

    private void InstantiatePet()
    {
        ownedSkillManager.damageSpirit = damageExtra;
        Transform orbitPosition = orbit;

        if (orbitPosition != null)
        {
            // Instancia um novo pet a partir do prefab
            ownedPet = Instantiate(petPrefab, orbitPosition);
            foreach (var bird in ownedPet.GetComponentsInChildren<Orbita>(true)) bird.BindOwner(Owner);
            foreach (var wolf in ownedPet.GetComponentsInChildren<WolfAI>(true)) wolf.BindOwner(Owner);
        
            // Opcional: Ajusta a posição local
            ownedPet.transform.localPosition = Vector3.zero;

            // Configura o petAnimator se necessário
            petAnimator = ownedPet.GetComponentInChildren<Animator>();
        }
        else
        {
            Debug.LogWarning("Orbit position is not set!");
        }
    }

    void ReleaseOwnedPet()
    {
        CancelInvoke(nameof(DesativarPower));
        if (ownedPet != null) Destroy(ownedPet);
        ownedPet = null;
        petAnimator = null;
        // A late callback from the old controller must not change a replacement.
        if (!ReferenceEquals(_playerNetworkManager, null) && powerOwners.TryGetValue(_playerNetworkManager, out var current) && ReferenceEquals(current, this))
        {
            powerOwners.Remove(_playerNetworkManager);
            if (_playerNetworkManager != null) _playerNetworkManager.isPowerPet.Value = previousPower;
            if (ownedSkillManager != null && ownedSkillManager.damageSpirit == damageExtra) ownedSkillManager.damageSpirit = previousDamageSpirit;
        }
        ownedSkillManager = null;
        _playerNetworkManager = null;
        Owner = null; orbit = null;
    }

    void OnDisable() => ReleaseOwnedPet();
    void OnDestroy() => ReleaseOwnedPet();





    private void ActivatePetPower()
    {
        if (petAnimator != null)
        {
            petAnimator.SetTrigger("Power");
        }
        else
        {
            Debug.LogWarning("Pet animator not found!");
        }
    }

    private void Update()
    {
        // if (_isAttackPressed)
        // {
        //     if (currentPet != null)
        //     {
        //         ActivatePetPower();
        //     }
        // }
    }
}
