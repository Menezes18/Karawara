# Relatório de migração Unity e DevPlayground — Karawara

> Registro histórico anterior à integração de Homol. Para o estado posterior, consulte [HomolIntegrationReport.md](HomolIntegrationReport.md). Os resultados abaixo não certificam as alterações posteriores do merge.

Data: 30/09/2026. Workspace: `C:\Users\migue\Dev\Karawara`.

## 1. Objetivo, escopo e preservação do trabalho

O trabalho trata da atualização de Unity **2022.3.21f1 para 6000.4.6f1**, da correção dos bloqueios encontrados e da criação de uma cena isolada para experimentar os sistemas reais de Karawara. O projeto já tinha muitas alterações locais antes desta sessão. Esse estado foi preservado; o diff completo contra Git não representa exclusivamente as mudanças desta entrega. Não foi criado commit.

As ferramentas novas ficam em `Assets/DevTools`. As alterações em scripts de produção corrigem problemas concretos de compilação, inicialização, referências e integração identificados durante a investigação. Não houve substituição geral dos sistemas de combate, entrada, câmera, IA ou save.

## 2. Ambiente e dependências

| Componente | Configuração inspecionada |
|---|---|
| Unity | 6000.4.6f1, revisão `0b051c2e5d54` |
| Plataforma de compilação de scripts | StandaloneWindows64 |
| Netcode for GameObjects | 2.11.1 |
| Input System | 1.19.0 |
| Cinemachine | 2.10.7 |
| Universal Render Pipeline | 17.4.0 |
| Visual Effect Graph | 17.4.0 |
| AI Navigation | 2.0.12 |
| Post Processing | 3.5.4 |
| MCP for Unity | 10.2.0, pacote local |

`Packages/manifest.json` aponta o MCP para `file:C:/Users/migue/.local/share/mcp/unity-mcp/MCPForUnity`; o `package.json` desse diretório confirma a versão 10.2.0. A conexão MCP com o Editor foi utilizada nesta sessão para compilação, inspeção e validação. A referência local depende desse diretório existir na máquina; a conexão atual não comprova uma instalação portátil em outro computador.

## 3. Diagnóstico e origem dos problemas

A auditoria estática indexou 20.338 GUIDs, inspecionou 60 cenas, 380 prefabs e 48 controllers e seguiu referências das cenas SceneMenu, CucaLoad, Video1, t e MapaEsboco até 844 assets alcançáveis. Seu snapshot inicial encontrou 64 referências não resolvidas em 14 arquivos alcançáveis. Esses números descrevem o alcance daquela auditoria, anterior a reparos posteriores.

Foram separados problemas que impediam a compilação, defeitos concretos de inicialização e referências antigas sem evidência suficiente de substituição. O histórico Git mostrou que parte das referências ausentes já existia antes da migração. A existência de uma referência alcançável não demonstra que seu objeto seja instanciado durante o jogo.

Detalhes, GUIDs, origem histórica e limites da investigação estão em [UnityMigrationAuditAssets.md](UnityMigrationAuditAssets.md).

## 4. Correções de compatibilidade e compilação

| Arquivo ou sistema | Problema observado e correção |
|---|---|
| `DialogueConditional.cs` | Removidos dois `[SerializeField]` aplicados a declarações de enum, uso inválido que gerava CS0592. Os campos que usam esses enums foram preservados. |
| `BaseNode.cs` | A geração de GUID no Editor usa `UnityEngine.GUID.Generate()` na versão atual; a alternativa de player mantém `System.Guid.NewGuid()`. |
| Assembly de diálogo do Editor | O `.asmdef` foi limitado a `Editor`, evitando levar código de Editor às plataformas de player. |
| `NodeParser.cs` | Removido `using UnityEditor` do código de runtime. Essa dependência afetava a compilação de player mesmo quando o Editor compilava. |
| `BossAISystem.cs` | A velocidade do Rigidbody de projétil utiliza `linearVelocity`, API atual do Unity 6. |

