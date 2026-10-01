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
