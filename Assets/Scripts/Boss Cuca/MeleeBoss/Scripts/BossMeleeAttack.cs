using System.Collections;
using UnityEngine;
using DG.Tweening;
using Random = UnityEngine.Random;

public class EnemyAI : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator anim;
    [SerializeField] private Transform player;

    [Header("Combat")]
    [SerializeField] private Transform attackPos;
    [SerializeField] private float quickAttackDeltaDistance;
    [SerializeField] private float heavyAttackDeltaDistance;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float reachTime = 0.3f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private string PlayerTag = "PlayerLock";
    public bool isAttacking = false;
    private Coroutine attackCoroutine;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 5f;

    [Header("Combos")]
    [SerializeField] private float comboWindow = 1f;
    private bool isComboAvailable = false;
    private int comboStep = 0;

    [Header("Animation Control")]
    [SerializeField] private float pauseTime = 2f; // Tempo de pausa antes de continuar
    private int pauseFrame = 151; // Frame para pausar

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag(PlayerTag).transform;
    }

    void Update()
    {
        HandleAI();
    }

    void HandleAI()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange && !isAttacking)
        {
            FacePlayer();
            StartAttacking();
        }
    }

    void StartAttacking()
    {
        StartCoroutine(PauseAtFrame("punch"));
        // if (attackCoroutine == null)
        // {
        //     attackCoroutine = StartCoroutine(AttackLoop());
        // }
    }

    private IEnumerator AttackLoop()
    {
        isAttacking = true;

        while (true)
        {
            if (!isComboAvailable)
            {
                Attack(Random.Range(0, 2));
                isComboAvailable = true;
            }
            else
            {
                yield return new WaitForSeconds(comboWindow);
                if (comboStep > 0)
                {
                    PerformComboAttack();
                }
                else
                {
                    StopAttacking();
                    break;
                }
            }
        }
    }

    public void Attack(int attackState)
    {
        if (isAttacking)
        {
            RandomAttackAnim(attackState);
            PerformAttack();
        }
    }

    private void RandomAttackAnim(int attackState)
    {
        
        switch (attackState)
        {
            case 0:
                QuickAttack();
                break;
            case 1:
                HeavyAttack();
                break;
        }
    }
    public void countAttack(){
        Debug.Log("Attack");
    }
    void QuickAttack()
    {
       
        int attackIndex = Random.Range(1, 4);
        MoveTowardsTarget(player.position, quickAttackDeltaDistance, "punch");

        switch (attackIndex)
        {
            case 1:
                StartCoroutine(PauseAtFrame("punch"));
                break;
            case 2:
                anim.SetTrigger("kick");
                break;
            case 3:
                anim.SetTrigger("mmakick");
                break;
        }

        StartCombo();
    }

    IEnumerator PauseAtFrame(string attackName)
    {
        // Inicia a animação de ataque
        anim.SetTrigger(attackName);

        yield return null; // Aguarda um frame para iniciar a animação

        // Aguarda até atingir o frame 151
        yield return StartCoroutine(WaitForAnimationFrame(pauseFrame));

        // Pausa a animação
        anim.speed = 0f;

        // Aguarda pelo tempo de pausa definido
        yield return new WaitForSeconds(pauseTime);

        // Continua a animação
        anim.speed = 1f;
    }

    private IEnumerator WaitForAnimationFrame(int targetFrame)
    {
        AnimatorStateInfo animState = anim.GetCurrentAnimatorStateInfo(0);
        float normalizedTargetTime = (float)targetFrame / animState.length;

        while (animState.normalizedTime < normalizedTargetTime)
        {
            animState = anim.GetCurrentAnimatorStateInfo(0);
            yield return null;
        }
    }

    void HeavyAttack()
    {
        int attackIndex = Random.Range(1, 3);
        FacePlayer();
        switch (attackIndex)
        {
            case 1:
                anim.SetTrigger("heavyAttack1");
                break;
            case 2:
                anim.SetTrigger("heavyAttack2");
                break;
        }
        StartCombo();
    }

    private void StartCombo()
    {
        comboStep = 1;
        StartCoroutine(ComboCountdown());
    }

    private IEnumerator ComboCountdown()
    {
        yield return new WaitForSeconds(comboWindow);
        isComboAvailable = false;
        comboStep = 0;
    }

    private void PerformComboAttack()
    {
        comboStep++;

        if (comboStep == 2)
        {
            QuickAttack();
        }
        else if (comboStep >= 3)
        {
            StopAttacking();
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

    public void ResetAttack()
    {
        anim.ResetTrigger("punch");
        anim.ResetTrigger("kick");
        anim.ResetTrigger("mmakick");
        anim.ResetTrigger("heavyAttack1");
        anim.ResetTrigger("heavyAttack2");
        isAttacking = false;
        comboStep = 0;
        isComboAvailable = false;
    }

    public void PerformAttack()
    {
        Collider[] hitPlayers = Physics.OverlapSphere(attackPos.position, attackRange, enemyLayer);
        foreach (Collider player in hitPlayers)
        {
            if (player.CompareTag(PlayerTag))
            {
                Rigidbody playerRb = player.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    Vector3 knockbackDirection = player.transform.position - transform.position;
                    knockbackDirection.y = 0;
                    playerRb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode.Impulse);
                }
            }
        }
    }

    public void MoveTowardsTarget(Vector3 target, float deltaDistance, string animationName)
    {
        PerformAttackAnimation(animationName);
        Vector3 finalPos = TargetOffset(target, deltaDistance);
        finalPos.y = transform.position.y;
        transform.DOMove(finalPos, reachTime);
    }

    void PerformAttackAnimation(string animationName)
    {
        anim.SetTrigger(animationName);
    }

    public Vector3 TargetOffset(Vector3 target, float deltaDistance)
    {
        return Vector3.MoveTowards(target, transform.position, deltaDistance);
    }

    public void FacePlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }
}