A compilação no Editor e a compilação dos assemblies de player são verificações distintas. O arquivo [UnityReleaseScriptCompilation.json](UnityReleaseScriptCompilation.json) registra a execução de compilação Release de scripts disponível; ele não é evidência de um executável completo construído e executado.

## 5. Recuperação de assets e correções de inicialização

O script `SystemFps.cs` foi recuperado do histórico com o GUID original `a022b4a18bd43614a9b172b4de9565eb`, resolvendo o componente existente na SceneMenu. Quando não há referência explícita, ele usa o TextMeshProUGUI local. Quando há toggle configurado, mantém o controle de visibilidade e remove o listener na destruição. Não altera o limite de FPS.

Foram limpas exatamente cinco referências de override a um controller já ausente nos Animators de suporte do `Player.prefab`. Elas passaram a `fileID: 0`; o controller principal e as demais referências foram preservados. Não foi atribuído um controller diferente por aproximação de nome. `TransformationsBase.Start` passou a verificar o Animator de suporte opcional antes de desativá-lo. O Awake do HUD passou a tolerar a ordem de inicialização do singleton da árvore de habilidades.

Os três prefabs `Circle_ref`, `Circle_ref 1` e `Circle_ref 2`, em `Assets/VFX_Shader/FeedBack_Combat/Alavanca`, conservavam seus dados locais completos, mas apontavam para uma origem de prefab inexistente. O reparo validou a estrutura, removeu apenas a proveniência obsoleta e o PrefabInstance correspondente e comparou os dados locais restantes. A estrutura local de cada efeito foi preservada. Isso comprova preservação de dados; aparência e execução dos efeitos continuam sujeitas à validação visual/runtime.

`WorldObjectManager` recebeu listas inicializadas e um bootstrap `GetOrCreateInstance`, consumido pelos spawners, para cenas que continham objetos de rede sem o manager correspondente. O manager real mantém o registro de objetos e fog walls e persiste entre cenas. `SceneChecker` deixou de assinar duas vezes o callback de carregamento e verifica jogador, câmera e marcador Spawn. Quando uma cena não tem Spawn, preserva a posição e registra um aviso.

Na Cuca legacy, `EnemyAI` prioriza seu próprio `BossAISystem`, evitando que duas instâncias usem a IA de outro boss. O dano resolve `CucaBossHealth` no pai do collider atingido, em vez de usar o singleton global ou exigir a tag `TriggerBoss`, ausente nos colliders reais da Cuca. Os casos específicos de duas instâncias e dano no alvo correto passaram no runner do playground.

`WorldSoundFXManager.ChooseRandomSFXFromArray` retorna null quando não há sons configurados, preservando a seleção aleatória quando o array contém sons. A inicialização do playground tolera referências Unity já destruídas no bootstrap do HUD. Foi removida do NetworkManager uma referência exatamente duplicada à mesma lista de prefabs. `UI_Boss_HP_Bar.OnDestroy` verifica o boss opcional antes de remover a assinatura de vida; ClearEnemies remove as barras associadas aos bosses apagados.

## 6. Renderização, navegação e referências ainda ausentes

As três faixas de qualidade utilizam URP 17.4.0. Balanced e High Fidelity têm SSAO; Performant não tem renderer feature. High Fidelity solicita texturas de profundidade e opaque. Os renderer assets inspecionados utilizam Forward. Essa inspeção de configuração não certifica aparência final, desempenho ou compilação de todos os shaders.

O snapshot final de [UnityMigrationEditorAudit.json](UnityMigrationEditorAudit.json), datado de `2026-10-01T00:07:02.9106974Z`, inspecionou 380 prefabs e seis cenas, com zero erros de carregamento e zero problemas de shader nos materiais classificados como produção. Permanecem 62 ocorrências de shader/material no conjunto de showcase e 28 entradas de objetos com scripts ausentes. Uma entrada de objeto pode indicar vários componentes ausentes. A classificação de materiais de produção depende do alcance da auditoria; não comprova compilação e aparência de todos os shaders em todos os cenários.

