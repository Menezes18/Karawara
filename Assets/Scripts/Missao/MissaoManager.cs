using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPGKarawara
{
    public class MissionManager : MonoBehaviour
    {
        [Header("Enemies in the Area")]
        public List<GameObject> enemies; // Lista de inimigos da missão, configurada no Inspector

        [Header("Mission Status")]
        public bool isMissionCompleted = false;

        [Header("Mission Objectives")]
        public GameObject reward; // Recompensa da missão
        public GameObject npcDialogue;

        public CanvasGroup popup;
        public TextMeshProUGUI popupText;
        public string textPopup;
        
        private void Start(){
            popup.alpha = 0;
            popup.interactable = false;
            popup.blocksRaycasts = false;
            
        }

        private void Update()
        {
            if (!isMissionCompleted)
            {
                CheckMissionStatus();
            }
        }
        
        [SerializeField] private float detectionRange = 10f; // Raio para detectar inimigos

        private void CheckMissionStatus()
        {
            // Encontra todos os colliders dentro do range
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange);

            // Adiciona inimigos que estão dentro do alcance à lista, mas não remove os que saíram
            foreach (Collider collider in colliders)
            {
                if (collider.CompareTag("Inimigo") && collider.gameObject.activeSelf)
                {
                    if (!enemies.Contains(collider.gameObject))
                    {
                        enemies.Add(collider.gameObject); // Adiciona o inimigo à lista
                        Debug.Log($"{collider.gameObject.name} entrou no alcance!");
                    }
                }
            }

            // Verifica se todos os inimigos estão desativados (isso conclui a missão)
            bool allEnemiesDeactivated = true;

            foreach (GameObject enemy in enemies)
            {
                if (enemy.activeSelf)
                {
                    allEnemiesDeactivated = false;
                    break; // Se algum inimigo ainda estiver ativo, não pode concluir a missão
                }
            }

            // Conclui a missão se todos os inimigos estiverem desativados
            if (allEnemiesDeactivated)
            {
                CompleteMission();
            }
        }


        private void CompleteMission()
        {
            isMissionCompleted = true;
            Debug.Log("Missão concluída!");
            
            ShowPopup(textPopup);
            if (reward != null)
            {
                Instantiate(reward, transform.position, Quaternion.identity);
            }
        }

        public void ShowPopup(string text)
        {
            popupText.text = text;
            StartCoroutine(FadeInAndOut());
        }

        private IEnumerator FadeInAndOut()
        {
            yield return StartCoroutine(Fade(0, 1, 1f));
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(Fade(1, 0, 1f));
        }

        private IEnumerator Fade(float startAlpha, float targetAlpha, float duration)
        {
            float time = 0;

            while (time < duration)
            {
                time += Time.deltaTime;
                popup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                yield return null;
            }

            popup.alpha = targetAlpha;
            popup.interactable = targetAlpha > 0;
            popup.blocksRaycasts = targetAlpha > 0;

            if (targetAlpha == 0)
            {
                popupText.text = "";
            }
            npcDialogue.SetActive(false);
        }
        private void OnDrawGizmosSelected()
        {
            // Opcional: Visualize algo na cena (ex.: posição da recompensa)
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, 0.5f);
            
            
            // Cor do Gizmo (azul transparente)
            Gizmos.color = new Color(0, 0, 1, 0.3f);
            // Desenha a esfera representando o raio de detecção
            Gizmos.DrawSphere(transform.position, detectionRange);

            // Cor para o contorno do Gizmo (azul sólido)
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            
            
        }
    }
}