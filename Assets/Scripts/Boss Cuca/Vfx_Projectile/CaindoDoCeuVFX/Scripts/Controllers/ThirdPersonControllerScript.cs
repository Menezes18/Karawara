
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//This script requires you to have setup your animator with 3 float parameters, "InputMagnitude", "InputX", "InputZ" and a trigger "Jump"
//With a blend tree to control the inputmagnitude and allow blending between animations.
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonControllerScript : MonoBehaviour 
{
	[Header("Movement")]
	public Animator anim;
    public float movementSpeed = 3;
	public float desiredRotationSpeed = 0.1f;
	public float allowPlayerRotation = 0.1f;

	[Header("Gravity")]
    public float gravity = -9.81f;
    public float jumpHeight = 3;
    public Transform groundCheck;
    public LayerMask groundMask;

    [Header("Animation Smoothing")]
    [Range(0, 1f)]
    public float HorizontalAnimSmoothTime = 0.2f;
    [Range(0, 1f)]
    public float VerticalAnimTime = 0.2f;
    [Range(0,1f)]
    public float StartAnimTime = 0.3f;
    [Range(0, 1f)]
    public float StopAnimTime = 0.15f;

    [Header("SFX")]
	[Range(0,1)]
	public float footstepsVolume;
	public List<AudioClip> footsteps;

	private Camera cam;
	private CharacterController controller;
	private float InputX;
	private float InputZ;
	private Vector3 desiredMoveDirection;
	private float speed;
    private float verticalVel;
    private Vector3 moveVector;
    private float originalMovSpeed;
	private bool canMove = true;
    private Vector3 velocity;
    private bool isGrounded;
    private float groundDistance = 0.2f;
	private AudioSource audioSource;
    
	void Start () 
	{
        originalMovSpeed = movementSpeed;
        anim = GetComponent<Animator> ();
		cam = Camera.main;
		controller = GetComponent<CharacterController> ();
		audioSource = GetComponent<AudioSource> ();
	}
	
	void Update () 
	{
		if(canMove)
			InputMagnitude ();

		if(anim.GetFloat ("InputMagnitude") < 0.01f)
			anim.SetFloat ("InputMagnitude", 0);
	}

	void PlayerMoveAndRotation() 
	{
		InputX = Input.GetAxis ("Horizontal");
		InputZ = Input.GetAxis ("Vertical");

		var camera = Camera.main;
		var forward = cam.transform.forward;
		var right = cam.transform.right;

		forward.y = 0f;
		right.y = 0f;

		forward.Normalize ();
		right.Normalize ();

		desiredMoveDirection = forward * InputZ + right * InputX;
        
		transform.rotation = Quaternion.Slerp (transform.rotation, Quaternion.LookRotation (desiredMoveDirection), desiredRotationSpeed);
        controller.Move(desiredMoveDirection * Time.deltaTime * movementSpeed);
    
	}

	void InputMagnitude() 
	{
		isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

		if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2 * gravity);

			if(anim != null && anim.ContainsParam("Jump"))
				anim.SetTrigger("Jump");
        }

        //gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

		//Calculate Input Vectors
		InputX = Input.GetAxis ("Horizontal");
		InputZ = Input.GetAxis ("Vertical");

		anim.SetFloat ("InputZ", InputZ, VerticalAnimTime, Time.deltaTime * 2f);
		anim.SetFloat ("InputX", InputX, HorizontalAnimSmoothTime, Time.deltaTime * 2f);

		//Calculate the Input Magnitude
		speed = new Vector2(InputX, InputZ).sqrMagnitude;

		//Physically move player
		if (speed > allowPlayerRotation) 
		{
			anim.SetFloat ("InputMagnitude", speed, StartAnimTime, Time.deltaTime);
			PlayerMoveAndRotation ();
		} 
		else if (speed < allowPlayerRotation) 
		{
			anim.SetFloat ("InputMagnitude", speed, StopAnimTime, Time.deltaTime);
		}
	}

    public void StopMovementTemporarily (float time, bool fade) 
	{
        StartCoroutine (StopMovementTemporarilyCo(time, fade));
    }

    IEnumerator StopMovementTemporarilyCo (float time, bool fade) 
	{
		canMove = false;

        if (fade) 
		{
            while (movementSpeed > 0) 
			{
                movementSpeed -= Time.deltaTime * 5;
                yield return new WaitForSeconds(0.01f);
            }
        }
        else 		
            movementSpeed = 0;
        
        yield return new WaitForSeconds(time);
        if (fade) 
		{
            while (movementSpeed < originalMovSpeed) 
			{
                movementSpeed += Time.deltaTime * 10;
                yield return new WaitForSeconds(0.01f);
            }
        }
        else 		
            movementSpeed = originalMovSpeed;    

		canMove = true;    
    }

	public void PlayFootSteps ()
	{
		if(footsteps.Count >0)
		{
			if(audioSource != null)
			{					
				var num = Random.Range(0, footsteps.Count);
				audioSource.PlayOneShot(footsteps[num], footstepsVolume);
			}
			else
				Debug.Log("No AudioSource found");
		}
	}
}
