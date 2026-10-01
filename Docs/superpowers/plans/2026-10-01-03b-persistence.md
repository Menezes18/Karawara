# Versioned Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restaurar player e progresso de mundo em qualquer um dos três slots, em processo novo, com migração e recuperação seguras.
**Architecture:** DTO V1 puro, catálogo com IDs estáveis, repository sobre storage atômico e serviço de captura/aplicação independente da UI. Migrar arquivos legados sem inventar dados e manter original até conversão validada.
**Tech Stack:** Unity 6000.4.6f1, C#, System.IO, JsonUtility para DTOs simples e NUnit.
**Spec:** [TO-BE](../specs/2026-10-01-karawara-to-be-design.md), §§5.5, 6, V1/V4; depende de G3A e proteção 1A.

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
- Paths abaixo são destinos de 3A; conferir `03a-file-move-map.csv` antes de editar, sem criar cópia paralela.

## Review Focus

- Reordenar catálogo/build settings não muda significado de um save — tarefas 1/2.
- Skill legada ambígua nunca vira desbloqueio inventado — tarefa 2.
- ID desconhecido/schema futuro retorna erro recuperável sem sobrescrever — tarefa 3.
- Load enquanto player está em ação/pausa não aplica estado parcialmente — tarefa 4.
- Morte de boss, missão concluída e portão já aberto não reaparecem após restart — tarefas 5/6.

## Task 1: IDs persistentes e catálogo legado congelado

**Files:**
- Create: `Assets/Karawara/Runtime/Core/Content/ContentDefinition.cs`, `IContentCatalog.cs`, `LegacyIdMap.cs`.
- Create: `Assets/Karawara/Runtime/Progression/StableContentCatalog.cs`, `PersistentObjectId.cs`, `LegacyIdMapAsset.cs` (wrapper ScriptableObject do mapping puro).
- Create: `Assets/Karawara/Editor/StableContentCatalogValidator.cs`.
- Modify: `Assets/Karawara/Runtime/Characters/Item.cs`, `Assets/Karawara/Runtime/Progression/SkillsData/Skill.cs`, `Assets/Karawara/Runtime/Characters/WorldItemDatabase.cs` segundo mapa 3A.
- Create during execution: `Assets/Karawara/Data/StableContentCatalog.asset`, `LegacyIdMap.asset`; scenes/prefabs próprios receberão IDs por inventário.
- Test: `Assets/Tests/EditMode/StableContentCatalogTests.cs`, `LegacyIdMapTests.cs`.

**Interfaces:**
- Produces: ContentDefinition com `Id:string`, `Kind:ContentKind`; `ContentKind { Scene, Item, Skill, Checkpoint, Boss, Mission, WorldObject }`.
- Produces: IContentCatalog.TryResolve conforme §6; catálogo Gameplay associa definition ID→asset/cena sem expor objetos Unity ao DTO.
- Produces: LegacyIdMap com mapping explícito antigo índice/ID→string. Slots atuais são `characterSlot_01`, `_02`, `_03` sem extensão; manter nomes durante conversão.

- [ ] **Step 1: Congelar mapping legado** antes de reordenar listas: relacionar IDs numéricos atribuídos por WorldItemDatabase, sceneIndex em build settings e IDs de checkpoint/boss à origem/GUID. Recuperar baseline `a2b43f09` se etapas anteriores alteraram ordem; não tratar índice atual diferente como identidade legada.
- [ ] **Step 2: Criar testes** `DuplicateIdsAreRejected`, `ReorderDoesNotChangeIdentity`, `LegacyNumericIdResolvesOriginalAsset`, `UnknownLegacyIdIsNotGuessed`; assertar que item antigo0 continua resolvendo o mesmo GUID mesmo após inverter lista.
- [ ] **Step 3: Executar V1**; red para ausência de catálogo/mapping.
- [ ] **Step 4: Adicionar IDs serializados gerados uma vez no Editor**, validator por tipo/cena e mapping legado. Duplicação de prefab deve gerar ID de instância persistente novo quando necessário; IDs de definição podem ser compartilhados. Nunca gerar ID no Awake nem derivar de índice/nome editável.
- [ ] **Step 5: Executar V1 e validator** MenuItem `Karawara/Validation/TOBE/Validate Stable Content IDs`; zero IDs ausentes/duplicados nos conteúdos de produção e mapping íntegro. Preparar checkpoint `feat: introduce stable content ids and legacy catalog mapping`.

