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

## Task 3: Atribuir draw calls, GC e retenção

**Files:**
- Create during execution: `Docs/Validation/TOBE/rendering-cost-breakdown.md`, `memory-retention-baseline.md`.
- Outputs: Frame Debugger/Profiler e snapshots no diretório de captura.
- Inspect: `Assets/Settings/URP-HighFidelity.asset`, `URP-HighFidelity-Renderer.asset`, `Assets/VFX_Shader/SkillTree/Invisivel/CustomRenderPassFeature.cs`.
- Inspect: `Assets/Scripts/Minimap/Scripts/Config/MiniMapPlane.cs`, `Assets/Scripts/Boss Cuca/CucaBoss.cs`, `Assets/Scripts/Missao/Script/MissaoManager.cs`.

**Interfaces:**
- Produces: lista ordenada de custo com amostra, marker/callstack/passe, objeto responsável, frequência e confiança; classificação `proven`, `candidate`, `not-measured`.

- [ ] **Step 1: Capturar Rendering/Frame Debugger** separado do timing; atribuir passes, sombras, transparência, materiais e câmeras/minimapa. Contar draw calls reais por checkpoint, sem inferir por MeshRenderers no YAML.
- [ ] **Step 2: Capturar GC Alloc** com callstacks em sessão curta de atribuição; identificar frequência/bytes dos OverlapSphere e outras operações relevantes. Registrar essa instrumentação separadamente do benchmark.
- [ ] **Step 3: Capturar memória após warm-up e após 10 ciclos** de cena e 10 ciclos de skills/pets/bosses. Usar snapshots suportados pelas ferramentas disponíveis; se pacote Memory Profiler for necessário, propor sua adição de QA em tarefa separada e versionada, sem atualizar outros packages.
- [ ] **Step 4: Inspecionar referências retidas** para materiais/clones/event handlers; explicar lifetime esperado, crescimento repetível e responsável. Distinguir cache estável de recurso owned sem cleanup.
- [ ] **Step 5: Preparar checkpoint** `docs: attribute rendering allocations and retained resources`.


