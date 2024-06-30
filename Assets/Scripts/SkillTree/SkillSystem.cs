using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RPGKarawara
{
    [System.Serializable]
    public class SkillNode
    {
        [FormerlySerializedAs("skill")] public SkillData skillData;
        public SkillSystem[] children;
        public bool isUnlocked;
        public bool isParent; // Indica se o nó é um pai

        public SkillNode(SkillData skillData, bool isParent = false)
        {
            this.skillData = skillData;
            this.children = new SkillSystem[0];
            this.isUnlocked = false;
            this.isParent = isParent;
        }
    }

    public class SkillSystem : MonoBehaviour
    {
        public SkillNode node;
        public bool unlocked = false;
        public TextMeshProUGUI textName;
        public Image image;
        public SystemXP systemXp;

        public void Start(){
            systemXp = FindObjectOfType<SystemXP>();
        }

        private void Awake()
        {
            
            if (node != null && node.skillData != null)
            {
                if (textName != null)
                {
                    textName.text = node.skillData.skillName;
                }

                if (image != null)
                {
                    image.sprite = node.skillData.icon;
                }
            }
        }

        public void Limpar(){
            systemXp.text.text = " ";
        }
        public void UnlockSkill()
        {
            if (SystemXP.Instance.currentLevel < node.skillData.xp)
            {
                systemXp.text.text = "Você não tem level suficiente";
                Invoke("Limpar", 1f);
                return;
            }

            // Verifica se a habilidade já está desbloqueada
            if (node.isUnlocked){
                systemXp.text.text = "Habilidade já desbloqueada";
                Invoke("Limpar", 1f);
                return;
            }

            // Verifica se a habilidade pode ser comprada
            if (!CanBuySkill()){
                systemXp.text.text = "Os pré-requisitos para esta habilidade não são atendidos";
                Invoke("Limpar", 1f);
                return;
            }

            // Desbloqueia a habilidade e compra com XP
            node.isUnlocked = true;
            node.skillData.BuyXp();
            systemXp.text.text = $"Skill {node.skillData.skillName} desbloqueado";
            Invoke("Limpar", 1f);

            // Habilita as habilidades filhas
            if (node.children[0] != null){
                foreach (var child in node.children)
                {
                    child.unlocked = true;
                }
            }
        }

        // Método para verificar se uma habilidade pode ser comprada
        public bool CanBuySkill()
        {
            // Se o nó atual já está desbloqueado, não pode comprar novamente
            if (node.isUnlocked) return false;

            // Se o nó atual é uma habilidade inicial (sem pais), pode ser comprada
            bool hasParent = false;

            // Procura por todos os SkillSystem no jogo
            SkillSystem[] allSkills = FindObjectsOfType<SkillSystem>();

            // Variável para verificar se há pelo menos um pai desbloqueado
            bool hasParentUnlocked = false;

            // Verifica se existe algum outro SkillSystem que tem este nó como filho
            foreach (SkillSystem skillSystem in allSkills)
            {
                if (skillSystem.node.children != null)
                {
                    foreach (SkillSystem child in skillSystem.node.children)
                    {
                        if (child == this)
                        {
                            hasParent = true;
                            if (skillSystem.node.isUnlocked)
                            {
                                hasParentUnlocked = true;
                                break; // Não precisa verificar mais, já sabemos que pelo menos um pai está desbloqueado
                            }
                        }
                    }
                }
            }

            // Se não há pais, é uma habilidade inicial e pode ser comprada diretamente
            if (!hasParent) return true;

            // Se pelo menos um pai está desbloqueado, a habilidade pode ser comprada
            return hasParentUnlocked;
        }

        // Método para desenhar gizmos que mostram as conexões entre habilidades
        private void OnDrawGizmos()
        {
            if (node == null || node.children == null) return;

            foreach (var child in node.children)
            {
                if (child != null)
                {
                    DrawArrowWithMargin(transform.position, child.transform.position, 50f, Color.green, 2);
                }
            }
        }

        // Método auxiliar para desenhar uma seta entre duas posições com uma margem, cor e grossura
        private void DrawArrowWithMargin(Vector3 start, Vector3 end, float margin, Color color, float thickness)
        {
#if UNITY_EDITOR
            Vector3 direction = (end - start).normalized;
            float distance = Vector3.Distance(start, end);

            // Calcula os novos pontos de início e fim da linha da seta, aplicando a margem
            Vector3 arrowStart = start + direction * margin;
            Vector3 arrowEnd = end - direction * margin;

            // Define a cor e a grossura da linha da seta
            Handles.color = color;
            Handles.DrawAAPolyLine(thickness, new Vector3[] { arrowEnd, arrowStart });

            // Desenha a cabeça da seta
            float arrowHeadLength = 6f; // tamanho da cabeça da seta
            Handles.ConeHandleCap(0, arrowEnd, Quaternion.LookRotation(direction), arrowHeadLength, EventType.Repaint);
#endif
        }
    }
}
