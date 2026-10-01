# Measured Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduzir custo de frame/alocações e eliminar retenção indevida comprovada, preservando gameplay e aparência.
**Architecture:** Renovar baseline após local/Freeflow, escolher candidatos por custo medido e fazer alterações A/B isoladas. Rendering, código e conteúdo têm critérios próprios; nenhuma configuração é reduzida indiscriminadamente.
**Tech Stack:** Unity 6000.4.6f1, URP17.4.0, Profiler/Frame Debugger/snapshots e tooling de 1B.
**Spec:** [TO-BE](../specs/2026-10-01-karawara-to-be-design.md), §5.7, V5; depende de G1B e G4 para aceite final integrado.

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
- Manter resolução, render scale, rota e população no A/B; usar mapas de movimento 3A para caminhos de código.

## Review Focus

- Buffer NonAlloc saturado perde inimigos e altera lógica de missão — tarefa 2.
- Material compartilhado é destruído como se fosse instância owned — tarefa 2.
- Passe condicional de invisibilidade quebra skill/câmera ou RenderGraph — tarefa 3.
- LOD/occlusion/light bake gera pop-in ou altera leitura do combate — tarefa 4.
- Draw calls menores sem melhora de frame time não justificam alegação de FPS — tarefas 1/5.

## Task 1: Renovar baseline e escolher ordem por gargalo

**Files:**
- Create during execution: `Docs/Validation/TOBE/05-post-freeflow-baseline.md`, `05-experiment-register.csv`.
- Outputs: `ProfilerCaptures/TOBE/post-freeflow/<hardware>/<route>/<runId>/`.
- Reuse: tooling/percurso de 1B, arena Freeflow de 4.

**Interfaces:**
- Produces: experimentId, commit-base/candidate, route/config/hardware, métrica primária, hipóteses, expectativa visual e decisão accept/reject/inconclusive.
- Tuning de performance não altera combate/estatísticas para reduzir workload.

- [ ] **Step 1: Capturar baseline V5 atualizado** nas mesmas rotas de 1B e nova arena, após warm-up. Comparação antigo→novo descreve efeito da migração, sem confundir Freeflow de workload diferente com A/B de otimização.
- [ ] **Step 2: Selecionar maior custo por rota** com CPU/GPU/GC/resource ownership demonstrados; tabela de candidatos é ordenada pela métrica real. Investigações podem mudar ordem das tarefas2/3/4, mantendo mudanças separadas.
- [ ] **Step 3: Preencher experiment register** antes de cada edição; fixar threshold de §5.7: melhora5% na métrica alvo e p95 sem regressão>3%, sob ruído≤5%.
- [ ] **Step 4: Validar equivalência de workload** (população, câmera, quality, resolution, scale, VSync/cap, build type); descartar runs inválidos com motivo registrado.
- [ ] **Step 5: Preparar checkpoint** `docs: refresh performance baseline after local freeflow migration`.

## Task 2: Percepção, alocações e ownership de recursos

**Files:**
- Candidate original paths: `Assets/Scripts/Missao/Script/MissaoManager.cs`, `Assets/Scripts/SkillTree/AnimalFogo/FireballShooter.cs`, `Assets/Scripts/SkillTree/PetAttack/WolfAI.cs`, `Assets/Scripts/Boss Cuca/CucaBoss.cs`, `Assets/Scripts/Minimap/Scripts/Config/MiniMapPlane.cs`.
- Candidate grass: `Assets/VFX_Shader/Natureza/Grass2/BotWGrass.cs`.
- Modify only measured active candidates at destination paths from 3A; no sweeping edits of all candidates.
- Create if repeated validated use warrants: `Assets/Karawara/Runtime/AI/Perception/OverlapQueryBuffer.cs`.
- Test: `Assets/Tests/PlayMode/PerceptionSaturationTests.cs`, `OwnedResourceLifecycleTests.cs`.

