# Architecture and Folder Structure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Separar responsabilidades, ownership e dependências do gameplay local e organizar o código próprio por domínio.
**Architecture:** Composição explícita de sessão/player/cena, input com bloqueios por token, apresentação por presenters e definição separada de estado mutável. Extrair no caminho atual, depois mover arquivos preservando GUIDs e criar assembly Gameplay.
**Tech Stack:** Unity 6000.4.6f1, C#, Input System, asmdef e NUnit.
**Spec:** [TO-BE](../specs/2026-10-01-karawara-to-be-design.md), §§5.1–5.4, 6; depende de G2.

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
- Não alterar tuning/visual/gameplay nesta fase.

## Review Focus

- Pausa e diálogo simultâneos: liberar um bloqueio não libera o outro — tarefa 1.
- Re-enable/cena causa subscription duplicada — tarefa 2.
- Dois usuários de uma definição de skill não compartilham cooldown/desbloqueio — tarefa 3.
- Movimento de arquivo rompe UnityEvents/animation events sem erro C# — tarefa 4.
- Assembly novo não pode referenciar Assembly-CSharp — tarefa 5.

## Task 1: Input central com bloqueios independentes

**Files:**
- Create: `Assets/Karawara/Runtime/Core/Input/InputGate.cs`, `InputBlockReason.cs`.
- Create initially: `Assets/Scripts/Core/InputRouter.cs`, `PauseService.cs`.
- Modify: `Assets/Scripts/States/Character/Player/PlayerInputManager.cs`, `PlayerUIHudManager.cs`, `Assets/Scripts/SkillTree/PlayerSkillManager.cs`, `AtivarSkillPop.cs`.
- Modify: consumidores de pausa/cutscene/diálogo enumerados em `Docs/Validation/TOBE/03a-input-consumers.md` antes das mudanças.
- Test: `Assets/Tests/EditMode/InputGateTests.cs`; legacy runner `Assets/DevTools/Editor/InputRoutingRegression.cs`.

**Interfaces:**
- Produces: InputGate conforme §6; `InputBlockReason { Pause, Dialogue, Cutscene, Loading, Death }`.
- Produces: `InputRouter.Bind(PlayerManager,InputGate):void`, `Unbind():void`; comandos gameplay/skills/UI roteados uma vez. UI pause/back continua acessível enquanto gameplay bloqueado; navigation de diálogo pertence à sessão de diálogo.
- Produces: `PauseService.SetPaused(bool):void`, `Dispose():void`; possui token Pause e restaura Time.timeScale ao encerrar sessão. Outras telas adquirem seus próprios tokens; nenhum token de loading depende do tempo escalado.

- [ ] **Step 1: Criar testes** `TwoLocksRequireTwoReleases`, `DisposeTokenTwiceIsSafe`, `UnbindStopsCommands`, `SkillHotkeyDoesNotBypassDialogue`, `PauseBackStillRoutesWhenGameplayBlocked`. Assertar IsBlocked após liberar só Pause enquanto Dialogue existe; após ambos liberar, IsBlocked=false.
- [ ] **Step 2: Executar V1 para InputGate e V2** pelo menu `Karawara/Validation/TOBE/Run Input Routing Regression`; reproduzir disputa real de consumidores, não só contar callbacks simulados.
- [ ] **Step 3: Implementar tokens e router**; migrar Keyboard direto nos consumidores próprios, mantendo keybindings existentes. InputActions geradas não são editadas à mão; a tarefa5 organiza seu assembly sem alterar conteúdo. TimeScale é controlado pelo serviço de pausa, não cada tela.
- [ ] **Step 4: Executar testes green e fluxo físico** gameplay→diálogo→pause→fechar pause→fechar diálogo; nada move/ataca antes do último bloqueio sair. Foco perdido não mantém movimento pressionado.
- [ ] **Step 5: Preparar checkpoint** `refactor: centralize gameplay input and independent input locks`.

## Task 2: Contextos e presenters com lifecycle explícito

