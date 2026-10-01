# Homol validation adaptations

Regenerate DevPlayground through **Karawara / Dev Playground / Generate Scene and Catalog** before running its smoke test. The generator now creates `LeverCameraDev.anim` and `LeverCameraDev.controller` in `Assets/DevTools/Data/Generated`; Unity creates their metadata during generation.

Each lever uses the production Homol `AnimationTrigger` on an active camera object with a disabled Camera component. The generated Animator triggers `AtivarCamera`, runs with unscaled time, and emits the production `AtivarCollider` and `DeativarCameraCutScene` events. The smoke test checks the pause, indicator toggle, camera disable, time restoration, and real `GateActivator` response. Production scenes and animations are unchanged.

The production scene smoke test derives its indices from enabled Editor Build Settings and observes all seven current scenes: SceneMenu, Tutorial_Final, Video1, CucaLoad, t, MapaEsboco, and Creditos. Video1 and Creditos do not require a spawned player to count as initialized. The report records player requirements explicitly and still captures initialization errors and save fingerprints.

These changes require Unity compilation, playground regeneration, and runtime smoke execution. Source inspection alone does not confirm animation delivery, scene initialization, or gameplay parity.
