using UnityEngine;

namespace RPGKarawara.SkillTree{
    public class SkillEscudo : MonoBehaviour{
        
        PlayerNetworkManager playerNetworkManager;
        public void Init(float duration){
            playerNetworkManager = FindObjectOfType<PlayerNetworkManager>();
            playerNetworkManager.isBlocking.Value = true;
            Invoke(nameof(DesativarEscudo), duration);
        }
        
       
        
        private void DesativarEscudo(){
            playerNetworkManager.isBlocking.Value = false;
        }
    }
}