## Task 2: DTO V1, validação e migração de legados

**Files:**
- Create: `Assets/Karawara/Runtime/Core/Persistence/SaveGameDataV1.cs`, `PlayerSnapshot.cs`, `WorldSnapshot.cs`, `SaveDataValidator.cs`, `LegacySaveMigration.cs`, `LegacySaveDto.cs`.
- Create: `Assets/Karawara/Runtime/Persistence/LegacySaveJsonCodec.cs` para desserialização Unity do formato antigo.
- Modify: adaptador legado `Assets/Karawara/Runtime/Persistence/CharacterSaveData.cs` conforme mapa 3A.
- Test: `Assets/Tests/EditMode/SaveDataValidatorTests.cs`, `LegacySaveMigrationTests.cs`.

**Interfaces:**
- Produces: SaveGameDataV1 conforme §5.5; player/world separados, listas de entradas ID→estado em vez de Dictionary não serializável por JsonUtility. Skill slots têm exatamente três IDs opcionais; unlocks são lista distinta.
- Produces: `SaveDataValidator.Validate(SaveGameDataV1,IContentCatalog):SaveResult<bool>`.
- Produces: `LegacySaveJsonCodec.Decode(string):SaveResult<LegacySaveDto>`; `LegacySaveMigration.Convert(LegacySaveDto,int slotId,LegacyIdMap):SaveResult<SaveGameDataV1>`. Parsing Unity fica em Gameplay; conversão pura em Core. Referências Unity do JSON legado só são aceitas se mapping comprova resolução. `schemaVersion` ausente é legado, não V1 válido.

- [ ] **Step 1: Criar testes** `V1RoundTripContainsNoUnityObjects`, `InvalidSlotRejected`, `InvalidPositionRejected`, `DuplicateWorldKeysRejected`, `LegacySceneUsesFrozenMap`, `AmbiguousSkillReturnsMigrationRequired`, `UnknownFutureVersionRejected`. Assertar schemaVersion1, slots1..3, floats finitos, HP clamp validado e três slots de skill.
- [ ] **Step 2: Executar V1** com `Karawara.Tests.Persistence`; fixtures legadas são cópias QA conhecidas, nunca arquivos pessoais referenciados pelo teste.
- [ ] **Step 3: Implementar DTO e validação**: validar IDs por kind, scene gameplay permitida, relação unlocked/equipped, contagens não negativas e chaves únicas. V1 não salva objetos Unity, posição com altura artificial nem estados transitórios. Campos legados ausentes recebem defaults documentados coerentes com NewGame; informação desconhecida vira diagnóstico, não progresso novo.
- [ ] **Step 4: Implementar conversão** apenas de dados verificáveis; preservar arquivo original como `.legacy` ao confirmar conversão. Erro mantém original intacto e orienta recuperação sem write automático.
- [ ] **Step 5: Executar casos green** e comparar diff semântico dos campos legados. Preparar checkpoint `feat: define validated save schema and non-destructive migration`.

## Task 3: Repository versionado e perfis de slot

**Files:**
- Create: `Assets/Karawara/Runtime/Core/Persistence/ISaveRepository.cs`, `SaveProfile.cs`.
- Create: `Assets/Karawara/Runtime/Persistence/VersionedSaveRepository.cs`, `SaveJsonCodec.cs`.
- Modify: `Assets/Karawara/Runtime/UI/SaveSlotsPresenter.cs`, `Assets/Karawara/Runtime/Persistence/SaveFileDataWriter.cs`.
- Test: `Assets/Tests/EditMode/Gameplay/VersionedSaveRepositoryTests.cs`, no asmdef Gameplay.EditModeTests criado em 3A.

**Interfaces:**
- Produces: ISaveRepository.Load/Save/Delete conforme §6; `ListProfiles():IReadOnlyList<SaveProfile>`; perfil contém SlotId/Status/Name/SecondsPlayed/SceneId/Message, statuses Empty/Valid/Recoverable/Invalid.
- Consumes: AtomicFileStore de 1A, DTO/validator/migrator de tarefas1/2. Codec serializa DTO completo; repository valida slot externo igual ao interno.

