# Karawara — TO-BE e planos de implementação

Data: 01/10/2026. Base inspecionada: `a2b43f09`, Unity `6000.4.6f1`.

Este pacote descreve a arquitetura proposta e o trabalho necessário. É uma proposta para revisão; tarefas e validações ainda não foram executadas. A criação destes documentos não autoriza executar mudanças de gameplay.

Leia primeiro o [TO-BE completo](superpowers/specs/2026-10-01-karawara-to-be-design.md). Ele define contratos compartilhados, decisões, critérios de aceite, migração e procedimentos de teste.

| Ordem | Plano | Entrega | Complexidade |
|---|---|---|---|
| 1A | [Save: correções críticas](superpowers/plans/2026-10-01-01a-save-critical.md) | Escrita protegida, seleção de slot e restauração no host atual | 3/5 |
| 1B | [Baseline de desempenho](superpowers/plans/2026-10-01-01b-performance-baseline.md) | Capturas reproduzíveis de CPU/GPU, draw calls, GC e memória | 2/5 |
| 2 | [Execução totalmente local](superpowers/plans/2026-10-01-02-local-runtime.md) | Gameplay e ferramentas sem host, RPC ou dependência Netcode | 4/5 |
| 3A | [Arquitetura e pastas](superpowers/plans/2026-10-01-03a-architecture.md) | Responsabilidades, ownership e dependências explícitas | 4/5 |
| 3B | [Persistência completa](superpowers/plans/2026-10-01-03b-persistence.md) | Save versionado, IDs estáveis e progresso restaurável | 4/5 |
| 4 | [Freeflow Combat](superpowers/plans/2026-10-01-04-freeflow.md) | Arena inicial, integração e acabamento do combate | 4/5 a 5/5 |
| 5 | [Otimização validada](superpowers/plans/2026-10-01-05-optimization.md) | Ganhos medidos e preservação visual | 2/5 a 4/5 |

1A e 1B são independentes. Depois, seguir 2 → 3A → 3B → 4 → 5. Correções de desempenho com evidência podem acontecer antes de 5, em alterações separadas; renovar o baseline depois da migração local e do Freeflow.

Cada plano contém arquivos existentes e propostos, interfaces, tarefas com checkboxes, casos de teste, evidências exigidas e condições de rollback. Nenhuma fase deve ser marcada como concluída apenas porque o projeto compilou.
