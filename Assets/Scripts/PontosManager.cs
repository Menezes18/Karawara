using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace RPGKarawara
{
    public class PontosManager : MonoBehaviour
    {
        public static PontosManager instance;
        public GameObject[] pontos;
        public CondicaoPonto[] condicaoPontos;
        public int index = 0;
        
        void Start(){
            instance = this;
            for(int i = 0; i < pontos.Length; i++){
                condicaoPontos[i] = pontos[i].GetComponent<CondicaoPonto>();
            }

        }

        void Update(){
            if(Vector3.Distance(pontos[index].transform.position, IndicadorObjetivoScript.indicador.jogador.position) < 3 &&
            condicaoPontos[index].podeAvançar){
                index ++;
                IndicadorObjetivoScript.indicador.objetivo = pontos[index].transform;
            }
        }

    }
}
