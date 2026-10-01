# Homol legacy asset audit

## Restored environment scripts

Four scripts and their original `.meta` files were restored byte-for-byte from `539da54a^`. Commit `539da54a` (`Otimizas Robson`) deleted the arena scripts in bulk while retained prefabs and production scene instances still referenced them. No replacement implementation exists in current sources.

| Script | Original GUID | Behavior |
| --- | --- | --- |
| Elevador | efba8b3251c11e14e96e6a11fa58611b | Moves the platform between Y -6.4 and -2.5 at its serialized speed. |
| QuedaTotemArena | f6fd88184b04cc04292bb456796f874c | Triggers the fall animation on HitPlayer contact; animation events toggle the first child. |
| ImpactoQuedaTotem | 7e1378fba70e08b4ca5d704ef742fdc5 | Caiu animation event enables the assigned hitbox, hides the model and disables collision/rendering; clears the hitbox after 0.3 seconds. |
| QuebraTotem | 8f47d7eabc6491a40aef65806cd0d9da | Hides the totem on Inimigo/Boss collision. |

Affected assets include `Assets/Data/Level/ARENA BOITATA/ARENA MONTADA/Arena Boitata.prefab`, `Assets/Data/Level/ARENA BOITATA/Tudo do CenÃ¡rio/Absolutamente tudo do cenario.prefab`, and arena instances in `Assets/Scenes/Tutorial_Final.unity`. Totem Animator controllers and existing Totem*Caianimation clips retain `AtivHit`/`Caiu` events. Assigned hitboxes and required Renderer/Collider components exist. The elevator has no competing movement component. No damage system was invented or replaced.

Unity compilation and runtime elevator/totem behavior must be verified after restoration. The pre-restoration editor report listed 38 affected objects (some objects contain multiple missing scripts); those counts must be refreshed rather than subtracting script-file counts.

## Remaining inherited missing scripts

These references were already present in Homol; the integration did not remove their source assets.

| Category | Evidence and decision |
| --- | --- |
| Arena Boss prototype | GUID 5fa689a30bc2c06419a8745e92949800 maps to historical Boss.cs. Restoring legacy health/combat could compete with current boss systems; not restored. |
| Video1 NPC cutscene | GUIDs 5ab2c2b9f387b7d4ea9f64bd40f79c28 and 412d55f7e78e0274b8b4f6f35ec9eaaa map to CutSceneMiguel.cs at cf3d4f49 and cutscenemiguel1.cs at d47d3f05. Both directly toggle cameras on keyboard E and dereference a Player at Start. Not restored because they may conflict with the current cutscene controller. |
| t/CucaFire prototype | acc908623fbf49bda342928370cd50f3 = CucaBossHealth; 2ce4c0b4e9e745d8bc06302a2aed34e1 = ColliderCajadoCuca; ba4d578f1647829429090841e2eb2c10 = Raylight. Historical sources exist, but restoring legacy health and keyboard L attacks could reactivate conflicting behavior. Not restored. |
| SettingsContainer package remnants | e7ab98226ccd6544995d0da41050492c, f7771332067bea74186ee34e63b6f936, e8818091a26e2994f9046af11accb9b5 and missing SG widget prefabs. Matching source metas were not found in current packages/assets or reachable Git history. |
| Projectile demo scripts | 12ba759cd568c0e47a7018ab70867a0d is referenced by the two Vfx_Projectile demo prefabs; no recoverable source meta was found. |
| Minimap PointerIcon | 052b1b96b899e4940bc6115c9fe1594c and f5f67c52d1564df4a8936ccd202a3bd8 have no recoverable source meta in the searched history. |
| Video1 Bush1_group | 3df6ca0806c0f0642872a56e9ccc6094 has no recoverable source meta in the searched history. |

Missing components were preserved; none were broadly deleted or replaced.

## Recovered production Highlight shader

`Assets/Materiais/HighlightMat.mat` references missing shader GUID `c71142ef968736d488939fb018fa8b3e`, shader file ID `-6465566751694194690`. The responsible production object is child `Sphere (1)` in `Assets/Scripts/Missao/Prefab/NPC TESTE.prefab`, MeshRenderer file ID `8178381848365499780`. Its material GUID is `60ad868f02bc7ea4bb6dc58482a8ebfb`.

