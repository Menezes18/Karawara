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


