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

