using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace RPGKarawara
{
    public class AtivarSkillPop : MonoBehaviour
    {
        public static AtivarSkillPop instance;
        public GameObject skillPop;
        //public Skill novaSkillAdd;
        public SkillTreeUiManager skillTreeUiManager;

        public Image skillPopImage;
        public TextMeshProUGUI skillPopText;
        public TextMeshProUGUI skillPopTextSkill;
        public FadeImages skillPopImageFade;

        public float tempoAtivacao = 2f; // Tempo em segundos para manter o skillPop ativo

        private void Awake(){
            instance = this;
        }

        void Start()
        {
            skillPop.SetActive(false);
        }

        void Update()
        {
            if (Keyboard.current.kKey.wasReleasedThisFrame){
                //AtivarSkill();
            }

            
        }

        public void AtivarSkill(Skill novaSkillAdd){
            skillPop.SetActive(true);
            //PlayerInputManager.instance.DisableAll();
            // Atualiza imagem e textos apenas quando a habilidade é ativada
            skillPopImage.sprite = novaSkillAdd.sprite;
            skillPopText.text = novaSkillAdd.name;
            skillPopTextSkill.text = novaSkillAdd.skillType.ToString();

            skillPopImageFade.Reiniciar();
            SkillTreeUiManager.instance.AddSkill(novaSkillAdd, novaSkillAdd.skillType);
                
            // Inicia a coroutine para desativar o skillPop após um tempo
            StartCoroutine(DesativarSkillPop());
        }

        private IEnumerator DesativarSkillPop()
        {
            // Aguarda o tempo definido antes de desativar o skillPop
            yield return new WaitForSeconds(tempoAtivacao);
            skillPop.SetActive(false);
            //PlayerInputManager.instance.EnableAll();
        }
    }
}