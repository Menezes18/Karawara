using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class cutSceneCuca : MonoBehaviour
    {
        public Camera[] cam;
        public AudioClip[] clip;
        public AudioSource audio;

        void Start()
        {
            foreach (var desativar in cam)
            {
                desativar.enabled = false;
            }

            DesativarCamara(0);
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
                    DesativarCamara(4);
                    PlayAudioClip(4);
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
                Invoke(nameof(FimDaCutscene), audio.clip.length);
            }
        }

        private void FimDaCutscene()
        {
            WorldSaveGameManager.instance.AttemptToCreateNewGame();
        }
    }
}
