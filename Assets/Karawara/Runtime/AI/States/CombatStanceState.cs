using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RPGKarawara
{
    [CreateAssetMenu(menuName = "A.I/States/Combat Stance")]
    public class CombatStanceState : AIState
    {
        [Header("Ataques")]
        public List<AICharacterAttackAction> aiCharacterAttacks;    //  Uma lista de todos os possíveis ataques que este personagem pode fazer
        [SerializeField] protected List<AICharacterAttackAction> potentialAttacks;     //  Todos os ataques possíveis nesta situação (baseados em ângulo, distância, etc)
        [SerializeField] private AICharacterAttackAction choosenAttack;   //  O ataque escolhido
        [SerializeField] private AICharacterAttackAction previousAttack;  //  O ataque anterior
        protected bool hasAttack = false;   // Verifica se já tem um ataque

        [Header("Combo")]
        [SerializeField] protected bool canPerformCombo = false;    // Se o personagem pode realizar um ataque em combo, após o ataque inicial
        [SerializeField] protected int chanceToPerformCombo = 25;   // A chance (em porcentagem) de o personagem realizar um combo no próximo ataque
        protected bool hasRolledForComboChance = false;  // Verifica se já foi calculada a chance de combo durante este estado

        [Header("Distância de Engajamento")]
        [SerializeField] public float maximumEngagementDistance = 5; // A distância que o personagem precisa estar do alvo antes de entrar no estado de perseguição

        public override AIState Tick(AICharacterManager aiCharacter)
        {
            // Se o personagem está realizando uma ação, permanece neste estado
            if (aiCharacter.isPerformingAction)
                return this;

            // Habilita o NavMeshAgent se estiver desabilitado
            if (!aiCharacter.navMeshAgent.enabled)
                aiCharacter.navMeshAgent.enabled = true;

            // Se o personagem deve girar em direção ao alvo fora do seu campo de visão
            if (aiCharacter.aiCharacterCombatManager.enablePivot)
            {
                if (!aiCharacter.aiCharacterNetworkManager.isMoving.Value)
                {
                    // Se o ângulo visível estiver fora do intervalo de -30 a 30 graus, gira em direção ao alvo
                    if (aiCharacter.aiCharacterCombatManager.viewableAngle < -30 || aiCharacter.aiCharacterCombatManager.viewableAngle > 30)
                        aiCharacter.aiCharacterCombatManager.PivotTowardsTarget(aiCharacter);
                }
            }

            // Gira em direção ao agente (player)
            aiCharacter.aiCharacterCombatManager.RotateTowardsAgent(aiCharacter);

            // Se o alvo não estiver mais presente, retorna ao estado Idle
            if (aiCharacter.aiCharacterCombatManager.currentTarget == null)
                return SwitchState(aiCharacter, aiCharacter.idle);

            // Se ainda não tiver um ataque, obtém um novo
            if (!hasAttack)
            {
                GetNewAttack(aiCharacter);
            }
            else
            {
                // Define o ataque atual e calcula a chance de combo
                aiCharacter.attack.currentAttack = choosenAttack;
                return SwitchState(aiCharacter, aiCharacter.attack);
            }

            // Se estiver fora da distância de combate, muda para o estado de perseguição
            if (aiCharacter.aiCharacterCombatManager.distanceFromTarget > maximumEngagementDistance)
                return SwitchState(aiCharacter, aiCharacter.pursueTarget);

            // Calcula o caminho até o alvo e o define no NavMeshAgent
            NavMeshPath path = new NavMeshPath();
            aiCharacter.navMeshAgent.CalculatePath(aiCharacter.aiCharacterCombatManager.currentTarget.transform.position, path);
            aiCharacter.navMeshAgent.SetPath(path);

            return this;
        }

        // Método para obter um novo ataque com base na distância e ângulo em relação ao alvo
        protected virtual void GetNewAttack(AICharacterManager aiCharacter)
        {
            potentialAttacks = new List<AICharacterAttackAction>();

            foreach (var potentialAttack in aiCharacterAttacks)
            {
                // Se estiver muito perto para este ataque, ignora
                if (potentialAttack.minimumAttackDistance > aiCharacter.aiCharacterCombatManager.distanceFromTarget){
                    Debug.Log("Se estiver muito perto para este ataque 1");               
                    continue;
                }

                // Se estiver muito longe para este ataque, ignora
                if (potentialAttack.maximumAttackDistance < aiCharacter.aiCharacterCombatManager.distanceFromTarget){
                    Debug.Log("Se estiver muito longe para este ataque"); 
                    continue;
                }

                // Se o ângulo visível for menor que o mínimo para este ataque, ignora
                if (potentialAttack.minimumAttackAngle > aiCharacter.aiCharacterCombatManager.viewableAngle){
                    Debug.Log("Se o ângulo visível for menor que o mínimo para este ataque, ignora");
                    continue;
                }

                // Se o ângulo visível for maior que o máximo para este ataque, ignora
                if (potentialAttack.maximumAttackDistance < aiCharacter.aiCharacterCombatManager.viewableAngle){
                    Debug.Log("Se o ângulo visível for maior que o máximo para este ataque");
                    continue;
                }

                // Adiciona o ataque à lista de ataques potenciais
                potentialAttacks.Add(potentialAttack);
            }

            // Se não houver ataques potenciais, retorna
            if (potentialAttacks.Count <= 0)
                return;

            var totalWeight = 0;

            // Calcula o peso total dos ataques potenciais
            foreach (var attack in potentialAttacks)
            {
                totalWeight += attack.attackWeight;
            }

            // Gera um valor aleatório com base no peso total
            var randomWeightValue = Random.Range(1, totalWeight + 1);
            var processedWeight = 0;

            // Seleciona o ataque com base no valor aleatório gerado
            foreach (var attack in potentialAttacks)
            {
                processedWeight += attack.attackWeight;

                if (randomWeightValue <= processedWeight)
                {
                    choosenAttack = attack;
                    previousAttack = choosenAttack;
                    hasAttack = true;
                    return;
                }
            }
        }

        // Método para calcular se a chance de um evento ocorrer foi alcançada
        protected virtual bool RollForOutcomeChance(int outcomeChance)
        {
            bool outcomeWillBePerformed = false;

            int randomPercentage = Random.Range(0, 100);

            if (randomPercentage < outcomeChance)
                outcomeWillBePerformed = true;

            return outcomeWillBePerformed;
        }

        // Método para redefinir os flags de estado
        protected override void ResetStateFlags(AICharacterManager aiCharacter)
        {
            base.ResetStateFlags(aiCharacter);

            hasAttack = false; // Reseta o flag de ataque
            hasRolledForComboChance = false; // Reseta o flag da chance de combo
        }
    }
}
