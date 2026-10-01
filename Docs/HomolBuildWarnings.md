# Warnings do build Windows Homol / Unity 6.4

## Evidência

Build final iniciado em 2026-10-01 12:38:35 UTC e concluído em 13:25:19 UTC: **Succeeded, 0 erros, 176 warnings**, 1.010.626.194 bytes. Fonte: `Library/LastBuild.buildreport`, lido offline com UnityPy; resumo confere com `Docs/HomolUnityWindowsReleaseBuild.json`. O Editor.log já pertence a outra sessão e não contém este build. Nenhuma operação do Editor, Assets, importação, build ou Play foi executada nesta revisão.

| Categoria | Ocorrências | Risco atual |
|---|---:|---|
| MCP WebSocket | 118 | Falha de transporte do Editor durante build; não demonstra defeito do player |
| Protótipos de árvore Terrain | 24 | 8 mensagens distintas repetidas 3 vezes; instanciamento/billboard/iluminação de protótipos precisam validação |
| Scripts ausentes | 14 | Componentes de produção e um asset DOTweenSettings continuam sem tipo válido |
| Shader `pow` com base potencialmente negativa | 18 | Possíveis NaN/artefatos em entradas negativas; warning não prova que o valor ocorre em runtime |
| `_FORWARD_PLUS` obsoleto | 1 | Compatibilidade atual mantida pelo URP; aumenta risco/custo de compilação e migração futura |
| Truncamento float4 para float3 | 1 | Conversão implícita de posição no shader CrystalBall; xyz preservado, não prova artefato |

**58 warnings de conteúdo; 118 de transporte MCP.** O build bem-sucedido não elimina as limitações abaixo.

## Scripts e origem

Tutorial_Final: TotemQueCai_ArenaEsq 3, TotemQueCai_ArenaDir 3, Elevador (1) 1. Os sete registros já entraram vazios (`m_Script: {fileID: 0}`, sem campos customizados) no commit `d42ccd2d`; herdados e não reconstruídos por inferência.

Video1: Bush1_group, ReimportComTransform, MiguelGuimaraes_768555, um componente cada. t: CucaFire, três. Permanecem no audit anterior; referências legadas foram classificadas em `Docs/HomolLegacyAssetAudit.md`. Este build não prova quais comportamentos legados ainda seriam necessários nem que novos controladores sejam equivalentes em todos os casos.

`Assets/Scripts/Boss Cuca/MeleeBoss/Resources/DOTweenSettings.asset`: um script inválido, fileID 16995157 / GUID `a811bde74b26b53498b4f6d872b09b6d`. O asset não difere de origin/Homol, e esse GUID não existe nas metas de Homol nem atuais: **referência herdada comprovada**. DLL atual `Assets/Scripts/Boss Cuca/MeleeBoss/Plugins/Demigiant/DOTween/DOTween.dll` tem GUID `026ba400d5d5b34478e388da43dae0e7`, também igual a Homol. Não foi redirecionado: seria necessário provar o tipo/fileID do DLL e o comportamento de fallback antes de reparar. Warning pode significar configurações de tween não carregadas; não prova falha dos tweens. Há dois assets distintos: `Assets/Resources/DOTweenSettings.asset` referencia corretamente o DLL GUID `026ba400d5d5b34478e388da43dae0e7` / fileID 16995157 (atual e Homol); o warning extraído é especificamente do asset legado duplicado no caminho MeleeBoss/Resources acima. Não confundir os dois nem concluir que a configuração principal falhou.

## Terrain

Protótipos: ToraMuro, Tree6, TallTree2, Rock.005, Rock, AmountStones, Pedra (5), ArvoreGrande. Tree6/TallTree2/AmountStones/ArvoreGrande não oferecem mesh renderer válido para instanciamento; demais recebem aviso de shader Nature/Soft Occlusion para billboard/lighting. São warnings reais, sem substituição automática de modelos/shaders.

