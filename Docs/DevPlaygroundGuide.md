# Guia do DevPlayground

> Integração Homol em validação: os números antigos no fim deste guia são históricos. Os relatórios novos usam o prefixo `Homol`.

O DevPlayground reúne o jogador, os inimigos e os sistemas existentes de Karawara em uma cena de desenvolvimento. Ele inicia um host local de Netcode for GameObjects e usa o prefab real do jogador. O catálogo permite experimentar habilidades, armas e modelos sem editar seus assets originais durante a sessão.

## Abrir e usar

1. Fora do Play Mode, use **Karawara → Dev Playground → Generate Scene and Catalog** se precisar gerar a cena. Para regenerar, feche primeiro a cena DevPlayground; o gerador protege a cena aberta.
2. Use **Karawara → Dev Playground → Open Scene**, ou abra `Assets/DevTools/Scenes/DevPlayground.unity`.
3. Inicie uma sessão nova de Play diretamente nessa cena. Aguarde a mensagem de jogador de produção e host local prontos.
4. Pressione **F1** para fechar o painel e controlar o personagem. Pressione novamente para abrir as ferramentas.

O painel aberto libera o cursor e desativa a entrada de movimentação e habilidades. Portanto, feche-o antes de experimentar o percurso ou interagir com E. Outra sessão de jogo já ativa, um NetworkManager existente ou um PlayerManager existente impedem a inicialização do playground.

**Karawara → Dev Playground → Refresh Catalog** atualiza a lista de assets. O catálogo fica em `Assets/DevTools/Data/DevPlaygroundCatalog.asset`. Gerar ou atualizar é uma operação de edição; não use durante Play Mode.

A aba **Player** lista os dois SpellItems existentes e permite equipá-los. `FireSpell` dispara a bola de fogo; `NewTestSpell` é um placeholder que apenas registra uma mensagem, não um segundo poder implementado. Feche o painel com F1, selecione o caster com Q e use o ataque da arma. O host mostra a porta local real; quando 7777 está ocupada, usa uma alternativa. Clientes precisam dessa porta.

## Controles do personagem

Os controles abaixo vêm de `Assets/Scripts/PlayerControls.inputactions` e dos consumidores atuais de entrada. A janela Game precisa ter foco.

| Controle | Ação |
|---|---|
| W, A, S, D | Movimentar |
| Mouse | Mover câmera |
| Shift | Correr |
| Ctrl | Esquivar; com movimento, rolar; parado, recuar |
| Q | Alternar lança e caster |
| Espaço | Pular |
| Botão esquerdo do mouse | Ataque leve/RB; no caster, clique dispara e segurar carrega a bola de fogo |
| Botão direito do mouse | Ataque RT; manter pressionado fornece entrada de carga |
| Botão central do mouse | Travar alvo |
| Setas esquerda/direita | Trocar alvo travado |
| E | Interagir pelo PlayerInteractionManager |
| 1, 2, 3 | Habilidades de ataque, defesa e suporte; o código usa a liberação da tecla |
| Escape | Entrada de pausa/voltar da interface de produção |
| F1 | Abrir ou fechar ferramentas de desenvolvimento |

O arquivo de ações também declara Z como Teleport, mas não o use como atalho garantido do playground: os teletransportes de teste estão no painel Systems e nos pontos de interação da cena. As entradas de troca de arma estão comentadas no consumidor atual; use Equip no painel.

## Ferramentas do painel

### Player

Exibe vida atual/máxima e fornece invulnerabilidade, restauração contínua de vida, cura/revivência, retorno ao ponto inicial e reset dos cooldowns. Permite alterar vitalidade, endurance, velocidades, salto e esquiva na instância em execução. O dano físico da arma equipada pode ser ajustado e reaplicado ao modelo de arma.

As habilidades do catálogo são clones desbloqueados em memória. **Equip** coloca a habilidade no slot da categoria; **Activate** chama diretamente sua ativação de produção. **Clear skills activated from tools** remove as raízes dos efeitos criados por essas ativações e limpa os flags de poder de espírito. Essa limpeza rastreia somente ativações pelo comando Tools Activate; habilidades disparadas pelas teclas de produção 1/2/3 não entram nesse registro. Armas também usam clones e a base de itens de produção.

Os botões de elementos usam o `ElementManager` real do jogador para escolher None, Fire ou Water. **Transformações humano/animal não estão disponíveis como gameplay nesta cena.** Os scripts antigos de roda de transformação não possuem referências serializadas nas cenas/prefabs atuais, e `TransformToPlayer` não tem implementação ativa. Os modelos Persona, Jabuti, Pantera e Bear podem ser inspecionados no catálogo visual; isso não transforma o personagem controlado.

Stamina e mana não são recursos ativos nos scripts atuais. Alterar endurance não demonstra consumo ou regeneração de uma barra de stamina inexistente.

### Enemies

