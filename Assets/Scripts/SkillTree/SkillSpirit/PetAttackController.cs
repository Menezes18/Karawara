using UnityEngine;
using UnityEngine.InputSystem;
using RPGKarawara;
using RPGKarawara.SkillTree;
using Unity.VisualScripting;
using PlayerInputManager = RPGKarawara.PlayerInputManager;

public class PetAttackController : MonoBehaviour
{
    private Animator petAnimator; 
    private PlayerNetworkManager _playerNetworkManager;
    public float damageExtra = 10f;
    private bool _isAttackPressed;
    public GameObject petPrefab; // Referência ao Prefab do pet
    public float duration = 10;

    private void Awake(){
        
        Init();
    }

   
    
    private void Start()
    {
        // Inicializações necessárias
    }

    public void Init()
    {
        _playerNetworkManager = FindObjectOfType<PlayerNetworkManager>();
        _playerNetworkManager.isPowerPet.Value = true;
        InstantiatePet();
        Invoke(nameof(DesativarPower), duration);
    }

    private void DesativarPower()
    {
        _playerNetworkManager.isPowerPet.Value = false;
        Destroy(gameObject);
    }

    private void InstantiatePet()
    {
        PlayerSkillManager.instance.damageSpirit = damageExtra;
        GameObject orbitPosition = GameObject.FindGameObjectWithTag("Orbit");

        if (orbitPosition != null)
        {
            // Instancia um novo pet a partir do prefab
            GameObject instantiatedPet = Instantiate(petPrefab, orbitPosition.transform);
        
            // Opcional: Ajusta a posição local
            instantiatedPet.transform.localPosition = Vector3.zero;

            // Configura o petAnimator se necessário
            petAnimator = instantiatedPet.GetComponentInChildren<Animator>();
        }
        else
        {
            Debug.LogWarning("Orbit position is not set!");
        }
    }





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
