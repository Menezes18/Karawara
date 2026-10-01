# Save Critical Protection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Proteger a última gravação válida e corrigir seleção/restauração dos três slots no runtime atual.
**Architecture:** Introduzir storage puro com erros tipados, mantendo o DTO legado. Separar consulta do menu de carregamento e aplicar dados uma vez após a cena/player estarem prontos, ainda no host atual.
**Tech Stack:** Unity 6000.4.6f1, C#, System.IO, JsonUtility, NUnit e fixtures Editor existentes.
**Spec:** [TO-BE](../specs/2026-10-01-karawara-to-be-design.md), §§4, 5.5, 6, V1–V4.

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
- Ainda não mudar schema nem remover Netcode.

## Review Focus

- Falha de escrita após flush do temporário conserva primary anterior — tarefa 1.
- Menu habilitado com slot vazio/corrompido não troca cena nem lança null reference — tarefa 2.
- Host persistente e host recriado aplicam dados exatamente uma vez — tarefa 3.
- Slot 02/03 nunca recebe skills de 01 — tarefa 3.
- Diretório de QA não pode coincidir com persistentDataPath real — tarefa 4.

## Task 1: Storage seguro e erro explícito

**Files:**
- Create: `Assets/Karawara/Runtime/Core/Karawara.Core.asmdef`.
- Create: `Assets/Karawara/Runtime/Core/Persistence/SaveResult.cs`, `IAtomicFileStore.cs`, `AtomicFileStore.cs`, `IFileOperations.cs`.
- Modify: `Assets/Scripts/SaveFileDataWriter.cs`.
- Test: `Assets/Tests/EditMode/Karawara.Core.EditModeTests.asmdef`, `AtomicFileStoreTests.cs`.

**Interfaces:**
- Produces: `SaveResult<T>` com `Success`, `Value`, `Error`, `Message`; `SaveError` definido em §6.
- Produces: `IAtomicFileStore.Read/Write/Delete` conforme §6; `AtomicFileStore(string root, Func<string,bool> validator, IFileOperations files)`.
- Produces: `IFileOperations` com `Exists(string):bool`, `Read(string):string`, `WriteAndFlush(string,string):void`, `Replace(string temp,string primary,string backup):void`, `Move(string,string):void`, `Delete(string):void`, `CreateDirectory(string):void`; implementação System.IO no mesmo arquivo. Seam permite falha determinística, sem matar processo.
- Produces: `SaveFileDataWriter.TryLoad():SaveResult<CharacterSaveData>` e `TrySave(CharacterSaveData):SaveResult<bool>`; manter métodos legados somente como wrappers de compatibilidade até consumidores migrarem.

- [ ] **Step 1: Criar casos NUnit** no namespace `Karawara.Tests.Persistence`: `FirstWriteCanBeRead`, `SecondWritePreservesBackup`, `ReplaceFailurePreservesPrimary`, `InvalidPrimaryRecoversBackup`, `InvalidPayloadDoesNotReplace`, `ReadIgnoresTmp`, `ConcurrentSameSlotReturnsBusy`. Assertions centrais: `Assert.That(primaryAfter, Is.EqualTo(primaryBefore));`, `Assert.That(result.Error, Is.EqualTo(SaveError.IoFailure));`, `Assert.That(recovered.Value, Is.EqualTo(validBackup));`.
- [ ] **Step 2: Executar V1** com filtro `Karawara.Tests.Persistence.AtomicFileStoreTests`; exigir falha causada por implementação ausente/defeito, registrando casos executados.
- [ ] **Step 3: Implementar storage**: root absoluto injetado, nomes relativos sem escape, validação antes de substituir, `.tmp`/`.bak` no mesmo volume, flush e File.Replace/Move. Falha preserva primary, delete remove primary/backup/tmp do slot para não ressuscitar um save excluído. Proteger concorrência por slot. Validator do legado desserializa/valida dados mínimos, não apenas JSON sintaticamente válido.
- [ ] **Step 4: Executar V1 novamente**; todos os sete casos e um caso `DeleteDoesNotRecoverBackup` devem passar. Confirmar que wrapper não absorve erro como sucesso.
- [ ] **Step 5: Revisar diff e preparar checkpoint** `fix: protect legacy save writes`; commit apenas conforme instruções da execução.