`Docs/UnityTerrainBuildWarnings.md` documenta o TerrainData `Assets/Terrain/Terrain_0_0_fa89942d-8e3c-490e-a681-cdecb9a37ef7.asset`, GUID `a810e2b5d78d66e4bad1c586cadbc3e5`, referências em SceneMenu/Video1, Tree6/TallTree2 com zero instâncias e 313 instâncias Tree4. Esses dois warnings/configurações já existiam no levantamento anterior. A contagem/atividade dos demais protótipos neste build não foi reavaliada; não generalizar zero instâncias a todos.

## Shaders

`PT_Vegetation_Plants_Shader.shader`, em `Assets/Polytope Studio/Lowpoly_Environments/Sources/Shaders/`: `_FORWARD_PLUS` é alias compatível definido pelo URP 17.4 em `ShaderLibrary/ForwardPlusKeyword.deprecated.hlsl`. O arquivo instalado recomenda `_CLUSTER_LIGHT_LOOP` e avisa impacto no tempo de compilação. Shader não difere do Dev HEAD original; warning de API atual, não nova alteração local. Nenhuma otimização aplicada.

`Assets/Data/Modelos/crystal-ball/textures/CrystalBallShader.shader:49`: `TransformObjectToHClip(IN.positionOS)` passa float4 a função float3. Fonte igual a Homol. Conversão explícita `.xyz` seria equivalente mas não é necessária para este build e não foi aplicada.

Warnings `pow` aparecem em Tornado_Vertex_Anim, Lit_ToonEroise 1, Toon Shader, ToonEroise, InteractiveWater e duas saídas de vfx_fire_1. Tornado_Vertex_Anim/InteractiveWater não diferem de Homol. As correções de Toon mantiveram variantes de iluminação; não alteraram cores ou a expressão Power. Compilador agora expõe warnings; não foi provado que cada warning já era emitido em Unity 2022. Não aplicar `abs` sem avaliar a expressão: alteraria o efeito artístico. Validação visual/entradas é o próximo passo para qualquer artefato concreto.

## Extração das mensagens

Cada linha abaixo é a primeira linha exata da mensagem do BuildReport; blocos repetidos de defines/keywords foram omitidos.

