//NOTE: THIS IS JUST AN EXAMPLE. A DEMO.
//In no way I recommend using this in a real game. This is not production ready. This was created with the sole porpuse of helping out in using Visual Effects in TPS or FPS
//You can take ideas from here but don't use this script in your game, it's infested with bugs. Unless you clean out the bugs :)


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using UnityEngine.VFX;

public enum AbilityType 
{
    AOE,
    PROJECTILE
}

public enum MarkerType
{
    LOCKED,
    FREE
}

[System.Serializable]
public class VFXAsset
{    
    public string VFXName;
    public GameObject VFXPrefab;
    [Tooltip("If a fire point is not provided it will spawn in the VFXMarker position.")]
    public Transform VFXFirePoint;
    [Tooltip("Used in case we want to parent the VFXPrefab.")]
    public Transform VFXParent;
    [Tooltip("Offsets the VFXPrefab a certain amount.")]
    public Vector3 VFXOffset;
    [Space]
    [Tooltip("When ON it will spawn if you hold the right mouse button")]
    public bool spawnWithRightMouseButton = false;
    [Tooltip("Should the VFXPrefab be spawned alternatively between two fire points? Only works if there's an alternateFirePoint")]
    public bool alternateBetweenFirePoints = false;
    [Tooltip("Only use alternate fire point to spawn left hand right hand for example")]
    public Transform alternateFirePoint;    
    [Space]
    public List<AudioClip> SFX;
    [Tooltip("Mostly used with a projectile to rotate to a direction and add velocity. When used in a AoE it will only rotate towards where we are aiming.")]
    public bool rotateToDestination = false;
    [Tooltip("Destroy the instantiated vfx after how many seconds?")]
    public float delayToDestroy = 10;  
}

[System.Serializable]
public class VFXParameters
{
    public string AbilityName;
    [Tooltip("Ability types. AOE doesnt need firepoint or projectile speed")]
    public AbilityType abilityType;    
    [Tooltip("Cooldown before shooting this ability again")]
    public float cooldown = 2;  
    [Tooltip("How much time is the character immovable to cast the spell?")]
    public float freezeTime;
    [Tooltip("The travelling speed of the projecitle. Only for Projectiles.")]
    public float projectileSpeed;
    [Tooltip("Rotates only in the Y axis. Excludes X and Z rotations.")]
    public bool rotateOnlyY = true;
    [Tooltip("The VFX Asset is to decalre the prefab, firepoint, parent and offset")]
    public List<VFXAsset> VFXAsset;
    [Space]
    [Tooltip("Should the ground marker be Free or Locked? Where locked is at the foot of the character and free is within a certain radius.")]
    public MarkerType markerType;
    [Tooltip("The Ground Marker Prefab")]
    public GameObject VFXMarker;
    [Tooltip("The Ground Marker position. Only for Projectiles")]
    public Transform VFXMarkerPosition;    
    [Header("POST-PRODUCTION")]
    [Tooltip("Camera Shake?")]
    public bool shake;
    [Tooltip("After how many seconds to shake? And how many times?")]
    public List<float> delays;
    public List<float> durations;
    public List<float> amplitudes;
    public List<float> frequencies;
    [Tooltip("Chromatic Aberration Punch Effect?")]
    public bool chromaticAberration;    
    [Tooltip("Goes from 0 to 1. 0 is no chromatic aberration")]
    public List<float> chromaticGoal;
    [Tooltip("Chromatic Rate is same as Refresh Rate. 0.05 is a good rate")]
    public List<float> chromaticRate;
}

public class SpawnAbilities : MonoBehaviour
{    
    [Tooltip("Is this game in First-Person? If off, works like a TPS")]
    public bool FPS = false;   
    public Camera cam;
    public AudioSource audioSource;
    [Tooltip("Dectect collisions in wich layer?")]
    public LayerMask collidingLayer;
    [FormerlySerializedAs("VFX")]
    [Tooltip("This is where we assign prefabs. List of effects will be automatically assigned to 1, 2, 3 and 4 keys respectively.")]
    public List<VFXParameters> Abilities;    
    [Space]
    [Header("POST-PRODUCTION")]
    [Tooltip("Assign a Global Volume to use Post Processing effects like chromatic aberration")]
    public Volume volume;    
    [Tooltip("Assign an impulse source for camera shake. Impulse Source should be attached to the camera")]
    public CinemachineImpulseSource impulseSource;