## Task 2: Slots consultáveis sem iniciar gameplay

**Files:**
- Modify: `Assets/Scripts/States/UI/UI_Character_Save_Slot.cs`, `Assets/Scripts/States/WorldManager/WorldSaveGameManager.cs`, `Assets/Scripts/TitleScreenManager.cs`.
- Create: `Assets/DevTools/Editor/SaveSlotRegression.cs`.
- Evidence: `Docs/Validation/TOBE/01a-save-slots.json`.

**Interfaces:**
- Consumes: `SaveFileDataWriter.TryLoad/TrySave`.
- Produces: `WorldSaveGameManager.RefreshCharacterProfiles():void`, `TryLoadSelectedSlot(CharacterSlot):SaveResult<CharacterSaveData>`; `UI_Character_Save_Slot.RefreshPresentation():void`.
- SelectedSlot muda apenas em comando de seleção. Exibição de arquivo inválido não acessa dados nulos. Slots vazios continuam apresentados conforme layout existente, com status recuperável/indisponível.

- [ ] **Step 1: Criar fixture** `Karawara/Validation/TOBE/Run Save Slot Regression`: habilitar/desabilitar slots 1/2/3 com dados vazios, válidos e corrompidos. Assertar `sceneTransitions==0` até clique explícito; clique em slot2 seleciona slot2; excluir/overwrite seguido de refresh mostra os dados novos.
- [ ] **Step 2: Executar V2**; caso de OnEnable do slot1 deve reproduzir o load indevido, com root temporário.
- [ ] **Step 3: Retirar LoadGame de OnEnable**; Refresh lê perfil apenas. Migrar comandos de save/new/load/delete para resultados explícitos; não trocar cena em Missing/InvalidData/IoFailure. Atualizar perfis somente após operação bem-sucedida. NewGame com escrita inicial falha permanece no menu; overwrite/delete continuam usando confirmações existentes.
- [ ] **Step 4: Executar V2**; exigir todos os casos previstos, zero erros capturados, nenhuma escrita fora do root QA.
- [ ] **Step 5: Revisar UnityEvents de SceneMenu e prefab Save Slot**; preservar os métodos públicos usados ou migrar bindings explicitamente. Preparar checkpoint `fix: separate save slot display from loading`.

## Task 3: Aplicação de estado no host e no slot correto

**Files:**
- Modify: `Assets/Scripts/States/Character/Player/PlayerManager.cs`, `Assets/Scripts/States/WorldManager/WorldSaveGameManager.cs`, `Assets/Scripts/SkillTree/PlayerSkillManager.cs`, `Assets/Scripts/SkillTree/SkillTreeUiManager.cs`.
- Create: `Assets/DevTools/Editor/LegacySaveRestoreRegression.cs`.
- Evidence: `Docs/Validation/TOBE/01a-legacy-restore.json`.

**Interfaces:**
- Consumes: slot validado da tarefa 2.
- Produces: `PlayerManager.ApplyLegacySave(CharacterSaveData):bool`; `WorldSaveGameManager.ApplyPendingSaveWhenReady():bool`. Usar geração de load para impedir aplicação repetida; resetar geração ao aceitar novo comando. Host/client legados chamam esse mesmo percurso.
- Skills usam o argumento do slot carregado; loading usa sceneIndex validado contra cenas habilitadas, com erro se inválido. Posição restaura XYZ sem `+100`; CharacterController desligado durante colocação e teste de spawn seguro.

