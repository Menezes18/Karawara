using UnityEngine;
using UnityEngine.InputSystem;
using RPGKarawara;
using RPGKarawara.SkillTree;
using Unity.VisualScripting;
using System.Collections.Generic;
using PlayerInputManager = RPGKarawara.PlayerInputManager;

public class PetAttackController : MonoBehaviour
{
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
        
        Init();
    }

   
    
    private void Start()
    {
        // Inicializações necessárias
    }

    public void Init()
    {
        // Init can be repeated, and a new cast can replace an older controller.
        // Retire only this owner's resources before acquiring the player's lease.
        ReleaseOwnedPet();
        _playerNetworkManager = FindObjectOfType<PlayerNetworkManager>();
        if (_playerNetworkManager == null || PlayerSkillManager.instance == null || petPrefab == null) return;
        if (powerOwners.TryGetValue(_playerNetworkManager, out var previous) && previous != null && previous != this)
        {
            previous.ReleaseOwnedPet();
            Destroy(previous.gameObject);
        }
        ownedSkillManager = PlayerSkillManager.instance;
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
        GameObject orbitPosition = GameObject.FindGameObjectWithTag("Orbit");

        if (orbitPosition != null)
        {
            // Instancia um novo pet a partir do prefab
            ownedPet = Instantiate(petPrefab, orbitPosition.transform);
        
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
