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
        private bool skillActive;
        private bool materialInstalled;
        private Material previousMaterial;
        private readonly Dictionary<AICharacterManager, bool> previousEnemyStates = new Dictionary<AICharacterManager, bool>();

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
            DeactivateAllEnemies();
            shouldDeactivateAll = false;
            skillActive = true;
            previousMaterial = renderPassFeature != null ? renderPassFeature.settings.material : null;
            skillStartTime = Time.time;
            ApplySkillEffect();
        }

        private void Update()
        {
            if (!skillActive) return;
            float elapsedTime = Time.time - skillStartTime;

            if (elapsedTime >= skillDuration)
            {
                DeactivateSkill();
                return;
            }

            if (elapsedTime >= skillDuration - 1.5f && !shouldDeactivateAll)
            {
                DeactivateAllEnemies();
                
            }
            else if (!shouldDeactivateAll){
                ApplySkillEffect();
            }

           
        }

        private void ApplySkillEffect()
        {
            if (curShader != null && renderPassFeature != null)
            {
                _FiltroTela.SetFloat("_Luminosity", 1.0f);
                renderPassFeature.UpdateMaterial(_FiltroTela);
                materialInstalled = true;
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
                    previousEnemyStates[enemy] = enemy.isDisabled;
                    enemy.isDisabled = true;
                    affectedEnemies.Add(enemy);
                }
            }
        }

        private void DeactivateAllEnemies()
        {
            shouldDeactivateAll = true;
            if (materialInstalled && renderPassFeature != null)
                renderPassFeature.UpdateMaterial(previousMaterial);
            materialInstalled = false;
            if (_filtroTela != null)
            {
                Destroy(_filtroTela);
                _filtroTela = null;
            }
            foreach (AICharacterManager enemy in affectedEnemies)
            {
                if (enemy != null)
                {
                    enemy.isDisabled = previousEnemyStates.TryGetValue(enemy, out bool wasDisabled) && wasDisabled;
                }
            }
            affectedEnemies.Clear();
            previousEnemyStates.Clear();
        }

        private void DeactivateSkill()
        {
            skillActive = false;
            DeactivateAllEnemies();
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            skillActive = false;
            DeactivateAllEnemies();
        }

        private void OnDestroy()
        {
            DeactivateAllEnemies();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, skillRadius);
        }
    }
}