Escolha um prefab e uma quantidade entre 1 e 50. Há opções de Spawn, Spawn all, Clear e Respawn selection, além de pausa das máquinas de estado de IA e derrota dos inimigos gerados. Clear também remove as barras de boss associadas aos atores removidos. A área de combate possui obstáculos e NavMesh construído em execução a partir da geometria de teste. Contagem de objetos gerados e inimigos vivos aparece no painel.

Os prefabs são os mesmos utilizados pelo projeto; não há uma IA substituta de demonstração. A seleção inclui apenas entradas descobertas pelo gerador, e a existência de uma entrada não garante ausência de erros no comportamento daquele inimigo.

### Assets

Oferece busca e prévia visual dos prefabs do catálogo, com opções de materiais e animações. A prévia remove scripts e comportamentos de rede antes de ativar o modelo, desativa colliders e torna rigidbodies cinemáticos sem colisões. Animators e componentes Animation ficam desativados para impedir eventos e state behaviours de chamarem código removido. No Editor, scripts ausentes são removidos somente do clone de prévia. Clips selecionados são amostrados no modelo de prévia.

Essa prévia permite observar aparência e montagem. Ela não testa ataques, IA, interações, física ou eventos de animação do prefab. Para testar efeitos funcionais, use as habilidades em Player; para inimigos funcionais, use Enemies.

### Systems

Teletransporta o jogador para as estações, chama a interação de produção e permite executar o ataque leve da arma equipada. Também oferece prévias visuais de efeitos e assets interativos, com as mesmas restrições de segurança visual da aba Assets.

## Estações da cena

| Estação | Experimento |
|---|---|
| Hub | Ponto inicial e retorno do jogador |
| Combat | Inimigos, combate e obstáculos de navegação |
| Movement | Degraus, rampa de 20°, rampa de 55° e teto baixo |
| Physics | Caixas e esfera com Rigidbody |
| Skills | Faixa para observar habilidades e efeitos |
| Showcase | Modelos de prévia visual |
| Interactions | Interações com E e teletransportes locais |

A estação de portão usa **Interactable → Lever → AnimationTrigger → GateActivator**, adaptada à implementação atual de Homol. Ative as duas alavancas com E para abrir o portão. Cada sequência usa uma câmera, Animator e eventos próprios e restaura o tempo após a sequência.

A plataforma de teste usa o componente **Plataforma** de produção: permanecer no gatilho inicia o temporizador e desativa a plataforma após três segundos. Reabrir a cena em uma nova sessão restaura o estado original; o runner de validação restaura apenas sua instância de teste. O script histórico de elevador da arena foi recuperado durante a integração Homol; não há uma estação funcional de elevador nesta cena.

## Isolamento e limites

O host local testa a integração com os componentes Netcode existentes. Uma sessão local não comprova funcionamento entre duas máquinas, capacidade de jogadores ou sincronização sob latência.

O controlador clona assets mutáveis de habilidades, armas e ações e usa contexto de personagem em memória. `WorldSaveGameManager.DevPlaygroundIsolationActive` bloqueia gravações de saves enquanto esse ambiente está ativo, inclusive chamadas de SaveGame feitas por componentes de produção. O runner compara os arquivos `characterSlot*` em `Application.persistentDataPath`; a evidência deve ser consultada no relatório da execução.

O código operacional do controlador está limitado a **UNITY_EDITOR ou DEVELOPMENT_BUILD**. Em builds Release comuns, o controlador desativa seu GameObject. A cena de desenvolvimento é aberta diretamente no Editor e não constitui uma rota adicional de jogo no menu de produção.

## Estrutura adicionada

| Arquivo | Responsabilidade |
|---|---|
| Assets/DevTools/Runtime/DevPlaygroundController.cs | Inicialização isolada, ferramentas em execução e integração com componentes existentes |
| Assets/DevTools/Runtime/DevPlaygroundCatalog.cs | Dados serializados de prefabs, habilidades, armas, materiais e animações |
| Assets/DevTools/Editor/DevPlaygroundBuilder.cs | Geração da cena, catálogo e estações |
| Assets/DevTools/Editor/DevPlaygroundSmokeTest.cs | Validação da sessão ativa com casos e relatório JSON |
| Assets/DevTools/Editor/UnityMigrationAudit.cs | Auditoria de integração no Editor |
| Assets/DevTools/Editor/LegacyAssetRepair.cs | Reparo controlado da origem ausente nos prefabs FireCircle |
| Assets/DevTools/Scenes/DevPlayground.unity | Cena gerada |
| Assets/DevTools/Data/DevPlaygroundCatalog.asset | Catálogo gerado |

## Evidências e validação

Este guia descreve o comportamento implementado e o snapshot de validação disponível. Consulte o resultado de cada execução e seu timestamp nos relatórios:

