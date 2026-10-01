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

## Task 4: Fechar baseline e backlog de otimização

**Files:**
- Create during execution: `Docs/Validation/TOBE/performance-backlog.md`.
- Modify during execution: `Docs/Validation/TOBE/performance-baseline-index.json`.

**Interfaces:**
- Produces: por candidato, custo alvo, experimento A/B, dependência visual, métrica e evidência que justificariam aceite; sem prometer FPS antes do teste.

- [ ] **Step 1: Calcular estatísticas** com mediana/p95 e métricas disponíveis; anexar dados brutos, hashes e hardware.
- [ ] **Step 2: Classificar gargalo por rota** CPU/GPU/GC/memória; GPU indisponível permanece limitação explícita.
- [ ] **Step 3: Preencher backlog** de passes/sombras, perception, materiais e conteúdo com prioridade por custo, não por facilidade de editar.
- [ ] **Step 4: Auditar G1B**: 3 runs válidos por rota, Release comparável, screenshots e snapshots de retenção; registrar qualquer captura não disponível como pendente, sem substituir por smoke test.
- [ ] **Step 5: Preparar checkpoint** `docs: finalize measured optimization backlog`.

## Gate G1B e rollback

Esta fase não precisa alterar URP, luzes ou texturas. Instrumentação deve poder ser desativada sem mudar gameplay. Se as ferramentas introduzirem custo mensurável, usar sessão separada para atribuição e medição externa/leve para timing. Remover tooling com problema e repetir as capturas afetadas.

