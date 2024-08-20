using UnityEngine;

namespace RPGKarawara.SkillTree {
    public class Projectile : MonoBehaviour {
        public float speed = 10f;
        private int _damage;

        public void Initialize(int damage, float projectileSpeed) {
            _damage = damage;
            speed = projectileSpeed;
        }

        private void Update() {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other) {
           
            
            //dano 
        }
    }
}