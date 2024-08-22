using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara {
    public class EarthquakeEffect : MonoBehaviour
    {
        [Header("Collider")]
        [SerializeField] private Collider damageCollider;

        [Header("Damage")]
        public float physicalDamage = 10f; // Ajuste conforme necessário
        public float pushForce = 5f; // Força para empurrar os inimigos para cima
        public float damageInterval = 1f; // Intervalo entre cada aplicação de dano
        public float earthquakeDuration = 15f; // Duração total do efeito de terremoto

        [Header("Contact Point")]
        private Vector3 contactPoint;

        [Header("Characters Damaged")]
        private List<CharacterManager> charactersDamaged = new List<CharacterManager>();

        private Dictionary<CharacterManager, Coroutine> activeCoroutines = new Dictionary<CharacterManager, Coroutine>();

        private void Awake()
        {
            if (damageCollider == null)
                damageCollider = GetComponent<Collider>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                return; // Ignora dano ao jogador

            CharacterManager damageTarget = other.GetComponentInParent<CharacterManager>();

            if (damageTarget != null && !activeCoroutines.ContainsKey(damageTarget))
            {
                Coroutine damageCoroutine = StartCoroutine(ApplyDamageOverTime(damageTarget));
                activeCoroutines[damageTarget] = damageCoroutine;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            CharacterManager damageTarget = other.GetComponentInParent<CharacterManager>();

            if (damageTarget != null && activeCoroutines.ContainsKey(damageTarget))
            {
                StopCoroutine(activeCoroutines[damageTarget]);
                activeCoroutines.Remove(damageTarget);
            }
        }

        private IEnumerator ApplyDamageOverTime(CharacterManager damageTarget)
        {
            float elapsedTime = 0f;

            while (elapsedTime < earthquakeDuration)
            {
                ApplyDamage(damageTarget);
                ApplyPush(damageTarget);

                elapsedTime += damageInterval;
                yield return new WaitForSeconds(damageInterval);
            }
        }

        private void ApplyDamage(CharacterManager damageTarget)
        {
            TakeDamageEffect damageEffect = Instantiate(WorldCharacterEffectsManager.instance.takeDamageEffect);
            damageEffect.physicalDamage = physicalDamage;
            damageEffect.contactPoint = contactPoint;

            damageTarget.characterEffectsManager.ProcessInstantEffect(damageEffect);
        }

        private void ApplyPush(CharacterManager damageTarget)
        {
            Rigidbody rb = damageTarget.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 pushDirection = Vector3.up; // Empurrar para cima
                rb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
            }
        }

        public void EnableDamageCollider()
        {
            damageCollider.enabled = true;
        }

        public void DisableDamageCollider()
        {
            damageCollider.enabled = false;

            // Parar todos os coroutines ativos
            foreach (var coroutine in activeCoroutines.Values)
            {
                StopCoroutine(coroutine);
            }

            activeCoroutines.Clear(); // Limpar as referências dos coroutines

            charactersDamaged.Clear(); // Resetar os alvos que foram atingidos
        }
    }
}