- [ ] **Step 1: Criar testes** `RoundTripSlotTwo`, `WrongInternalSlotRejected`, `InvalidPrimaryUsesValidatedBackup`, `FutureVersionDoesNotOverwrite`, `DeleteClearsRecoveryFiles`, `MigrationPreservesLegacyOriginal`, `ProfileReadDoesNotStartSession`.
- [ ] **Step 2: Executar V1**; o asmdef Gameplay.EditModeTests referencia Core/Gameplay, usando codec real, e não depende de Assembly-CSharp.
- [ ] **Step 3: Implementar repository** com serialização/operação por slot, validação, recovery e migração explícita. Mensagens de UI descrevem ação recuperável; existência física do arquivo não equivale a save válido.
- [ ] **Step 4: Executar testes e falhas de IO**; verificar last-good primary, backup íntegro e nenhum auto write durante ListProfiles/Load que não seja confirmação de migração.
- [ ] **Step 5: Preparar checkpoint** `feat: add validated versioned save repository and slot profiles`.

## Task 4: Captura/aplicação do player independente da UI

**Files:**
- Create: `Assets/Karawara/Runtime/Persistence/SaveGameService.cs`, `PlayerSaveAdapter.cs`.
- Modify: `Assets/Karawara/Runtime/Session/GameSessionController.cs`, `Assets/Karawara/Runtime/Characters/Player/PlayerManager.cs`, `PlayerInventoryManager.cs`, `PlayerEquipmentManager.cs`.
- Modify: `Assets/Karawara/Runtime/Progression/PlayerSkillManager.cs`, `SkillTreeUiManager.cs`, fachada WorldSave segundo mapa.
- Test: `Assets/Tests/PlayMode/PlayerSaveRoundTripTests.cs`.

**Interfaces:**
- Produces: SaveGameService.Capture/Apply conforme §6; `PlayerSaveAdapter.Capture():PlayerSnapshot`, `Apply(PlayerSnapshot):SaveResult<bool>`.
- Snapshot conserva player/stat/inventory/equipment/skills/tempo jogado; tempo cresce apenas durante gameplay não pausado. Apply só ocorre com scene ready e input bloqueado, usando generation de load; rollback de apply falho retorna ao menu sem salvar.

- [ ] **Step 1: Criar tests** `RestoresHealthStatsInventoryAndEquipment`, `RestoresUnlockedSkillsAndThreeSlots`, `SaveWhileTransitioningReturnsBusy`, `ApplyRunsOnce`, `PlaytimeExcludesPause`, `UnknownItemFailsBeforeMutatingPlayer`. Assertar igualdade semântica do snapshot antes/depois; skill cooldown começa resetado, não revive ação transitória.
- [ ] **Step 2: Executar V1 PlayMode** contra sessão QA local; comprovar falhas de dados ainda não capturados.
- [ ] **Step 3: Implementar snapshot/apply** usando runtime state e catálogos; validar tudo antes de mutar player, aplicar stats→inventário/equip→skills→posição→HUD. Remover leitura de slot01/global de apresentação. A UI só envia comandos ao serviço de sessão/save.
- [ ] **Step 4: Executar tests e V4** com processos distintos; slots1/2/3 têm valores, loadout e skills diferentes. Conferir campos e hashes dos saves QA; não aceitar fixture no mesmo processo como prova final.
- [ ] **Step 5: Preparar checkpoint** `feat: persist and restore complete player state`.

## Task 5: Persistência de mundo, missões e bosses

**Files:**
- Create: `Assets/Karawara/Runtime/Persistence/WorldSaveAdapter.cs`, `IPersistentWorldObject.cs`.
- Modify: `Assets/Karawara/Runtime/Progression/SiteOfGraceInteractable.cs`, `Assets/Karawara/Runtime/AI/AIBoss/AIBossCharacterManager.cs`, `Assets/Karawara/Runtime/AI/Cuca/CucaBoss.cs`, `HealthManager.cs` conforme mapa 3A.
- Modify: `Assets/Karawara/Runtime/Progression/Missions/MissaoManager.cs`, `Assets/Karawara/Runtime/Progression/GateActivator.cs`, `Assets/Karawara/Runtime/Session/SceneContext.cs` e outros persistentes do inventário desta tarefa.
- Test: `Assets/Tests/PlayMode/WorldSaveRoundTripTests.cs`.