    private Animator anim;
    private ThirdPersonControllerScript movementInput;
    private VFXParameters effectToSpawn;
    private ChromaticAberration chromatic;
    private int currentAttack = 0;
    private bool aiming = false;
    private bool attacking = false;
    private bool chromaticIncrease = false;
    private bool leftHand;
    private bool spawnedRMBEffects;
    private GameObject vfxMarker;
    private Vector3 destination;
    private List<GameObject> RMBEffects = new List<GameObject>();
    private List<VisualEffect> RMBVFXGraphs = new List<VisualEffect>();

    void Start() 
    {        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if(Abilities.Count>0)        
            VFXSelecter(1);  

        anim = GetComponent<Animator>();

        movementInput = GetComponent<ThirdPersonControllerScript>();

        if(volume != null)
            volume.profile.TryGet<ChromaticAberration>(out chromatic);
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && !aiming)
            VFXSelecter(1);

        if (Input.GetKeyDown(KeyCode.Alpha2) && !aiming)
            VFXSelecter(2);

        if (Input.GetKeyDown(KeyCode.Alpha3) && !aiming)
            VFXSelecter(3);

        if (Input.GetKeyDown(KeyCode.Alpha4) && !aiming)
            VFXSelecter(4);

        if (Input.GetMouseButtonDown(1))
        {
            aiming = true;

            if(vfxMarker != null)
                vfxMarker.SetActive(true);
            
            for(int i=0; i<effectToSpawn.VFXAsset.Count; i++)
            {
                if(effectToSpawn.VFXAsset[i].spawnWithRightMouseButton && !spawnedRMBEffects)
                {   
                    spawnedRMBEffects = true;  
                    UpdateRMBEffect(true);
                }
            }

            if(anim != null && anim.ContainsParam("Idle"))
                 anim.SetBool("Idle", false);
        }

        if (Input.GetMouseButtonUp(1))
        {
            aiming = false;

            if(vfxMarker != null)
                vfxMarker.SetActive(false);
            
            for(int i=0; i<effectToSpawn.VFXAsset.Count; i++)
            {
                if(effectToSpawn.VFXAsset[i].spawnWithRightMouseButton && spawnedRMBEffects)
                {
                    spawnedRMBEffects = false;  
                    UpdateRMBEffect(false);
                }
            }

            if(anim != null && anim.ContainsParam("Idle"))
                anim.SetBool("Idle", true);
        }

