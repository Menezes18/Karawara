using Karawara.Core.Characters;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace RPGKarawara
{
    public class AICharacterManager : CharacterManager
    {
        [Header("Character Name")]
        public string characterName = "";

        [Header("Elementos")]
        public Element characterElement = Element.None;

        [Header("Elementos")]
        [HideInInspector] public AICharacterNetworkManager aiCharacterNetworkManager;
        [HideInInspector] public AICharacterCombatManager aiCharacterCombatManager;
        [HideInInspector] public AICharacterLocomotionManager aiCharacterLocomotionManager;

        [Header("Navmesh Agent")]
        public NavMeshAgent navMeshAgent;

        [Header("Current State")]
        [SerializeField] protected AIState currentState;

        [Header("States")]
        public IdleState idle;
        public PursueTargetState pursueTarget;
        public CombatStanceState combatStance;
        public AttackState attack;

        readonly List<AIState> ownedRuntimeStates = new List<AIState>(5);
        IdleState idleTemplate;
        PursueTargetState pursueTemplate;
        CombatStanceState combatTemplate;
        AttackState attackTemplate;
        bool runtimeStatesAcquired;

        protected T CreateRuntimeState<T>(T template) where T : AIState
        {
            if (template == null) return null;
            var clone = Instantiate(template);
            ownedRuntimeStates.Add(clone);
            return clone;
        }
        void AcquireRuntimeStates()
        {
            if (runtimeStatesAcquired) return;
            idleTemplate = idle; pursueTemplate = pursueTarget; combatTemplate = combatStance; attackTemplate = attack;
            runtimeStatesAcquired = true;
            idle = CreateRuntimeState(idleTemplate);
            pursueTarget = CreateRuntimeState(pursueTemplate);
            combatStance = CreateRuntimeState(combatTemplate);
            attack = CreateRuntimeState(attackTemplate);
            currentState = idle;
        }
        void ReleaseRuntimeStates()
        {
            currentState = null;
            if (runtimeStatesAcquired)
            {
                idle = idleTemplate; pursueTarget = pursueTemplate; combatStance = combatTemplate; attack = attackTemplate;
                idleTemplate = null; pursueTemplate = null; combatTemplate = null; attackTemplate = null;
                runtimeStatesAcquired = false;
            }
            foreach (var state in ownedRuntimeStates) if (state != null) { if (Application.isPlaying) Destroy(state); else DestroyImmediate(state); }
            ownedRuntimeStates.Clear();
        }
        protected override void OnDestroy()
        {
            ReleaseRuntimeStates();
            base.OnDestroy();
        }

        [Header("Control Variables")]
        public bool isDisabled = false; // Novo booleano para controle

        public SkinnedMeshRenderer skinnedMeshRenderer;

        protected override void Awake()
        {
            base.Awake();

            aiCharacterNetworkManager = GetComponent<AICharacterNetworkManager>();
            aiCharacterCombatManager = GetComponent<AICharacterCombatManager>();
            aiCharacterLocomotionManager = GetComponent<AICharacterLocomotionManager>();

            navMeshAgent = GetComponentInChildren<NavMeshAgent>();
        }

        protected override void Start()
        {
            base.Start();
            if (navMeshAgent == null) return;
            navMeshAgent.enabled = false;
            Invoke("Inimigo", 1f);
        }

        void Inimigo()
        {
            if (navMeshAgent != null) navMeshAgent.enabled = true;
        }

        public override void InitializeLocal(CharacterRuntimeState state, ICharacterRegistry registry)
        {
            if (IsInitialized) return;
            base.InitializeLocal(state, registry);
            AcquireRuntimeStates();
            WorldAIManager.instance?.AddCharacterToSpawnedCharactersList(this);
        }

        public override IEnumerator ProcessDeathEvent(bool manuallySelectDeathAnimation = false)
        {
            WorldAIManager.instance?.RemoveCharacter(this);
            Registry?.Unregister(LocalId);
            foreach (var hitbox in GetComponentsInChildren<DamageCollider>(true)) hitbox.DisableDamageCollider();
            yield return base.ProcessDeathEvent(manuallySelectDeathAnimation);
        }

        public override void ShutdownLocal()
        {
            WorldAIManager.instance?.RemoveCharacter(this);
            ReleaseRuntimeStates();
            base.ShutdownLocal();
        }

        public void ChangeMaterial(Material agua, Material fogo, Element element)
        {
            if (element == Element.Fire)
            {
                skinnedMeshRenderer.material = fogo;
            }
            else if (element == Element.Water)
            {
                skinnedMeshRenderer.material = agua;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (IsInitialized && aiCharacterCombatManager != null) aiCharacterCombatManager.HandleActionRecovery(this);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (IsInitialized && !State.IsDead)
                ProcessStateMachine();
        }

        private void ProcessStateMachine()
        {
            if (navMeshAgent == null) return;
            if (isDisabled && !isBoss)
            {

                currentState = idle;

                if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh) { navMeshAgent.isStopped = true; navMeshAgent.ResetPath(); }
                aiCharacterNetworkManager.isMoving.Value = false;

                return;
            }

            AIState nextState = currentState?.Tick(this);

            if (nextState != null)
            {
                currentState = nextState;
            }

            navMeshAgent.transform.localPosition = Vector3.zero;
            navMeshAgent.transform.localRotation = Quaternion.identity;

            if (aiCharacterCombatManager.currentTarget != null)
            {
                aiCharacterCombatManager.targetsDirection = aiCharacterCombatManager.currentTarget.transform.position - transform.position;
                aiCharacterCombatManager.viewableAngle = WorldUtilityManager.Instance.GetAngleOfTarget(transform, aiCharacterCombatManager.targetsDirection);
                aiCharacterCombatManager.distanceFromTarget = Vector3.Distance(transform.position, aiCharacterCombatManager.currentTarget.transform.position);
            }

            if (navMeshAgent.enabled)
            {
                Vector3 agentDestination = navMeshAgent.destination;
                float remainingDistance = Vector3.Distance(agentDestination, transform.position);

                if (remainingDistance > navMeshAgent.stoppingDistance)
                {
                    aiCharacterNetworkManager.isMoving.Value = true;
                }
                else
                {
                    aiCharacterNetworkManager.isMoving.Value = false;
                }
            }
            else
            {
                aiCharacterNetworkManager.isMoving.Value = false;
            }
        }

        // Novo método para verificar o elemento do inimigo
        public void VerificarElementoDoInimigo(AICharacterManager inimigo)
        {
            if (inimigo != null)
            {
                Element elementoDoInimigo = inimigo.characterElement;
                // Realize ações com base no elemento do inimigo
                Debug.Log($"Elemento do inimigo: {elementoDoInimigo}");
            }
        }
    }
}
