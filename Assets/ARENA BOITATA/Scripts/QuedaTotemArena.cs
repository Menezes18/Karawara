using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuedaTotemArena : MonoBehaviour
{
    private Animator animator;
    private GameObject childObject;
    

    // Start is called before the first frame update
    void Start()
    {
        animator = this.GetComponent<Animator>();
        // Supondo que o objeto filho seja o primeiro filho do objeto principal
        childObject = this.transform.GetChild(0).gameObject;
        
    }

    private void OnTriggerEnter(Collider other)
    {
        // Verifica se o objeto colidido tem a tag "HitPlayer"
        if (other.CompareTag("HitPlayer"))
        {
            
            // Aciona o trigger "Cai" no Animator
            animator.SetTrigger("Cai");
            
            
        }
    }

    // Update is called once per frame
    void AtivHit()
    {
        // Ativa o objeto filho
        childObject.SetActive(true);
    }

    void DesativaHit()
    {
        // Desativa o objeto filho
        childObject.SetActive(false);
    }
}