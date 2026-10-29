using Karawara.Core.Characters;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.SceneManagement;

namespace RPGKarawara
{
    public class AIBossCharacterManager : AICharacterManager
    {
        public int bossID = 0;

        [Header("Music")]
        [SerializeField] AudioClip bossIntroClip;
        [SerializeField] AudioClip bossBattleLoopClip;

        [Header("Status")]
        public ObservableValue<bool> bossFightIsActive = new ObservableValue<bool>(false);
        public ObservableValue<bool> hasBeenAwakened = new ObservableValue<bool>(false);
        public ObservableValue<bool> hasBeenDefeated = new ObservableValue<bool>(false);
        [SerializeField] List<FogWallInteractable> fogWalls;
        [SerializeField] string sleepAnimation;
        [SerializeField] string awakenAnimation;

        [Header("Phase Shift")]
        public float minimumHealthPercentageToShift = 50;
        [SerializeField] string phaseShiftAnimation = "Phase_Change_01";
        [SerializeField] CombatStanceState phase02CombatStanceState;

        [Header("States")]
        [SerializeField] BossSleepState sleepState;
        BossSleepState sleepStateTemplate;
        bool sleepStateAcquired;
        UI_Boss_HP_Bar ownedBossHPBar;
        bool phaseShifted;

        protected override void OnEnable()
        {
            base.OnEnable();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        protected override void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            RemoveOwnedBossHPBar();
            base.OnDisable();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            RemoveOwnedBossHPBar();
        }

        private void RemoveOwnedBossHPBar(float delay = 0)
        {
            if (ownedBossHPBar != null) ownedBossHPBar.RemoveHPBar(delay);
            ownedBossHPBar = null;
        }

        //  WHEN THIS A.I IS SPAWNED, CHECK OUR SAVE FILE (DICTIONARY)
        //  IF THE SAVE FILE DOES NOT CONTAIN A BOSS MONSTER WITH THIS I.D ADD IT
        //  IF IT IS PRESENT, CHECK IF THE BOSS HAS BEEN DEFEATED
        //  IF THE BOSS HAS BEEN DEFEATED, DISABLE THIS GAMEOBJECT
        //  IF THE BOSS HAS NOT BEEN DEFEATED, ALLOW THIS OBJECT TO CONTINUE TO BE ACTIVE

        protected override void Awake()
        {
            base.Awake();
        }

        public override void InitializeLocal(CharacterRuntimeState state, ICharacterRegistry registry)
        {
            if (IsInitialized) return;
            base.InitializeLocal(state, registry);
            bossFightIsActive.OnValueChanged += OnBossFightIsActiveChanged;
            sleepStateTemplate = sleepState; sleepStateAcquired = true;
            sleepState = CreateRuntimeState(sleepStateTemplate);
            currentState = sleepState != null ? sleepState : idle;
            var data = WorldSaveGameManager.instance?.currentCharacterData;
            if (data != null)
            {
                if (!data.bossesAwakened.ContainsKey(bossID)) data.bossesAwakened[bossID] = false;
                if (!data.bossesDefeated.ContainsKey(bossID)) data.bossesDefeated[bossID] = false;
                hasBeenAwakened.Value = data.bossesAwakened[bossID];
                hasBeenDefeated.Value = data.bossesDefeated[bossID];
            }
            if (hasBeenAwakened.Value) currentState = idle;
            if (phaseShifted && phase02CombatStanceState != null)
            {
                combatStance = CreateRuntimeState(phase02CombatStanceState);
                if (bossFightIsActive.Value) currentState = combatStance;
            }
            RefreshFogWalls();
            WorldObjectManager.GetOrCreateInstance().FogWallsChanged += RefreshFogWalls;
            if (hasBeenDefeated.Value) { state.SetActive(false); return; }
            if (!hasBeenAwakened.Value && !string.IsNullOrEmpty(sleepAnimation))
                characterAnimatorManager.PlayTargetActionAnimation(sleepAnimation, true);
            OnBossFightIsActiveChanged(false, bossFightIsActive.Value);
        }

        public override void ShutdownLocal()
        {
            if (WorldObjectManager.instance != null) WorldObjectManager.instance.FogWallsChanged -= RefreshFogWalls;
            bossFightIsActive.OnValueChanged -= OnBossFightIsActiveChanged;
            if (sleepStateAcquired) { sleepState = sleepStateTemplate; sleepStateTemplate = null; sleepStateAcquired = false; }
            RemoveOwnedBossHPBar();
            if (IsInitialized && bossFightIsActive.Value && WorldSoundFXManager.instance != null) WorldSoundFXManager.instance.StopBossMusic();
            base.ShutdownLocal();
        }

