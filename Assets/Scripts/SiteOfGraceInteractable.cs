using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

using UnityEngine.InputSystem;



namespace RPGKarawara
{
    public class SiteOfGraceInteractable : Interactable
    {
        [Header("Site Of Grace Info")]
        [SerializeField] int siteOfGraceID;
        public NetworkVariable<bool> isActivated = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [Header("VFX")]
        [SerializeField] GameObject activatedParticles;

        [Header("Interaction Text")]
        [SerializeField] string unactivatedInteractionText = "Restore Site Of Grace";
        [SerializeField] string activatedInteractionText = "Rest";
        [SerializeField] private GameObject painel;
        private PlayerManager _playerManager;
        protected override void Start()
        {
            base.Start();

            if (IsOwner)
            {
                if (WorldSaveGameManager.instance.currentCharacterData.sitesOfGrace.ContainsKey(siteOfGraceID))
                {
                    isActivated.Value = WorldSaveGameManager.instance.currentCharacterData.sitesOfGrace[siteOfGraceID];
                }
                else
                {
                    isActivated.Value = false;
                }
            }

            if (isActivated.Value)
            {
                interactableText = activatedInteractionText;
            }
            else
            {
                interactableText = unactivatedInteractionText;
            }
        }
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            //  IF WE JOIN WHEN THE STATUS HAS ALREADY CHANGED, WE FORCE THE ONCHANGE FUNCTION TO RUN HERE UPON JOINING
            if (!IsOwner)
                OnIsActivatedChanged(false, isActivated.Value);

            isActivated.OnValueChanged += OnIsActivatedChanged;
        }

        public void Update(){
            if (Keyboard.current.escapeKey.wasReleasedThisFrame){
                
               
                painel.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked; // Libera o cursor
                Cursor.visible = false; // Torna o cursor visível (opcional)
                Time.timeScale = 1f;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            isActivated.OnValueChanged -= OnIsActivatedChanged;
        }

        private void RestoreSiteOfGrace(PlayerManager player){
            var painelUI = FindObjectOfType<PlayerUIHudManager>();
            painelUI.paneldesativar = true;
            bool panelActive = !painel.activeSelf;
            painel.SetActive(panelActive);
            _playerManager = player;

            // Travar/Desativar o Cursor e a Câmera
            if (panelActive){

                Cursor.lockState = CursorLockMode.None; // Trava o cursor
                Cursor.visible = true; // Torna o cursor visível (opcional)
            }
            else{

                Cursor.lockState = CursorLockMode.Locked; // Libera o cursor
                Cursor.visible = false; // Torna o cursor visível (opcional)

            }

            // Pausar/Despausar o jogo (opcional)
            Time.timeScale = panelActive ? 0 : 1;
        }


        public void ChangeElement(string element){
            
            var elemento = FindObjectOfType<ElementManager>();

            switch (element.ToLower())
            {
                case "fogo":
                    elemento.SetElement(Element.Fire);
                    break;
                case "agua":
                    elemento.SetElement(Element.Water);
                    break;
                default:
                    Debug.LogWarning("Elemento não reconhecido.");
                    break;
            }
           

        }
        public void ResetSite(){
            isActivated.Value = true;

            //  IF OUR SAVE FILE CONTAINS INFO ON THIS SITE OF GRACE, REMOVE IT
            if (WorldSaveGameManager.instance.currentCharacterData.sitesOfGrace.ContainsKey(siteOfGraceID))
                WorldSaveGameManager.instance.currentCharacterData.sitesOfGrace.Remove(siteOfGraceID);

            //  THEN RE-ADD IT WITH THE VALUE OF "TRUE" (IS ACTIVATED)
            WorldSaveGameManager.instance.currentCharacterData.sitesOfGrace.Add(siteOfGraceID, true);

            //player.playerAnimatorManager.PlayTargetActionAnimation("Activate_Site_Of_Grace_01", true);
            //  HIDE WEAPON MODELS WHILST PLAYING ANIMATION IF YOU DESIRE

            PlayerUIManager.instance.playerUIPopUpManager.SendGraceRestoredPopUp("Restore enemy to Totem");
            RestAtSiteOfGrace(_playerManager);
            StartCoroutine(WaitForAnimationAndPopUpThenRestoreCollider());
        }

        private void RestAtSiteOfGrace(PlayerManager player)
        {
            Debug.Log("RESTING");

            //  TEMPORARY CODE SECTION
            interactableCollider.enabled = true; // TEMPORARILY RE-ENABLING THE COLLIDER HERE UNTIL WE ADD THE MENU SO YOU CAN RESPAWN MONSTERS INDEFINITELY
                                                 //player.playerNetworkManager.currentHealth.Value = player.playerNetworkManager.maxHealth.Value;



            //  REFILL FLASKS (TO DO)
            //  UPDATE/FORCE MOVE QUEST CHARACTERS (TO DO)
            WorldAIManager.instance.ResetAllCharacters();
        }

        private IEnumerator WaitForAnimationAndPopUpThenRestoreCollider()
        {
            yield return new WaitForSeconds(2); //  THIS SHOULD GIVE ENOUGH TIME FOR THE ANAIMATION TO PLAY AND THE POP UP TO BEGIN FADING
            interactableCollider.enabled = true;
        }

        private void OnIsActivatedChanged(bool oldStatus, bool newStatus)
        {
            if (isActivated.Value)
            {
                //  PLAY SOME FX HERE IF YOU'D LIKE OR ENABLE A LIGHT OR SOMETHING TO INDICATE THIS CHECK POINT IS ON
                activatedParticles.SetActive(true);
                interactableText = activatedInteractionText;
            }
            else
            {
                interactableText = unactivatedInteractionText;
            }
        }

        public override void Interact(PlayerManager player)
        {
            base.Interact(player);
            RestoreSiteOfGrace(player);
            player.playerNetworkManager.currentHealth.Value = player.playerNetworkManager.maxHealth.Value;
        }
    }
}
