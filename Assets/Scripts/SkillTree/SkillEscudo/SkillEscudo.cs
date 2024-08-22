using UnityEngine;

namespace RPGKarawara.SkillTree{
    public class SkillEscudo : MonoBehaviour{
        
        
        public void Init(float duration){
            PlayerSkillManager.instance.Escudo = true;
            Invoke(nameof(DesativarEscudo), duration);
        }
        
       
        
        private void DesativarEscudo() {
            PlayerSkillManager.instance.Escudo = false;
        }
    }
}