- [ ] **Step 1: Criar casos** `LocalHostRestoresSelectedSlot`, `ExistingPlayerRestoresOnce`, `RecreatedPlayerRestoresOnce`, `SlotThreeSkillsDoNotUseSlotOne`, `SavedSceneIsUsed`, `InvalidSceneDoesNotTransition`, `RestorePositionDoesNotAddHeight`. Dados QA: slot1 vitality10/HP70 e skillA; slot3 vitality20/HP90 e skillB. Assertar HP90, vitality20, skillB e `applyCount==1` no slot3.
- [ ] **Step 2: Executar V2** via `Karawara/Validation/TOBE/Run Legacy Save Restore Regression`; registrar o comportamento atual do host, sem interpretar a ausência de chamada como reprodução já feita.
- [ ] **Step 3: Implementar aplicação após scene ready/player ready** no fluxo comum do load; remover guarda que exclui host como única via; rebindar HUD depois de stats/HP/skills; usar dados escolhidos nas skills. Clipe/evento tardio não reaplica save.
- [ ] **Step 4: Executar V2 e V3**; validar spawn seguro em cenas reais com altura salva e sem queda artificial. Se posição legada for inválida, fallback explícito ao checkpoint/start autorizado pela cena, registrado no relatório.
- [ ] **Step 5: Preparar checkpoint** `fix: restore selected legacy save after scene initialization`.

## Task 4: Isolamento de QA e round-trip de processo

**Files:**
- Create: `Assets/Karawara/Runtime/Core/Persistence/SaveRootPolicy.cs`.
- Modify: `Assets/Scripts/States/WorldManager/WorldSaveGameManager.cs`, `Assets/DevTools/Editor/UnityMigrationAudit.cs`.
- Test: `Assets/Tests/EditMode/SaveRootPolicyTests.cs`.
- Evidence: `Docs/Validation/TOBE/01a-save-roundtrip.md` e hashes dos arquivos QA.

**Interfaces:**
- Produces: `SaveRootPolicy.Resolve(string persistentRoot,string[] args,bool qaEnabled):SaveResult<string>`; `-karawaraSaveRoot` só em Editor/Development ou define `KARAWARA_QA`.
- Build QA Release usa define específico e output separado `Builds/TOBE-QA/WindowsRelease`; distribuição não herda define nem root customizado.

- [ ] **Step 1: Criar testes** `ReleaseIgnoresRootOverride`, `QaRejectsRelativeRoot`, `QaRejectsRealPersistentRoot`, `QaAcceptsDistinctAbsoluteRoot`; assertar erro nos paths relativos/reais e root padrão no Release comum.
- [ ] **Step 2: Executar V1**; exigir falhas antes de implementar a política.
- [ ] **Step 3: Injetar root em todos os leitores/writers** do sistema atual e fixtures. Acrescentar comando `Karawara/Validation/TOBE/Build QA Release` ao builder, restaurando defines/configuração mesmo em falha; não modificar build de distribuição.
- [ ] **Step 4: Executar V1 e V4**: nos três slots, criar→salvar→encerrar processo A→abrir B→validar campos legados. Repetir corrupção de primary com backup, delete e falha de escrita. Confirmar hashes dos saves reais inalterados.
- [ ] **Step 5: Revisar evidências de G1A**; guardar limites da persistência legada ainda não resolvidos (IDs/skills/mundo são 3B). Preparar checkpoint `test: validate isolated legacy save round trips`.

## Gate G1A e rollback

Não encerrar G1A sem proteção de escrita, seleção dos slots e restauração dos campos legados verificáveis no host real e processos novos. Se Skill[] não resolver no processo novo, registrar o caso reprovado e preservar seus dados para IDs/migração de 3B; o gate de estabilidade integral G3B permanece pendente. G1A não promete corrigir um formato de skills não recuperável; a limitação precisa ser explícita para seguir a migração local sem alegar save completo.

Rollback é reversão dos commits desta fase no checkout de execução. Não restaurar saves reais automaticamente. Diretórios QA são descartáveis; o original do usuário e seus backups permanecem preservados.
