using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace RPGKarawara
{
    public class Interactable : MonoBehaviour
    {
        public string interactableText; //  TEXT PROMPT WHEN ENTERING THE INTERACTION COLLIDER (PICK UP ITEM, PULL LEVER ECT)
        [SerializeField] protected Collider interactableCollider;   //  COLLIDER THAT CHECKS FOR PLAYER INTERACTION
        [SerializeField] protected bool hostOnlyInteractable = true;    //  WHEN ENABLED, OBJECT CANNOT BE INTERACTED WITH BY CO-OP PLAYERS

        protected virtual void Awake()
        {
            //  CHECK IF ITS NULL, IN SOME CASES YOU MAY WANT TO MANUALLY ASIGN A COLLIDER AS A CHILD OBJECT (DEPENDING ON INTERACTABLE)
            if (interactableCollider == null)
                interactableCollider = GetComponent<Collider>();
        }
        protected virtual void Start()
        {

        }

        public virtual void Interact(PlayerManager player)
        {
            Debug.Log("YOU HAVE INTERACTED!");

            if (!player.IsOwner)
                return;

            //  REMOVE THE INTERACTION FROM THE PLAYER
            interactableCollider.enabled = false;
            player.playerInteractionManager.RemoveInteractionFromList(this);
            PlayerUIManager.instance.playerUIPopUpManager.CloseAllPopUpWindows();
            Invoke("EnableCollider", 1f);
        }

        public virtual void OnTriggerEnter(Collider other)
        {
           
            PlayerManager player = other.GetComponent<PlayerManager>();

            if (player != null)
            {
                
                if (!player.playerNetworkManager.IsHost && hostOnlyInteractable)
                    return;

                if (!player.IsOwner)
                    return;

                //  PASS THE INTERACTION TO THE PLAYER
                player.playerInteractionManager.AddInteractionToList(this);
                
                
            }
        }

        public virtual void OnTriggerExit(Collider other)
        {
            PlayerManager player = other.GetComponent<PlayerManager>();

            if (player != null)
            {
                if (!player.playerNetworkManager.IsHost && hostOnlyInteractable)
                    return;

                if (!player.IsOwner)
                    return;

                //  REMOVE THE INTERACTION FROM THE PLAYER
               // interactableCollider.enabled = true;
                
                player.playerInteractionManager.RemoveInteractionFromList(this);
                PlayerUIManager.instance.playerUIPopUpManager.CloseAllPopUpWindows();
            }
        }


        private void EnableCollider()
        {
            interactableCollider.enabled = true;
        }
    }
}
