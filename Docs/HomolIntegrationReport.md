# Integração de Homol em refactor/qol

## Base e recuperação

- Branch de trabalho: `refactor/qol`.
- Origem integrada: `origin/Homol`, commit `1ceb81c3` (302 commits além da base `main`).
- Snapshot de todas as alterações locais anteriores: `85abcde1`.
- Branch de recuperação: `codex/backup-before-homol-20260930`.
- Unity mantida em **6000.4.6f1**, conforme solicitado.
- Nenhum push feito.

## Decisões de integração

- Mantidas as cenas, tutoriais, créditos, input e arquitetura de gameplay mais recentes de Homol.
- Mantidos os packages já validados para Unity 6.4, em vez de retornar às versões antigas de Homol. A ponte Unity MCP local foi preservada.
- `runInBackground` segue Homol, habilitado.
- Materiais, fontes e VFX em conflito preservam os dados artísticos de Homol; a serialização é migrada pelo importer da Unity 6.4.
- `Player.prefab` usa a nova hierarquia de Homol, que já não contém as cinco referências antigas de Animator corrigidas anteriormente.
- `Circle_ref.prefab` conserva seus dados locais sem a origem ausente e incorpora a mudança de partícula de Homol.
- Os scripts movidos de `Scripts-Video` para `States` mantêm os fixes de inicialização e isolamento de saves compatíveis com a nova estrutura.
- Removido o `EnemyAI` antigo, já deletado em Homol, evitando duplicação de tipo. A Cuca atual usa `CucaBoss` e `HealthManager`, incluindo sua própria detecção de lança e fluxo para créditos.
- A velocidade de projéteis da nova Cuca utiliza `Rigidbody.linearVelocity`.
- DevPlayground adaptado ao boss atual. Derrota no playground não carrega créditos; o comportamento de produção foi preservado.
- A nova lista de SpellItems do banco de itens é clonada antes do Awake no playground, preservando os assets de origem.
- A estação de alavancas usa o `AnimationTrigger` de Homol com Animator, câmera e eventos completos; o runner acompanha a nova sequência.
- O runner de cenas enumera as sete cenas habilitadas atuais, incluindo Tutorial_Final e Creditos.
- Reparados nove `.meta` que já continham marcadores de conflito em Homol. A escolha de GUID preservou as referências existentes: `R_Cura` mantém a referência usada por HealCircle, os três prefabs de cabana/estrutura mantêm as referências de Video1 e do catálogo, e as pastas mantêm suas identidades correspondentes. Nenhuma referência precisou ser reescrita.

## Validação

Concluída a integração local e a validação descrita abaixo. Os relatórios anteriores em `Docs/Unity*` e `Docs/DevPlayground*` comprovam o estado **anterior ao merge**, e não o resultado desta integração. As novas evidências usam o prefixo `Homol`.

- Índice Git sem entradas não resolvidas.
- Nenhum GUID duplicado nem marcador de conflito nos `.meta` após os reparos.
- A auditoria de referências em 100 cenas/configurações/render assets de Homol e no Player não encontrou referência com origem válida em Homol que tenha perdido essa origem na integração.
- Importação pós-merge concluída na Unity 6000.4.6f1, com MCP conectado e scripts compilados.

## Limites

As evidências abaixo distinguem compilação, fixtures de runtime e observação de cenas. O executável Release foi gerado, mas não foi jogado nesta validação. Paridade visual integral, percurso completo e multiplayer com dois clientes permanecem pendentes.

## Regressões das capturas: correções e evidências

