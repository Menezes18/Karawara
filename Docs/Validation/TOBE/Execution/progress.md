# Execução TO-BE — 2026-10-01

Base: a2b43f091dad6459acc65094eceedf9200d28373, branch refactor/qol.
Baseline local: apenas Docs/TO-BE.md e Docs/superpowers/ não rastreados. Editor Karawara 6000.4.6f1 conectado, SceneMenu, fora de Play, Console com zero erros.

Decisões de execução:
- Trabalhar no checkout atual ligado ao Editor; preservar documentos existentes. Nenhum commit/push/merge automático.
- A autorização da sessão para implementações supera o estado de proposta dos documentos.
- Uma pessoa controla o Editor/MCP: coordenador. Workers editam somente seus escopos e entregam fixtures para execução centralizada.
- Fases dependentes só iniciam depois das evidências dos gates anteriores. Não substituir benchmark/round-trip por compilação.

## Preflight
| Tarefas | Interface/arquivo compartilhado | Resultado |
|---|---|---|
| 1A.1 → 1A.2 | TryLoad/TrySave; WorldSave | Produção e consumo alinhados |
| 1A.2 → 1A.3 | WorldSave/selected slot | Executar sequencialmente |
| 1A.3 → 1A.4 | WorldSave/root | Executar sequencialmente |
| 1A.1 → 1A.4 | Core/Tests asmdefs | Compartilhar assembly existente |
| 1A.1–4 individualmente | casos vs interfaces/arquivos | Coerentes; QA Release exige builder/root separado |
| 1B.1 → 1B.2 | CaptureConfig/Result | Instrumentação antes de capturas |
| 1B.2 → 1B.3 → 1B.4 | baseline/index | Métricas não medidas permanecem pendentes |
| 1B.1–4 individualmente | casos vs saídas | Coerentes; GPU/counter indisponível não é zero |
| 1A × 1B | Editor, cena/build | Serializar execução MCP; edição com ownership distinto |
| 1A/1B → 02 → 03A → 03B → 04 → 05 | gates | Respeitar dependências; sem rollout antes de evidências |

## Progresso
- Leitura: índice, TO-BE e sete planos concluída.
- 1A.1: delegada, storage atômico e testes.
- 1B.1: delegada, instrumentação QA e fixture.
- 02: somente inventário antecipado, sem alterações de runtime.
- 1A.1 RED: 12 casos executados, 12 falhas esperadas por implementação ausente (job 8c4b65dd9e41404d90a0a1a9d486916a).
- 1B.1 RED: fixture executada, CaptureContract ausente, passed=false, evidência arquivada. Primeira tentativa durante reload/menu não importado não contou como teste.
- 1A.1: 13 casos executados, somente novo teste de temporário corrompido falhou; 12 originais passaram. Correção de releitura pós-flush em andamento.
- 1A.1 GREEN: 13/13 passaram, 0 falhas/skip (job b32d4d1c9fb545568bb4497155266fb5); revisão independente pendente.
- Inventário 02 entregue: 63 arquivos próprios/QA, 35 NetworkVariables, 16 RPCs; referências estáticas, sem migração ou gate fechado.
- Fixtures iniciais EditMode identificaram limite do harness: Awake/OnEnable/OnDisable não executam como PlayMode. Corrigir fixtures para exercitar componentes reais; resultado 4/8 slots não conta como red de bugs de gameplay.
- 1A.1 revisão: flaw de validação JSON raiz confirmado em fixture (12 casos, 7 falhas), fix round1 autorizado. Requisito de status Recoverable será indicado por Message=RecoveredFromBackup propagada.
- 1A.2 RED corrigido: 8 casos, 6 falhas de comportamento confirmado, 2 pass; OnEnable load indevido/slot01 forçado/caches stale. Implementação autorizada.
- 1B.1 fixture Editor GREEN: 124 checks, 10 Begin/End cycles, disposal e saída parseável; callbacks Disable/Destroy reais aguardam PlayMode. Sem baseline de timing concluído.