- `Docs/UnityMigrationAuditAssets.md`: referências, alcance da auditoria estática, histórico e reparos.
- `Docs/UnityMigrationEditorAudit.json`: verificações executadas no Editor.
- `Docs/UnityReleaseScriptCompilation.json`: evidência de compilação Release.
- `Docs/UnityDevelopmentScriptCompilation.json`: compilação de scripts de player Development.
- `Docs/UnityWindowsReleaseBuild.json`: estado e resultado da construção standalone completa.
- `Docs/ProductionSceneSmokeTest.json`: execução das cenas de produção.
- `Docs/DevPlaygroundSmokeTest.json`: gerado após executar **Karawara → Validation → Run Playground Smoke Test** numa sessão DevPlayground pronta.
- `Docs/DevPlaygroundGameplaySmokeTest.json`: execução focada nas mudanças recentes de gameplay e limpeza.

O runner do playground não inicia nem encerra Play Mode. Seus casos incluem dano real, morte/revivência, cura, invulnerabilidade, navegação, aterramento, habilidades, armas, inimigos, prévias, alavancas/portão, plataforma e isolamento de saves. O baseline de assets/saves é capturado ao invocar o runner; ele não cobre alterações anteriores feitas durante a inicialização da cena. Falhas e exceções ficam associadas a cada caso no JSON.

A execução completa encerrada em `2026-09-30T23:55:00.4817212Z` registra `completed: true`, `passed: true`, 739 casos e zero erros capturados. Ela precede as últimas mudanças de limpeza. A execução focada nessas mudanças terminou em `2026-10-01T00:03:58.6270036Z`, com 58 casos, `completed: true`, `passed: true` e zero erros capturados. O teste de produção registra cinco cenas, zero erros capturados e saves inalterados.

A [captura final](Captures/DevPlayground-final.png) mostra o playground após a limpeza, sem os objetos de Spirit ou barras de boss anteriores. As compilações de scripts Release e Development mais recentes registram sucesso em `2026-10-01T00:04:51.7738032Z` e `2026-10-01T00:05:36.132132Z`, respectivamente. São horários UTC, correspondentes à noite de 30/09 em São Paulo.

**Karawara → Validation → Build Windows Release** inicia a construção standalone completa. A execução terminou em `2026-10-01T00:24:34.6877541Z` com `Succeeded`, zero erros e 21 avisos, incluindo avisos de protótipos de Terrain detalhados em [UnityTerrainBuildWarnings.md](UnityTerrainBuildWarnings.md). O executável existe em `Builds/DevToolsValidation/WindowsRelease/Karawara.exe`; o conteúdo do build ocupa aproximadamente 509 MiB. O jogo construído ainda não foi lançado nem jogado. O resultado final está em [UnityWindowsReleaseBuild.json](UnityWindowsReleaseBuild.json).

## Animator após importação do merge

Se um estado de magia avançar sem animação ou eventos, confirme no Animator se o clip está presente. Nesta integração, o cache compilado de Humanoid omitia 19 clips apesar de os motions resolverem no asset. Reimportar `Assets/Animations/Humanoid.controller` e os overrides em `Assets/Animations/AnimatorOverrideController` recuperou os clips; o teste real Ctrl/Q/primeiro clique passou depois disso. Não substitua o controller por uma implementação de debug.

## Validação atual da integração Homol

O catálogo atual contém sete skills, quatro armas, dois SpellItems, oito prefabs de inimigo/boss, 940 prévias de prefab, 213 VFX, 1171 clips e 1292 materiais. `NewTestSpell` continua um placeholder; uma prévia de asset não equivale a um poder funcional.

- `HomolDevPlaygroundGameplaySmokeTest.json`: 72/72 casos aprovados, zero erros, saves inalterados, após os fixes de ciclo de vida e input.
- `HomolInputRegression.json`: Ctrl/rolagem de 3,38 m e Q/primeiro clique com bola de fogo, sem erros, numa sessão nova.
- `HomolCombatWindowRegression.json`: idle sem dano, janela de ataque real, disparo normal/carregado e término natural da ação antes de restaurar equipamento; três casos aprovados.
- `HomolUIRegression.json`, `HomolBossTransitionRegression.json`, `HomolDialogueRegression.json`: HUD, boss bars e escritores de diálogo validados nos limites descritos nos relatórios.
- `HomolProductionSceneSmokeTest.json`: sete cenas inicializadas sem erros capturados, sem inimigos atravessando cenas, um AudioListener por cena, saves preservados. Cenas pausadas são observadas por tempo não escalado; isso não percorre seu gameplay.

As fixtures de input usam uma cópia temporária de InputSettings para rotear os dispositivos virtuais à GameView; o asset original é restaurado. Scripts reproduzíveis e requisitos estão em `Docs/Validation/README.md`. Aguarde a animação carregada terminar antes de restaurar equipamento; quatro segundos eram insuficientes para a fixture.

A execução completa antiga `HomolDevPlaygroundSmokeTest.json` confirmou as 940 prévias mas falhou numa seleção incorreta de collider da Cuca; a correção foi revalidada nos 72 casos de gameplay. Não se considera essa execução completa integralmente aprovada. Referências herdadas ainda ausentes e shaders de showcase são detalhados em `HomolLegacyAssetAudit.md`.