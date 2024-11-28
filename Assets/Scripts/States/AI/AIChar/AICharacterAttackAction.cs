using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [CreateAssetMenu(menuName = "A.I/Actions/Attack")] // Cria uma opção no menu do Unity para criar uma nova ação de ataque para IA.
    public class AICharacterAttackAction : ScriptableObject
    {
        // ----- Attack -----
        [Header("Attack")] // Seção que organiza as variáveis no inspector do Unity sob "Attack".
        [SerializeField] private string attackAnimation; // Nome da animação de ataque que será usada. Ex.: "Sword_Slash".

        // ----- Combo Action -----
        [Header("Combo Action")] // Seção que organiza variáveis relacionadas ao combo.
        public AICharacterAttackAction comboAction; // Ação de ataque que pode seguir este ataque, formando um combo.

        // ----- Action Values -----
        [Header("Action Values")] // Seção que define os parâmetros gerais da ação de ataque.
        [SerializeField] AttackType attackType; // Tipo de ataque. Ex.: Corpo a corpo (melee) ou à distância (ranged).
        public int attackWeight = 50; // Define a prioridade ou chance de escolha deste ataque pela IA. Ex.: um ataque mais forte pode ter um peso maior.

        // Tempo de recuperação do ataque. Ex.: O personagem terá que esperar 1.5 segundos antes de poder atacar novamente.
        public float actionRecoveryTime = 1.5f; // Tempo necessário para o próximo ataque após realizar este.

        // Define o ângulo no qual o ataque pode atingir. Ex.: -35 a 35 graus seria um ataque que pode acertar qualquer inimigo dentro desse cone.
        public float minimumAttackAngle = -35; // Ângulo mínimo (à esquerda) que o ataque pode cobrir. Ex.: -35 graus à esquerda da frente do personagem.
        public float maximumAttackAngle = 35; // Ângulo máximo (à direita) que o ataque pode cobrir. Ex.: 35 graus à direita da frente do personagem.

        // Exemplos de distância mínima e máxima para o ataque:
        // Um ataque corpo a corpo pode ter uma distância mínima de 0 (o inimigo deve estar bem perto) e uma distância máxima de 2 metros.
        // Um ataque à distância pode ter uma distância mínima maior, como 5 metros, e uma máxima de 20 metros.
        public float minimumAttackDistance = 0; // Distância mínima que o inimigo precisa estar para que o ataque aconteça. Ex.: 0 (muito próximo).
        public float maximumAttackDistance = 2; // Distância máxima que o ataque pode atingir. Ex.: 2 metros para ataques corpo a corpo.

        // Define se o ataque é de longo alcance ou corpo a corpo.
        public bool isRanged; // Se "true", o ataque é à distância (por exemplo, arco e flecha ou magia).

        // Função que tenta executar a ação de ataque.
        public void AttemptToPerformAction(AICharacterManager aiCharacter)
        {
            // Executa a animação de ataque baseada no "attackType" e "attackAnimation".
            aiCharacter.characterAnimatorManager.PlayTargetActionAnimation(attackAnimation, true);

            // Se o ataque for à distância, invoca o projétil ou magia.
            if (isRanged)
            {
               MagicSpeel.instance.Tiro(aiCharacter.aiCharacterCombatManager.currentTarget.transform);
            }
        }
    }
}