**Interfaces:**
- Produces: `IPersistentWorldObject.PersistentId:string`, `Capture():PersistentObjectState`, `Apply(PersistentObjectState):void`; DTO PersistentObjectState com ID/type e campos de estado explicitamente declarados por tipo, sem JSON arbitrário de componentes.
- Produces: WorldSaveAdapter.Capture/Apply WorldSnapshot, com ordem checkpoint→boss/mission→objects→spawn/interaction. Registrar estado antes de habilitar comportamento de objetos para evitar respawn/reward duplicado.

- [ ] **Step 1: Inventariar mundo persistente** nas cenas habilitadas: checkpoints, Cuca/Saci/outros bosses, missões, recompensas coletadas, portões/alavancas e objetos cuja interação muda progresso. Listar cada objeto, ID, campos e política de respawn em `Docs/Validation/TOBE/03b-world-persistence-map.md`.
- [ ] **Step 2: Criar tests** `DefeatedBossDoesNotRespawn`, `CompletedMissionDoesNotRewardTwice`, `OpenedGateRemainsOpen`, `CollectedUniqueRewardRemainsCollected`, `CheckpointAndSceneRestore`, `SceneUnloadRemovesParticipants`; fixture recebe WorldSnapshot conhecido.
- [ ] **Step 3: Executar V1 PlayMode red**, depois implementar adapters/mapping para todas as entradas do inventário. Definir inimigos comuns respawnáveis como não persistentes, salvo requisito explícito do conteúdo; registrar decisão por categoria.
- [ ] **Step 4: Executar PlayMode green e V4**: concluir missão/derrotar boss/abrir portão/coletar recompensa, fechar processo, carregar e verificar estado/ausência de reward duplicado; tutorial/diálogo retoma no ponto definido de progresso sem persistir coroutines.
- [ ] **Step 5: Preparar checkpoint** `feat: persist world progression and idempotent rewards`.

## Task 6: Matriz completa, compatibilidade e encerramento

**Files:**
- Create during execution: `Docs/Validation/TOBE/03b-save-acceptance.md`, `save-schema-v1.md`, `legacy-migration-results.json`.
- Modify: guia DevPlayground, presenters e fachada WorldSave se adapters legados ainda tiverem consumidores.

**Interfaces:**
- Produces: matriz por slot/versão/erro e campos restaurados; nenhum dado prometido fica sem evidência de processo novo.

- [ ] **Step 1: Executar V1/V3** e matriz V4 dos três slots: create/load/overwrite/delete, primary corrupto, backup corrupto, versão futura, migração resolvível/ambígua, ID desconhecido, falha de IO/replace e interrupção controlada em QA.
- [ ] **Step 2: Verificar dados semânticos** player/world/inventário/skills/tempo/checkpoints/bosses/missões/objetos entre processos. Não basta comparar apenas tamanho/hash do JSON.
- [ ] **Step 3: Verificar operação de distribuição** sem override QA e com persistentDataPath padrão; não gravar saves pessoais em testes. Confirmar que define KARAWARA_QA não entra no build entregue ao jogador.
- [ ] **Step 4: Remover APIs/atalhos legados sem consumidor** e atualizar schema/guia; manter migrador e catálogo legado para compatibilidade. Saves ambíguos permanecem preservados e explicitamente não migrados, sem afirmar recuperação total.
- [ ] **Step 5: Auditar G3B**; preparar checkpoint `test: validate save migration and process restart matrix`.

## Gate G3B e rollback

Encerrar apenas quando novos saves V1 restauram tudo definido e cada cópia legada tem resultado conhecido: migrada com equivalência ou preservada com erro recuperável. Não exigir inventar informação perdida para dizer que migrou.

Rollback de código V1 não deve abrir automaticamente arquivos V1 em código legado. Preservar `.legacy` para retorno e limitar rollback de dados às cópias QA; nunca fazer downgrade por truncamento. Distribuição precisa informar formato suportado antes de substituir versão.
