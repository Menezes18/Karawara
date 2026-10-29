using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class SaveTrigger : MonoBehaviour{
        public SavePositionOnTrigger savePositionOnTrigger;
        public int num;
        private void OnTriggerEnter(Collider other){
            if (other.CompareTag("Player")){
                savePositionOnTrigger.numberSave = num;
                savePositionOnTrigger.savedPostion[num] = this.transform;
            }
        }
    }
}
