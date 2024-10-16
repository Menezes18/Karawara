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
        public GameObject skillPop;
        public Skill novaSkillAdd;
        public SkillTreeUiManager skillTreeUiManager;

        public Image skillPopImage;
        public TextMeshProUGUI skillPopText;
        public TextMeshProUGUI skillPopTextSkill;
        public FadeImages skillPopImageFade;

        public float tempoAtivacao = 2f; // Tempo em segundos para manter o skillPop ativo

        void Start()
        {
            skillPop.SetActive(false);
        }

        void Update()
        {
            skillPopImage.sprite = novaSkillAdd.sprite;
            skillPopText.text = novaSkillAdd.name;
            skillPopTextSkill.text = novaSkillAdd.skillType.ToString();

            if (Keyboard.current.kKey.wasReleasedThisFrame)
            {
                skillPop.SetActive(true);
                skillPopImageFade.Reiniciar();
                SkillTreeUiManager.instance.AddSkill(novaSkillAdd, novaSkillAdd.skillType);
                
                // Inicia a coroutine para desativar o skillPop após um tempo
                StartCoroutine(DesativarSkillPop());
            }

            
        }

        private IEnumerator DesativarSkillPop()
        {
            // Aguarda o tempo definido antes de desativar o skillPop
            yield return new WaitForSeconds(tempoAtivacao);
            skillPop.SetActive(false);
        }
    }
}