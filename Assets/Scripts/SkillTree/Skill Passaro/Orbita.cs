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
    public float offsetX = 0.5f; // Deslocamento no eixo X
    public float offsetY = 1.0f; // Deslocamento no eixo Y
    public float offsetZ = 0.5f; // Deslocamento no eixo Z
    [Header("Speed na variação do Y")] public float ySpeed = 2f;


    private void Awake(){
        GameObject Orbit = GameObject.FindWithTag("Orbit");
        centerObject = Orbit.transform;
    }

    void Update(){
        Vector3 newPosition = centerObject.position + new Vector3(offsetX, offsetY, offsetZ);
        transform.position = newPosition; // Atualizar a posição do pássaro
    
        Vector3 lookDirection = centerObject.position - transform.position; // Direção do objeto para o centerObject
        GameObject lookRotation =  GameObject.FindWithTag("Player");
        transform.rotation = lookRotation.transform.rotation; // Aplicar a rotação ao objeto
    }
    
    //metodo dps
       /*
        if (centerObject != null){
            currentAngle += orbitSpeed * Time.deltaTime;
            float x = Mathf.Cos(currentAngle) * orbitRadius;
            float z = Mathf.Sin(currentAngle) * orbitRadius;

            float y = Mathf.Sin(Time.time * ySpeed) * yAmplitude;
            transform.position = new Vector3(x, y, z) + centerObject.position;
        }*/
}
