# Performance Baseline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produzir um baseline reproduzível que permita atribuir custo e comparar futuras mudanças.
**Architecture:** Instrumentação de QA isolada registra condições e frame times; CPU/GPU/Rendering e memória são medidos em capturas próprias. Nenhuma otimização entra nesta fase.
**Tech Stack:** Unity Profiler, Frame Debugger, ProfilerRecorder quando disponível, Windows standalone e JSON/CSV.
**Spec:** [TO-BE](../specs/2026-10-01-karawara-to-be-design.md), §§4, 5.7, V5.

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
- Captura de timing sem Deep Profile; saves QA quando a rota requer gravação.

## Review Focus

- Counter de profiler não suportado fica indisponível, nunca vira zero — tarefa 1.
- VSync/FPS cap/Editor focus alteram medição e invalidam comparação — tarefa 2.
- Contagem YAML de renderers não equivale a draw calls — tarefa 3.
- Memória cresce por cache inicial sem leak; comparar após warm-up — tarefa 3.
- Captura só na máquina disponível não prova desempenho em outro hardware — tarefa 4.

## Task 1: Contrato de captura e saída rastreável

**Files:**
- Create: `Assets/DevTools/Runtime/Performance/PerformanceCaptureController.cs`, `PerformanceCaptureData.cs`.
- Create: `Assets/DevTools/Editor/PerformanceCaptureValidation.cs`.
- Create during execution: `Docs/Validation/TOBE/performance-baseline-index.json`.

**Interfaces:**
- Produces: `PerformanceCaptureController.Begin(CaptureConfig):bool`, `End():CaptureResult`; config fixa `warmupSeconds=30`, `captureSeconds=120`, routeId/runId/outputRoot e condições.
- Produces: CaptureResult com commit/build/hardware/resolution/renderScale/quality/vsync/cap/route/enemyCount/camera, frame timestamps e métricas opcionais. Counter indisponível tem `available=false`, não valor numérico zero. Arquivos levam runId único e nunca sobrescrevem outra captura.

- [ ] **Step 1: Criar fixture** `Karawara/Validation/TOBE/Validate Performance Capture`: timestamps artificiais de 30s de warm-up e 120s de captura; assertar warm-up excluído, IDs únicos, null para counter ausente e mediana/p95 de série conhecida `[10,10,10,20,30]` pelo método nearest-rank (`p95=30`).
- [ ] **Step 2: Executar V2** e registrar falhas de saída/estatística antes da implementação.
- [ ] **Step 3: Implementar recorder** somente em QA; resolver nomes de counters por enumeração/API real da Unity instalada. Não depender de nomes presumidos; tratar suporte por plataforma. Fechar ProfilerRecorders e streams ao terminar/desativar.
- [ ] **Step 4: Executar V2**; validar saída parseável, duração, disponibilidade de métricas e ausência de handles owned após 10 ciclos Begin/End.
- [ ] **Step 5: Preparar checkpoint** `chore: add isolated performance capture tooling`.