CucaLoad, Video1 e t possuem referências resolvidas a dados de NavMesh. O playground constrói seu próprio NavMesh em execução a partir do piso e dos obstáculos estáticos. A geometria de teste usa a layer Environment, compatível com a máscara de aterramento e colisão da câmera do jogador. A máscara de ambiente do WorldUtilityManager da sessão inclui essa layer.

Não foram inventadas substituições para texturas, clips, scripts ou origens de prefab sem proveniência suficiente. Permanecem, entre outros, um clip Fly ausente em controller antigo, texturas antigas de materiais e subobjetos de Editor em VFX graphs que exigem diagnóstico de importação antes de qualquer remoção. O `Boss.prefab` placeholder de Boitatá tem script ausente sem fonte correspondente; não foi certificado como boss funcional.

## 7. Estrutura e cobertura do DevPlayground

| Local | Responsabilidade |
|---|---|
| `Assets/DevTools/Runtime/DevPlaygroundController.cs` | Bootstrap, host local NGO, painel F1, comandos e limpeza da sessão |
| `Assets/DevTools/Runtime/DevPlaygroundCatalog.cs` | Referências serializadas de gameplay e assets de prévia |
| `Assets/DevTools/Editor/DevPlaygroundBuilder.cs` | Geração da cena, catálogo, estações e extração controlada da Cuca |
| `Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs` | Casos de gameplay e relatório JSON |
| `Assets/DevTools/Editor/ProductionSceneSmokeTest.cs` | Inicialização e transições das cenas de produção |
| `Assets/DevTools/Editor/UnityMigrationAudit.cs` | Auditoria de assets no Editor e compilação de scripts de player |
| `Assets/DevTools/Editor/LegacyAssetRepair.cs` | Reparo validado da proveniência dos FireCircle |
| `Assets/DevTools/Editor/DevValidationSceneSession.cs` | Preparação e restauração da cena de validação sem salvar a cena original |
| `Assets/DevTools/Scenes/DevPlayground.unity` | Arena e estações geradas |
| `Assets/DevTools/Data/Generated/CucaDev.prefab` | Hierarquia real da Cuca extraída de CucaLoad, com dependências internas verificadas |
| `Assets/DevTools/Data/DevPlaygroundCatalog.asset` | Catálogo gerado |

O catálogo serializado inspecionado contém **5 inimigos/bosses, 6 habilidades, 3 armas, 682 entradas de showcase, 131 efeitos, 1 prefab interativo, 780 clips e 948 materiais**. Contagens são de descoberta de assets e podem mudar ao regenerar. As estações interativas criadas na cena existem além do único prefab interativo do catálogo.

Os cinco inimigos são Cuca de produção, Dummmy, DummmyRanged, Durk e HumanoDummy. A Cuca precisava de extração porque sua IA era adicionada na cena, e uma busca apenas por prefab/modelo não a encontrava. O gerador preserva a cena de origem e interrompe a extração se encontrar scripts ausentes ou dependências que escapem da hierarquia copiada.

As seis habilidades disponíveis como Skill assets são AttackFogo, Escudo, HealCircle, PetPantera, PetSpirit e SkillPassaro. Seus clones são desbloqueados na sessão. Isso não cria poderes adicionais a partir de scripts experimentais sem Skill asset ou integração de gameplay existente.

### Arquivos de produção alterados e atualizações automáticas

A lista abaixo identifica as correções conhecidas desta sessão. Não atribui todas as alterações do worktree à entrega; assets já estavam modificados antes da investigação. Os arquivos novos de DevTools estão discriminados na tabela acima.