- Fallback de host entre a porta configurada e 19 alternativas, seguido de porta do sistema; menu mostra a porta real. Novo jogo aguarda o player e preserva slots ocupados.
- Referências opcionais de footsteps, morte, câmera de missão e áudio verificadas. Projéteis pertencem ao inimigo correto e cancelam callbacks ao desativar.
- Colliders de ataque fecham fora da ação; boss bars são removidas no despawn ou troca de cena.
- Q deixa de cancelar o cast a cada frame; o primeiro clique resolve a ação após equipar a arma.
- Diálogo com um único escritor e sessão pertencente ao trigger correto. Campos de mídia duplicados em SupportSkill corrigidos preservando referências.
- HUD do playground com escala válida, slots visíveis e SpellItems disponíveis no painel; armas e spells mutáveis clonados.
- Animações temporárias de DummyRanged excluem o evento exclusivo da Saci, mantendo os assets de produção.
- Variantes de luz preservadas nos quatro graphs customizados; API de sombras atualizada e material de partículas de cogumelos migrado para URP com textura existente.

O teste real de input em `Docs/HomolInputRegression.json` passou: Ctrl acionou a rolagem com deslocamento de 3,36 m, Q selecionou o caster e o primeiro clique criou FireBallManager, sem erros. A causa adicional era cache compilado do Animator incompleto: Humanoid resolvia motions no asset, mas sua lista compilada tinha 44 clips e omitia magia/rolagem. Reimportar Humanoid e overrides recuperou 63 clips. Corrigidos também o callback de carga da mão direita, o lookup nulo de spell e o evento de disparo normal.

`Docs/HomolUIRegression.json` passou nos quatro casos de HUD/boss/morte/cleanup e preservou os saves. `Docs/HomolDialogueRegression.json` passou nos sete casos com classes reais em fixtures descartáveis; tecla física E/Enter e graphs de produção permanecem fora dessa prova. `Docs/HomolPortFallbackRuntime.json` registra host ouvindo na 7778 com a 7777 ocupada, player criado e dono local.

Recuperados quatro scripts originais de ambiente da arena com seus GUIDs: elevador, queda, impacto e quebra de totens. Veja `Docs/HomolLegacyAssetAudit.md` para origem e limites dos assets herdados sem fonte recuperável.

## Evidências atuais após Homol

- `HomolUnityMigrationEditorAudit.json`: oito cenas abertas, 562 prefabs, 640 materiais e 43 controllers. Zero erros de abertura e zero shaders problemáticos nas dependências de produção. Restam 62 problemas de shader em assets de showcase e 28 objetos com 43 componentes ausentes herdados; o histórico e os limites estão em `HomolLegacyAssetAudit.md`.
- O Highlight do NPC foi recuperado por correspondência exata dos identificadores de propriedades do material com o graph histórico; graph e textura originais importados, brilho 2 preservado.
- A textura Light_Point tinha extensão JPEG mas conteúdo WebP, que a Unity não importava como Texture2D. Conversão sem perdas para PNG com o mesmo GUID resolveu a origem das partículas quadradas. A captura `Captures/Homol-Production-CucaLoad.png` mostra chão/paredes iluminados e partículas com textura. Não constitui comparação de paridade visual integral.
- `HomolBossTransitionRegression.json`: um caso aprovado com boss real ainda spawned e troca real de active scene temporária; barra removida e saves preservados. Não carrega uma cena de produção nessa fixture.
- `HomolDevPlaygroundSmokeTest.json`: 940 prévias de assets passaram; a execução completa registra falha de fixture da Cuca porque escolhia o collider da mão esquerda. Não se apresenta esse relatório como aprovado integralmente.
- `HomolDevPlaygroundGameplaySmokeTest.json`: 72 casos aprovados após selecionar o collider da lança real. Dano, cura, morte, skills, armas, inimigos, alavancas e limpeza com saves preservados. A execução final após alterações de ciclo de vida/input terminou em 2026-10-01T12:20:37Z com 72/72 casos aprovados e zero erros; fingerprints de saves inalterados.
- `HomolProductionSceneSmokeTest.json`: passagem completa por sete cenas, zero erros capturados, nenhum inimigo sobrevivente da cena anterior e um AudioListener habilitado por cena. O runner reproduz apenas a posição inicial de NewGame, sem chamar NewGame nem gravar saves. Aguarda cinco segundos de tempo não escalado e trinta frames; as pausas de tutorial são registradas e respeitadas.

