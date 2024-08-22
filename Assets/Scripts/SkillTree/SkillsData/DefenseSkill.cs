using UnityEngine;
namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Defense Skill", menuName = "Skills/DefenseSkill")]
    public class DefenseSkill : Skill {
        
        public GameObject vfxPrefab;
        public string childName = "Escudo"; // Nome do filho onde o VFX será instanciado

        protected override void Execute(GameObject user)
        {
            Debug.LogWarning("Escudo");
            GameObject escudo = GameObject.FindGameObjectWithTag("Escudo");
    
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