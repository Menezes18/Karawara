using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

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
            WorldAIManager.instance.SpawnCharacter(this);
            gameObject.SetActive(false);
        }

        public void ChangeElement(GameObject character)
        {
            Debug.Log(characterElement);
            
            characterManager = character.GetComponent<AICharacterManager>();
            characterManager.characterElement = characterElement;
            characterManager.ChangeMaterial(agua, fogo, characterElement);
            if (boss){
                var AIBossCharacterManager = character.GetComponent<AIBossCharacterNetworkManager>();
                AIBossCharacterManager.fogoBoss = true;
            }
            
            
        }

        


        public void AttemptToSpawnCharacter()
        {
            if (characterGameObject != null)
            {
                instantiatedGameObject = Instantiate(characterGameObject, transform.position, transform.rotation);
                instantiatedGameObject.GetComponent<NetworkObject>().Spawn();
                
                WorldAIManager.instance.AddCharacterToSpawnedCharactersList(instantiatedGameObject.GetComponent<AICharacterManager>());
                
//                ChangeElement(instantiatedGameObject);
              
            }
        }
    }
}
