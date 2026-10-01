# Fully Local Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rodar produção e DevPlayground integralmente sem Netcode, host, RPC e ownership, preservando o gameplay atual.
**Architecture:** Estado local com eventos substitui NetworkVariables; bootstrap e cena tornam a inicialização explícita. Migrar por famílias de componentes, manter bridges somente enquanto consumidores antigos existem e retirar a dependência de rede ao final.
**Tech Stack:** Unity 6000.4.6f1, C#, MonoBehaviour, Core/NUnit, Input System e ferramentas QA existentes.
**Spec:** [TO-BE](../specs/2026-10-01-karawara-to-be-design.md), §§5.1–5.4, 6; depende das entregas de proteção G1A e medição G1B.

## Global Constraints

- Unity permanece em `6000.4.6f1`; URP permanece em `17.4.0` durante estas fases.
- Plataforma de aceite: Windows x64 standalone; Editor e fixtures são evidências complementares.
- Preservar GUIDs/metas e referências de cenas, prefabs, UnityEvents e animation events em migrações.
- Preservar saves reais. Testes usam diretório temporário injetado; restauração manual usa cópias e um diretório de QA separado.
- Não editar código de terceiros ou gerado: `PlayerControls.cs`, xNode, DOTween, PathCreator e plugins ficam fora das refatorações próprias.
- Sem novo framework de DI, event bus global ou persistência cloud. Usar composição explícita e eventos tipados no dono do estado.
- Nenhum ganho de performance é aceito só por reduzir draw calls; comparar frame time, memória e aparência nas mesmas condições.
- Compilação, teste automatizado, gameplay e desempenho são gates distintos; relatório deve dizer quais foram executados.
- Não fazer commit/push/merge automático durante a execução futura sem respeitar as instruções da sessão e a baseline de alterações locais.
- Combate atual é a referência; Freeflow só entra na fase 4.

## Review Focus

- NetworkVariable aparentemente ornamental dispara side effects de HUD/equipamento — tarefas 1/2.
- Despawn/desativação/troca de cena fecha hitbox e remove callbacks — tarefas 3/4.
- Entrada direta em cena e retorno ao menu duplicam player/contexto — tarefa 4.
- Loading com Time.timeScale0 precisa concluir ou abortar com erro recuperável — tarefa 4.
- DevTools ainda chamam Netcode depois de produção migrar — tarefa 5.

## Task 1: Manifest da migração e estado local observável

**Files:**
- Create: `Assets/Karawara/Runtime/Core/Characters/CharacterRuntimeState.cs`, `CharacterRegistry.cs`, `ICharacterRegistry.cs`.
- Create: `Assets/DevTools/Editor/LocalRuntimeAudit.cs`.
- Test: `Assets/Tests/EditMode/CharacterRuntimeStateTests.cs`, `CharacterRegistryTests.cs`.
- Evidence: `Docs/Validation/TOBE/02-network-migration-map.md`.

**Interfaces:**
- Produces: CharacterRuntimeState e ICharacterRegistry conforme §6; `SetMaxHealth(int)`, `SetHealth(int)`, `SetTarget(CharacterRuntimeState)`; setters para atributos/flags com eventos somente onde há consumidores.
- Produces: `LocalRuntimeAudit.Run():void`, MenuItem `Karawara/Validation/TOBE/Audit Local Runtime`; inventário de scripts, prefabs, cenas, eventos e packages; não editar durante inventário.

- [ ] **Step 1: Inventariar cada dependência** com `rg -n 'Unity.Netcode|NetworkVariable|NetworkBehaviour|NetworkObject|IsOwner|IsServer|IsHost|ServerRpc|ClientRpc|OnNetworkSpawn|OnNetworkDespawn' Assets Packages`. Abrir cada match relevante, incluindo DevTools e código fora de Assets/Scripts. Mapear campo→escritores→subscribers→substituição→fixture; descartar só campos puramente de sincronização. Enumerar GUIDs de NetworkManager/NetworkObject/transforms e referências serializadas nas sete cenas, dependências e playground.
- [ ] **Step 2: Criar testes** `HealthClampsToMax`, `HealthChangedFiresOnce`, `DeathIsIdempotent`, `TargetClearsOnUnregister`, `RegistryDoesNotReturnInactiveCharacter`. Assertions: HP acima de MaxHealth vira MaxHealth; morte não emite evento duplicado; unregister invalida lookup.
- [ ] **Step 3: Executar V1** com `Karawara.Tests.Characters`; confirmar red antes de criar estado/registro.
- [ ] **Step 4: Implementar contratos e registro**; sem referências Unity.Netcode em Core. Validar testes green e registrar campo/consumidor omitido como erro do manifest, não como comportamento opcional.
- [ ] **Step 5: Preparar checkpoint** `refactor: define local character state and migration map`.

## Task 2: Player, equipamento, stats e apresentação locais

