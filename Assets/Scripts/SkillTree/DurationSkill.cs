using UnityEngine;

namespace RPGKarawara.SkillTree{
    public class DurationSkill : MonoBehaviour{

        public void Init(float duration){
            Invoke(nameof(DestroyCircle), duration);
        }
        private void DestroyCircle() {
            Destroy(gameObject);
        }
    }
}