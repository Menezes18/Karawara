using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class BossControl : MonoBehaviour
{
    [Space]
    [Header("Components")]
    [SerializeField] private Animator anim;
    [SerializeField] private Transform target; // Referência ao jogador
    [SerializeField] private Transform attackPos;
    [Tooltip("Offset Stoping Distance")] [SerializeField] private float quickAttackDeltaDistance;
    [Tooltip("Offset Stoping Distance")] [SerializeField] private float heavyAttackDeltaDistance;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float airknockbackForce = 10f;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float minDistanceToPlayer = 2f; // Distância mínima do boss ao player
    [SerializeField] private float reachTime = 0.3f;

    private bool isAttacking = false;

    void Start()
    {
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        // Verificar se o jogador está dentro do alcance de ataque
        float distanceToPlayer = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(target.position.x, 0, target.position.z));

        if (distanceToPlayer < attackRange && !isAttacking)
        {
            Attack(Random.Range(0, 2)); // Ataque aleatório entre 0 e 1
        }
    }

    public void Attack(int attackState)
    {
        if (isAttacking) return;

        isAttacking = true;
        RandomAttackAnim(attackState);
    }

    private void RandomAttackAnim(int attackState)
    {
        switch (attackState)
        {
            case 0: // Quick Attack
                QuickAttack();
                break;
            case 1: // Heavy Attack
                HeavyAttack();
                break;
        }
    }

    void QuickAttack()
    {
        int attackIndex = Random.Range(1, 4);
        switch (attackIndex)
        {
            case 1: // Exemplo de animação de ataque rápido
                MoveTowardsTarget(target.position, quickAttackDeltaDistance, "quickAttack1");
                break;
            case 2:
                MoveTowardsTarget(target.position, quickAttackDeltaDistance, "quickAttack2");
                break;
            case 3:
                MoveTowardsTarget(target.position, quickAttackDeltaDistance, "quickAttack3");
                break;
        }
    }

    void HeavyAttack()
    {
        int attackIndex = Random.Range(1, 3);
        switch (attackIndex)
        {
            case 1:
                MoveTowardsTarget(target.position, heavyAttackDeltaDistance, "heavyAttack1");
                break;
            case 2:
                MoveTowardsTarget(target.position, heavyAttackDeltaDistance, "heavyAttack2");
                break;
        }
    }

    public void ResetAttack() // Evento de animação ---- para resetar ataque
    {
        anim.SetBool("quickAttack1", false);
        anim.SetBool("quickAttack2", false);
        anim.SetBool("quickAttack3", false);
        anim.SetBool("heavyAttack1", false);
        anim.SetBool("heavyAttack2", false);
        isAttacking = false;
    }

    public void PerformAttack() // Evento de animação ---- para atacar o jogador
    {
        Collider[] hitPlayers = Physics.OverlapSphere(attackPos.position, attackRange);

        foreach (Collider player in hitPlayers)
        {
            if (player.CompareTag("PlayerLock")){
                
                Rigidbody playerRb = player.GetComponent<Rigidbody>();
                // Adicione a lógica de dano ao jogador aqui
                Debug.Log("Player hit!");
                // Exemplo de knockback
                if (playerRb != null)
                {
                    Vector3 knockbackDirection = player.transform.position - transform.position;
                    knockbackDirection.y = airknockbackForce; // Mantém o knockback horizontal
                    playerRb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode.Impulse);
                }
            }
        }
    }

    public void MoveTowardsTarget(Vector3 target_, float deltaDistance, string animationName_)
    {
        PerformAttackAnimation(animationName_);
        FaceThis(target_);
        Vector3 finalPos = TargetOffset(target_, deltaDistance);
        finalPos.y = 0; // Garantir que o boss não suba no eixo Y
        
        // Adicionar verificação de distância mínima
        float distanceToPlayer = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(target_.x, 0, target_.z));
        if (distanceToPlayer > minDistanceToPlayer) // Verificar se o boss está longe o suficiente
        {
            transform.DOMove(finalPos, reachTime);
        }
    }

    void PerformAttackAnimation(string animationName_)
    {
        anim.SetBool(animationName_, true);
    }

    public Vector3 TargetOffset(Vector3 target, float deltaDistance)
    {
        // Calcular a direção para manter uma distância mínima do jogador
        Vector3 direction = (transform.position - target).normalized;
        return target + direction * deltaDistance; // Manter distância mínima ao se mover
    }

    public void FaceThis(Vector3 target)
    {
        Quaternion lookAtRotation = Quaternion.LookRotation(target - transform.position);
        lookAtRotation.x = 0;
        lookAtRotation.z = 0;
        transform.DOLocalRotateQuaternion(lookAtRotation, 0.2f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPos.position, attackRange); // Visualiza o alcance de ataque
    }
}


