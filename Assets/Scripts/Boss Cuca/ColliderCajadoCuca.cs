using UnityEngine;


    public class ColliderCajadoCuca : MonoBehaviour{
        
        public Collider _collider;


        public void AtivarC(){
            _collider.enabled = true;
        }
        public void DesativarC(){
            Debug.Log("Desativar");
            _collider.enabled = false;
        }
        
        
        
        
    }
