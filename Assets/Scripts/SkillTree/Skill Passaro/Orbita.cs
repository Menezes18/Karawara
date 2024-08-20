using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Orbita : MonoBehaviour{
    public Transform centerObject;
    public float orbitSpeed = 4f;
    public float orbitRadius = 5f;
    public float yAmplitude = 0.5f;
    private float currentAngle = 0f;

    [Header("Speed na variação do Y")] public float ySpeed = 2f;


    private void Awake(){
        GameObject Orbit = GameObject.FindWithTag("Orbit");
        centerObject = Orbit.transform;
    }

    void Update(){
       
        if (centerObject != null){
            currentAngle += orbitSpeed * Time.deltaTime;
            float x = Mathf.Cos(currentAngle) * orbitRadius;
            float z = Mathf.Sin(currentAngle) * orbitRadius;

            float y = Mathf.Sin(Time.time * ySpeed) * yAmplitude;
            transform.position = new Vector3(x, y, z) + centerObject.position;
        }

    }
}