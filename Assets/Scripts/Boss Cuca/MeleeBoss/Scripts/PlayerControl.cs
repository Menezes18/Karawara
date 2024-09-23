using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Random = UnityEngine.Random;
using RPGKarawara;
public class EnemyAI : MonoBehaviour
{
    [Space]
    [Header("Components")]
    [SerializeField] private Animator anim;
    [SerializeField] private Transform player; // Referência ao jogador

    [Space]
    [Header("Combat")]
    [SerializeField] private Transform attackPos;
    [SerializeField] private float quickAttackDeltaDistance;
    [SerializeField] private float heavyAttackDeltaDistance;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float airknockbackForce = 10f;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float reachTime = 0.3f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private string PlayerTag = "PlayerLock";

    public bool isAttacking = false;
    private Coroutine attackCoroutine;

    [Space]
    [Header("Detection")]
    [SerializeField] private float detectionRange = 5f;

    [SerializeField] private BossAISystem _bossAISystem;
    private void Start(){
        player = GameObject.FindGameObjectWithTag(PlayerTag).transform;
        _bossAISystem = FindObjectOfType<BossAISystem>();
    }

    void Update()
    {
        HandleAI();
        HandleAttackToggle();
    }

    void HandleAI()
    {
        if (player == null) return;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange && !isAttacking && _bossAISystem.AttackMelee)
        {
            
                FaceThis(player.position);
                StartAttacking(); // Inicia o loop de ataque
                
            
            
        }
        // else if (distanceToPlayer > detectionRange && isAttacking)
        // {
        //     StopAttacking(); // Para o ataque se o jogador sair da detecção
        // }
    }

    void HandleAttackToggle()
    {
        // if (Input.GetKeyDown(KeyCode.Space))
        // {
        //     if (isAttacking)
        //     {
        //         StopAttacking();
        //     }
        //     else
        //     {
        //         StartAttacking();
        //     }
        // }
    }

    void StartAttacking()
    {
        if (attackCoroutine == null)
        {
            attackCoroutine = StartCoroutine(AttackLoop());
        }
    }

    void StopAttacking()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
            ResetAttack();
        }
    }

    private IEnumerator AttackLoop()
    {
        isAttacking = true;

        while (true)
        {
            Attack(Random.Range(0, 2)); // Ataca de forma aleatória (0 ou 1)
            yield return new WaitForSeconds(1f); // Espera um segundo entre os ataques
            if (index >= _bossAISystem.AttackIndex)
            {
                Debug.Log("Parando ataque.");
                StopAttacking(); // Para os ataques
                _bossAISystem.MoveToRandomPositionIfClear();
                break; // Sai do loop
            }
        }
    }

    public void Attack(int attackState)
    {
        if (isAttacking)
        {
            RandomAttackAnim(attackState);
            PerformAttack(); // Chame PerformAttack aqui para aplicar o dano
        }
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

    public int index = 0;
    void QuickAttack()
    {
        int attackIndex = Random.Range(1, 4);
        MoveTowardsTarget(player.position, quickAttackDeltaDistance, "punch"); // Mova antes da animação
        switch (attackIndex)
        {
            
            case 1: // punch
                anim.SetBool("punch", true);
                break;
            case 2: // kick
                anim.SetBool("kick", true);
                break;
            case 3: // mmakick
                anim.SetBool("mmakick", true);
                break;
                
        }
    }

    public void countAttack(){
        index++;
        Debug.Log("Attack");
    }
    void HeavyAttack()
    {
        int attackIndex = Random.Range(1, 3);
        FaceThis(player.position);
        switch (attackIndex)
        {
            case 1: // heavyAttack1
                anim.SetBool("heavyAttack1", true);
                break;
            case 2: // heavyAttack2
                anim.SetBool("heavyAttack2", true);
                break;
        }
    }

    public void ResetAttack()
    {
        anim.SetBool("punch", false);
        anim.SetBool("kick", false);
        anim.SetBool("mmakick", false);
        anim.SetBool("heavyAttack1", false);
        anim.SetBool("heavyAttack2", false);
        // Não redefine isAttacking aqui
    }

    public void PerformAttack()
    {
        Collider[] hitPlayers = Physics.OverlapSphere(attackPos.position, attackRange);
        foreach (Collider player in hitPlayers){
            if (player.CompareTag(PlayerTag)) {// Verifica se o objeto tem a tag "Player"
                Rigidbody playerRb = player.GetComponent<Rigidbody>();
                if (playerRb != null){
                    // Vector3 knockbackDirection = player.transform.position - transform.position;
                    //knockbackDirection.y = airknockbackForce;
                    // playerRb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode.Impulse);
                    // Adicione aqui a lógica para causar dano ao jogador
                }
        }
    }
    }

    public void MoveTowardsTarget(Vector3 target_, float deltaDistance, string animationName_)
    {
        PerformAttackAnimation(animationName_);
        Vector3 finalPos = TargetOffset(target_, deltaDistance);
        finalPos.y = transform.position.y; // Mantém o eixo Y atual
        transform.DOMove(finalPos, reachTime);
    }


    void PerformAttackAnimation(string animationName_)
    {
        anim.SetBool(animationName_, true);
    }

    public Vector3 TargetOffset(Vector3 target, float deltaDistance)
    {
        return Vector3.MoveTowards(target, transform.position, deltaDistance);
    }

    public void FaceThis(Vector3 target)
    {
        Quaternion lookAtRotation = Quaternion.LookRotation(target - transform.position);
        lookAtRotation.x = 0;
        lookAtRotation.z = 0;
        transform.rotation = lookAtRotation;
    }
    public void GetClose()
    {
        Vector3 targetPosition = player.position;
        targetPosition.y = transform.position.y; // Mantém o eixo Y atual
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, quickAttackDeltaDistance);
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPos.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange); // Visualiza a área de detecção
    }
}