The originally referenced shader GUID was not found in current Assets/package metas or reachable Git meta history. A second audit searched the material property IDs instead and recovered `Assets/ShaderCombat/HiglightShader.shadergraph` from `b1c04d21^`. Both exact reference names match: `Texture2D_215049cc388a46a39355f103cb9fc2e6` and `Vector1_d17073cd821e48cea1ca5b053fadc972` (GlowPower). The historical graph uses URP Lit, SampleTexture2D and Fresnel; its original GUID is `97c0b877a5009034e8d1e9bb639cafad`. This property identity and the historical companion Highlight material support the inference that it is the intended authored graph despite the later dangling GUID.

Restored the graph and original meta from `b1c04d21^`, plus the referenced original `Paladin_diffuse.png` and meta (GUID `0c51c12fd23ea3c4ba0fabc40c1726c4`). Commit `b1c04d21` (`Redo`) had deleted these Combat sources in bulk. Redirected only the existing HighlightMat shader GUID, retaining its texture reference and GlowPower value 2. Original directory metas were restored where absent. No replacement shader appearance was invented. Unity 6000.4.6f1 import confirmed Shader Graphs/HiglightShader is supported, both texture loads resolve, and GlowPower remains 2. Production visual checks remain required.

M_Boat and the Vfx_Projectile Ground material are showcase issues rather than entries in productionShaderProblems. Their shader GUIDs (`92a542d49c0d1d0478c243e758b81084`, `6e4ae4064600d784cac1e41a9e6f2e59`) likewise lack recoverable source evidence.

## Final Editor audit (2026-10-01 11:54:08 UTC)

Fresh Edit Mode audit after original Arena scripts and Highlight recovery loaded all eight audited scenes, 562 prefabs, 640 reachable materials and 43 animator controllers. Result: 28 objects with 43 missing MonoBehaviour components, zero production shader issues, 62 showcase shader issues and zero scene/asset load errors. See `HomolUnityMigrationEditorAudit.json`.

Tutorial_Final retains seven empty MonoBehaviour records with `m_Script: {fileID: 0}`: three each on TotemQueCai_ArenaDir/Esq and one on Elevador (1). These are not unresolved restored GUIDs: the records have no script GUID and no custom serialized fields. Their first appearance in commit `d42ccd2d` (Totem Teleporta) already has the same empty records. The elevator resembles the historical Arena prefab, but its serialized speed was lost; the three totem records cannot uniquely distinguish QuedaTotemArena, ImpactoQuedaTotem and QuebraTotem, and Impacto also lost its required hitbox reference. No speculative scene component repair was applied.

Production initialization smoke report dated 11:45:34 UTC captured zero errors in six scenes and 80 repetitions of one unique error in Creditos: StandaloneInputModule reads UnityEngine.Input.mousePosition while only the Input System is enabled. This report predates the separate production UI input module repair. It checks initialization only and does not certify visual appearance or full gameplay.


## Production texture header repairs (2026-10-01)

Header scan of PNG/JPG/JPEG assets identified three remaining files containing WEBP bytes despite a PNG extension. Unity loaded none as Texture2D before repair. Converted all three losslessly to actual PNG at their original paths, preserving original meta files and GUIDs:

- `Assets/VFX_Shader/CutSceneCaverna/MAT/Rock_Normal.png`: GUID `62daa6e313cf77a47852ab086a4004c2`; decoded 800x800 pixels identical; existing NormalMap importer produces 1024x1024.
- `Assets/VFX_Shader/Boss/Cuca/Laser/imagem_2024-12-07_183700556-removebg-preview.png`: GUID `312a1e7418b963840b7e2d95f7a46d99`; identical 200x200 RGBA pixels; Sprite importer preserved.
- `Assets/VFX_Shader/Artificial/Teste/Noise.png`: GUID `08bb629784634f548b5035f2fe456a01`; identical 820x820 RGB pixels; existing Default importer produces 1024x1024.

All three are reachable by serialized GUID dependencies from enabled production scenes. Synchronous Unity imports now return valid Texture2D assets. Validated only URP Lit materials with a loaded `_BumpMap` using the installed LitShader.ValidateMaterial: terrain Base/Parede and tutorial Tut_Parede now enable `_NORMALMAP`. No shader, authored color, blend or texture reference was replaced. Baked Lit materials were not assigned normal mapping. Final header scan found zero mislabeled WEBP files. This proves texture import recovery, not final visual parity.
