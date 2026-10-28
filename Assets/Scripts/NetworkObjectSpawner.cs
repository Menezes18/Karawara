using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace RPGKarawara
{
    public class NetworkObjectSpawner : MonoBehaviour
    {
        [Header("Object")]
        [SerializeField] protected GameObject networkGameObject;
        [SerializeField] protected GameObject instantiatedGameObject;

        private void Awake()
        {

        }

        private void Start()
        {
            WorldObjectManager.GetOrCreateInstance().SpawnObject(this);
            gameObject.SetActive(false);
        }

        public virtual void AttemptToSpawnObject()
        {
            if (networkGameObject != null && instantiatedGameObject == null)
            {
                instantiatedGameObject = Instantiate(networkGameObject);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instantiatedGameObject, gameObject.scene);
                instantiatedGameObject.transform.position = transform.position;
                instantiatedGameObject.transform.rotation = transform.rotation;

            }
        }
    }
}