        if (aiming)
        {
            RaycastHit hit;
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5F, 0.5F, 0));

            if (Physics.Raycast(ray, out hit, 10000, collidingLayer))
            {
                destination = hit.point;              

                if(vfxMarker != null)
                {
                    if(effectToSpawn.markerType == MarkerType.FREE)
                    {
                        vfxMarker.transform.position = destination;
                    }
                    else if(effectToSpawn.markerType == MarkerType.LOCKED)
                    {
                        vfxMarker.transform.position = effectToSpawn.VFXMarkerPosition.position;
                        RotateToDestination(vfxMarker, destination, true);
                    }

                    vfxMarker.SetActive(true);
                }
            }
            else
            {
                destination = ray.GetPoint(1000);

                if(vfxMarker != null)
                    vfxMarker.SetActive(false);
            }            
        }

        if (Input.GetButton("Fire1") && currentAttack != 0 && !attacking && effectToSpawn != null)
        {
            if (aiming)
                StartCoroutine (Attack());

            if(anim != null && anim.ContainsParam("Idle"))
                anim.SetBool("Idle", false);
            
            for(int i=0; i<effectToSpawn.VFXAsset.Count; i++)
            {
                if(effectToSpawn.VFXAsset[i].alternateBetweenFirePoints)
                {
                    if(leftHand)
                        leftHand = false;
                    else
                        leftHand = true;

                    break;
                }
            }
        }      

        if(Input.GetButtonUp("Fire1"))
        {
            if(anim != null && anim.ContainsParam("Idle"))
                anim.SetBool("Idle", true);
        }  
    }

    //When we press 1 2 3 or 4 (or other keybidings), this function sets the current attack number, the effect to spawn, and the marker on the ground.
    void VFXSelecter (int number)
    {
        currentAttack = number;
        if (Abilities.Count > number-1)
        {
            effectToSpawn = Abilities[number-1];
            
            if(effectToSpawn.VFXMarker != null)
            {
                if(vfxMarker == null)
                {
                    vfxMarker = new GameObject();
                    vfxMarker.name = "VfxMarker";
                    vfxMarker.SetActive(false);                
                }

                if(vfxMarker.name != effectToSpawn.VFXMarker.name)
                {
                    if(effectToSpawn.VFXMarker != null)
                    {
                        Destroy (vfxMarker);
                        vfxMarker = Instantiate (effectToSpawn.VFXMarker) as GameObject;//vfxMarker = effectToSpawn.VFXMarker;
                        vfxMarker.name = effectToSpawn.VFXMarker.name;
                        vfxMarker.SetActive(false); 
                    }
                }
            }
            else
            {
                Destroy(vfxMarker);
                vfxMarker = null;   
            }                   
        }
        else
            Debug.Log("Please assign a VFX in the inspector.");
    }

    //does the animation and spawns all effects in question (AoE or Projectile). 
    //it can also stop movement temporarily if 'movementInput' script provided and effect to spawn has a 'freezeTime' above 0.
    //also handles post production effects such as shake and chromatic punch 
    IEnumerator Attack ()
    {
        attacking = true;

        if(vfxMarker != null)
            vfxMarker.SetActive(false);       

        if(anim != null && anim.ContainsParam("Attack0" + currentAttack.ToString()))
            anim.SetTrigger("Attack0" + currentAttack.ToString());

        if(FPS == false) // rotates the character to face the ability that is going to spawn
            RotateToDestination(gameObject, destination, true);        

        if(movementInput != null && effectToSpawn != null)
            movementInput.StopMovementTemporarily(effectToSpawn.freezeTime, false);
                
        if (effectToSpawn.shake && impulseSource != null)
            StartCoroutine (ShakeCameraWithImpulse(effectToSpawn.delays, effectToSpawn.durations, effectToSpawn.amplitudes, effectToSpawn.frequencies));

        if (effectToSpawn.chromaticAberration && chromatic != null)
            StartCoroutine (ChromaticAberrationPunch(effectToSpawn.delays, effectToSpawn.chromaticGoal, effectToSpawn.chromaticRate));

        if(effectToSpawn.abilityType == AbilityType.AOE) //AOE//
        {        
            for(int i=0; i<effectToSpawn.VFXAsset.Count; i++)
            {  
                if(!effectToSpawn.VFXAsset[i].spawnWithRightMouseButton)
                {              
                    GameObject aoeVFX;

                    yield return new WaitForSeconds (effectToSpawn.delays[i]);

                    if(effectToSpawn.VFXAsset[i].VFXFirePoint != null)                
                        aoeVFX = Instantiate(effectToSpawn.VFXAsset[i].VFXPrefab, effectToSpawn.VFXAsset[i].VFXFirePoint.position + effectToSpawn.VFXAsset[i].VFXOffset, Quaternion.identity) as GameObject;
                    else
                        aoeVFX = Instantiate(effectToSpawn.VFXAsset[i].VFXPrefab, vfxMarker.transform.position + effectToSpawn.VFXAsset[i].VFXOffset, Quaternion.identity) as GameObject;

                    if(effectToSpawn.VFXAsset[i].VFXParent != null)
                    {
                        aoeVFX.transform.SetParent(effectToSpawn.VFXAsset[i].VFXParent);
                        aoeVFX.transform.localPosition = Vector3.zero;
                        aoeVFX.transform.localEulerAngles = Vector3.zero;
                    }
                    
                    if(effectToSpawn.VFXAsset[i].rotateToDestination)
                    {
                        Ray newRay = new Ray(vfxMarker.transform.position, vfxMarker.transform.forward);
                        RotateToDestination (aoeVFX, newRay.GetPoint(100), effectToSpawn.rotateOnlyY);
                    }

                    if(effectToSpawn.VFXAsset[i].SFX.Count > 0)
                    {
                        if(audioSource == null)
                            audioSource = GetComponent<AudioSource>();
                        
                        if(audioSource != null)
                        {
                            var num = Random.Range (0, effectToSpawn.VFXAsset[i].SFX.Count);
                            audioSource.PlayOneShot(effectToSpawn.VFXAsset[i].SFX[num]);
                        } 
                    }
                    
                    var trails = aoeVFX.GetComponent<DetachGameObjects>();
                    if(trails != null)
                        StartCoroutine (trails.Detach(effectToSpawn.VFXAsset[i].delayToDestroy-0.1f));

                    Destroy(aoeVFX, effectToSpawn.VFXAsset[i].delayToDestroy);
                }
            }
        }

        else if(effectToSpawn.abilityType == AbilityType.PROJECTILE) //PROJECTILE//
        { 
            for(int i=0; i<effectToSpawn.VFXAsset.Count; i++)
            {
                if(effectToSpawn.delays[i] > 0)
                    yield return new WaitForSeconds (effectToSpawn.delays[i]);

                if(!effectToSpawn.VFXAsset[i].spawnWithRightMouseButton)
                { 
                    GameObject projectileVFX;

                    if(effectToSpawn.VFXAsset[i].alternateBetweenFirePoints)
                    {
                        if(leftHand)
                        {
                            if(anim != null && anim.ContainsParam("Left"))
                                anim.SetTrigger("Left");
                            projectileVFX = Instantiate(effectToSpawn.VFXAsset[i].VFXPrefab, effectToSpawn.VFXAsset[i].alternateFirePoint.position + effectToSpawn.VFXAsset[i].VFXOffset, Quaternion.identity) as GameObject;
                        }
                        else
                        {   
                            if(anim != null && anim.ContainsParam("Right"))
                                anim.SetTrigger("Right");                 
                            projectileVFX = Instantiate(effectToSpawn.VFXAsset[i].VFXPrefab, effectToSpawn.VFXAsset[i].VFXFirePoint.position + effectToSpawn.VFXAsset[i].VFXOffset, Quaternion.identity) as GameObject;
                        }
                    }
                    else
                    {
                        if(anim != null && anim.ContainsParam("Right"))
                            anim.SetTrigger("Right");   

                        projectileVFX = Instantiate(effectToSpawn.VFXAsset[i].VFXPrefab, effectToSpawn.VFXAsset[i].VFXFirePoint.position + effectToSpawn.VFXAsset[i].VFXOffset, Quaternion.identity) as GameObject;
                        
                        if(effectToSpawn.VFXAsset[i].VFXParent != null)
                        {
                            projectileVFX.transform.SetParent(effectToSpawn.VFXAsset[i].VFXParent);
                            projectileVFX.transform.localPosition = Vector3.zero;
                            projectileVFX.transform.localEulerAngles = Vector3.zero;
                        }                
                    }
                    
                    if(effectToSpawn.VFXAsset[i].SFX.Count > 0)
                    {
                        if(audioSource == null)
                            audioSource = GetComponent<AudioSource>();
                        
                        if(audioSource != null)
                        {
                            var num = Random.Range (0, effectToSpawn.VFXAsset[i].SFX.Count);
                            audioSource.PlayOneShot(effectToSpawn.VFXAsset[i].SFX[num]);
                        }                    
                    }              

                    if(effectToSpawn.VFXAsset[i].rotateToDestination) //basically if it's meant to be a projectile. Which needs to rotate to a direction and velocity
                    {
                        if(vfxMarker != null)
                        {
                            Ray newRay = new Ray(vfxMarker.transform.position, vfxMarker.transform.forward);
                            RotateToDestination (projectileVFX, newRay.GetPoint(100), effectToSpawn.rotateOnlyY);
                        }
                        else
                        {
                            RotateToDestination (projectileVFX, destination, effectToSpawn.rotateOnlyY);
                        }

                        var rb = projectileVFX.GetComponent<Rigidbody>();

                        if(rb != null)
                        {
                            if(effectToSpawn.VFXMarker != null)
                                projectileVFX.GetComponent<Rigidbody>().velocity = vfxMarker.transform.forward * effectToSpawn.projectileSpeed;
                            else
                                projectileVFX.GetComponent<Rigidbody>().velocity = transform.forward * effectToSpawn.projectileSpeed;
                        }
                        else
                        {
                            Debug.Log("This projectile doesn't have a rigidbody.");
                        }

                        Destroy(projectileVFX, effectToSpawn.VFXAsset[i].delayToDestroy);
                    }
                    else
                    {
                        Destroy(projectileVFX, effectToSpawn.VFXAsset[i].delayToDestroy);
                    }
                }
            }
        }

        yield return new WaitForSeconds (effectToSpawn.cooldown);

        attacking = false;    
    }

    //used for the effect that are placed in the hand of a character when holding right mouse button for example. Mostly used in FPS mode
    void UpdateRMBEffect (bool active)
    {      
        for(int i=0; i<effectToSpawn.VFXAsset.Count; i++)
        {
            if(effectToSpawn.VFXAsset[i].spawnWithRightMouseButton)
            {
                if(active)
                {
                    var rmbEffect = Instantiate (effectToSpawn.VFXAsset[i].VFXPrefab) as GameObject;
                    rmbEffect.name = effectToSpawn.VFXAsset[i].VFXPrefab.name;
                    RMBEffects.Add(rmbEffect);

                    var rmbVFXGraph = rmbEffect.transform.GetChild(0).GetComponent<VisualEffect>();
                    RMBVFXGraphs.Add(rmbVFXGraph);

                    if(effectToSpawn.VFXAsset[i].VFXParent != null)
                    {
                        rmbEffect.transform.SetParent(effectToSpawn.VFXAsset[i].VFXParent);
                        rmbEffect.transform.localPosition = Vector3.zero;
                    }

                    rmbVFXGraph.Play();            

                    if(anim != null && anim.ContainsParam("HandEffect0" + currentAttack.ToString()))
                        anim.SetBool("HandEffect0" + currentAttack.ToString(), true);
                }
                else
                {
                    for(int j=0; j<RMBEffects.Count; j++)
                        Destroy(RMBEffects[j],effectToSpawn.VFXAsset[i].delayToDestroy);

                    for(int j=0; j<RMBEffects.Count; j++)
                    {
                        RMBVFXGraphs[j].Stop();
                    }

                    RMBEffects.Clear();
                    RMBVFXGraphs.Clear();
                    
                    if(anim != null && anim.ContainsParam("HandEffect0" + currentAttack.ToString()))
                        anim.SetBool("HandEffect0" + currentAttack.ToString(), false);            
                }
            }
        }
    }

    #region BASIC FUNCTIONS

    void RotateToDestination (GameObject obj, Vector3 destination, bool onlyY) 
    {
		var direction = destination - obj.transform.position;
		var rotation = Quaternion.LookRotation (direction);

        if(onlyY)
        {
            rotation.x = 0;
            rotation.z = 0;
        }        
		obj.transform.localRotation = Quaternion.Lerp (obj.transform.rotation, rotation, 1);
	}

    #endregion

    #region POST-PRODUCTION FUNCTIONS

    IEnumerator ShakeCameraWithImpulse(List<float> delays, List<float> shakeDuration, List<float> shakeAmplitude, List<float> shakeFrequency)
    {
        for(int i=0; i<delays.Count; i++)
        {
            yield return new WaitForSeconds (delays[i]);
            impulseSource.m_ImpulseDefinition.m_TimeEnvelope.m_SustainTime = shakeDuration[i];
            impulseSource.m_ImpulseDefinition.m_AmplitudeGain = shakeAmplitude[i];
            impulseSource.m_ImpulseDefinition.m_FrequencyGain = shakeFrequency[i];
            impulseSource.GenerateImpulse();
        }
    }

    IEnumerator ChromaticAberrationPunch(List<float> delays, List<float> crhomaticGoals, List<float> chromaticRates)
    {   
        for(int i=0; i<delays.Count; i++)
        { 
            yield return new WaitForSeconds (delays[i]);
            if(!chromaticIncrease)
            {    
                chromaticIncrease = true;
                float amount = 0;
                while (amount < crhomaticGoals[i])
                {
                    amount += chromaticRates[i];
                    chromatic.intensity.value = amount;
                    yield return new WaitForSeconds (0.05f);
                }
                while (amount > 0)
                {
                    amount -= chromaticRates[i];
                    chromatic.intensity.value = amount;
                    yield return new WaitForSeconds (0.05f);
                }
                chromaticIncrease = false;
            }
        }
    }

    #endregion
}
