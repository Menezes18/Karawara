using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace RPGKarawara
{
    public class AICharacterSpawner : MonoBehaviour
    {
        [Header("Character")]
        [SerializeField] GameObject characterGameObject;
        [SerializeField] GameObject instantiatedGameObject;
        [SerializeField] private SkinnedMeshRenderer skin;
        public AICharacterManager characterManager;
        public Element characterElement = Element.None;
        public Material agua;
        public Material fogo;
        public bool boss = false;
        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.SpawnRequested);
#endif
            WorldAIManager.GetOrCreateInstance(gameObject.scene).SpawnCharacter(this);
            gameObject.SetActive(false);
        }

        public void ChangeElement(GameObject character)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.ElementChanged);
#endif
            Debug.Log(characterElement);

            characterManager = character.GetComponent<AICharacterManager>();
            characterManager.characterElement = characterElement;
            characterManager.ChangeMaterial(agua, fogo, characterElement);
            if (boss){
                var AIBossCharacterManager = character.GetComponent<AIBossCharacterNetworkManager>();
                AIBossCharacterManager.fogoBoss = true;
            }


        }




        public void ResetSpawnedCharacter()
        {
            if (instantiatedGameObject != null)
            {
                instantiatedGameObject.GetComponent<AICharacterManager>()?.ShutdownLocal();
                instantiatedGameObject.SetActive(false);
                Destroy(instantiatedGameObject);
                instantiatedGameObject = null;
            }
            AttemptToSpawnCharacter();
        }

        public void AttemptToSpawnCharacter()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.SpawnRequested);
            long qaAttempt = RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory.StartAttempt(this);
            try
            {
#endif
            if (characterGameObject != null && (instantiatedGameObject == null || !instantiatedGameObject.activeInHierarchy))
            {
                instantiatedGameObject = Instantiate(characterGameObject, transform.position, transform.rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instantiatedGameObject, gameObject.scene);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory.Observe(qaAttempt, this, instantiatedGameObject, RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory.Stage.Instantiated);
#endif
                characterManager = instantiatedGameObject.GetComponent<AICharacterManager>();
                characterManager.InitializeLocal(characterManager.State, GameBootstrap.EnsureInstance().Registry);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory.Observe(qaAttempt, this, instantiatedGameObject, RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory.Stage.LocalInitialized);
#endif

                WorldAIManager.instance.AddCharacterToSpawnedCharactersList(instantiatedGameObject.GetComponent<AICharacterManager>());
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
                RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory.Observe(qaAttempt, this, instantiatedGameObject, RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory.Stage.Registered);
#endif

//                ChangeElement(instantiatedGameObject);

            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            }
            finally { RPGKarawara.DevTools.Performance.PerformanceAiCreationHistory.FinishAttempt(qaAttempt); }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        public GameObject QaPrefab => characterGameObject;
        public GameObject QaSpawned => instantiatedGameObject;
        void OnEnable() { RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.Enabled); }
        void OnDisable() { RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.Disabled); }
        void OnDestroy() { RPGKarawara.DevTools.Performance.PerformanceAuxiliaryContext.Notify(this, RPGKarawara.DevTools.Performance.PerformanceAuxiliaryEvent.Destroyed); }
#endif
}
}