| Caminho exato | Alteração conhecida |
|---|---|
| `Assets/Dialogo/Scripts/Runtime/DialogueGraph/DialogueConditional.cs` | Atributos inválidos sobre enums |
| `Assets/Dialogo/Scripts/Runtime/DialogueGraph/Nodes/BaseNode.cs` | Namespace GUID |
| `Assets/Dialogo/Scripts/Runtime/DialogueGraph/NodeParser.cs` | Dependência de UnityEditor removida de runtime |
| `Assets/Dialogo/Scripts/Editor/com.larsellefsen.dialoguesystem.editor.asmdef` | Assembly restrita a Editor |
| `Assets/Scripts/Boss Cuca/BossAISystem.cs` | Rigidbody.linearVelocity |
| `Assets/Scripts/Boss Cuca/MeleeBoss/Scripts/PlayerControl.cs` | EnemyAI ligado à própria Cuca |
| `Assets/Scripts/Scripts-Video/Colliders/MeleeWeaponDamageCollider.cs` | Dano no boss realmente atingido |
| `Assets/Scripts/TransformationsBase.cs` | Animator de suporte opcional |
| `Assets/Scripts/Scripts-Video/Character/Player/PlayerUIHudManager.cs` | Inicialização tolera singleton ausente |
| `Assets/Scripts/SkillTree/SkillTreeUiManager.cs` | Reset de assets bloqueado no isolamento de desenvolvimento |
| `Assets/Scripts/Scripts-Video/WorldManager/WorldSaveGameManager.cs` | Guardas de isolamento de save |
| `Assets/Scripts/Scripts-Video/WorldManager/WorldObjectManager.cs` | Bootstrap/listas/ciclo de vida |
| `Assets/Scripts/NetworkObjectSpawner.cs` | Bootstrap do WorldObjectManager real |
| `Assets/Scripts/SceneChecker.cs` | Callback único e referências opcionais |
| `Assets/Scripts/Scripts-Video/WorldManager/WorldSoundFXManager.cs` | Array de áudio vazio/opcional |
| `Assets/Scripts/Scripts-Video/UI/UI_Boss_HP_Bar.cs` | Remoção segura de assinatura ao destruir a barra |
| `Assets/Scripts/Menu/SystemFps.cs` e `Assets/Scripts/Menu/SystemFps.cs.meta` | Recuperação histórica com GUID original |
| `Assets/Data/Prefabs/Player.prefab` | Cinco overrides de controller ausente limpos |
| `Assets/Data/Prefabs/World Managers/World NetworkManager.prefab` | Referência duplicada à mesma lista removida |
| `Assets/VFX_Shader/FeedBack_Combat/Alavanca/Circle_ref.prefab` | Proveniência ausente removida, dados locais preservados |
| `Assets/VFX_Shader/FeedBack_Combat/Alavanca/Circle_ref 1.prefab` | Mesmo reparo validado |
| `Assets/VFX_Shader/FeedBack_Combat/Alavanca/Circle_ref 2.prefab` | Mesmo reparo validado |

Também foram observadas atualizações automáticas do Unity/importadores: serialização TMP em `Assets/Data/Prefabs/Boss Health Bar.prefab`; `linearVelocity` em `Assets/Scripts/Boss Cuca/Vfx_Projectile/CaindoDoCeuVFX/Scripts/SpawnAbilities.cs` e `Assets/Scripts/Boss Cuca/Vfx_Projectile/CaindoDoCeuVFX/Scripts/SpawnMeteorScript.cs`; formato de `ProjectSettings/SceneTemplateSettings.json` e campos de `ProjectSettings/ShaderGraphSettings.asset`. Essas atualizações de formato/API não equivalem à certificação visual desses assets. Versão e pacotes são registrados em `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json` e `Packages/packages-lock.json`.

## 8. Uso, controles e isolamento

O procedimento completo e os controles estão em [DevPlaygroundGuide.md](DevPlaygroundGuide.md). Fora do Play Mode, gere/abra a cena pelo menu **Karawara → Dev Playground** e inicie uma sessão nova diretamente nela. **F1** abre ou fecha o painel; aberto, ele libera o cursor e suspende a entrada de movimentação e habilidades. Feche-o para usar os controles de produção e interagir com E.