Inimigos deixaram de herdar DontDestroyOnLoad e seus spawners usam destroyWithScene; o player continua persistente. O listener do player só é habilitado no dono local quando a cena não possui listener autoral habilitado. A interface de créditos passou ao InputSystemUIInputModule com as referências de ações padrão existentes.

`NewTestSpell` é um placeholder existente em Homol, não um segundo poder funcional. O playground expõe os sete assets de skill, quatro armas e dois SpellItems encontrados, sem inventar sistemas ausentes. Multiplayer com dois clientes, latência, percurso completo e funcionamento natural de cada ataque continuam fora destas provas.
A revisão de 935 imagens encontrou outras três texturas com cabeçalho WebP e extensão PNG. `Rock_Normal.png`, a imagem do laser da Cuca e `Artificial/Teste/Noise.png` foram convertidas para PNG no mesmo caminho, mantendo exatamente os pixels decodificados e os GUIDs/metas. A Unity não carregava nenhuma das três como Texture2D antes; passou a carregá-las depois. Os três materiais URP Lit com normal map foram validados pela API de LitShader, restaurando _NORMALMAP; BakedLit preservados. Zero imagens com essa assinatura incorreta restaram no escopo verificado.

O teste final de input usa InputSettings temporário clonado para impedir que o foco do Editor consuma os eventos virtuais; o asset original e os mapas são restaurados. `HomolInputRegression.json` registrou Ctrl com deslocamento 3,38 m e Q/primeiro clique com projétil real, sem erros. Isso comprova o caminho de input/animação em fixture, não uma sessão de teclado físico com janela sem foco.
A fixture de combate final aguarda o término natural da ação antes de restaurar a arma: três casos aprovados em `HomolCombatWindowRegression.json`, incluindo carga completa e flags livres após a animação. Uma tentativa anterior restaurava o equipamento após quatro segundos e deixava a próxima fixture aguardando ação; não foi aplicado patch especulativo às transições do Animator. Os corpos C#6 utilizados estão preservados em `Docs/Validation`.

Compilações finais de scripts em 2026-10-01T12:30:53Z (Release) e 12:31:07Z (Development): sucesso, 47 assemblies cada. Auditoria final às 12:30:37Z mantém oito cenas abertas, zero erros de carga, zero shaders de produção problemáticos; referências herdadas ausentes continuam documentadas. O primeiro build standalone foi cancelado; seu resultado está em `HomolUnityWindowsReleaseBuild-CancelledAttempt.json`, e a nova tentativa usa `HomolUnityWindowsReleaseBuild.json`.
## Encerramento da validação

- Build Windows Release concluído em 2026-10-01T13:25:19Z: **Succeeded, zero erros, 176 warnings**, sete cenas, 1.010.626.194 bytes de saída. Executável atualizado em 2026-10-01T13:24:57Z. O resultado integral está em `HomolUnityWindowsReleaseBuild.json`; a tentativa cancelada permanece separada. Não foi lançada nem jogada a versão standalone.
- Passagem final pós-build em 2026-10-01T17:08:12Z: sete cenas observadas, zero erros capturados, um AudioListener ativo em cada cena, nenhum inimigo persistente da cena anterior, saves inalterados. `HomolProductionSceneSmokeTest.json` registra o resultado e o escopo; as capturas em `Captures/Homol-Production-*.png` mostram essa passagem, incluindo a caverna após as últimas conversões de textura.
- Os warnings do BuildReport são detalhados em `HomolBuildWarnings.md`; sucesso de build não significa ausência de avisos ou correção de todos os assets históricos.
- O playground abre por `Karawara > Dev Playground > Open Scene` ou pelo asset `Assets/DevTools/Scenes/DevPlayground.unity`. Execute Play e use F1 para abrir/fechar o painel. Consulte `DevPlaygroundGuide.md` para controles, estações, catálogo e limites.
- Arquivos da integração e migração estão enumerados em `HomolStagedIntegrationFiles.txt` e `HomolAdditionalMigrationFiles.txt`. As ferramentas de desenvolvimento continuam em `Assets/DevTools`; as fixtures C#6 ficam fora de Assets em `Docs/Validation`.