- **3×** The tree ToraMuro must use the Nature/Soft Occlusion shader. Otherwise billboarding/lighting will not work correctly.
- **3×** The tree Tree6 couldn't be instanced because the prefab contains no valid mesh renderer.
- **3×** The tree TallTree2 couldn't be instanced because the prefab contains no valid mesh renderer.
- **3×** The tree Rock.005 must use the Nature/Soft Occlusion shader. Otherwise billboarding/lighting will not work correctly.
- **3×** The tree Rock must use the Nature/Soft Occlusion shader. Otherwise billboarding/lighting will not work correctly.
- **3×** The tree AmountStones couldn't be instanced because the prefab contains no valid mesh renderer.
- **3×** The tree Pedra (5) must use the Nature/Soft Occlusion shader. Otherwise billboarding/lighting will not work correctly.
- **3×** The tree ArvoreGrande couldn't be instanced because the prefab contains no valid mesh renderer.
- **3×** Script attached to 'TotemQueCai_ArenaEsq' in scene 'Assets/Scenes/Tutorial_Final.unity' is missing or no valid script is attached.
- **1×** Script attached to 'Elevador (1)' in scene 'Assets/Scenes/Tutorial_Final.unity' is missing or no valid script is attached.
- **3×** Script attached to 'TotemQueCai_ArenaDir' in scene 'Assets/Scenes/Tutorial_Final.unity' is missing or no valid script is attached.
- **1×** Script attached to 'Bush1_group' in scene 'Assets/Scenes/Video1.unity' is missing or no valid script is attached.
- **1×** Script attached to 'ReimportComTransform' in scene 'Assets/Scenes/Video1.unity' is missing or no valid script is attached.
- **1×** Script attached to 'MiguelGuimaraes_768555' in scene 'Assets/Scenes/Video1.unity' is missing or no valid script is attached.
- **3×** Script attached to 'CucaFire' in scene 'Assets/Scenes/t.unity' is missing or no valid script is attached.
- **1×** Script attached to 'DOTweenSettings' in asset 'Assets/Scripts/Boss Cuca/MeleeBoss/Resources/DOTweenSettings.asset' is missing or no valid script is attached.
- **1×** Shader warning in 'Shader Graphs/Tornado_Vertex_Anim': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Assets/VFX_Shader/Boss/Saci/Tornado_Body/Tornado_Vertex_Anim.shadergraph(354) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/Tornado_Vertex_Anim': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Assets/VFX_Shader/Boss/Saci/Tornado_Body/Tornado_Vertex_Anim.shadergraph(849) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/Tornado_Vertex_Anim': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Assets/VFX_Shader/Boss/Saci/Tornado_Body/Tornado_Vertex_Anim.shadergraph(1323) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/Tornado_Vertex_Anim': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Assets/VFX_Shader/Boss/Saci/Tornado_Body/Tornado_Vertex_Anim.shadergraph(1802) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/Lit_ToonEroise 1': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at 1.shadergraph(404) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/Lit_ToonEroise 1': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at 1.shadergraph(4675) (on d3d11)
- **1×** <b><color=#cc7a00>MCP-FOR-UNITY</color></b>: [WebSocket] Receive loop error: The remote party closed the WebSocket connection without completing the close handshake.
- **59×** <b><color=#cc7a00>MCP-FOR-UNITY</color></b>: [WebSocket] Connection closed: The remote party closed the WebSocket connection without completing the close handshake.
- **58×** <b><color=#cc7a00>MCP-FOR-UNITY</color></b>: [WebSocket] Keep-alive failed: The remote party closed the WebSocket connection without completing the close handshake.
- **1×** Shader warning in 'Shader Graphs/Toon Shader': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Shader.shadergraph(322) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/Toon Shader': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Shader.shadergraph(2130) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/Toon Shader': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Shader.shadergraph(2948) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/ToonEroise': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Assets/VFX_Shader/Artificial/Teste/Shaders_Phelipe/ToonEroise.shadergraph(319) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/ToonEroise': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Assets/VFX_Shader/Artificial/Teste/Shaders_Phelipe/ToonEroise.shadergraph(2360) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/ToonEroise': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Assets/VFX_Shader/Artificial/Teste/Shaders_Phelipe/ToonEroise.shadergraph(3273) (on d3d11)
- **1×** Shader warning in 'Polytope Studio/PT_Vegetation_Plants_Shader': _FORWARD_PLUS shader keyword has been deprecated. Please update your shaders to use _CLUSTER_LIGHT_LOOP shader keyword instead, otherwise shader compilation times may be negatively affected. at /Users/migue/Dev/Karawara/Library/PackageCache/com.unity.render-pipelines.universal@0d0659f1368d/ShaderLibrary/ForwardPlusKeyword.deprecated.hlsl(12)
- **1×** Shader warning in 'Custom/URP_CrystalBallShaderWithSmoke': 'TransformObjectToHClip': implicit truncation of vector type at Assets/Data/Modelos/crystal-ball/textures/CrystalBallShader.shader(49) (on d3d11)
- **1×** Shader warning in 'Hidden/VFX/vfx_fire_1/System/Inside Flames': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Flames/Art/VFX/vfx_fire_1.vfx(324) (on d3d11)
- **1×** Shader warning in 'Hidden/VFX/vfx_fire_1/System/Inside Flames': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Flames/Art/VFX/vfx_fire_1.vfx(1461) (on d3d11)
- **1×** Shader warning in 'Hidden/VFX/vfx_fire_1/System (1)/Outside Flames': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Flames/Art/VFX/vfx_fire_1.vfx(324) (on d3d11)
- **1×** Shader warning in 'Hidden/VFX/vfx_fire_1/System (1)/Outside Flames': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Flames/Art/VFX/vfx_fire_1.vfx(1461) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/InteractiveWater': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Assets/VFX_Shader/Artificial/Elementos/InteractiveWater/InteractiveWater.shadergraph(408) (on d3d11)
- **1×** Shader warning in 'Shader Graphs/InteractiveWater': pow(f, e) will not work for negative f, use abs(f) or conditionally handle negative values if you expect them at Assets/VFX_Shader/Artificial/Elementos/InteractiveWater/InteractiveWater.shadergraph(4591) (on d3d11)