O painel fornece cura, revivência, respawn, invulnerabilidade, restauração contínua de vida, reset de cooldowns, atributos de vida/movimento, equipamento e ativação de habilidades/armas. A aba Enemies oferece seleção, quantidade, spawn, spawn de todos, clear, respawn e pausa de IA. As estações exercitam combate, degraus/rampas, física, efeitos, interações, teletransporte, duas alavancas e portão e plataforma que desativa após seu temporizador de produção.

Stamina e mana não são recursos ativos nos scripts atuais. O valor de endurance pode ser alterado, mas isso não demonstra consumo ou regeneração de uma barra implementada. Elementos None/Fire/Water usam o ElementManager ativo do jogador. **Transformações humano/animal não estão disponíveis como gameplay:** a UI legacy não possui referências serializadas e o código de `TransformToPlayer` está comentado. Persona, Jabuti, Pantera e Bear estão disponíveis como modelos de prévia; não alteram a forma controlada do jogador.

Habilidades, armas, ações e templates mutáveis de efeitos são clonados em memória. `WorldSaveGameManager.DevPlaygroundIsolationActive` bloqueia operações de saves durante a sessão, inclusive chamadas explícitas de componentes de produção. `ClearActiveSkills` remove raízes criadas por Tools Activate e limpa os flags de poder de espírito; ativações feitas pelas teclas de produção não são rastreadas por essa limpeza. A prévia remove scripts/rede antes da ativação, desliga colisões e desativa Animator/Animation; scripts ausentes são removidos somente dos clones no Editor. Clips selecionados são amostrados diretamente, sem executar a IA ou eventos da prévia.

O controlador operacional é limitado a `UNITY_EDITOR || DEVELOPMENT_BUILD`. Em Release comum, desativa seu GameObject. A cena de desenvolvimento não foi adicionada como nova rota ao menu de produção. A validação pode preparar o playground de forma aditiva, desativando temporariamente as raízes da cena original e restaurando seus estados ao sair do Play Mode, sem salvar ou descartar essa cena.

## 9. Validação executada e evidências

Consulte os **JSONs e seus timestamps** para o resultado efetivo de cada execução. Esta versão do relatório não transforma casos planejados, arquivos parciais ou uma execução em andamento em aprovação.

| Evidência | O que demonstra e limite |
|---|---|
| [UnityMigrationAuditAssets.md](UnityMigrationAuditAssets.md) | Auditoria estática, proveniência e reparos; não é teste de gameplay. |
| [UnityMigrationEditorAudit.json](UnityMigrationEditorAudit.json) | Snapshot final de `2026-10-01T00:07:02.9106974Z`: 380 prefabs, seis cenas, zero erros de carregamento, zero problemas de shader classificados como produção e 62 problemas no showcase. Não certifica todas as cenas em execução. |
| [UnityReleaseScriptCompilation.json](UnityReleaseScriptCompilation.json) | Execução atual de `2026-10-01T00:04:51.7738032Z` registra `success: true` para assemblies StandaloneWindows64 sem Development. Não gera um executável completo. |
| [UnityDevelopmentScriptCompilation.json](UnityDevelopmentScriptCompilation.json) | Execução atual de `2026-10-01T00:05:36.132132Z` registra `success: true` para assemblies de player Development, incluindo as ferramentas protegidas por DEVELOPMENT_BUILD. |
| [ProductionSceneSmokeTest.json](ProductionSceneSmokeTest.json) | Execução concluída: SceneMenu e quatro cenas de gameplay, cinco fases no total, zero erros capturados e `saveFilesUnchanged: true`. Valida inicialização/transições exercitadas no host local, não gameplay completo nem multiplayer. |
| [DevPlaygroundSmokeTest.json](DevPlaygroundSmokeTest.json) | Execução completa iniciada em `2026-09-30T23:49:56.5371219Z` e encerrada em `2026-09-30T23:55:00.4817212Z`: `completed: true`, `passed: true`, 739 casos e zero erros capturados. Precede as últimas alterações de limpeza runtime. |
| [DevPlaygroundGameplaySmokeTest.json](DevPlaygroundGameplaySmokeTest.json) | Execução focada iniciada em `2026-10-01T00:02:01.5259034Z` e encerrada em `2026-10-01T00:03:58.6270036Z`: 58 casos, `completed: true`, `passed: true`, zero erros capturados. Exercita as alterações posteriores de gameplay e limpeza. |
| [DevPlayground-final.png](Captures/DevPlayground-final.png) | Captura final após a limpeza, sem objetos de Spirit ou barras de boss anteriores. É evidência visual daquele momento. |
| [UnityWindowsReleaseBuild.json](UnityWindowsReleaseBuild.json) | Build Windows Release completo: `Succeeded`, zero erros e 21 avisos. Concluído em `2026-10-01T00:24:34.6877541Z` (30/09, 21:24 em São Paulo), com as cinco cenas de produção e 533.838.395 bytes, aproximadamente 509 MiB. Executável existente em `Builds/DevToolsValidation/WindowsRelease/Karawara.exe`; ainda não executado. |

