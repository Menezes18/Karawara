using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class cutSceneFight : MonoBehaviour
    {
        public Camera[] cam;
        public AudioClip[] clip;
        public AudioSource audio;
        public SkinnedMeshRenderer[] materialErode;
        public GameObject CucaDeFato, TainaraStop;
        void Start()
        {
            foreach (var desativar in cam)
            {
                desativar.enabled = false;
            }

            DesativarCamara(0);
        }
        IEnumerator ErodeObject()
        {
            yield return new WaitForSeconds(1f);
            float t=0;
                    while (t < 1){
                        foreach ( var mat in materialErode){
                            mat.material.SetFloat("_Erode", t);
                        }
                        t += 0.03f;
                        yield return new WaitForSeconds(0.01f);
                    }
        }
        public void CameraChange(int numberCam)
        {
            switch (numberCam)
            {
                case 0:
                    DesativarCamara(0);
                    PlayAudioClip(0);
                    break;
                case 1:
                    StartCoroutine(ErodeObject());
                    DesativarCamara(1);
                    PlayAudioClip(1);
                    break;
                case 2:
                    DesativarCamara(2);
                    PlayAudioClip(2);
                    break;
                case 3:
                    DesativarCamara(3);
                    PlayAudioClip(3);
                    break;
                case 4:
                    cam[4].gameObject.GetComponent<Animator>().SetBool("Anim4", true);
                    CucaDeFato.SetActive(true);
                    DesativarCamara(4);
                    PlayAudioClip(4);
                    break;
                case 5:
                    DesativarCamara(0);
                    PlayAudioClip(0);
                    break;
                case 6:
                    cam[1].gameObject.GetComponent<Animator>().SetBool("Anim5", true);
                    cam[1].gameObject.GetComponent<Animator>().SetBool("Anim4", false);
                    DesativarCamara(1);
                    PlayAudioClip(1);
                    break;
                case 7:
                    cam[2].gameObject.GetComponent<Animator>().SetBool("Anim6", true);
                    cam[2].gameObject.GetComponent<Animator>().SetBool("Anim5", false);
                    DesativarCamara(2);
                    PlayAudioClip(2);
                    break;
                case 8:
                    cam[0].gameObject.GetComponent<Animator>().SetBool("Anim7", true);
                    TainaraStop.SetActive(true);
                    TainaraStop.GetComponent<Animator>().SetBool("Fala1",false);
                    TainaraStop.GetComponent<Animator>().SetBool("Fala2",true);
                    cam[0].gameObject.GetComponent<Animator>().SetBool("Anim6", false);
                    DesativarCamara(0);
                    PlayAudioClip(0);
                    break;
                default:
                    Debug.LogWarning("Câmera não encontrada: " + numberCam);
                    break;
            }
        }

        public void DesativarCamara(int indiceAtiva)
        {
            for (int i = 0; i < cam.Length; i++)
            {
                cam[i].enabled = (i == indiceAtiva);
            }
        }

        private void PlayAudioClip(int clipIndex)
        {
            audio.clip = clip[clipIndex];
            audio.Play();

            // Se for o último áudio, chamar o método para indicar o fim da cutscene
            if (clipIndex == clip.Length - 1)
            {
                // Invocar o método quando o áudio terminar
                Debug.Log("fim");
            }
        }

    }
}