**Files:**
- Create initially: `Assets/Scripts/Core/PlayerContext.cs`, `Assets/Scripts/UI/HudPresenter.cs`, `PauseMenuPresenter.cs`, `SaveSlotsPresenter.cs`.
- Modify: `Assets/Scripts/States/Character/Player/PlayerManager.cs`, `PlayerUIManager.cs`, `PlayerUIHudManager.cs`, `PlayerUIPopUpManager.cs`, `Assets/Scripts/Core/GameSessionController.cs`, `SceneContext.cs` (criados em 02).
- Modify: `Assets/Scripts/States/WorldManager/WorldSaveGameManager.cs`, `Assets/Scripts/LoadScene.cs`.
- Test: `Assets/DevTools/Editor/ContextLifecycleRegression.cs`.

**Interfaces:**
- Produces: `PlayerContext.Initialize(CharacterRuntimeState,ICharacterRegistry,InputGate):void`, `Dispose():void`; `HudPresenter.Bind(CharacterRuntimeState):void`, `Unbind():void`.
- Context resolve componentes uma vez; bootstrap possui serviços e SceneContext possui atores. WorldSave fica fachada de storage/captura legado até 3B; SceneTransition owns loading UI/cena, e conteúdo opcional de AssetData pertence a scene initializer separado.

- [ ] **Step 1: Criar casos** `HudRebindShowsCurrentState`, `TenEnableCyclesHaveOneSubscription`, `SceneUnloadReleasesSceneReferences`, `ReturnToMenuRemovesPlayerContext`, `LoadFailureLeavesRecoverableMenu`; MenuItem `Karawara/Validation/TOBE/Run Context Lifecycle Regression`.
- [ ] **Step 2: Executar V2** e registrar qualquer subscription/referência retida.
- [ ] **Step 3: Extrair responsabilidades**: presenters só UI; player/context só composição local; sessão só coordenação; scene service só transição. Remover Find/singleton de caminhos de ação migrados, injetando referência no Bind/Initialize. Não adicionar util genérico para esconder dependência.
- [ ] **Step 4: Executar V2 e V3**; HP/HUD/câmera/skills correspondem ao contexto atual após load e troca de cena; callbacks antigos não atuam na UI nova.
- [ ] **Step 5: Preparar checkpoint** `refactor: isolate contexts presentation and scene orchestration`.

## Task 3: Definições imutáveis e estado por usuário

**Files:**
- Create: `Assets/Karawara/Runtime/Core/Progression/SkillRuntimeState.cs`, `SkillProgressState.cs`, `WorldProgressState.cs`.
- Modify: `Assets/Scripts/SkillTree/SkillsData/Skill.cs`, `Assets/Scripts/SkillTree/PlayerSkillManager.cs`, `SkillTreeUiManager.cs`, `Assets/Scripts/States/AI/AIState.cs`, `Assets/Scripts/States/Character/Player/PlayerInventoryManager.cs`.
- Test: `Assets/Tests/EditMode/SkillRuntimeStateTests.cs`, `Assets/DevTools/Editor/DefinitionOwnershipRegression.cs`.

**Interfaces:**
- Produces: `SkillRuntimeState(string definitionId,float cooldownSeconds)`, `TryActivate(float gameplayTime):bool`, `Progress(float gameplayTime):float`; unlock/equip pertencem a SkillProgressState, não ao asset.
- Produces: WorldProgressState com checkpoints, bosses, missões e objetos persistentes por ID; 3B preenche IDs estáveis e DTOs. Fase 3A não inventa IDs nem migra arquivos de save.
- Dados de ataque/skill/arma são definições; qualquer clone necessário tem owner e Dispose/Destroy simétrico.

- [ ] **Step 1: Criar testes** `SameDefinitionHasIndependentCooldown`, `LockedSkillCannotActivate`, `CooldownRespectsPausedGameplayTime`, `RuntimeUseDoesNotChangeDefinitionAsset`; assertar que usuárioB pode ativar mesmo após usuárioA ativar, e que pausado não reduz cooldown.
- [ ] **Step 2: Executar V1/V2** com `Karawara/Validation/TOBE/Run Definition Ownership Regression` e hashes dos assets antes/depois.
- [ ] **Step 3: Extrair runtime/progressão dos assets**; manter subclasses Execute existentes via adaptador durante a migração. Preservar efeitos e custo/duração; IA possui seus estados mutáveis, sem compartilhar flags entre inimigos.
- [ ] **Step 4: Executar testes green e V3**; validar pet/skills/bosses e ausência de alteração de assets. Dispose de clones é auditado em ciclos de cena.
- [ ] **Step 5: Preparar checkpoint** `refactor: separate content definitions from runtime progression`.