O runner do playground inclui aterramento, navegação, dano real, morte/revivência, cura, invulnerabilidade, habilidades e duração dos efeitos, armas, cada inimigo, duas Cucas com IA própria, dano somente no boss atingido, observação de IA/projéteis, prévias, alavancas/portão, plataforma e comparação de saves/assets. Sua linha de base é capturada quando o runner é invocado, depois da inicialização da sessão; não comprova retrospectivamente que nada tenha mudado antes desse ponto.

O caminho de saves observado é `C:/Users/migue/AppData/LocalLow/UaiStudio/Karawara - RPG`. Flags de comparação como `saveFilesUnchanged` em um relatório ainda `running` não devem ser interpretadas como resultado final.

Os timestamps dos JSONs usam UTC. As execuções de `2026-10-01T00:xxZ` ocorreram na noite de 30/09 no fuso de São Paulo.

## 10. Limites, riscos remanescentes e próximos passos

A validação descrita utiliza um host local. Ainda não há evidência de duas instâncias/clientes, duas máquinas, sincronização sob latência ou capacidade de jogadores. O build standalone completo foi concluído com sucesso e o executável existe, mas não foi lançado nem jogado. A ferramenta **Karawara → Validation → Build Windows Release** foi adicionada em `UnityMigrationAudit.cs`. A saída foi direcionada a `Builds/DevToolsValidation/WindowsRelease` após o Unity 6 rejeitar o destino inicialmente proposto dentro de Library.

MapaEsboco é uma cena de esboço sem marcador Spawn. O aviso de SceneChecker e a preservação da posição nesse caso são esperados; não constituem validação de um percurso jogável completo. Modelos, efeitos e entradas de catálogo não foram todos certificados visualmente apenas por serem carregáveis.

O build emitiu avisos de instancing para os protótipos de Terrain Tree6 e TallTree2. A leitura do TerrainData compartilhado por SceneMenu e Video1 confirmou zero instâncias desses dois protótipos; as 313 árvores pintadas usam Tree4. Os FBXs possuem geometria, portanto não se trata de GUID ou mesh ausente. Os protótipos não foram removidos nem substituídos por wrappers com escala diferente. Consulte [UnityTerrainBuildWarnings.md](UnityTerrainBuildWarnings.md) para a evidência e os limites dessa conclusão.

Os avisos de shader, scripts ausentes de assets não exercitados e referências antigas sem origem comprovada exigem decisão por asset e verificação visual. Não se deve substituir recursos por semelhança de nome nem remover componentes históricos indiscriminadamente.

Os runners completos/focados, as compilações de scripts e o build Windows Release estão concluídos nos limites registrados acima. Os próximos testes são jogar o executável standalone, completar combates e transições manualmente, validar multiplayer com dois clientes reais e conferir visualmente os assets legados cujas origens estão ausentes. Esses testes devem preservar o estado local já existente.
