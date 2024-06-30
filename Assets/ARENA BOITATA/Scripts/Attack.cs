using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
    private Animator animator; // Referência ao componente Animator
    private CharacterController charCon;

    public GameObject hitBoxPlayer;

 void Start() {
    animator=this.GetComponent<Animator>();
    charCon = GetComponent<CharacterController>();
    hitBoxPlayer.SetActive(false);
}
    void Update()
    {
        // Verifica se o botão esquerdo do mouse foi pressionado
        if (Input.GetMouseButtonDown(0) && animator.GetBool("Grounded") && !animator.GetBool("Jump") && !animator.GetBool("FreeFall"))
        {
            // Inicia a coroutine para realizar o ataque
            StartCoroutine(ExecutarAtaque());
        }
    }

    private IEnumerator ExecutarAtaque()
    {
        // Define o parâmetro "Ataque" para true
        animator.SetBool("Ataque", true);

        charCon.enabled = false;
        //hitBoxPlayer.SetActive(true);

        // Espera 1 segundo
        yield return new WaitForSeconds(0.8f);

        // Define o parâmetro "Ataque" para false
        animator.SetBool("Ataque", false);

        charCon.enabled = true;
       // hitBoxPlayer.SetActive(false);
    }

    void AtivHit()
    {
        hitBoxPlayer.SetActive(true);
    }
    void DesativaHit()
    {
        hitBoxPlayer.SetActive(false);
    }
}