## Task 4: Mover código próprio por domínio preservando referências

**Files:**
- Create during execution: `Docs/Validation/TOBE/03a-file-move-map.csv`, `architecture-ownership.md`.
- Move: scripts próprios inventariados de `Assets/Scripts` para `Assets/Karawara/Runtime/{Session,Input,Characters,Combat,AI,Progression,Persistence,UI}` com `.meta`; Core contém somente contratos/modelos puros já criados.
- Move: render feature próprio para `Assets/Karawara/Runtime/Rendering` somente após mapear referências/dependências; conteúdo artístico permanece nos caminhos existentes.
- Modify: consumers de namespace e bridges conforme mapa; não mover terceiros localizados dentro de Assets/Scripts.

**Interfaces:**
- Produces: mapa source→destination→GUID→tipo→responsabilidade→consumidores serializados. Mantém nomes de métodos ligados a UnityEvents/animation events até migração explícita.

Destinos fixados para os planos seguintes, todos sob `Assets/Karawara/Runtime/`:

| Origem atual/da fase2 | Destino |
|---|---|
| `Assets/Scripts/Core/GameBootstrap.cs`, GameSessionController, SceneContext, SceneTransitionService | `Session/` com os mesmos filenames |
| `Assets/Scripts/Core/InputRouter.cs`, PauseService | `Input/` com os mesmos filenames |
| `Assets/Scripts/Core/PlayerContext.cs` | `Characters/Player/PlayerContext.cs` |
| `Assets/Scripts/States/Character/Player/*.cs` | `Characters/Player/` exceto scripts UI, que vão a `UI/` |
| `Assets/Scripts/States/Character/*.cs` | `Characters/` exceto CharacterSaveData, que vai a `Persistence/` |
| `Assets/Scripts/States/WorldManager/WorldSaveGameManager.cs` e `SaveFileDataWriter.cs` da raiz | `Persistence/` |
| `Assets/Scripts/Item.cs`, WorldItemDatabase e arquivos próprios de inventário | `Characters/` |
| `Assets/Scripts/SkillTree/SkillsData/Skill.cs` | `Progression/SkillsData/Skill.cs` |
| PlayerSkillManager e SkillTreeUiManager | `Progression/` |
| `Assets/Scripts/States/Colliders/*.cs` | `Combat/Colliders/` |
| LightAttackWeaponItemAction, HeavyAttackWeaponItemAction, WeaponItemAction | `Combat/` |
| `Assets/Scripts/States/AI/AIChar/*.cs`, `States/AI/AIBoss/*.cs` | `AI/AIChar/`, `AI/AIBoss/` |
| `Assets/Scripts/States/State/*.cs` | `AI/States/` |
| CucaBoss e HealthManager em `Assets/Scripts/Boss Cuca/` | `AI/Cuca/` sem mover conteúdo de terceiros da subárvore |
| SiteOfGraceInteractable e GateActivator da raiz | `Progression/` |
| `Assets/Scripts/Missao/Script/MissaoManager.cs` | `Progression/Missions/MissaoManager.cs` (preservar filename; classe é MissionManager) |
| Presenters criados em tarefa2 | `UI/` |

Outros scripts próprios recebem destino explícito no CSV antes do movimento. Qualquer alteração dos destinos fixados precisa atualizar 3B/4/5 no mesmo checkpoint. Não renomear arquivos/pastas por suposição ao executar plano futuro.

