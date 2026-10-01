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