**Files:**
- Modify: `Assets/Scripts/States/Character/CharacterManager.cs`, `CharacterNetworkManager.cs`, `CharacterAnimatorManager.cs`, `CharacterCombatManeger.cs`, `CharacterLocomotionManager.cs`.
- Modify: `Assets/Scripts/States/Character/Player/PlayerManager.cs`, `PlayerNetworkManager.cs`, `PlayerAnimatorManager.cs`, `PlayerStatsManager.cs`, `PlayerCombatManager.cs`, `PlayerEquipmentManager.cs`, `PlayerLocomotionManager.cs`, `PlayerUIHudManager.cs`.
- Modify: `Assets/Scripts/WeaponItemAction.cs`, `LightAttackWeaponItemAction.cs`, `HeavyAttackWeaponItemAction.cs`, `Assets/Scripts/Item/CastIncantationAction.cs` e consumidores adicionais enumerados no manifest da tarefa 1.
- Create: `Assets/DevTools/Editor/LocalPlayerRegression.cs`.

**Interfaces:**
- Consumes: CharacterRuntimeState, registry.
- Produces: `CharacterManager.InitializeLocal(CharacterRuntimeState,ICharacterRegistry):void`, `ShutdownLocal():void`; player initialization conecta os componentes uma vez. Bridge existente mantém campos/métodos serializados enquanto cada consumidor migra, com registro de remoção na tarefa 5.
- Produces: vida/atributos/flags/target são estado local; animação/equipamento usam chamadas locais e eventos de mudança.

- [ ] **Step 1: Criar fixture** `Karawara/Validation/TOBE/Run Local Player Regression` com movimentação, dano/HP/HUD, rolagem, salto, carga de magia, troca de arma, morte/revive e cleanup. Assertar que não há NetworkManager criado pela fixture e input do único player altera o mesmo estado exibido no HUD.
- [ ] **Step 2: Executar V2**; falhas esperadas são a dependência real de rede/estado ainda ausente, sem fingir IsOwner=true.
- [ ] **Step 3: Migrar player por campo do manifest**: ownership guards saem; OnNetworkSpawn/Despawn vira Initialize/Shutdown; RPCs viram métodos locais; callbacks são bindados/desbindados simetricamente. Preservar velocidade, HP, dano, animações e bindings atuais.
- [ ] **Step 4: Executar fixture e V3 gameplay**; primeiro clique de lança/caster, Q e Ctrl mantêm comportamento. Verificar listener/câmera únicos e callbacks sem duplicação após re-enable.
- [ ] **Step 5: Preparar checkpoint** `refactor: run player gameplay on local state`.

## Task 3: Dano, IA, bosses e objetos sem IDs de rede

**Files:**
- Modify: `Assets/Scripts/States/Colliders/DamageCollider.cs`, `MeleeWeaponDamageCollider.cs`, `Assets/Scripts/Item/Spell/SpellProjectileDamageCollider.cs`.
- Modify: `Assets/Scripts/States/AI/AIChar/AICharacterManager.cs`, `AICharacterNetworkManager.cs`, `AICharacterCombatManager.cs`, `Assets/Scripts/States/AI/AIBoss/AIBossCharacterManager.cs`, `AIBossCharacterNetworkManager.cs`.
- Modify: `Assets/Scripts/States/WorldManager/WorldAIManager.cs`, `WorldObjectManager.cs`, `WorldGameSessionManager.cs`, `Assets/Scripts/NetworkObjectSpawner.cs`, `BossSpawnerInput.cs`, `SiteOfGraceInteractable.cs`.
- Other exact files: completar manifest antes de editar subtipos Durk/Saci, estados, interactables, spawners e spells; revisá-lo com diff.
- Create: `Assets/DevTools/Editor/LocalWorldRegression.cs`.

**Interfaces:**
- Consumes: registry e estado local.
- Produces: dano em referência local de CharacterManager/receptor; `InitializeLocal/ShutdownLocal` para IA; spawner local usa Instantiate e lifecycle da cena; IDs locais nunca entram em save.
- CucaBoss/HealthManager já locais preservam comportamento; adaptar somente dependências efetivamente encontradas.

- [ ] **Step 1: Criar fixture** `Karawara/Validation/TOBE/Run Local World Regression`: inimigo nasce/pensa/ataca/morre, bosses despertam/mudam fase/morrem, projectile pertence ao atirador, checkpoint e fog wall respondem. Assertar cada dano uma vez e zero inimigos/targets stale após unload.
- [ ] **Step 2: Executar V2** e registrar dependências de spawn/dano atuais.
- [ ] **Step 3: Migrar FSM e spawners**; clonar estados mutáveis por inimigo, remover ownership/NetworkObjectId lookup e converter métodos RPC. Subscription de boss bar e fog wall pertence ao objeto/cena e termina no Shutdown. Limpar clones owned.
- [ ] **Step 4: Executar V2 e V3** nas sete cenas e playground; comparar quantidade de inimigos ativos e eventos com manifest/baseline.
- [ ] **Step 5: Preparar checkpoint** `refactor: migrate enemies damage and world objects to local runtime`.

## Task 4: Sessão, cenas e save no bootstrap local