**Interfaces:**
- Proposed buffer: `OverlapQueryBuffer.Query(Vector3 center,float radius,int layerMask):int`, `Get(int):Collider`; owned reusable buffer; saturation expands outside steady-state hot path or performs bounded fallback, never silently truncates mission population.
- Owner policy: class that instantiates Material/ScriptableObject/pool owns cleanup. Shared assets are never Destroyed. Renderer property changes use MaterialPropertyBlock where compatible; state stays per renderer.

- [ ] **Step 1: Reproduce active cases** in Profiler/snapshots; verify referenced scenes/prefabs before changing an inactive candidate. Test names: `DenseEnemiesAreNotDropped`, `LayerFilterPreservesRelevantTargets`, `TenSpawnCyclesReleaseOwnedMaterials`, `SharedMaterialSurvivesOwnerDestroy`, `SubscriptionsDoNotAccumulate`.
- [ ] **Step 2: Execute V1 red for the selected defect**; capture allocations/resource references before edits. Low-impact config changes do not need tests mirroring serialized values.
- [ ] **Step 3: Correct one ownership/perception subsystem**: NonAlloc with saturation policy and relevant masks; lower perception frequency only if measured responsiveness stays within current gameplay requirements. Reuse buffer per owner, not static shared scratch across nested calls. Dispose owned clones/materials/recorders and unregister callbacks.
- [ ] **Step 4: Execute tests green, V3 and V5 A/B**; steady-state relevant query should allocate0 bytes after warm-up. For retention fix, show resource count/references stable over 10 cycles. Pooling only where repeated Instantiate/Destroy cost is measured, with reset rules for damage/team/coroutines/callbacks.
- [ ] **Step 5: Record accept/reject** per experiment and prepare checkpoint `perf: reduce measured allocations and retained resources`; revert edits without measurable benefit unless independently fixing proven lifecycle defect.

## Task 3: URP, render features, sombras e câmeras

**Files:**
- Modify only experiment candidates: `Assets/Settings/URP-HighFidelity.asset`, `URP-HighFidelity-Renderer.asset`, `URP-Balanced.asset`, `URP-Performant.asset` e renderers correspondentes.
- Modify only if measured: `Assets/VFX_Shader/SkillTree/Invisivel/CustomRenderPassFeature.cs` at Rendering destination from 3A; câmera/minimapa settings identified by Frame Debugger.
- Evidence: `Docs/Validation/TOBE/05-urp-experiments.md` e screenshots pareadas.

**Interfaces:**
- Produces: tabela preset→efeitos necessários→settings/resultados; default High Fidelity preserva aparência. Feature de invisibilidade tem owner de efeito/câmera e ativação condicional, sem state global quebrado em segunda câmera.

- [ ] **Step 1: Confirmar uso real** de depth/opaque/HDR/MSAA, sombras, SSAO e passes por material/efeito/câmera; SRP Batcher já está ligado. Catalogar câmeras e render targets do minimapa/HUD.
- [ ] **Step 2: Prototipar um A/B por custo dominante**: passe de invisibilidade condicional quando inativo; downsample SSAO; resolução/distância/cascatas de sombras; frequência/resolução do minimapa. Não assumir que todos podem ser reduzidos. Clone temporário do preset para A/B, sem alterar distribuição antes de decisão.
- [ ] **Step 3: Exercitar efeito ativo/inativo e múltiplas câmeras**; VFX/transparências/contornos/skills/iluminação de boss continuam corretos. RenderGraph controla temporários; não liberar recurso que não pertence ao feature.
- [ ] **Step 4: Executar V5 e comparação visual** por checkpoint, incluindo invisibilidade, vegetação, sombras próximas/distantes e ambiente escuro da Cuca. Aprovar apenas ganho estável sem diferença visual indesejada; se houver downgrade perceptível, manter preset ou submeter mudança explícita ao usuário.
- [ ] **Step 5: Consolidar settings aceitos e registrar rejeitados**; preparar checkpoint por experimento com paths exatos. Reavaliar Balanced/Performant separadamente, sem misturar resultado de qualidade inferior com High Fidelity.

## Task 4: Materiais, luzes, geometria e texturas por cena

