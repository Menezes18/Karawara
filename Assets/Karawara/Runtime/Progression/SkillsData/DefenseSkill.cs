using UnityEngine;
namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Defense Skill", menuName = "Skills/DefenseSkill")]
    public class DefenseSkill : Skill {
        
        public GameObject vfxPrefab;

        protected override void Execute(GameObject user)
        {
            Debug.LogWarning("Escudo");
            GameObject escudo = null;
            foreach (var child in user.GetComponentsInChildren<Transform>(true)) if (child.CompareTag("Escudo")) { escudo = child.gameObject; break; }
    
            // Verifique se escudo não é null antes de acessar o transform
            if (escudo != null)
            {
                    // Instancie o prefab na posição e rotação do filho
                    GameObject vfxDefense = Instantiate(vfxPrefab, escudo.transform.position, escudo.transform.rotation);
                    vfxDefense.transform.SetParent(escudo.transform);
            
                    SkillEscudo scriptEscudo = vfxDefense.GetComponent<SkillEscudo>();
                    DurationSkill durationSkill = vfxDefense.GetComponent<DurationSkill>();
                    
                    if (scriptEscudo != null && durationSkill != null)
                    {
                        durationSkill.Init(cooldownDuration);
                        scriptEscudo.BindOwner(user.GetComponent<PlayerManager>());
                        scriptEscudo.Init(cooldownDuration);
                    }
                    else
                    {
                        Debug.LogWarning("SkillEscudo component not found on vfxPrefab.");
                    }
            }
            else
            {
                Debug.LogWarning("No GameObject with tag 'Escudo' found.");
            }
        }

    }
}