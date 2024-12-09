using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.InputSystem;

public class CameraShaker : MonoBehaviour
{
    float rotationX = 0;
    float rotationY = 0;
    float rotationZ = 0;

    public float maxAngleX = 10;
    public float maxAngleY = 10;
    public float maxAngleZ = 10;

    public float intensity = 0;

    public float growthIntensity = 0.5f;
    public float decayIntensity  = 0.5f;

    float seedX;
    float seedY;
    float seedZ;

    public float speed = 5;

    void Start() {

        seedX = Random.Range(-1000, 1000);
        seedY = Random.Range(-1000, 1000);
        seedZ = Random.Range(-1000, 1000);

    }

    void Update() {

        var dt = Time.deltaTime;

        if(Keyboard.current.eKey.wasPressedThisFrame) {
            intensity += growthIntensity * dt;
        } else {
            //intensity -= decayIntensity * dt;
        }

        intensity = Mathf.Clamp(intensity, 0, 1);

        var intensityExp = intensity * intensity;

        var time  = Time.time * speed;

        rotationX = intensityExp * maxAngleX * PerlinNoise(seedX, time);
        rotationY = intensityExp * maxAngleY * PerlinNoise(seedY, time);
        rotationZ = intensityExp * maxAngleZ * PerlinNoise(seedZ, time);

        transform.rotation = Quaternion.Euler(rotationY, rotationZ, rotationZ);

    }

    float PerlinNoise(float seed, float time) {
        return (1 - 2 * Mathf.PerlinNoise(seed + time, seed + time));
    }
}
