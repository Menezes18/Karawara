# Avisos de árvores no build Windows

Investigação em 2026-09-30 durante o build, somente leitura de Assets, arquivos binários e Editor.log. Nenhuma cena, TerrainData, prefab ou configuração de importação foi alterada, e nenhuma operação do Editor foi executada.

## Evidência e alcance

O log registra **“The tree Tree6 couldn't be instanced because the prefab contains no valid mesh renderer.”** e o mesmo aviso para **TallTree2**. Exemplos na captura atual: `%LOCALAPPDATA%/Unity/Editor/Editor.log`, linhas 3453468 e 3453484; há ocorrências anteriores na mesma sessão. Os números de linha podem mudar quando o log for substituído.

Nas cinco cenas principais, as referências diretas a TerrainData aparecem em:

- `Assets/Scenes/SceneMenu.unity:291866` e 291877.
- `Assets/Scenes/Video1.unity:112851` e 112862.

Ambas usam GUID `a810e2b5d78d66e4bad1c586cadbc3e5`, arquivo `Assets/Terrain/Terrain_0_0_fa89942d-8e3c-490e-a681-cdecb9a37ef7.asset`. CucaLoad, t e MapaEsboco não possuem referência direta de TerrainData no YAML inspecionado. Isso não representa inspeção visual de todas as instâncias ou prova de ausência de terrenos carregados por código.

O TerrainData binário foi decodificado em memória com a biblioteca UnityPy já instalada. Seu cabeçalho registra Unity 2022.3.21f1. A tabela `m_DetailDatabase.m_TreePrototypes` e os índices de `m_TreeInstances` mostram:

| Índice | Asset de origem | Instâncias neste terreno |
|---|---|---:|
| 0 | ToraMuro.prefab, GUID `1bea67645963db048a76cca29b8d51c1` | 0 |
| 1 | FolhagemLOD/Tree6.fbx, GUID `73542b1f87043a643b204940b6c57dfd`, fileID 919132149155446097 | 0 |
| 2 | Folhagem/TallTree2.fbx, GUID `edd54d4a5fa656944bf657938fa509aa`, fileID 919132149155446097 | 0 |
| 3 | FolhagemLOD/Prefabs/Tree4.prefab, GUID `18e356607736bb14984d0f35f0e2513f` | 313 |

Os caminhos Folhagem e FolhagemLOD estão em `Assets/Data/Modelos/AssetsBlocagem/PackNatureza/`. Todas as 313 árvores serializadas usam Tree4. Portanto, os dois avisos correspondem a protótipos sem instâncias neste terreno; não demonstram perda de árvores visíveis nesses dados.

## Geometria e montagem

Os GUIDs e arquivos FBX existem. A leitura estrutural do FBX binário encontrou geometria não vazia:

- TallTree2: sete nós Geometry/Mesh, todos com arrays Vertices e PolygonVertexIndex não vazios.
- Tree6: seis nós Geometry/Mesh, correspondentes a folhas/tronco de LOD 0, 1 e 2, todos não vazios.

O TerrainData referencia o **FBX importado**, não o prefab Tree6 preparado pelo projeto. `FolhagemLOD/Prefabs/Tree6.prefab` contém seis MeshFilters e seis MeshRenderers habilitados; cada filtro aponta para um subasset do mesmo Tree6.fbx. Seu LODGroup possui três níveis, com dois renderers por nível. LOD 0 usa Trunk6_LOD_0 (fileID 8703180686028769943) e Leaves6_LOD_0 (fileID 6781159195490162293). Os MeshFilters não estão vazios e os GUIDs resolvem; a validade atual dos fileIDs importados não foi consultada ao Editor durante o build.

**Limite de equivalência:** o root do prefab Tree6 tem escala aproximadamente `(0.88987964, 0.8898796, 0.8898796)`, enquanto seus grupos/meshes LOD 0 têm posição zero, rotação identidade e escala um. Não é uma substituição comprovadamente equivalente em escala para o modelo bruto. Não foi encontrado um wrapper próprio de TallTree2 para comparar.

## Causa provável e correção segura

As evidências afastam a hipótese de arquivos/GUIDs ou geometria de origem ausentes. A hipótese principal é incompatibilidade da hierarquia importada selecionada como protótipo com os renderers/LOD exigidos pelo caminho de instanciamento de árvores. Isso é uma inferência: faltam inspeção dos objetos importados e reprodução comparativa para distinguir mudança específica do Unity 6 de configuração antiga já inadequada. O TerrainData e os modelos inspecionados não apresentam diff contra HEAD; a configuração é anterior à migração atual.

Como os protótipos 1 e 2 têm zero instâncias, um reparo possível após o build é removê-los da tabela **com remapeamento explícito dos índices restantes**. Preservar integralmente os 313 TreeInstances (posição, rotação, escala, cor e lightmapColor), apenas remapeando Tree4 de índice 3 para seu novo índice, evita substituir a aparência. Remover entradas sem remapear índices seria incorreto.

Outra opção é criar wrappers compatíveis usando os mesmos subassets, materiais e transformações, com LODGroup corretamente ligado, antes de trocar protótipos. O prefab Tree6 existente fornece evidência de topologia/LOD, mas sua escala deve ser tratada explicitamente; não selecionar substitutos apenas pelo nome. TallTree2 exige construção e validação de um wrapper próprio.

Nenhuma correção foi aplicada. O aviso é uma limitação do protótipo de terreno, não prova de falha de compilação. Resultado final do build e observação visual das árvores em um player nativo devem ser registrados separadamente.