        void RefreshFogWalls()
        {
            fogWalls = new List<FogWallInteractable>();
            if (WorldObjectManager.instance == null) return;
            foreach (var wall in WorldObjectManager.instance.fogWalls)
                if (wall != null && wall.fogWallID == bossID)
                {
                    fogWalls.Add(wall);
                    wall.isActive.Value = hasBeenAwakened.Value && !hasBeenDefeated.Value;
                }
        }

        bool deathProcessed;
        public override IEnumerator ProcessDeathEvent(bool manuallySelectDeathAnimation = false)
        {
            if (deathProcessed) yield break;
            deathProcessed = true;
            if (PlayerUIManager.instance != null && PlayerUIManager.instance.playerUIPopUpManager != null)
                PlayerUIManager.instance.playerUIPopUpManager.SendBossDefeatedPopUp("BOSS DERROTADO");


            {
                foreach (var hitbox in GetComponentsInChildren<DamageCollider>(true)) hitbox.DisableDamageCollider();
                characterNetworkManager.currentHealth.Value = 0;

                bossFightIsActive.Value = false;

                foreach (var fogWall in fogWalls)
                {
                    fogWall.isActive.Value = false;
                }

                //  RESET ANY FLAGS HERE THAT NEED TO BE RESET
                //  NOTHING YET

                //  IF WE ARE NOT GROUNDED, PLAY AN AERIAL DEATH ANIMATION

                if (!manuallySelectDeathAnimation)
                {
                    characterAnimatorManager.PlayTargetActionAnimation("Dead_01", true);
                }

                hasBeenDefeated.Value = true;

                var save = WorldSaveGameManager.instance;
                var data = save?.currentCharacterData;
                if (data != null)
                {
                    data.bossesAwakened[bossID] = true;
                    data.bossesDefeated[bossID] = true;
                    save.SaveGame();
                }
                WorldAIManager.instance?.RemoveCharacter(this);
                Registry?.Unregister(LocalId);
            }

            //  PLAY SOME DEATH SFX

            yield return new WaitForSeconds(5);

            //  AWARD PLAYERS WITH RUNES

            //  DISABLE CHARACTER
        }

        public void WakeBoss()
        {
            if (!IsInitialized || hasBeenDefeated.Value) return;

            {
                if (!hasBeenAwakened.Value)
                {
                    characterAnimatorManager.PlayTargetActionAnimation(awakenAnimation, true);
                }

                bossFightIsActive.Value = true;
                hasBeenAwakened.Value = true;
                currentState = idle;

                var data = WorldSaveGameManager.instance?.currentCharacterData;
                if (data != null) data.bossesAwakened[bossID] = true;

                for (int i = 0; i < fogWalls.Count; i++)
                {
                    fogWalls[i].isActive.Value = true;
                }
            }
        }

        private void OnBossFightIsActiveChanged(bool oldStatus, bool newStatus)
        {
            if (bossFightIsActive.Value)
            {
                if (WorldSoundFXManager.instance != null)
                    WorldSoundFXManager.instance.PlayBossTrack(bossIntroClip, bossBattleLoopClip);

                if (ownedBossHPBar != null) return;
                PlayerUIHudManager hud = PlayerUIManager.instance != null ? PlayerUIManager.instance.playerUIHudManager : null;
                if (hud == null || hud.bossHealthBarObject == null || hud.bossHealthBarParent == null) return;

                GameObject bossHealthBar =
                Instantiate(hud.bossHealthBarObject, hud.bossHealthBarParent);

                UI_Boss_HP_Bar bossHPBar = bossHealthBar.GetComponentInChildren<UI_Boss_HP_Bar>();
                if (bossHPBar != null)
                {
                    ownedBossHPBar = bossHPBar;
                    bossHPBar.EnableBossHPBar(this);
                }
                else Destroy(bossHealthBar);
            }
            else
            {
                RemoveOwnedBossHPBar(hasBeenDefeated.Value || isDead.Value ? 2.5f : 0);
                if (WorldSoundFXManager.instance != null) WorldSoundFXManager.instance.StopBossMusic();
            }
        }

        public void PhaseShift()
        {
            if (phaseShifted || !IsInitialized || State.IsDead) return;
            phaseShifted = true;
            DevTools.Performance.PerformanceActorLifecycleScope.Notify(this, DevTools.Performance.PerformanceActorLifecycle.PhaseChanged);
            characterAnimatorManager.PlayTargetActionAnimation(phaseShiftAnimation, true);
            if (phase02CombatStanceState == null) return;
            combatStance = CreateRuntimeState(phase02CombatStanceState);
            currentState = combatStance;
        }
    }
}
