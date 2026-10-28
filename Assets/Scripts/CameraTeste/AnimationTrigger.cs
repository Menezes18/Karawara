using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class AnimationTrigger : MonoBehaviour
{
    public Animator animator;

    public GameObject target;
    public Camera cam;
    private void Start(){
        cam = GetComponent<Camera>();
    }

    void Update()
    {
    }

    public void AtivarCameraCutScene(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this);
#endif

        cam.enabled = true;
        Time.timeScale = 0;
        
        // Configura o Animator para usar Unscaled Time
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        // Ativa a animação
        animator.SetTrigger("AtivarCamera");
    }

    public void AtivarCollider(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this);
#endif

        target.SetActive(!target.activeSelf);
    }

    public void DeativarCameraCutScene(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this);
#endif

    Time.timeScale = 1;
        Debug.Log("AAA");
    cam.enabled = false;
    }
}