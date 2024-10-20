using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class MagicSpeel : MonoBehaviour
    {
        public static MagicSpeel instance;
        public GameObject tiro;
        public Transform inicio;
        public Transform player; // Refer�ncia para o jogador
        public float speed = 35f;

        public void Start()
        {
            instance = this;
        }

        public void Tiro(Transform Player)
        {
            player = Player;
            // Instancia o objeto 'tiro' na posi��o e rota��o definidas por 'inicio'
            GameObject tiroInstance = Instantiate(tiro, inicio.position, inicio.rotation);
            Projetil pro = tiroInstance.GetComponent<Projetil>();
            pro._Velocidade = speed;
            pro.BuscarAlvo(Player);

            // Inicia a coroutine para mover o tiro em dire��o ao jogador
            //StartCoroutine(MoveTowardsPlayer(tiroInstance));
        }
    }
}
