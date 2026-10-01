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


