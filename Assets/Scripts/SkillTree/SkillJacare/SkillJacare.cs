using System.Collections.Generic;
using RPGKarawara.SkillTree;
using UnityEngine;

namespace RPGKarawara
{
    public class SkillJacare : MonoBehaviour
    {
        public float skillRadius = 10f;
        public float skillDuration = 5f;
        public LayerMask enemyLayer;
        public CustomRenderPassFeature renderPassFeature; // Referência à Feature
        public Shader curShader; 
        private Material _filtroTela;
        Material _FiltroTela
        {
            get
            {
                if (_filtroTela == null)
                {
                    if (curShader == null)
                    {
                        Debug.LogError("Shader não atribuído!");
                        return null;
                    }

                    _filtroTela = new Material(curShader);
                    _filtroTela.hideFlags = HideFlags.HideAndDontSave;
                }
                return _filtroTela;
            }
        }

        [SerializeField]
        private List<AICharacterManager> affectedEnemies = new List<AICharacterManager>();
        private Transform _playerTransform;

        private float skillStartTime;
        private bool shouldDeactivateAll = false;

        private void Awake()
        {
            _playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
            transform.SetParent(_playerTransform);
            transform.localPosition = Vector3.zero;
        }

        private void Start()
        {
            ActivateSkill();
        }

        public void ActivateSkill()
        {
            skillStartTime = Time.time;
            ApplySkillEffect();
        }

        private void Update()
        {
            float elapsedTime = Time.time - skillStartTime;

            if (elapsedTime >= skillDuration - 1.5f && !shouldDeactivateAll)
            {
                DeactivateAllEnemies();
                
            }
            else{
                ApplySkillEffect();
            }

            if (elapsedTime >= skillDuration)
            {
                DeactivateSkill();
            }

           
        }

        private void ApplySkillEffect()
        {
            if (curShader != null && renderPassFeature != null)
            {
                _FiltroTela.SetFloat("_Luminosity", 1.0f);
                renderPassFeature.UpdateMaterial(_FiltroTela);
                Debug.Log("Luminosity aplicado: " + _FiltroTela.GetFloat("_Luminosity"));
            }
            else
            {
                Debug.LogError("Shader ou RenderPassFeature não configurados!");
            }
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, skillRadius, enemyLayer);
            foreach (Collider collider in hitColliders)
            {
                AICharacterManager enemy = collider.GetComponent<AICharacterManager>();
                if (enemy != null && !affectedEnemies.Contains(enemy))
                {
                    enemy.isDisabled = true;
                    affectedEnemies.Add(enemy);
                }
            }
        }

        private void DeactivateAllEnemies()
        {
            if (_filtroTela != null)
            {
                _filtroTela.SetFloat("_Luminosity", 0.0f); // Reseta o valor
                renderPassFeature.settings.material = null; // Remove o material da feature
                DestroyImmediate(_filtroTela); // Destrói o material
                _filtroTela = null;
            }
            foreach (AICharacterManager enemy in affectedEnemies)
            {
                if (enemy != null)
                {
                    enemy.isDisabled = false;
                }
            }
            affectedEnemies.Clear();
        }

        private void DeactivateSkill()
        {
            if (_filtroTela != null)
            {
                _filtroTela.SetFloat("_Luminosity", 0.0f); // Reseta o valor
                renderPassFeature.settings.material = null; // Remove o material da feature
                DestroyImmediate(_filtroTela); // Destrói o material
                _filtroTela = null;
            }
            affectedEnemies.Clear(); // Limpa a lista para evitar reativação de objetos destruídos
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, skillRadius);
        }
    }
}