- [ ] **Step 1: Capturar mapa e baseline de referências** no Editor, incluídos scripts legados e componentes ausentes herdados; saída CSV com todos os arquivos próprios a mover e exclusões de terceiros/gerados.
- [ ] **Step 2: Mover lote Session/Input/Characters** via AssetDatabase.MoveAsset ou file+meta juntos com Unity fora da importação; reimportar e validar GUIDs antes do próximo lote. Nenhum serviço Unity dependente de Gameplay entra na subárvore Core.
- [ ] **Step 3: Mover Combat/AI/Progression/Persistence/UI/Rendering** por lote; conferir scripts e referências de prefab/cena. Não renomear classe e arquivo simultaneamente sem um caso de migração.
- [ ] **Step 4: Executar V3 e fixture de referências**; nenhuma nova referência ausente, UnityEvents e animation events exercitados, hashes/GUIDs iguais para assets movidos. Remover somente wrappers/folders sem consumidores demonstrados.
- [ ] **Step 5: Preparar checkpoint por lote** com escopo de caminhos explícito; atualizar os planos 3B/4/5 com o mapa se houver decisão diferente do destino proposto.

## Task 5: Assembly Gameplay e gates de arquitetura

**Files:**
- Create: `Assets/Karawara/Runtime/Karawara.Gameplay.asmdef` e `.meta`; `Assets/Tests/PlayMode/Karawara.Gameplay.PlayModeTests.asmdef` e `.meta`.
- Create: `Assets/Tests/EditMode/Gameplay/Karawara.Gameplay.EditModeTests.asmdef` e `.meta`, referenciando Core/Gameplay.
- Create: `Assets/Karawara/Generated/Input/Karawara.Input.Generated.asmdef`, referenciando `Unity.InputSystem`.
- Move intact with metas: `Assets/Scripts/PlayerControls.cs` e `PlayerControls.inputactions` para `Assets/Karawara/Generated/Input/`, mantendo namespace/classe e bindings.
- Modify: asmdefs de testes Core se necessário; DevTools passam a referenciar Gameplay via seu assembly existente ou Assembly-CSharp, sem circularidade.
- Create: `Assets/Tests/PlayMode/SessionLifecycleTests.cs`, `InputRoutingTests.cs`, `DefinitionOwnershipTests.cs`.
- Create during execution: `Docs/Validation/TOBE/03a-architecture-acceptance.md`.

**Interfaces:**
- Produces: Core puro; Gameplay→Core e pacotes realmente usados; testes→Core/Gameplay; Core não conhece player/UI/assets. Dependências de diálogo e terceiros devem apontar para seus asmdefs reais. Se código terceiro ficar em Assembly-CSharp e for consumido, extrair adapter fora de Gameplay ou dar assembly próprio ao terceiro sem alterar seu conteúdo; não referenciar Assembly-CSharp.

- [ ] **Step 1: Auditar grafo real de referências** antes de criar Gameplay; listar consumidores de classes ainda em Assembly-CSharp e resolver localização/adapters no mapa.
- [ ] **Step 2: Organizar wrapper gerado e criar Gameplay/testes** só após scripts próprios estarem dentro do assembly. Generated/Input recebe wrapper e inputactions juntos com GUIDs/conteúdo preservados; reimportar e conferir wrapper gerado única vez, hash do conteúdo e referências das ações. Gameplay referencia Karawara.Input.Generated/Core/pacotes reais. Migrar fixtures de 1A/02 para PlayMode tests onde possível, preservando também casos físicos/standalone.
- [ ] **Step 3: Executar V1 EditMode/PlayMode**; exigir ausência de ciclos e de referências inversas Core→Gameplay. Executar compilação Release/Development por V3.
- [ ] **Step 4: Executar V3/V4** e comparar baseline pós-G2; documentar owners/subscriptions/clones, pastas e APIs no ownership report. Zero regressão funcional nova e nenhum wrapper de rede.
- [ ] **Step 5: Preparar checkpoint** `refactor: organize gameplay assemblies and verify architecture boundaries`.

## Gate G3A e rollback

Reorganização só encerra com referências serializadas e fluxo real validados. C# compilando não prova integridade de cenas/controllers. Não avaliar qualidade pela quantidade de managers/interfaces, mas por responsabilidade, ownership e ausência de dependências circulares.

Rollback restaura lote de arquivos e seus metas em conjunto. Após mover pastas, atualizar paths de testes e plano antes da tarefa seguinte; nunca manter dois tipos com a mesma identidade para facilitar rollback.
