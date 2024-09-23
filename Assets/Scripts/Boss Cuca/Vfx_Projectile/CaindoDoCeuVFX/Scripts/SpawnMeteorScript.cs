using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnMeteorScript : MonoBehaviour {

	public GameObject comet;
	public GameObject startPoint;
	public GameObject endPoint;
	public float projectileSpeed = 15;
	public float delay;
	public float rateOfFire;
	public float radius;
	public float quantity;
	public float waves;

	void Start () 
	{
		StartCoroutine (SpawnVFX(comet, delay, rateOfFire));
	}

    IEnumerator SpawnVFX (GameObject vfxPrefab, float delay, float rateDelay)
	{	
		for (int j = 0; j < waves; j++) 
		{ 	
			yield return new WaitForSeconds (delay);

			for (int i = 0; i < quantity; i++) 
			{
				var startPos = startPoint.transform.position;
				var endPos = endPoint.transform.position;

				if(radius != 0)
					startPos = new Vector3 (startPoint.transform.position.x + Random.Range (-radius, radius), startPoint.transform.position.y, startPoint.transform.position.z + Random.Range (-radius, radius));				
				
				if(radius != 0)
					endPos = new Vector3 (endPoint.transform.position.x + Random.Range (-radius, radius), endPoint.transform.position.y, endPoint.transform.position.z + Random.Range (-radius, radius));
				
				GameObject vfx = Instantiate (vfxPrefab, startPos, Quaternion.identity) as GameObject;
				var destination = endPos - startPos;				
				RotateTo (vfx, endPos);
				vfx.GetComponent<Rigidbody>().velocity = destination.normalized * projectileSpeed;

				yield return new WaitForSeconds (rateDelay);
			}
		}
	}

	void RotateTo (GameObject obj, Vector3 destination ) 
	{
		var direction = destination - obj.transform.position;
		var rotation = Quaternion.LookRotation (direction);
		obj.transform.localRotation = Quaternion.Lerp (obj.transform.rotation, rotation, 1);
	}

}
