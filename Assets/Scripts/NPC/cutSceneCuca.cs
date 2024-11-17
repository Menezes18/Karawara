using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class cutSceneCuca : MonoBehaviour{
        public Camera[] cam;

        void Start()
        {
            foreach (var desativar in cam){
                desativar.enabled = false;
            }

            DesativarCamara(0);
        }

        public void CameraChange(int numberCam){
            
            switch (numberCam)
            {
                case 0:
                    
                    DesativarCamara(0);
                    break;
                case 1:
                    DesativarCamara(1);
                    break;
                case 2:
                    DesativarCamara(2);
                    break;
                case 3:
                    DesativarCamara(3);
                    break;
                case 4:
                    DesativarCamara(4);
                    break;
                default:
                    Debug.LogWarning("Câmera não encontrada: " + numberCam);
                    break;
            }


        }

        public void DesativarCamara(int indiceAtiva) {
            for (int i = 0; i < cam.Length; i++) {
                if (i == indiceAtiva) {
                    cam[i].enabled = true; 
                } else {
                    cam[i].enabled = false; 
                }
            }
        }
        
    }
}
