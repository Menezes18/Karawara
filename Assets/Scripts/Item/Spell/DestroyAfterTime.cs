using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class DestroyAfterTime : MonoBehaviour{
        [SerializeField] private float timeToDestroy = 5f;

        private void Start(){
            Destroy(gameObject, timeToDestroy);
        }
        private void Destroyer(){
            Destroy(gameObject, timeToDestroy);
        }
    }
}