**Files:**
- Modify only enumerated candidates in `Docs/Validation/TOBE/05-content-candidate-map.csv`: materiais/prefabs/importers/lighting data das cenas de produção.
- Inspect: `ProjectSettings/QualitySettings.asset`, cenas Tutorial_Final/CucaLoad/MapaEsboco/`t`, dependencies de lightmaps, LOD e OcclusionCullingData.
- Evidence: `Docs/Validation/TOBE/05-content-experiments.md`.

**Interfaces:**
- Produces: mapa GUID/path/scene/active/render cost/residency/expected visual impact; cada alteração identifica o objeto/material efetivamente renderizado. Quantidade de importers não é residência de textura em runtime.

- [ ] **Step 1: Identificar causas dos batches** por Frame Debugger: material distinto, shader/pass, skinning, luz/sombra, transparency/overdraw e câmeras. Verificar compatibilidade de SRP Batcher e instancing, sem tratar os dois como benefícios cumulativos garantidos.
- [ ] **Step 2: Aplicar experimento isolado** por categoria: compartilhar material onde propriedades são iguais, instancing onde suportado, LOD/occlusion por visibilidade, bake de luzes estáticas e sombras de luzes relevantes. Preservar assets de origem e comparar cena/lighting após bake; não converter todas as luzes em baked.
- [ ] **Step 3: Revisar texturas residentes**: resolução por uso/tamanho em tela, compressão/plataforma/mips; Read/Write já é raro no snapshot atual. Streaming mipmaps só para grupo elegível com memória/qualidade medidas e budget explicitado; não ativar globalmente sem validar.
- [ ] **Step 4: Executar V5** com viewpoints próximos/distantes, movimento e ataques/VFX; avaliar pop-in/LOD, shimmer, normais, alpha, luz e leitura dos inimigos. Redução de draw calls sem timing melhora permanece resultado técnico limitado, não sucesso de FPS.
- [ ] **Step 5: Registrar decisão e preparar checkpoints** por cena/categoria; rollback inclui materiais/metas/lightmaps/scene settings do mesmo experimento.

## Task 5: Integração, memória e confirmação Release

**Files:**
- Create during execution: `Docs/Validation/TOBE/05-performance-acceptance.md`, `05-final-experiment-register.csv`.
- Update: baseline index e guia de condições/reprodução; relatórios de G5.

**Interfaces:**
- Produces: conclusão por hardware/rota/qualidade com baseline/candidate, dados brutos, screenshots, ganhos, regressões e limites. Não extrapolar para GPU/laptop não medido.

- [ ] **Step 1: Executar V1/V3/V4** das áreas alteradas e salvar/carregar sessões com Freeflow; nenhuma otimização quebra persistência ou conteúdo.
- [ ] **Step 2: Executar V5 final** Development para atribuição e Release para timing, com 3 runs por base/candidate e métricas de §5.7.
- [ ] **Step 3: Repetir snapshots de retenção** pós-warm-up e 10 ciclos; demonstrar ausência de recursos owned sem owner/cleanup e tendência crescente repetível. Memória total estável não prova todos os leaks inexistentes; descrever o escopo medido.
- [ ] **Step 4: Revisar screenshots pareadas e gameplay**; excluir instrumentação pesada/defines QA do build de distribuição. Se hardware-alvo faltar, declarar gate de desempenho desse hardware pendente.
- [ ] **Step 5: Auditar G5 e preparar checkpoint final** `perf: validate integrated local freeflow performance`; manter candidato rejeitado revertido, não acumulá-lo por parecer mais otimizado.

## Gate G5 e rollback

Aceitar somente ganhos repetíveis ou correções de retenção/alocação demonstradas, com aparência e comportamento preservados. Relatório separa benchmark, callstacks, snapshots e observação visual.

Rollback é por experimento pequeno e inclui settings/assets/código juntos. Evitar reverter toda a arquitetura ou mudar contagem de inimigos para recuperar FPS. Novo gargalo após Freeflow exige nova captura, não reutilizar números históricos como prova.