**Files:**
- Create: `Assets/Karawara/Runtime/Core/Session/SessionResult.cs`, `ISceneTransitionService.cs`.
- Create: `Assets/Scripts/Core/GameBootstrap.cs`, `GameSessionController.cs`, `SceneContext.cs`, `SceneTransitionService.cs` (movidos para Assets/Karawara na fase 3A).
- Modify: `Assets/Scripts/TitleScreenManager.cs`, `States/WorldManager/WorldSaveGameManager.cs`, `States/Character/Player/PlayerInputManager.cs`, `LoadScene.cs`.
- Test: `Assets/DevTools/Editor/LocalSessionRegression.cs`.

**Interfaces:**
- Produces: GameSessionController/ISceneTransitionService conforme §6. `SceneContext.IsReady:bool`; readiness é explícita, não espera cega por X frames.
- Consumes: storage e aplicação legada de 1A; a fachada WorldSave mantém UnityEvents e delega coordenação, sem readiness de IsSpawned/IsOwner.

- [ ] **Step 1: Criar casos** `MenuCreatesNoHost`, `NewGameCreatesExactlyOnePlayer`, `LoadAppliesOnceAfterReady`, `ReturnToMenuDisposesSession`, `DuplicateTransitionReturnsBusy`, `PausedLoadCompletes`, `CancelledLoadReleasesInput`, `DirectQaSceneUsesIsolatedSession`; inserir MenuItem `Karawara/Validation/TOBE/Run Local Session Regression`.
- [ ] **Step 2: Executar V2**; registrar falhas antes do novo bootstrap.
- [ ] **Step 3: Implementar sequência de §5.1**: validação → cena pronta → player → apply → HUD → input. Loading usa unscaled time, cancellation token e erro recuperável. Menu perde status/porta de host. Boot direto de QA não escreve saves reais.
- [ ] **Step 4: Executar V2, regressões 1A e V4**; jogar NewGame/load/retorno/menu sem sockets de gameplay. Confirmar processo ativo sem necessidade de porta 7777 livre.
- [ ] **Step 5: Preparar checkpoint** `feat: bootstrap a fully local game session`.

## Task 5: Prefabs, DevTools e retirada final de Netcode

**Files:**
- Modify: `Assets/Data/Prefabs/Player.prefab`, `Assets/Data/Prefabs/World Managers/World NetworkManager.prefab`, as sete `Assets/Scenes/*.unity` habilitadas e outros assets listados no manifest.
- Modify: `Assets/DevTools/Runtime/DevPlaygroundController.cs`, `Assets/DevTools/Editor/DevPlaygroundBuilder.cs`, `DevPlaygroundSmokeTest.cs`, `ProductionSceneSmokeTest.cs` e fixtures que iniciavam host.
- Delete after zero consumers: `Assets/Scripts/LocalHostStartup.cs` e scripts exclusivamente de rede listados no manifest, com `.meta` correspondente.
- Modify: `Packages/manifest.json`, `Packages/packages-lock.json`, `Docs/DevPlaygroundGuide.md`.

**Interfaces:**
- Produces: produção/QA usam mesmo bootstrap local; zero bridges de rede restantes. A pasta de projeto pode conter histórico em Docs, mas nenhum assembly compilado exige Netcode.

- [ ] **Step 1: Auditoria red de fechamento** pelo MenuItem Local Runtime: falha se scripts/imports/tipos de rede, componentes serializados ou pacote Netcode ainda existirem no escopo de runtime/QA.
- [ ] **Step 2: Migrar assets no Editor** conservando GUIDs de componentes próprios; remover NetworkObjects/transforms e bindings de host; substituir root de bootstrap sem apagar referências de componentes não relacionados. Preservar nome/GUID do prefab World NetworkManager durante transição, renomeando para Game Bootstrap somente em lote verificado.
- [ ] **Step 3: Migrar/regenerar playground e runners**; eliminar host/porta/configs e assertions de IsSpawned. Remover pacote Netcode e Multiplayer Center se sem consumidor; transport sai pelo resolvedor se órfão. Não atualizar outros packages.
- [ ] **Step 4: Executar audit green** e `rg -n 'Unity.Netcode|NetworkVariable|NetworkBehaviour|ServerRpc|ClientRpc|OnNetworkSpawn|OnNetworkDespawn' Assets -g '*.cs'`; saída vazia para código compilado. Auditoria por GUID verifica componentes de rede em prefabs/cenas; regex isolado não basta.
- [ ] **Step 5: Executar V3 e V4**; gerar/jogar Release e rever diff de lockfile/refs. Preparar checkpoint `refactor: remove netcode from production and development tools`.

## Gate G2 e rollback

Completar todas as famílias do manifest, não somente player. Registrar comparação da rota/combate atual, saves e ciclos de cena. Retornar ao menu deve limpar sessão, e abrir jogo/cena deve criar um único contexto. Preservar relatório de regressões herdadas.

Rollback por commits em ordem inversa restaura assets/metas/packages juntos. Não mesclar saves QA com pessoais. Não manter host oculto como fallback de entrega final.
