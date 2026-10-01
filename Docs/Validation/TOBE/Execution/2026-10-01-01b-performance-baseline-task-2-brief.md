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

## Task 2: Rotas e capturas pareadas de baseline

**Files:**
- Create during execution: `Docs/Validation/TOBE/performance-routes.md`, `performance-baseline.md`.
- Outputs: `ProfilerCaptures/TOBE/baseline/<hardware>/<route>/<runId>/`.
- Existing reference: `Assets/DevTools/Runtime/DevPlaygroundController.cs`; sete cenas em EditorBuildSettings.

**Interfaces:**
- Consumes: CaptureConfig/Result.
- Produces: routeId estável, posição/câmera inicial, checkpoints/tempo/ações e inimigos fixos; manifest de screenshots com frame/checkpoint e hashes.

- [ ] **Step 1: Definir scripts de percurso** para Tutorial_Final, MapaEsboco, `t`, CucaLoad e menu/skills/minimapa. Registrar posições reais por amostra no Editor; não inventar coordenadas. Rota de combate fixa composição e seed quando houver aleatoriedade controlável.
- [ ] **Step 2: Capturar Development Player**: 30s warm-up +120s, 3 execuções por rota; 1920x1080/renderScale1/High Fidelity propostos, VSync e cap documentados. Não juntar execuções com settings diferentes.
- [ ] **Step 3: Repetir rota de maior custo em Release** para baseline de timing e screenshots; não comparar números de Development com Release como A/B.
- [ ] **Step 4: Verificar validade**: todos os runs completam, sem foco perdido/pausa não planejada/população diferente; ruído acima de 5% exige ajustar protocolo e recapturar apenas os runs afetados.
- [ ] **Step 5: Preparar checkpoint** `docs: record reproducible performance routes and baseline`.


