using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuedaTotem : MonoBehaviour
{
    private Animator animator;

    // Start is called before the first frame update
    void Start()
    {
        animator=this.GetComponent<Animator>();
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
    void Update()
    {
        
    }
}
