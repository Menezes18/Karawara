using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.SceneManagement;


namespace RPGKarawara
{
    public class Interactable : MonoBehaviour
    {
        public enum InteractionType{
            Teleport,
            ShowText,
            ActivateLever,
            SceneTeleport,
            SiteOfGraceInteractable
        }
        readonly HashSet<PlayerManager> interactingPlayers = new HashSet<PlayerManager>();
        public string interactableText; //  TEXT PROMPT WHEN ENTERING THE INTERACTION COLLIDER (PICK UP ITEM, PULL LEVER ECT)

        [SerializeField] protected Collider interactableCollider;   //  COLLIDER THAT CHECKS FOR PLAYER INTERACTION
        [SerializeField] protected bool hostOnlyInteractable = true;    //  WHEN ENABLED, OBJECT CANNOT BE INTERACTED WITH BY CO-OP PLAYERS
        public InteractionType interactionType;
        public Transform teleportTarget;
        public Lever lever;
        [SerializeField] public string sceneName;
        protected virtual void Awake()
        {
            //  CHECK IF ITS NULL, IN SOME CASES YOU MAY WANT TO MANUALLY ASIGN A COLLIDER AS A CHILD OBJECT (DEPENDING ON INTERACTABLE)
            if (interactableCollider == null)
                interactableCollider = GetComponent<Collider>();
        }
        protected virtual void Start()
        {

        }

        public virtual void Interact(PlayerManager player){
            Debug.Log("YOU HAVE INTERACTED!");



            //  REMOVE THE INTERACTION FROM THE PLAYER
            interactableCollider.enabled = false;
            player.playerInteractionManager.RemoveInteractionFromList(this);
            PlayerUIManager.instance.playerUIPopUpManager.CloseAllPopUpWindows();

            // Perform interaction based on the enum type
            switch (interactionType){
                case InteractionType.Teleport:
                    if (teleportTarget != null){
                        player.transform.position = teleportTarget.position;
                    }

                    break;

                case InteractionType.ShowText:
                    //Debug.Log(interactableText);
                    break;

                case InteractionType.ActivateLever:
                    if (lever != null){
                        lever.Activate();
                    }
                    break;
                case InteractionType.SiteOfGraceInteractable:
                    break;
                case InteractionType.SceneTeleport:
                    ChangeScene(sceneName);
                    break;
            }

            // Re-enable the collider after 1 second
            Invoke("EnableCollider", 0.5f);
        }

        public void EnableCollider(){
            interactableCollider.enabled = true;
        }
        private void ChangeScene(string sceneName){
            SceneManager.LoadScene(sceneName);
        }


        protected virtual void OnDisable()
        {
            CancelInvoke();
            foreach (var player in interactingPlayers)
                if (player != null && player.playerInteractionManager != null) player.playerInteractionManager.RemoveInteractionFromList(this);
            interactingPlayers.Clear();
        }

        public virtual void OnTriggerEnter(Collider other)
        {
            PlayerManager player = other.GetComponent<PlayerManager>();

            if (player != null)
            {




                //  PASS THE INTERACTION TO THE PLAYER
                interactingPlayers.Add(player);
                player.playerInteractionManager.AddInteractionToList(this);
            }
        }

        public virtual void OnTriggerExit(Collider other){
            PlayerManager player = other.GetComponent<PlayerManager>();

            if (player != null)
            {




                //  REMOVE THE INTERACTION FROM THE PLAYER
                interactingPlayers.Remove(player);
                player.playerInteractionManager.RemoveInteractionFromList(this);
                PlayerUIManager.instance.playerUIPopUpManager.CloseAllPopUpWindows();
            }
        }
    }
}
